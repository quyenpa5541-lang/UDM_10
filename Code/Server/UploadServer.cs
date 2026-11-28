using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Shared;

namespace Server
{
    public sealed class ManageClients : IAsyncDisposable
    {
        private readonly AppSettings _settings;
        private readonly Upload _uploadHandler;
        private readonly FileLogger _logger;
        private TcpListener? _listener;
        private CancellationTokenSource? _serverCts;
        private Task? _acceptTask;
        private int _currentConnections;

        public static int CurrentConnection;

        public bool IsRunning => _listener != null;
        public int ActiveConnections => Volatile.Read(ref _currentConnections);

        public ManageClients(AppSettings appSettings, FileLogger? logger = null)
        {
            _settings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
            _logger = logger ?? new FileLogger();
            var storage = new FileStorage(_settings.UploadDir);
            _uploadHandler = new Upload(storage, _settings, _logger);
        }

        public Task StartAsync()
        {
            if (IsRunning) return Task.CompletedTask;

            IPAddress address = IPAddress.TryParse(_settings.Host, out var parsed)
                ? parsed
                : throw new InvalidDataException($"Địa chỉ IP Server không hợp lệ: {_settings.Host}");

            var listener = new TcpListener(address, _settings.Port);
            listener.Start();
            _listener = listener;
            _serverCts = new CancellationTokenSource();
            _acceptTask = AcceptClientsAsync(_serverCts.Token);
            _logger.Info($"Server đã khởi động {address}:{_settings.Port}");
            return Task.CompletedTask;
        }

        public void AcceptClients()
        {
            StartAsync().GetAwaiter().GetResult();
            try { _acceptTask?.GetAwaiter().GetResult(); }
            finally { StopAsync().GetAwaiter().GetResult(); }
        }

        public void Stop()
        {
            _serverCts?.Cancel();
            _listener?.Stop();
        }

        private async Task AcceptClientsAsync(CancellationToken cancellationToken)
        {
            var listener = _listener;
            if (listener == null) return;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    TcpClient client = await listener
                        .AcceptTcpClientAsync(cancellationToken)
                        .ConfigureAwait(false);

                    int active = Interlocked.Increment(ref _currentConnections);
                    Interlocked.Exchange(ref CurrentConnection, active);

                    if (active > _settings.MaxConnections)
                    {
                        Interlocked.Decrement(ref _currentConnections);
                        Interlocked.Exchange(ref CurrentConnection, ActiveConnections);
                        _logger.Warning(
                            $"Từ chối {client.Client.RemoteEndPoint} - vượt quá số kết nối tối đa.");

                        try
                        {
                            using (client)
                            using (var rejectedStream = client.GetStream())
                            {
                                await Protocol.WriteJsonAsync(
                                    rejectedStream,
                                    new ResponseHeader
                                    {
                                        Code = Protocol.ErrorUnavailable,
                                        Message = "Server đã đạt số kết nối tối đa."
                                    },
                                    TimeSpan.FromSeconds(3),
                                    CancellationToken.None).ConfigureAwait(false);
                            }
                        }
                        catch { client.Dispose(); }
                        continue;
                    }

                    _ = ProcessClientAsync(client, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.Error("Lỗi vòng lặp accept: " + ex.Message);
            }
        }

        private async Task ProcessClientAsync(TcpClient client, CancellationToken serverToken)
        {
            string endpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
            _logger.Info($"Máy khách đã kết nối {endpoint} (active={ActiveConnections})");

            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    await _uploadHandler
                        .HandleRequestAsync(stream, endpoint, serverToken)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (serverToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.Warning($"Lỗi kết nối từ client {endpoint}: {ex.Message}");
            }
            finally
            {
                int active = Interlocked.Decrement(ref _currentConnections);
                Interlocked.Exchange(ref CurrentConnection, Math.Max(0, active));
                _logger.Info($"Mất kết nối {endpoint} (active={Math.Max(0, active)})");
            }
        }

        public async Task StopAsync()
        {
            if (_listener == null) return;

            _serverCts?.Cancel();
            _listener.Stop();
            _listener = null;

            if (_acceptTask != null)
            {
                try { await _acceptTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }

            _serverCts?.Dispose();
            _serverCts = null;
            _acceptTask = null;
            _logger.Info("Server dừng");
        }

        public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
    }

    public sealed class Upload
    {
        private readonly FileStorage _storage;
        private readonly AppSettings _settings;
        private readonly Validator _validator;
        private readonly FileLogger _logger;

        public Upload(FileStorage storage, AppSettings settings, FileLogger? logger = null)
        {
            _storage = storage;
            _settings = settings;
            _validator = new Validator(settings);
            _logger = logger ?? new FileLogger();
        }

        public async Task HandleRequestAsync(
            Stream networkStream, string endpoint, CancellationToken serverToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(serverToken);
            CancellationToken token = timeoutCts.Token;
            TimeSpan readTimeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);

            try
            {
                RequestHeader? request = await Protocol
                    .ReadJsonAsync<RequestHeader>(networkStream, readTimeout, token)
                    .ConfigureAwait(false);

                if (request == null)
                {
                    await SendError(networkStream, Protocol.ErrorBadRequest,
                        "Request rỗng.", token).ConfigureAwait(false);
                    return;
                }

                if (request.Command == Protocol.CmdSession)
                {
                    await Protocol.WriteJsonAsync(
                        networkStream,
                        new ResponseHeader { Code = Protocol.StatusSuccess, Message = "PONG" },
                        readTimeout, token).ConfigureAwait(false);

                    byte[] probe = new byte[1];
                    while (await networkStream
                        .ReadAsync(probe.AsMemory(0, 1), serverToken)
                        .ConfigureAwait(false) > 0)
                    {
                        throw new InvalidDataException(
                            "Dữ liệu không hợp lệ trên phiên điều khiển.");
                    }
                    return;
                }

                if (request.Command == Protocol.CmdPing)
                {
                    await Protocol.WriteJsonAsync(
                        networkStream,
                        new ResponseHeader { Code = Protocol.StatusSuccess, Message = "PONG" },
                        readTimeout, token).ConfigureAwait(false);
                    return;
                }

                if (request.Command != Protocol.CmdUpload)
                {

                    await SendError(networkStream, Protocol.ErrorBadRequest,
                        "Command Upload không hợp lệ.", token).ConfigureAwait(false);
                    return;
                }

                timeoutCts.CancelAfter(TimeSpan.FromMinutes(20));

                await HandleUploadAsync(networkStream, request, endpoint, token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (serverToken.IsCancellationRequested) { }
            catch (OperationCanceledException)
            {
                _logger.Warning($"Quá thời hạn {endpoint}");
                try
                {
                    await SendError(networkStream, Protocol.ErrorTimeout,
                        "Hết thời gian chờ.", CancellationToken.None).ConfigureAwait(false);
                }
                catch { }
            }
            catch (EndOfStreamException ex)
            {
                _logger.Warning($"Upload bị hủy giữa chừng ở {endpoint}: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Lỗi xử lý request từ {endpoint}: {ex.Message}");
                try
                {
                    await SendError(networkStream, Protocol.ErrorServer,
                        "Server gặp lỗi khi xử lý yêu cầu.", CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch { }
            }
        }

        private async Task HandleUploadAsync(
            Stream networkStream, RequestHeader header, string endpoint, CancellationToken token)
        {
            var validation = _validator.ValidateUpload(header);
            if (!validation.Passed)
            {
                _logger.Warning($"Từ chối {endpoint} [{validation.ErrorCode}]: {validation.Message}");
                await SendError(networkStream, validation.ErrorCode, validation.Message, token)
                    .ConfigureAwait(false);
                return;
            }

            await Protocol.WriteJsonAsync(
                networkStream,
                new ResponseHeader { Code = Protocol.StatusReady, Message = "READY" },
                TimeSpan.FromSeconds(10), token).ConfigureAwait(false);

            _logger.Info($"Upload từ {endpoint}: '{header.FileName}' ({header.Size} bytes)");

            try
            {
                string savedName = await _storage.SaveVerifiedFileAsync(
                    header.FileName, networkStream, header.Size, header.Sha256, token)
                    .ConfigureAwait(false);

                await Protocol.WriteJsonAsync(
                    networkStream,
                    new ResponseHeader
                    {
                        Code = Protocol.StatusSuccess,
                        Message = "Upload thành công",
                        SavedAs = savedName
                    },
                    TimeSpan.FromSeconds(10), token).ConfigureAwait(false);

                _logger.Info($"Tên file sau khi được chỉnh để lưu vào danh sách: '{savedName}'");
            }
            catch (InvalidDataException ex)
            {
                _logger.Warning($"Upload lỗi {endpoint} '{header.FileName}': {ex.Message}");
                await SendError(networkStream, Protocol.ErrorInvalidFile, ex.Message, token)
                    .ConfigureAwait(false);
            }
            catch (EndOfStreamException)
            {
                _logger.Warning($"Upload không hoàn tất {endpoint} '{header.FileName}'");
                throw;
            }
            catch (OperationCanceledException) { throw; }
            catch (IOException ex)
            {
                _logger.Error($"Lỗi ghi file tại {endpoint}: {ex.Message}");
                await SendError(networkStream, Protocol.ErrorServer,
                    "Không thể ghi file trên Server.", token).ConfigureAwait(false);
            }
        }

        private static Task SendError(
            Stream stream, int code, string message, CancellationToken token) =>
            Protocol.WriteJsonAsync(
                stream,
                new ResponseHeader { Code = code, Message = message },
                TimeSpan.FromSeconds(5), token);
    }

    public sealed class Validator
    {
        private readonly AppSettings _settings;

        public sealed class Result
        {
            public bool Passed { get; init; }
            public int ErrorCode { get; init; }
            public string Message { get; init; } = string.Empty;
        }

        public Validator(AppSettings settings) => _settings = settings;

        public Result ValidateUpload(RequestHeader? request)
        {
            if (request == null)
                return Fail(Protocol.ErrorBadRequest, "Upload không hợp lệ.");

            if (string.IsNullOrWhiteSpace(request.FileName))
                return Fail(Protocol.ErrorBadRequest, "Tên file không được để trống.");

            string normalized = request.FileName.Replace('\\', '/');
            string safeName = Path.GetFileName(normalized);

            if (safeName != normalized ||
                safeName.Length > 200 ||
                safeName is "." or ".." ||
                safeName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                safeName.EndsWith(".part", StringComparison.OrdinalIgnoreCase) ||
                safeName.EndsWith('.') ||
                safeName.EndsWith(' ') ||
                IsReservedWindowsName(safeName))
            {
                return Fail(Protocol.ErrorBadRequest, "Tên file không hợp lệ.");
            }

            if (request.Size <= 0)
                return Fail(Protocol.ErrorBadRequest, "Dung lượng file phải lớn hơn 0.");

            long maxBytes = (long)_settings.MaxFileSizeMB * 1024 * 1024;
            if (request.Size > maxBytes)
                return Fail(Protocol.ErrorFileTooLarge,
                    $"File vượt giới hạn {_settings.MaxFileSizeMB} MB.");

            if (string.IsNullOrWhiteSpace(request.Sha256) ||
                request.Sha256.Length != 64 ||
                !request.Sha256.All(Uri.IsHexDigit))
            {
                return Fail(Protocol.ErrorInvalidFile, "SHA-256 không hợp lệ.");
            }

            return new Result { Passed = true, Message = "Hợp lệ" };
        }

        private static bool IsReservedWindowsName(string fileName)
        {
            string stem = Path.GetFileNameWithoutExtension(fileName).TrimEnd(' ', '.');
            string[] reserved = {
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
            };
            return reserved.Contains(stem, StringComparer.OrdinalIgnoreCase);
        }

        private static Result Fail(int code, string message) =>
            new() { Passed = false, ErrorCode = code, Message = message };
    }
}

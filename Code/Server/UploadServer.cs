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
    public class ManageClients
    {
        public static int CurrentConnection;
        private readonly AppSettings _settings;
        private readonly Upload _uploadHandler;
        private readonly FileLogger _logger;
        private TcpListener? _server;

        public ManageClients(AppSettings appSettings, FileLogger? logger = null)
        {
            _settings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
            _logger = logger ?? new FileLogger();

            var storage = new FileStorage(_settings.UploadDir);
            _uploadHandler = new Upload(storage, _settings, _logger);
        }

        public void AcceptClients()
        {
            try
            {
                IPEndPoint ipep = new IPEndPoint(IPAddress.Parse(_settings.Host), _settings.Port);
                _server = new TcpListener(ipep);
                _server.Start();
                _logger.Info($"Server bắt đầu {ipep}");

                while (true)
                {
                    try
                    {
                        TcpClient client = _server.AcceptTcpClient();
                        int activeConnections = Interlocked.Increment(ref CurrentConnection);

                        if (activeConnections > _settings.MaxConnections)
                        {
                            Interlocked.Decrement(ref CurrentConnection);
                            _logger.Warning($"Từ chối {client.Client.RemoteEndPoint} - vượt quá số kết nối tối đa.");
                            client.Close();
                            continue;
                        }

                        _ = Task.Run(() => ProcessOfClient(client, _uploadHandler, _logger));
                    }
                    catch (SocketException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Lỗi tiếp nhận kết nối: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Lỗi không chạy server: {ex.Message}");
            }
        }

        public void Stop()
        {
            try
            {
                _server?.Stop();
                _logger.Info("Server dừng");
            }
            catch { }
        }

        public static async Task ProcessOfClient(TcpClient client, Upload uploadHandler, FileLogger logger)
        {
            string endpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
            logger.Info($"Kết nối {endpoint} (active={CurrentConnection})");

            try
            {
                using NetworkStream stream = client.GetStream();
                await uploadHandler.HandleRequestAsync(stream, endpoint);
            }
            catch (Exception e)
            {
                logger.Warning($"Lỗi kết nối từ client {endpoint}: {e.Message}");
            }
            finally
            {
                client.Close();
                int active = Interlocked.Decrement(ref CurrentConnection);
                logger.Info($"Mất kết nối {endpoint} (active={Math.Max(0, active)})");
            }
        }
    }

    public class Upload
    {
        private readonly FileStorage _storage;
        private readonly AppSettings _settings;
        private readonly Validator _validator;
        private readonly FileLogger _logger;

        public Upload(FileStorage storage, AppSettings settings, FileLogger? logger = null)
        {
            _storage = storage;
            _settings = settings;
            _validator = new Validator(_settings);
            _logger = logger ?? new FileLogger();
        }

        public async Task HandleRequestAsync(Stream networkStream, string endpoint = "unknown")
        {
            var timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
            using var token = new CancellationTokenSource(timeout);

            try
            {
                var Obj = await Protocol.ReadJsonAsync<RequestHeader>(networkStream, timeout, token.Token);
                if (Obj == null)
                {
                    await SendErrorAsync(networkStream, Protocol.ErrorBadRequest, "Request rỗng.", token.Token);
                    return;
                }

                if (Obj.Command == Protocol.CmdPing)
                {
                    var response = new ResponseHeader
                    {
                        Code = Protocol.StatusSuccess,
                        Message = "PONG"
                    };
                    await Protocol.WriteJsonAsync(networkStream, response, timeout, token.Token);
                }
                else if (Obj.Command == Protocol.CmdUpload)
                {
                    await HandleUploadAsync(networkStream, Obj, endpoint);
                }
                else
                {
                    _logger.Warning($"từ chối {endpoint}: Command không hợp lệ ({Obj.Command})");
                    await SendErrorAsync(networkStream, Protocol.ErrorBadRequest, "Command không hợp lệ.", token.Token);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Warning($"Quá thời hạn {endpoint}: Hết thời gian chờ đọc lệnh.");
            }
            catch (Exception ex)
            {
                _logger.Error($"Lỗi khi đọc dữ liệu điều khiển từ {endpoint}: {ex.Message}");
            }
        }

        private async Task HandleUploadAsync(Stream networkStream, RequestHeader header, string endpoint)
        {
            TimeSpan timeout = TimeSpan.FromMinutes(5);
            using var token = new CancellationTokenSource(timeout);

            try
            {
                var validateResult = _validator.ValidateUpload(header);
                if (!validateResult.Passed)
                {
                    _logger.Warning($"Từ chối {endpoint} [{validateResult.ErrorCode}]: {validateResult.Message}");
                    await SendErrorAsync(networkStream, validateResult.ErrorCode, validateResult.Message, token.Token);
                    return;
                }

                var readyResponse = new ResponseHeader
                {
                    Code = Protocol.StatusReady,
                    Message = "READY"
                };
                await Protocol.WriteJsonAsync(networkStream, readyResponse, TimeSpan.FromSeconds(10), token.Token);

                _logger.Info($"Upload bắt đầu {endpoint} '{header.FileName}' ({header.Size} bytes)");

                string savedFileName = await _storage.SaveVerifiedFileAsync(
                    fileName: header.FileName,
                    inputStream: networkStream,
                    fileSize: header.Size,
                    expectedSha256: header.Sha256,
                    cancellationToken: token.Token
                );

                var successResponse = new ResponseHeader
                {
                    Code = Protocol.StatusSuccess,
                    Message = "Upload thành công",
                    SavedAs = savedFileName
                };
                await Protocol.WriteJsonAsync(networkStream, successResponse, TimeSpan.FromSeconds(10), token.Token);

                _logger.Info($"Upload xong {endpoint} '{header.FileName}' -> '{savedFileName}'");
            }
            catch (InvalidDataException ex)
            {

                _logger.Warning($"Upload lỗi {endpoint} '{header.FileName}': {ex.Message}");
                await SendErrorAsync(networkStream, Protocol.ErrorInvalidFile, ex.Message, token.Token);
            }
            catch (EndOfStreamException ex)
            {

                _logger.Warning($"Upload không hoàn tất {endpoint} '{header.FileName}': {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                _logger.Warning($"Upload quá thời hạn {endpoint} '{header.FileName}'");
                await SendErrorAsync(networkStream, Protocol.ErrorTimeout, "Hết thời gian upload file.", CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.Error($"Lỗi khi upload từ {endpoint}: {ex.Message}");
                await SendErrorAsync(networkStream, Protocol.ErrorServer, "Lỗi xử lý lưu file tại Server.", CancellationToken.None);
            }
        }

        private static async Task SendErrorAsync(Stream stream, int code, string message, CancellationToken token)
        {
            try
            {
                var errorResponse = new ResponseHeader { Code = code, Message = message };
                await Protocol.WriteJsonAsync(stream, errorResponse, TimeSpan.FromSeconds(5), token);
            }
            catch { }
        }
    }

    public class Validator
    {
        private readonly AppSettings _settings;

        public class Result
        {
            public bool Passed { get; set; }
            public int ErrorCode { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        public Validator(AppSettings settings)
        {
            _settings = settings;
        }

        public Result ValidateUpload(RequestHeader? request)
        {
            if (request == null)
                return new Result { Passed = false, ErrorCode = Protocol.ErrorBadRequest, Message = "Upload không hợp lệ (null)." };

            if (string.IsNullOrWhiteSpace(request.FileName))
                return new Result { Passed = false, ErrorCode = Protocol.ErrorBadRequest, Message = "Tên file không được để trống." };

            string normalized = request.FileName.Replace('\\', '/');
            string safeName = Path.GetFileName(normalized);

            if (safeName != normalized ||
                safeName.Length > 200 ||
                string.IsNullOrWhiteSpace(safeName) ||
                safeName == "." ||
                safeName == ".." ||
                safeName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                safeName.EndsWith(".part", StringComparison.OrdinalIgnoreCase) ||
                safeName.EndsWith('.') ||
                safeName.EndsWith(' ') ||
                IsReservedWindowsName(safeName))
            {
                return new Result
                {
                    Passed = false,
                    ErrorCode = Protocol.ErrorBadRequest,
                    Message = "Tên file không hợp lệ."
                };
            }

            if (request.Size <= 0)
                return new Result { Passed = false, ErrorCode = Protocol.ErrorBadRequest, Message = "Dung lượng file không hợp lệ (âm hoặc 0)." };

            long maxFileSizeBytes = (long)_settings.MaxFileSizeMB * 1024 * 1024;
            if (request.Size > maxFileSizeBytes)
                return new Result { Passed = false, ErrorCode = Protocol.ErrorFileTooLarge, Message = $"Dung lượng file vượt mức cho phép ({_settings.MaxFileSizeMB}MB)." };

            if (string.IsNullOrWhiteSpace(request.Sha256) ||
                request.Sha256.Length != 64 ||
                !request.Sha256.All(Uri.IsHexDigit))
            {
                return new Result
                {
                    Passed = false,
                    ErrorCode = Protocol.ErrorInvalidFile,
                    Message = "SHA-256 không hợp lệ."
                };
            }

            return new Result { Passed = true, ErrorCode = 0, Message = "Hợp lệ" };
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
    }
}
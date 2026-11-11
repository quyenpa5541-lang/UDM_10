using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
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

        public ManageClients(AppSettings appSettings)
        {
            _settings = appSettings;

            var storage = new FileStorage(_settings.UploadDir);
            _uploadHandler = new Upload(storage, _settings);
        }

        public void AcceptClients()
        {
            try
            {
                IPEndPoint ipep = new IPEndPoint(IPAddress.Parse(_settings.Host), _settings.Port);
                TcpListener server = new TcpListener(ipep);
                server.Start();

                while (true)
                {
                    try
                    {
                        TcpClient client = server.AcceptTcpClient();
                        Interlocked.Increment(ref CurrentConnection);

                        if (CurrentConnection > _settings.MaxConnections)
                        {
                            Interlocked.Decrement(ref CurrentConnection);
                            client.Close();
                            continue;
                        }
                        _ = Task.Run(() => ProcessOfClient(client, _uploadHandler));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Lỗi tiếp nhận kết nối: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi không chạy server: {ex.Message}");
            }
        }

        public static async Task ProcessOfClient(TcpClient client, Upload uploadHandler)
        {
            try
            {
                using NetworkStream stream = client.GetStream();
                await uploadHandler.HandlePing(stream);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Lỗi kết nối từ client: {e.Message}");
            }
            finally
            {
                client.Close();
                Interlocked.Decrement(ref CurrentConnection);
            }
        }
    }

    public class Upload
    {
        private readonly FileStorage _storage;
        private readonly AppSettings _settings;
        private readonly Validator _validator;

        public Upload(FileStorage storage, AppSettings settings)
        {
            _storage = storage;
            _settings = settings;
            _validator = new Validator(_settings);
        }

        public async Task HandlePing(Stream networkStream)
        {
            var timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
            using var token = new CancellationTokenSource(timeout);

            try
            {
                var Obj = await Protocol.ReadJsonAsync<RequestHeader>(networkStream, timeout, token.Token);
                if (Obj != null)
                {
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
                        await HandleUpload(networkStream, Obj);
                    }
                    else
                    {
                        var CommandError = new ResponseHeader
                        {
                            Code = Protocol.ErrorBadRequest,
                            Message = "Command không hợp lệ."
                        };
                        await Protocol.WriteJsonAsync(networkStream, CommandError, timeout, token.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi đọc dữ liệu điều khiển: {ex.Message}");
            }
        }

        private async Task HandleUpload(Stream networkStream, RequestHeader header)
        {
            TimeSpan timeout = TimeSpan.FromMinutes(5);
            using var token = new CancellationTokenSource(timeout);

            try
            {
                var validateResult = _validator.ValidateUpload(header);
                if (!validateResult.Passed)
                {
                    var errorRes = new ResponseHeader { Code = validateResult.ErrorCode, Message = validateResult.Message };
                    await Protocol.WriteJsonAsync(networkStream, errorRes, TimeSpan.FromSeconds(5), token.Token);
                    return;
                }

                var readyResponse = new ResponseHeader
                {
                    Code = Protocol.StatusReady,
                    Message = "READY"
                };
                await Protocol.WriteJsonAsync(networkStream, readyResponse, TimeSpan.FromSeconds(10), token.Token);

                string savedFileName = await _storage.SaveFileAsync(
                    fileName: header.FileName,
                    inputStream: networkStream,
                    fileSize: header.Size,
                    cancellationToken: token.Token
                );

                string fullSavedPath = Path.Combine(_settings.UploadDir, savedFileName);
                string serverHash = CalculateSha256(fullSavedPath);

                if (!string.Equals(serverHash, header.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"SHA-256 không khớp ở Client: {header.Sha256} | Server: {serverHash}");

                    if (File.Exists(fullSavedPath))
                    {
                        try { File.Delete(fullSavedPath); } catch { }
                    }

                    var hashError = new ResponseHeader
                    {
                        Code = Protocol.ErrorInvalidFile,
                        Message = "Dữ liệu bị hỏng trong lúc truyền."
                    };
                    await Protocol.WriteJsonAsync(networkStream, hashError, TimeSpan.FromSeconds(5), token.Token);
                    return;
                }

                var successResponse = new ResponseHeader
                {
                    Code = Protocol.StatusSuccess,
                    Message = "Upload thành công",
                    SavedAs = savedFileName
                };
                await Protocol.WriteJsonAsync(networkStream, successResponse, TimeSpan.FromSeconds(10), token.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi upload: {ex.Message}");

                try
                {
                    var errorResponse = new ResponseHeader
                    {
                        Code = Protocol.ErrorServer,
                        Message = "Lỗi xử lý lưu file tại Server."
                    };
                    await Protocol.WriteJsonAsync(networkStream, errorResponse, TimeSpan.FromSeconds(5), default);
                }
                catch { }
            }
        }

        private string CalculateSha256(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = sha256.ComputeHash(stream);
            return Convert.ToHexString(hash).ToLower();
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

        public Result ValidateUpload(RequestHeader request)
        {
            if (request == null)
                return new Result { Passed = false, ErrorCode = Protocol.ErrorBadRequest, Message = "Upload không hợp lệ (null)." };

            if (string.IsNullOrWhiteSpace(request.FileName))
                return new Result { Passed = false, ErrorCode = Protocol.ErrorBadRequest, Message = "Tên file không được để trống." };

            char[] Invalid_Chars = Path.GetInvalidFileNameChars();
            if (request.FileName.Any(chaR => Invalid_Chars.Contains(chaR)))
                return new Result { Passed = false, ErrorCode = Protocol.ErrorBadRequest, Message = "Tên file chứa ký tự lạ." };

            if (request.Size <= 0)
                return new Result { Passed = false, ErrorCode = Protocol.ErrorBadRequest, Message = "Dung lượng file không hợp lệ (âm hoặc 0)." };

            long maxFileSizeBytes = (long)_settings.MaxFileSizeMB * 1024 * 1024;
            if (request.Size > maxFileSizeBytes)
                return new Result { Passed = false, ErrorCode = Protocol.ErrorFileTooLarge, Message = $"Dung lượng file vượt mức cho phép ({_settings.MaxFileSizeMB}MB)." };

            if (string.IsNullOrWhiteSpace(request.Sha256) || request.Sha256.Length != 64)
                return new Result { Passed = false, ErrorCode = Protocol.ErrorInvalidFile, Message = "Mã SHA-256 không đủ 64 ký tự hex." };

            return new Result { Passed = true, ErrorCode = 0, Message = "Hợp lệ" };
        }
    }
}
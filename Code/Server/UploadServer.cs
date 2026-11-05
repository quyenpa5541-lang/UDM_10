using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.Design.Serialization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Shared;

namespace Server
{
    public class ManageClients
    {
        public static int CurrentConnection;
        private AppSettings settings;
        public ManageClients(AppSettings appSettings)
        {
            settings = appSettings;
        }

        public void AcceptClients()
        {
            try
            {
                
                IPEndPoint ipep = new IPEndPoint(IPAddress.Parse(settings.Host), settings.Port);
                TcpListener server = new TcpListener(ipep);
           
                server.Start();
            
                while (true)
                {
                    try
                    {
                        TcpClient client = server.AcceptTcpClient();

                        // xử lý MaxConnection
                        Interlocked.Increment(ref CurrentConnection);

                        if (CurrentConnection > settings.MaxConnections)
                        {
                            Interlocked.Decrement(ref CurrentConnection);
                            client.Close();
                            continue;
                        }
               
                        _ = Task.Run(() => ProcessOfClient.GetFileAsync(client));
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine($"Lỗi tiếp nhận: {ex.Message}");       
                    }  
                }
            
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Lỗi không chạy server: {ex.Message}");
            }
            
        }
    }

    public class ProcessOfClient
    {
        public static async Task GetFileAsync(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();

                // đọc kích cỡ JSON Header
                byte[] JSON_sizefile_number = new byte[4];
                await stream.ReadExactlyAsync(JSON_sizefile_number, 0, 4);

                // dịch sang số
                int JSON_sizefile = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(JSON_sizefile_number, 0));

                // đọc JSON khi có số cụ thể
                byte[] JSON = new byte[JSON_sizefile];
                await stream.ReadExactlyAsync(JSON, 0, JSON_sizefile);

                // chuyển thành văn bản
                string JSON_Header = Encoding.UTF8.GetString(JSON);

                using JsonDocument Doc = JsonDocument.Parse(JSON_Header);
                
                JsonElement origin = Doc.RootElement;

                // đọc filename và filesize để tạo thư mục và lưu file_path
                string filename = origin.GetProperty("FileName").GetString() ?? "unknown";

                long filesize = origin.GetProperty("Size").GetInt64();

                byte [] _filesize = BitConverter.GetBytes(filesize);
                FileStorage file = new FileStorage();
                string filepath= await file.SaveFileAsync(filename,_filesize);

                string command = origin.GetProperty("Command").GetString()!;

                if (command == Shared.Protocol.CmdUpload)
                {
                    
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Lỗi client: {e.Message}");
            }
            finally
            {
                client.Close();
                Interlocked.Decrement(ref ManageClients.CurrentConnection);
            }
        }


        public Result Validate(UploadRequest request)
        {
            if (request == null)
            {
                return new Result { Passed = false, ErrorCode = "400", Message = "Upload không hợp lệ (null)." };
            }

            if (string.IsNullOrWhiteSpace(request.FileName))
            {
                return new Result { Passed = false, ErrorCode = "400", Message = "Tên file không được để trống." };
            }

            char[] Invalid_Chars = Path.GetInvalidFileNameChars();
            if (request.FileName.Any(chaR => Invalid_Chars.Contains(chaR)))
            {
                return new Result {Passed = false, ErrorCode = "400", Message = "Tên file chứa ký tự lạ"};
            }
            
            if (request.FileSize <= 0)
            {
                return new Result{Passed = false, ErrorCode = "400", Message = "dung lượng file không hợp lệ (âm hoặc 0)"};
            }

            AppSettings maxfilesize = new AppSettings();
            if (request.FileSize >= maxfilesize.MaxFileSizeMB * 1024 * 1024)
            {
                return new Result {Passed = false, ErrorCode = "413", Message = "dung lượng file vượt mức cho phép"};
            }

            return new Result { Passed = true, ErrorCode = null, Message = "Hợp lệ" }; 
        } 
    }

    public class Result
    {
        public bool Passed { get; set; }
        public string? ErrorCode { get; set; }
        public string? Message { get; set; }
    }
}

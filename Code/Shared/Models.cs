
namespace Shared
{
    public class UploadRequest // Client gửi tên file và dung lượng file muốn upload
    {
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }

    public class UploadResponse // 	Server phản hồi upload thành công hay thất bại, tên file đã lưu
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string SavedFileName { get; set; } = string.Empty;
    }

    public class ErrorResponse // Mô tả mã lỗi và nội dung lỗi
    {
        public string ErrorCode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
    public class RequestHeader  // Client gửi thông tin file cho Server
    {
        public string Command { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Sha256 { get; set; } = string.Empty;
    }

    public class ResponseHeader     // Server phản hồi kết quả cho Client
    {
        public int Code { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? SavedAs { get; set; }
    }

}

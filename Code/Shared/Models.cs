
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
}

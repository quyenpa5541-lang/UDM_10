namespace Client;

public enum UploadState
{
    Hashing,
    Connecting,
    Uploading
}

public sealed record UploadProgress(
    long BytesSent,
    long TotalBytes,
    double BytesPerSecond = 0);

public sealed record UploadResult(
    bool Success,
    int Code,
    string Message,
    string? SavedAs = null,
    bool IsCanceled = false);

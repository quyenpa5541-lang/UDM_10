using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Shared;

public static class Protocol
{
    public const int MaxHeaderBytes = 8 * 1024;
    public const int BufferSize = 64 * 1024;

    public const string CmdPing = "PING";
    public const string CmdUpload = "UPLOAD";

    // Các mã trạng thái dùng chung
    public const int StatusReady = 100;
    public const int StatusSuccess = 200;
    public const int ErrorBadRequest = 400;
    public const int ErrorNotFound = 404;
    public const int ErrorTimeout = 408;
    public const int ErrorFileTooLarge = 413;
    public const int ErrorInvalidFile = 422;
    public const int ErrorClientClosed = 499;
    public const int ErrorServer = 500;
    public const int ErrorUnavailable = 503;
    public const int ErrorStorageFull = 507;

    // Gửi JSON: 4 byte độ dài + nội dung JSON
    public static async Task WriteJsonAsync<T>(
        Stream stream,
        T data,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(data);
        if (body.Length == 0 || body.Length > MaxHeaderBytes)
            throw new InvalidDataException("Độ dài JSON không hợp lệ.");

        byte[] lengthBytes = BitConverter.GetBytes(
            IPAddress.HostToNetworkOrder(body.Length));

        await stream.WriteAsync(lengthBytes, cancellationToken)
            .AsTask().WaitAsync(timeout, cancellationToken);
        await stream.WriteAsync(body, cancellationToken)
            .AsTask().WaitAsync(timeout, cancellationToken);
        await stream.FlushAsync(cancellationToken)
            .WaitAsync(timeout, cancellationToken);
    }
    // Đọc thông điệp JSON
    public static async Task<T?> ReadJsonAsync<T>(
        Stream stream,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        byte[] lengthBytes = new byte[4];
        await ReadExactAsync(stream, lengthBytes, 4, timeout, cancellationToken);

        int length = IPAddress.NetworkToHostOrder(
            BitConverter.ToInt32(lengthBytes, 0));
        if (length <= 0 || length > MaxHeaderBytes)
            throw new InvalidDataException("Độ dài JSON không hợp lệ.");

        byte[] body = new byte[length];
        await ReadExactAsync(stream, body, length, timeout, cancellationToken);
        return JsonSerializer.Deserialize<T>(body);
    }
    // Đọc một phần dữ liệu từ Stream
    public static async Task<int> ReadSomeAsync(
        Stream stream,
        byte[] buffer,
        int count,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (count < 0 || count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        return await stream.ReadAsync(
            buffer.AsMemory(0, count), cancellationToken)
            .AsTask().WaitAsync(timeout, cancellationToken);
    }
    // Đọc đủ số byte yêu cầu
    public static async Task ReadExactAsync(
        Stream stream,
        byte[] buffer,
        int count,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (count < 0 || count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        int offset = 0;
        while (offset < count)
        {
            int bytesRead = await stream.ReadAsync(
                buffer.AsMemory(offset, count - offset),
                cancellationToken)
                .AsTask().WaitAsync(timeout, cancellationToken);

            if (bytesRead == 0)
                throw new EndOfStreamException(
                    "Kết nối đã đóng trước khi nhận đủ dữ liệu.");

            offset += bytesRead;
        }
    }
}

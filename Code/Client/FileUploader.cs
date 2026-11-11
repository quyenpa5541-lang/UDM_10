using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;

using Shared;

namespace Client;

/// <summary>Logic upload phía Client (TV4): hash, kết nối, gửi dữ liệu, tính tốc độ, timeout, hủy, bắt lỗi.</summary>
public static class FileUploader
{
    private static async Task<TcpClient> ConnectAsync(string host, int port, TimeSpan timeout, CancellationToken ct)
    {
        var tcp = new TcpClient();
        try
        {
            await tcp.ConnectAsync(host, port, ct).AsTask().WaitAsync(timeout, ct).ConfigureAwait(false);
            return tcp;
        }
        catch
        {
            tcp.Dispose();
            throw;
        }
    }

    public static async Task<(bool Ok, string Message)> PingAsync(string host, int port, TimeSpan timeout, CancellationToken ct)
    {
        try
        {
            using var tcp = await ConnectAsync(host, port, timeout, ct).ConfigureAwait(false);
            using var stream = tcp.GetStream();
            await Protocol.WriteJsonAsync(stream, new RequestHeader { Command = Protocol.CmdPing }, timeout, ct).ConfigureAwait(false);
            var resp = await Protocol.ReadJsonAsync<ResponseHeader>(stream, timeout, ct).ConfigureAwait(false);
            if (resp == null) return (false, "Server không gửi phản hồi PING hợp lệ.");
            if (resp.Code != Protocol.StatusSuccess)
                return (false, $"Server từ chối PING (mã {resp.Code}): {resp.Message}");
            if (!string.Equals(resp.Message, "PONG", StringComparison.OrdinalIgnoreCase))
                return (false, "Server không phản hồi PONG hợp lệ.");
            return (true, resp.Message);
        }
        catch (TimeoutException) { return (false, "Hết thời gian chờ (timeout)."); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return (false, "Đã hủy."); }
        catch (OperationCanceledException) { return (false, "Hết thời gian chờ (timeout)."); }
        catch (SocketException ex) { return (false, "Không kết nối được: " + ex.Message); }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public static async Task<string> ComputeSha256Async(string path, CancellationToken ct, IProgress<int>? progress = null)
    {
        const int hashBuffer = 1024 * 1024; // đọc từng khối 1 MB, tuần tự -> nhanh hơn với file lớn
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, hashBuffer,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buf = new byte[hashBuffer];
        long total = fs.Length, done = 0;
        int lastPct = -1;
        int n;
        while ((n = await fs.ReadAsync(buf.AsMemory(), ct).ConfigureAwait(false)) > 0)
        {
            sha.AppendData(buf, 0, n);
            done += n;
            int pct = total <= 0 ? 100 : (int)(done * 100 / total);
            if (pct != lastPct)
            {
                lastPct = pct;
                progress?.Report(pct);
            }
        }
        if (lastPct < 100) progress?.Report(100);
        return Convert.ToHexString(sha.GetHashAndReset());
    }

    public static async Task<UploadResult> UploadAsync(
        string host, int port, string path, TimeSpan timeout,
        IProgress<UploadProgress>? progress, IProgress<UploadState>? state, CancellationToken ct,
        IProgress<int>? hashProgress = null)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) return new UploadResult(false, Protocol.ErrorNotFound, "Không tìm thấy file.");
            long size = info.Length;
            if (size <= 0)
                return new UploadResult(false, Protocol.ErrorBadRequest, "Server không chấp nhận file trống.");

            state?.Report(UploadState.Hashing);
            string hash = await ComputeSha256Async(path, ct, hashProgress).ConfigureAwait(false);

            state?.Report(UploadState.Connecting);
            using var tcp = await ConnectAsync(host, port, timeout, ct).ConfigureAwait(false);
            using var stream = tcp.GetStream();

            var header = new RequestHeader
            {
                Command = Protocol.CmdUpload,
                FileName = info.Name,
                Size = size,
                Sha256 = hash
            };
            await Protocol.WriteJsonAsync(stream, header, timeout, ct).ConfigureAwait(false);

            // Server kiểm tra header trước, trả READY (100) hoặc lỗi.
            var ready = await Protocol.ReadJsonAsync<ResponseHeader>(stream, timeout, ct).ConfigureAwait(false);
            if (ready == null)
                return new UploadResult(false, Protocol.ErrorServer, "Server không gửi phản hồi READY.");
            if (ready.Code != Protocol.StatusReady)
            {
                if (ready.Code >= Protocol.ErrorBadRequest)
                    return new UploadResult(false, ready.Code, ready.Message);
                return new UploadResult(false, Protocol.ErrorServer,
                    $"Phản hồi không đúng giao thức: cần READY (100), nhận {ready.Code}.");
            }

            state?.Report(UploadState.Uploading);
            progress?.Report(new UploadProgress(0, size));

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, Protocol.BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                byte[] buf = new byte[Protocol.BufferSize];
                long sent = 0;
                var sw = Stopwatch.StartNew();
                long lastReportMs = 0;
                long lastReportBytes = 0;
                double speed = 0; // byte/giây, làm mượt bằng trung bình trượt mũ (EMA)
                while (sent < size)
                {
                    int toRead = (int)Math.Min(buf.Length, size - sent);
                    int n = await fs.ReadAsync(buf.AsMemory(0, toRead), ct).ConfigureAwait(false);
                    if (n == 0) throw new IOException("File bị thay đổi trong lúc gửi.");
                    await stream.WriteAsync(buf.AsMemory(0, n), ct).AsTask().WaitAsync(timeout, ct).ConfigureAwait(false);
                    sent += n;

                    long nowMs = sw.ElapsedMilliseconds;
                    if (progress != null && (nowMs - lastReportMs >= 100 || sent == size))
                    {
                        double dt = (nowMs - lastReportMs) / 1000.0;
                        if (dt > 0)
                        {
                            double instant = (sent - lastReportBytes) / dt;
                            speed = speed <= 0 ? instant : speed * 0.7 + instant * 0.3;
                        }
                        lastReportMs = nowMs;
                        lastReportBytes = sent;
                        progress.Report(new UploadProgress(sent, size, speed));
                    }
                }
            }
            await stream.FlushAsync(ct).WaitAsync(timeout, ct).ConfigureAwait(false);

            var final = await Protocol.ReadJsonAsync<ResponseHeader>(stream, timeout, ct).ConfigureAwait(false);
            if (final == null)
                return new UploadResult(false, Protocol.ErrorServer, "Server không gửi kết quả upload.");
            if (final.Code == Protocol.StatusSuccess)
                return new UploadResult(true, final.Code, final.Message, final.SavedAs);
            if (final.Code >= Protocol.ErrorBadRequest)
                return new UploadResult(false, final.Code, final.Message);
            return new UploadResult(false, Protocol.ErrorServer,
                $"Phản hồi cuối không đúng giao thức: cần SUCCESS (200), nhận {final.Code}.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new UploadResult(false, 0, "Đã hủy.", null, true);
        }
        catch (TimeoutException)
        {
            return new UploadResult(false, Protocol.ErrorTimeout, "Hết thời gian chờ (timeout).");
        }
        catch (OperationCanceledException)
        {
            return new UploadResult(false, Protocol.ErrorTimeout, "Hết thời gian chờ (timeout).");
        }
        catch (SocketException ex)
        {
            return new UploadResult(false, Protocol.ErrorUnavailable, "Lỗi kết nối: " + ex.Message);
        }
        catch (EndOfStreamException)
        {
            return new UploadResult(false, Protocol.ErrorClientClosed, "Server đã ngắt kết nối giữa chừng.");
        }
        catch (IOException ex) when (ex.InnerException is SocketException)
        {
            // NetworkStream thường bọc lỗi reset kết nối bằng IOException.
            return new UploadResult(false, Protocol.ErrorUnavailable,
                "Mất kết nối tới Server: " + ex.Message);
        }
        catch (IOException ex)
        {
            return new UploadResult(false, Protocol.ErrorServer, "Lỗi I/O: " + ex.Message);
        }
        catch (Exception ex)
        {
            return new UploadResult(false, Protocol.ErrorServer, "Lỗi: " + ex.Message);
        }
    }
}
// Các kiểu kết quả được sử dụng bởi FileUploader và giao diện MainForm.
// Định nghĩa tại Client, không thay đổi Shared/Models.cs hoặc API của Server.
public enum UploadState
{
    Hashing,
    Connecting,
    Uploading
}

public readonly record struct UploadProgress(
    long SentBytes,
    long TotalBytes,
    double BytesPerSecond = 0);

public sealed record UploadResult(
    bool Ok,
    int Code,
    string Message,
    string? SavedAs = null,
    bool Canceled = false);

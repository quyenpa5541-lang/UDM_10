using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Server;

public class FileStorage
{
    private readonly string _uploadFolder;

    public FileStorage(string uploadFolder = "Uploads")
    {
        _uploadFolder = Path.GetFullPath(uploadFolder);
        Directory.CreateDirectory(_uploadFolder);
    }

    public async Task<string> SaveFileAsync(string fileName, byte[] fileData)
    {
        if (fileData == null) throw new ArgumentNullException(nameof(fileData));
        using var stream = new MemoryStream(fileData);
        return await SaveFileAsync(fileName, stream, fileData.LongLength);
    }

    // Lưu vào .part trước; chỉ đổi sang tên chính thức sau khi nhận đủ byte.
    public async Task<string> SaveFileAsync(
        string fileName, Stream inputStream, long fileSize,
        CancellationToken cancellationToken = default)
    {
        return await SaveVerifiedFileAsync(
            fileName, inputStream, fileSize, expectedSha256: null, cancellationToken);
    }

    public async Task<string> SaveVerifiedFileAsync(
        string fileName, Stream inputStream, long fileSize,
        string? expectedSha256, CancellationToken cancellationToken = default)
    {
        string safeName = ValidateFileName(fileName);
        if (inputStream == null) throw new ArgumentNullException(nameof(inputStream));
        if (!inputStream.CanRead) throw new ArgumentException("Stream không thể đọc.");
        if (fileSize <= 0) throw new ArgumentOutOfRangeException(nameof(fileSize));

        string tempPath = Path.Combine(_uploadFolder, $".{Guid.NewGuid():N}.part");
        string finalPath = string.Empty;

        try
        {
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var output = new FileStream(
                tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                byte[] buffer = new byte[81920];
                long remaining = fileSize;

                while (remaining > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int wanted = (int)Math.Min(buffer.Length, remaining);
                    int read = await inputStream.ReadAsync(
                        buffer.AsMemory(0, wanted), cancellationToken);
                    if (read == 0)
                        throw new EndOfStreamException("Dữ liệu file chưa được truyền đầy đủ.");

                    sha.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    remaining -= read;
                }

                await output.FlushAsync(cancellationToken);
            }

            string actualHash = Convert.ToHexString(sha.GetHashAndReset());
            if (!string.IsNullOrWhiteSpace(expectedSha256) &&
                !CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(actualHash),
                    Convert.FromHexString(expectedSha256)))
            {
                throw new InvalidDataException("SHA-256 không khớp. File nhận được có thể đã bị hỏng.");
            }

            // File.Move không ghi đè; tên được chọn lại nếu có upload trùng tên đồng thời.
            string name = Path.GetFileNameWithoutExtension(safeName);
            string extension = Path.GetExtension(safeName);
            for (int index = 0; ; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string candidate = index == 0 ? safeName : $"{name}_{index}{extension}";
                finalPath = Path.Combine(_uploadFolder, candidate);
                try
                {
                    File.Move(tempPath, finalPath, overwrite: false);
                    return Path.GetFileName(finalPath);
                }
                catch (IOException) when (File.Exists(finalPath))
                {
                    continue;
                }
            }
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            throw;
        }
    }

    private static string ValidateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Tên file không hợp lệ.");

        string normalized = fileName.Replace('\\', '/');
        string safeName = Path.GetFileName(normalized);
        if (!string.Equals(safeName, normalized, StringComparison.Ordinal) ||
            safeName is "." or ".." ||
            safeName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            safeName.EndsWith(".part", StringComparison.OrdinalIgnoreCase) ||
            IsReservedWindowsName(safeName))
            throw new ArgumentException("Tên file không hợp lệ.");

        return safeName;
    }

    private static bool IsReservedWindowsName(string fileName)
    {
        string stem = Path.GetFileNameWithoutExtension(fileName).TrimEnd(' ', '.');
        string[] reserved = { "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
        return reserved.Contains(stem, StringComparer.OrdinalIgnoreCase);
    }
}


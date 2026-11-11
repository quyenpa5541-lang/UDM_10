using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Server;

public class FileStorage
{
    private readonly string _uploadFolder;

    public FileStorage(string uploadFolder = "Uploads")
    {
        _uploadFolder = uploadFolder;
        Directory.CreateDirectory(_uploadFolder);
    }

    public async Task<string> SaveFileAsync(
        string fileName, byte[] fileData)
    {
        if (fileData == null)
            throw new ArgumentNullException(nameof(fileData));

        using var stream = new MemoryStream(fileData);

        return await SaveFileAsync(
            fileName, stream, fileData.LongLength);
    }

    public async Task<string> SaveFileAsync(
        string fileName,
        Stream inputStream,
        long fileSize,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Ten file khong hop le.");

        if (inputStream == null)
            throw new ArgumentNullException(nameof(inputStream));

        if (!inputStream.CanRead)
            throw new ArgumentException("Stream khong the doc.");

        if (fileSize < 0)
            throw new ArgumentOutOfRangeException(nameof(fileSize));

        string normalized = fileName.Replace('\\', '/');
        string safeName = Path.GetFileName(normalized);

        if (safeName != normalized ||
            string.IsNullOrWhiteSpace(safeName) ||
            safeName == "." ||
            safeName == ".." ||
            safeName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Ten file khong hop le.");
        }

        string name = Path.GetFileNameWithoutExtension(safeName);
        string extension = Path.GetExtension(safeName);

        string filePath;
        FileStream output;

        int index = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string candidate = index == 0
                ? safeName
                : $"{name}_{index}{extension}";

            filePath = Path.Combine(_uploadFolder, candidate);

            try
            {
                output = new FileStream(
                    filePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    useAsync: true);

                break;
            }
            catch (IOException) when (File.Exists(filePath))
            {
                index++;
            }
        }

        try
        {
            await using (output)
            {
                byte[] buffer = new byte[81920];
                long remaining = fileSize;

                while (remaining > 0)
                {
                    int count = (int)Math.Min(
                        buffer.Length, remaining);

                    int bytesRead = await inputStream.ReadAsync(
                        buffer.AsMemory(0, count),
                        cancellationToken);

                    if (bytesRead == 0)
                        throw new EndOfStreamException(
                            "Du lieu file chua du.");

                    await output.WriteAsync(
                        buffer.AsMemory(0, bytesRead),
                        cancellationToken);

                    remaining -= bytesRead;
                }
            }

            return Path.GetFileName(filePath);
        }
        catch
        {
            output.Dispose();

            try
            {
                File.Delete(filePath);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            throw;
        }
    }
}


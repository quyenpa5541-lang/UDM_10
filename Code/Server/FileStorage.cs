using System;
using System.IO;
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
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Ten file khong hop le.");

        if (fileData == null)
            throw new ArgumentNullException(nameof(fileData));

        string safeName = Path.GetFileName(fileName);

        if (string.IsNullOrWhiteSpace(safeName) ||
            safeName == "." || safeName == "..")
            throw new ArgumentException("Ten file khong hop le.");

        string filePath = Path.Combine(_uploadFolder, safeName);

        await File.WriteAllBytesAsync(filePath, fileData);

        return filePath;
    }
}


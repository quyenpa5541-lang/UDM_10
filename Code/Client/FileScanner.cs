
using System;
using System.Collections.Generic;
using System.IO;

namespace Client
{
    public class ScannedFile
    {
        public string FullPath { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public string ErrorMessage { get; set; } = string.Empty;

        public bool IsValid => string.IsNullOrEmpty(ErrorMessage);
    }

    public static class FileScanner
    {
        public static ScannedFile ScanFile(string filePath)
        {
            ScannedFile result = new ScannedFile
            {
                FullPath = filePath ?? string.Empty
            };

            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    throw new ArgumentException(
                        "Đường dẫn file không hợp lệ.");
                }

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException(
                        "Không tìm thấy file.", filePath);
                }

                FileInfo info = new FileInfo(filePath);

                result.FullPath = info.FullName;
                result.FileName = info.Name;
                result.FileSize = info.Length;
            }
            catch (Exception ex) when (
                ex is ArgumentException ||
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is NotSupportedException)
            {
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        public static List<ScannedFile> ScanFiles(
            IEnumerable<string> filePaths)
        {
            List<ScannedFile> results = new List<ScannedFile>();

            if (filePaths == null)
            {
                return results;
            }

            foreach (string path in filePaths)
            {
                ScannedFile scannedFile = ScanFile(path);

                results.Add(scannedFile);
            }

            return results;
        }
    }
}

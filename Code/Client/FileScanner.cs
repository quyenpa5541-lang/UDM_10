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
    }

    public static class FileScanner
    {
        
        public static ScannedFile ScanFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Đường dẫn file không hợp lệ.");
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    "Không tìm thấy file.", filePath);
            }

            FileInfo info = new FileInfo(filePath);

            if (info.Length <= 0)
            {
                throw new InvalidDataException(
                    "File rỗng, không thể upload.");
            }

            return new ScannedFile
            {
                FullPath = info.FullName,
                FileName = info.Name,
                FileSize = info.Length
            };
        }

        public static List<ScannedFile> ScanFiles(
            IEnumerable<string> filePaths)
        {
            List<ScannedFile> results = new List<ScannedFile>();

            foreach (string path in filePaths)
            {
                results.Add(ScanFile(path));
            }

            return results;
        }
    }
}
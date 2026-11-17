using System;
using System.Collections.Generic;
using System.IO;
using System.Security;

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
        public static ScannedFile ScanFile(
            string? filePath,
            long maxFileSizeBytes = 0)
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

                string fullPath = Path.GetFullPath(filePath);
                result.FullPath = fullPath;

                if (Directory.Exists(fullPath))
                {
                    throw new IOException(
                        "Đây là thư mục, không phải file.");
                }

                FileInfo info = new FileInfo(fullPath);

                if (!info.Exists)
                {
                    throw new FileNotFoundException(
                        "Không tìm thấy file.", fullPath);
                }

                using (new FileStream(
                    fullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite))
                {
                }

                result.FileName = info.Name;
                result.FileSize = info.Length;

                if (result.FileSize <= 0)
                {
                    result.ErrorMessage = "File rỗng (0 byte).";
                }
                else if (maxFileSizeBytes > 0 &&
                         result.FileSize > maxFileSizeBytes)
                {
                    result.ErrorMessage =
                        $"File vượt quá giới hạn {maxFileSizeBytes / (1024 * 1024):N0} MB.";
                }
            }
            catch (Exception ex) when (
                ex is ArgumentException ||
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is NotSupportedException ||
                ex is SecurityException)
            {
                result.ErrorMessage = ex is UnauthorizedAccessException
                    ? "Không có quyền đọc file."
                    : ex.Message;
            }

            return result;
        }

        public static List<ScannedFile> ScanFiles(
            IEnumerable<string>? filePaths,
            long maxFileSizeBytes = 0,
            bool expandDirectories = false)
        {
            List<ScannedFile> results = new List<ScannedFile>();

            if (filePaths == null)
            {
                return results;
            }

            HashSet<string> seen = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            void ScanAndAdd(string? path)
            {
                ScannedFile scanned = ScanFile(path, maxFileSizeBytes);

                if (scanned.FullPath.Length > 0 && !seen.Add(scanned.FullPath))
                {
                    return;
                }

                results.Add(scanned);
            }

            foreach (string path in filePaths)
            {
                if (expandDirectories &&
                    !string.IsNullOrWhiteSpace(path) &&
                    Directory.Exists(path))
                {
                    try
                    {
                        EnumerationOptions options = new EnumerationOptions
                        {
                            RecurseSubdirectories = true,
                            IgnoreInaccessible = true
                        };

                        foreach (string file in Directory.EnumerateFiles(
                            path, "*", options))
                        {
                            ScanAndAdd(file);
                        }
                    }
                    catch (Exception ex) when (
                        ex is IOException ||
                        ex is UnauthorizedAccessException ||
                        ex is SecurityException)
                    {
                        results.Add(new ScannedFile
                        {
                            FullPath = path,
                            ErrorMessage = ex.Message
                        });
                    }

                    continue;
                }

                ScanAndAdd(path);
            }

            return results;
        }
    }
}

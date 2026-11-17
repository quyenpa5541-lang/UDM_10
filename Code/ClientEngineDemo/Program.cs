using Client;

if (args.Length == 0)
{
    Console.WriteLine("Hay truyen it nhat mot duong dan file.");
    return;
}

var scannedFiles = FileScanner.ScanFiles(args);

foreach (var file in scannedFiles)
{
    Console.WriteLine($"Name: {file.FileName}");
    Console.WriteLine($"Path: {file.FullPath}");
    Console.WriteLine($"Size: {file.FileSize}");
    Console.WriteLine($"Valid: {file.IsValid}");
    Console.WriteLine($"Error: {file.ErrorMessage}");
    Console.WriteLine("--------------------");
}

var queue = new UploadQueue();
int added = queue.AddFiles(scannedFiles);
Console.WriteLine($"Added: {added}");
Console.WriteLine($"Waiting: {queue.WaitingCount}");

while (queue.TryGetNext(out var item))
{
    Console.WriteLine($"{item.Source.FileName}: {item.Status}, {item.Progress}%");

    queue.UpdateProgress(item, 60);
    Console.WriteLine($"{item.Source.FileName}: {item.Status}, {item.Progress}%");

    queue.UpdateStatus(item, UploadStatus.Completed);
    Console.WriteLine($"{item.Source.FileName}: {item.Status}, {item.Progress}%");
}

Console.WriteLine($"Waiting: {queue.WaitingCount}");

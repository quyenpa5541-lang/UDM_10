using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Shared;

// Công cụ stress/performance test cho UDM_10.
// Cách dùng:
//   StressTest                                  -> chạy 2 mức tải mặc định tới 127.0.0.1:9000
//   StressTest <host> <port> <clients> <filesPerClient> <fileSizeKB>
Console.OutputEncoding = Encoding.UTF8;

string host = "127.0.0.1";
int port = 9000;
var levels = new List<(int Clients, int Files, int SizeKB)>();

if (args.Length >= 5)
{
    host = args[0];
    port = int.Parse(args[1]);
    levels.Add((int.Parse(args[2]), int.Parse(args[3]), int.Parse(args[4])));
}
else
{
    levels.Add((10, 5, 1024));   // mức tải 1: 10 client x 5 file x 1 MB
    levels.Add((50, 5, 1024));   // mức tải 2: 50 client x 5 file x 1 MB
}

Console.WriteLine($"Máy test : {Environment.MachineName} | {RuntimeInformation.OSDescription} | {Environment.ProcessorCount} luồng CPU | RAM {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024)} MB");
Console.WriteLine($"Server   : {host}:{port}");

var (ok, msg) = await FileUploader.PingAsync(host, port, TimeSpan.FromSeconds(5), CancellationToken.None);
if (!ok)
{
    Console.WriteLine("Không kết nối được server: " + msg);
    return;
}

string csv = Path.Combine(AppContext.BaseDirectory, "stress-results.csv");
if (!File.Exists(csv))
    File.WriteAllText(csv, "time,clients,files_per_client,file_size_kb,total_files,success,failed,error_rate_pct,duration_s,throughput_mb_s,files_per_s,avg_latency_ms,p95_latency_ms,client_cpu_pct,client_peak_ram_mb\n");

foreach (var (clients, files, sizeKB) in levels)
{
    string dir = Path.Combine(Path.GetTempPath(), "udm10_stress_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    string src = Path.Combine(dir, "stress.bin");
    var data = new byte[sizeKB * 1024];
    new Random(42).NextBytes(data);
    File.WriteAllBytes(src, data);

    Console.WriteLine();
    Console.WriteLine($"=== Mức tải: {clients} client x {files} file x {sizeKB} KB ===");

    var latencies = new ConcurrentBag<double>();
    var errors = new ConcurrentDictionary<string, int>();
    int success = 0, failed = 0;

    var proc = Process.GetCurrentProcess();
    var cpuStart = proc.TotalProcessorTime;
    var sw = Stopwatch.StartNew();

    var tasks = Enumerable.Range(0, clients).Select(_ => Task.Run(async () =>
    {
        for (int i = 0; i < files; i++)
        {
            var t = Stopwatch.StartNew();
            var res = await FileUploader.UploadAsync(host, port, src, TimeSpan.FromSeconds(30), null, null, CancellationToken.None);
            t.Stop();
            if (res.Success)
            {
                Interlocked.Increment(ref success);
                latencies.Add(t.Elapsed.TotalMilliseconds);
            }
            else
            {
                Interlocked.Increment(ref failed);
                errors.AddOrUpdate($"[{res.Code}] {res.Message}", 1, (_, c) => c + 1);
            }
        }
    })).ToArray();

    await Task.WhenAll(tasks);
    sw.Stop();
    proc.Refresh();

    int total = clients * files;
    double secs = sw.Elapsed.TotalSeconds;
    double mb = success * sizeKB / 1024.0;
    var sorted = latencies.OrderBy(x => x).ToList();
    double avg = sorted.Count > 0 ? sorted.Average() : 0;
    double p95 = sorted.Count > 0 ? sorted[(int)Math.Min(sorted.Count - 1, Math.Ceiling(sorted.Count * 0.95) - 1)] : 0;
    double cpu = (proc.TotalProcessorTime - cpuStart).TotalSeconds / (secs * Environment.ProcessorCount) * 100;
    double ram = proc.PeakWorkingSet64 / (1024.0 * 1024.0);
    double errRate = total == 0 ? 0 : failed * 100.0 / total;

    Console.WriteLine($"Tổng file     : {total}  (thành công {success}, thất bại {failed})");
    Console.WriteLine($"Tỷ lệ lỗi     : {errRate:0.##} %");
    Console.WriteLine($"Thời gian     : {secs:0.00} s");
    Console.WriteLine($"Throughput    : {mb / secs:0.00} MB/s  |  {success / secs:0.00} file/s");
    Console.WriteLine($"Độ trễ/file   : trung bình {avg:0} ms, p95 {p95:0} ms");
    Console.WriteLine($"CPU client    : {cpu:0.0} %  |  RAM đỉnh client: {ram:0} MB");
    foreach (var kv in errors) Console.WriteLine($"  Lỗi x{kv.Value}: {kv.Key}");

    var inv = CultureInfo.InvariantCulture;
    File.AppendAllText(csv, string.Join(",",
        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", inv), clients, files, sizeKB, total, success, failed,
        errRate.ToString("0.##", inv), secs.ToString("0.00", inv), (mb / secs).ToString("0.00", inv),
        (success / secs).ToString("0.00", inv), avg.ToString("0", inv), p95.ToString("0", inv),
        cpu.ToString("0.0", inv), ram.ToString("0", inv)) + "\n");

    try { Directory.Delete(dir, true); } catch { }
}

Console.WriteLine();
Console.WriteLine("Kết quả đã ghi vào: " + csv);

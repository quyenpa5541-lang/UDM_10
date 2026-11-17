using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using Client;

internal class Program
{
    private static readonly CancellationTokenSource Cts = new();

    // Ba muc kiem thu: 10, 30 va 50 Client.
    private static readonly (string Name, int Clients)[] Tests =
    {
        ("Basic", 10),
        ("Medium", 30),
        ("High", 50)
    };

    private static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Cts.Cancel();
            Console.WriteLine("\nDang yeu cau dung kiem thu...");
        };

        Console.WriteLine("===== UDM_10 AUTOMATED STRESS TEST =====");
        Console.Write("IP Server [127.0.0.1]: ");
        string host = Console.ReadLine()?.Trim() is { Length: > 0 } h ? h : "127.0.0.1";

        Console.Write("Port [9000]: ");
        string portText = Console.ReadLine() ?? "";
        int port = string.IsNullOrWhiteSpace(portText) ? 9000 :
            int.TryParse(portText, out int p) && p is > 0 and <= 65535 ? p : 0;

        if (port == 0)
        {
            Console.WriteLine("Port khong hop le.");
            return;
        }

        Console.WriteLine("\n[1] 10 Client  [2] 30 Client  [3] 50 Client  [4] Chay ca 3  [0] Thoat");
        Console.Write("Lua chon: ");
        string choice = Console.ReadLine()?.Trim() ?? "0";

        var selected = choice == "4" ? Tests :
            int.TryParse(choice, out int index) && index is >= 1 and <= 3
                ? new[] { Tests[index - 1] }
                : Array.Empty<(string Name, int Clients)>();

        if (selected.Length == 0)
            return;

        var ping = await FileUploader.PingAsync(
            host, port, TimeSpan.FromSeconds(5), Cts.Token);

        if (!ping.Ok)
        {
            Console.WriteLine("Khong ket noi duoc Server: " + ping.Message);
            return;
        }

        Console.WriteLine("Server da san sang.\n");

        foreach (var test in selected)
        {
            if (Cts.IsCancellationRequested) break;
            await RunTestAsync(host, port, test.Name, test.Clients);
        }

        Console.WriteLine("\nHoan tat StressTest.");
    }

    private static async Task RunTestAsync(string host, int port, string name, int clientCount)
    {
        const int filesPerClient = 3;
        const int fileSizeKb = 1024;
        int totalFiles = clientCount * filesPerClient;
        string folder = Path.Combine(Path.GetTempPath(), "UDM10_StressTest");
        Directory.CreateDirectory(folder);
        string sourceFile = Path.Combine(folder, $"stress_{Guid.NewGuid():N}.bin");
        string csv = Path.Combine(AppContext.BaseDirectory, "stress-results.csv");

        Console.WriteLine($"\n--- {name}: {clientCount} Client, {totalFiles} file, 1 MB/file ---");

        try
        {
            await CreateTestFileAsync(sourceFile, fileSizeKb * 1024L, Cts.Token);

            using var monitor = IsLocalServer(host) ? ServerMonitor.Start() : null;
            var times = new ConcurrentBag<double>();
            var errors = new ConcurrentBag<string>();
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int success = 0, attempted = 0, completed = 0;

            var timer = Stopwatch.StartNew();
            Task[] clients = Enumerable.Range(0, clientCount).Select(async client =>
            {
                await gate.Task;

                for (int file = 0; file < filesPerClient && !Cts.IsCancellationRequested; file++)
                {
                    var watch = Stopwatch.StartNew();

                    try
                    {
                        var result = await FileUploader.UploadAsync(
                            host, port, sourceFile, TimeSpan.FromSeconds(120),
                            null, null, Cts.Token);

                        if (result.Ok)
                            Interlocked.Increment(ref success);
                        else
                            errors.Add($"Client {client + 1}: {result.Message}");
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Client {client + 1}: {ex.Message}");
                    }
                    finally
                    {
                        watch.Stop();
                        times.Add(watch.Elapsed.TotalMilliseconds);
                        Interlocked.Increment(ref attempted);

                        int done = Interlocked.Increment(ref completed);
                        Console.Write($"\rTien do: {done}/{totalFiles}");
                    }
                }
            }).ToArray();

            gate.SetResult();
            await Task.WhenAll(clients);
            timer.Stop();

            int failed = attempted - success;
            double seconds = Math.Max(timer.Elapsed.TotalSeconds, 0.001);
            double mb = success * fileSizeKb / 1024d;
            double throughput = mb / seconds;
            double[] sorted = times.OrderBy(x => x).ToArray();
            double average = sorted.Length == 0 ? 0 : sorted.Average();
            double p95 = sorted.Length == 0 ? 0 :
                sorted[(int)Math.Ceiling(sorted.Length * 0.95) - 1];
            double errorRate = attempted == 0 ? 0 : failed * 100d / attempted;
            var server = monitor?.Stop();

            Console.WriteLine("\nKet qua:");
            Console.WriteLine($"Thanh cong: {success}/{attempted}");
            Console.WriteLine($"That bai: {failed} ({errorRate:0.##}%)");
            Console.WriteLine($"Thoi gian: {seconds:0.00} giay");
            Console.WriteLine($"Throughput: {throughput:0.00} MB/s");
            Console.WriteLine($"Latency TB: {average:0.##} ms | p95: {p95:0.##} ms");

            if (server != null)
                Console.WriteLine($"CPU Server: {server.Value.Cpu:0.0}% | RAM Server dinh: {server.Value.RamMb:0.0} MB");
            else
                Console.WriteLine("CPU/RAM Server: khong do duoc (can chay Server tren cung may).");

            foreach (string error in errors.Take(5))
                Console.WriteLine("Loi: " + error);

            SaveCsv(csv, name, clientCount, filesPerClient, fileSizeKb,
                totalFiles, attempted, success, failed, errorRate, seconds,
                throughput, success / seconds, average, p95, server,
                string.Join(" | ", errors.Take(5)));

            Console.WriteLine("Da luu ket qua: " + csv);
        }
        finally
        {
            if (File.Exists(sourceFile))
                File.Delete(sourceFile);
        }
    }

    // Tao file theo tung khoi nho, tranh chiem RAM bang kich thuoc file.
    private static async Task CreateTestFileAsync(string path, long size, CancellationToken ct)
    {
        byte[] buffer = new byte[64 * 1024];
        var random = new Random(42);

        await using var file = new FileStream(path, FileMode.Create, FileAccess.Write,
            FileShare.None, buffer.Length, useAsync: true);

        while (size > 0)
        {
            ct.ThrowIfCancellationRequested();
            random.NextBytes(buffer);
            int count = (int)Math.Min(buffer.Length, size);
            await file.WriteAsync(buffer.AsMemory(0, count), ct);
            size -= count;
        }
    }

    private static void SaveCsv(
        string path, string name, int clients, int filesPerClient, int sizeKb,
        int total, int attempted, int success, int failed, double errorRate,
        double seconds, double throughput, double filesPerSecond,
        double average, double p95, (double Cpu, double RamMb)? server,
        string errorSummary)
    {
        const string header = "time,scenario,clients,files_per_client,file_size_kb,total_files,attempted_files,success,failed,error_rate_pct,duration_s,throughput_mb_s,files_per_s,avg_latency_ms,p95_latency_ms,server_cpu_pct,server_ram_mb,error_summary";

        if (!File.Exists(path) || new FileInfo(path).Length == 0)
            File.WriteAllText(path, header + Environment.NewLine, new UTF8Encoding(false));

        string[] values =
        {
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            name, clients.ToString(), filesPerClient.ToString(), sizeKb.ToString(),
            total.ToString(), attempted.ToString(), success.ToString(), failed.ToString(),
            errorRate.ToString("0.##", CultureInfo.InvariantCulture),
            seconds.ToString("0.00", CultureInfo.InvariantCulture),
            throughput.ToString("0.00", CultureInfo.InvariantCulture),
            filesPerSecond.ToString("0.00", CultureInfo.InvariantCulture),
            average.ToString("0.##", CultureInfo.InvariantCulture),
            p95.ToString("0.##", CultureInfo.InvariantCulture),
            server?.Cpu.ToString("0.0", CultureInfo.InvariantCulture) ?? "",
            server?.RamMb.ToString("0.0", CultureInfo.InvariantCulture) ?? "",
            Csv(errorSummary)
        };

        File.AppendAllText(path, string.Join(",", values) + Environment.NewLine,
            new UTF8Encoding(false));
    }

    private static string Csv(string value) =>
        "\"" + value.Replace("\"", "\"\"") + "\"";

    private static bool IsLocalServer(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        (IPAddress.TryParse(host, out IPAddress? address) && IPAddress.IsLoopback(address));

    private sealed class ServerMonitor : IDisposable
    {
        private readonly Process process;
        private readonly TimeSpan startCpu;
        private readonly Stopwatch watch = Stopwatch.StartNew();
        private double peakRam;
        private readonly Timer sampler;

        private ServerMonitor(Process p)
        {
            process = p;
            startCpu = p.TotalProcessorTime;
            peakRam = p.WorkingSet64 / 1024d / 1024d;
            sampler = new Timer(_ =>
            {
                try
                {
                    if (!process.HasExited)
                        peakRam = Math.Max(peakRam, process.WorkingSet64 / 1024d / 1024d);
                }
                catch { }
            }, null, 0, 500);
        }

        public static ServerMonitor? Start()
        {
            try
            {
                Process? p = Process.GetProcessesByName("Server").FirstOrDefault();
                return p == null ? null : new ServerMonitor(p);
            }
            catch { return null; }
        }

        public (double Cpu, double RamMb)? Stop()
        {
            sampler.Dispose();
            watch.Stop();

            try
            {
                if (process.HasExited) return null;
                double cpu = (process.TotalProcessorTime - startCpu).TotalSeconds /
                    (Math.Max(watch.Elapsed.TotalSeconds, 0.001) * Environment.ProcessorCount) * 100;
                return (Math.Clamp(cpu, 0, 100), peakRam);
            }
            catch { return null; }
            finally { process.Dispose(); }
        }

        public void Dispose()
        {
            sampler.Dispose();
            process.Dispose();
        }
    }
}

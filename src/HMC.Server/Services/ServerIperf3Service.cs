using System.Diagnostics;
using System.Text.Json;
using HMC.Shared.Models;

namespace HMC.Server.Services;

/// <summary>
/// Server 端的 iPerf3 进程管理（直接在 Linux 上运行 iperf3 命令）。
/// </summary>
public class ServerIperf3Service
{
    private readonly ILogger<ServerIperf3Service> _log;
    private readonly Dictionary<int, Process> _runningServers = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly bool _available;

    public bool IsAvailable => _available;

    public ServerIperf3Service(ILogger<ServerIperf3Service> log)
    {
        _log = log;
        _available = CheckAvailable();
    }

    private bool CheckAvailable()
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "iperf3",
                Arguments = "--version",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });
            if (proc == null) return false;
            proc.WaitForExit(3000);
            _log.LogInformation("Server iPerf3 available: {Version}",
                proc.StandardOutput.ReadToEnd().Trim());
            return proc.ExitCode == 0;
        }
        catch
        {
            _log.LogWarning("iperf3 not found on server. Install with: apt install iperf3");
            return false;
        }
    }

    public async Task<Process?> StartServerAsync(int port, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await StopServerInternalAsync(port);
            var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "iperf3",
                Arguments = $"-s -p {port} --one-off",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            if (proc != null)
            {
                _runningServers[port] = proc;
                _log.LogInformation("Server iPerf3 listening on port {Port}, PID={Pid}", port, proc.Id);
            }
            return proc;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Iperf3Result> RunClientAsync(string targetIp, int port,
        int threads, int duration, CancellationToken ct = default)
    {
        var result = new Iperf3Result
        {
            TestId = Guid.NewGuid().ToString("N")[..8],
            SourceDeviceId = "_server_",
            TargetDeviceId = "_server_"
        };

        try
        {
            var args = $"-c {targetIp} -p {port} -P {threads} -t {duration} -J";
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "iperf3",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            if (proc == null) throw new Exception("Failed to start iperf3");

            var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode != 0)
            {
                result.Success = false;
                result.ErrorMessage = await proc.StandardError.ReadToEndAsync(ct);
                return result;
            }

            result.RawJson = stdout;
            ParseResult(result, stdout);
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public async Task StopServerAsync(int port)
    {
        await _lock.WaitAsync();
        try { StopServerInternalAsync(port); }
        finally { _lock.Release(); }
    }

    private Task StopServerInternalAsync(int port)
    {
        if (_runningServers.TryGetValue(port, out var proc))
        {
            try { if (!proc.HasExited) proc.Kill(); }
            catch (Exception ex) { _log.LogWarning(ex, "Failed to kill iperf3 port {Port}", port); }
            _runningServers.Remove(port);
        }
        return Task.CompletedTask;
    }

    private static void ParseResult(Iperf3Result result, string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var end = doc.RootElement.GetProperty("end");
            var sum = end.TryGetProperty("sum_sent", out var s) ? s
                : end.TryGetProperty("sum_received", out var r) ? r : default;
            if (sum.ValueKind == JsonValueKind.Object)
            {
                if (sum.TryGetProperty("bits_per_second", out var bps))
                    result.BitsPerSecond = bps.GetDouble();
                if (sum.TryGetProperty("bytes", out var bytes))
                    result.BytesTransferred = bytes.GetInt64();
                if (sum.TryGetProperty("retransmits", out var retx))
                    result.Retransmits = retx.GetDouble();
            }
        }
        catch { }
    }
}

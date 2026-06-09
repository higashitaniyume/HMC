using System.Diagnostics;
using System.Text.Json;
using HMC.Shared.Models;
using Microsoft.Extensions.Logging;

namespace HMC.Agent.Services;

/// <summary>
/// 管理 iPerf3 进程 (Server + Client 模式)。
/// </summary>
public interface IIperf3Service
{
    /// <summary>iPerf3.exe 是否存在</summary>
    bool IsAvailable { get; }

    /// <summary>iPerf3 版本</summary>
    string Version { get; }

    /// <summary>启动 iPerf3 -s (Server 模式)</summary>
    Task<Process> StartServerAsync(int port, CancellationToken ct = default);

    /// <summary>运行 iPerf3 -c (Client 模式) 并返回解析后的结果</summary>
    Task<Iperf3Result> RunClientAsync(Iperf3TestRequest request, CancellationToken ct = default);

    /// <summary>停止指定的 iPerf3 Server 进程</summary>
    Task StopServerAsync(int port, CancellationToken ct = default);
}

public class Iperf3Service : IIperf3Service, IDisposable
{
    private readonly ILogger<Iperf3Service> _log;
    private readonly string _iperf3Path;
    private readonly Dictionary<int, Process> _runningServers = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _isAvailable;

    public bool IsAvailable => _isAvailable;
    public string Version { get; private set; } = "unknown";

    public Iperf3Service(ILogger<Iperf3Service> log)
    {
        _log = log;
        _iperf3Path = Path.Combine(AppContext.BaseDirectory, "tools", "iperf3.exe");
        CheckAvailability();
    }

    private void CheckAvailability()
    {
        if (!File.Exists(_iperf3Path))
        {
            _log.LogWarning("iPerf3 not found at {Path}", _iperf3Path);
            _isAvailable = false;
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _iperf3Path,
                Arguments = "--version",
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
            if (proc?.StandardOutput.ReadToEnd() is { } output && output.Length > 0)
            {
                Version = output.Trim();
            }
            _isAvailable = true;
            _log.LogInformation("iPerf3 found: {Version}", Version);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to verify iPerf3");
            _isAvailable = false;
        }
    }

    public async Task<Process> StartServerAsync(int port, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            // 先停止可能存在的旧进程
            await StopServerAsync(port, ct);

            var psi = new ProcessStartInfo
            {
                FileName = _iperf3Path,
                Arguments = $"-s -p {port} --one-off",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start iPerf3 server");

            _runningServers[port] = proc;
            _log.LogInformation("iPerf3 server started on port {Port}, PID={Pid}", port, proc.Id);

            // Fire and forget: 等待进程退出后自动清理
            _ = Task.Run(async () =>
            {
                try
                {
                    await proc.WaitForExitAsync(ct);
                    _log.LogInformation("iPerf3 server on port {Port} exited", port);
                }
                finally
                {
                    await _lock.WaitAsync();
                    try { _runningServers.Remove(port); }
                    finally { _lock.Release(); }
                }
            }, ct);

            return proc;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Iperf3Result> RunClientAsync(Iperf3TestRequest request, CancellationToken ct = default)
    {
        var result = new Iperf3Result
        {
            TestId = request.TestId,
            SourceDeviceId = request.SourceDeviceId,
            TargetDeviceId = request.TargetDeviceId
        };

        try
        {
            var args = $"-c {request.TargetIp} -p {request.Port} " +
                       $"-P {request.ParallelThreads} -t {request.DurationSeconds} -J";

            _log.LogInformation("Running iPerf3 client: {Args}", args);

            var psi = new ProcessStartInfo
            {
                FileName = _iperf3Path,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start iPerf3 client");

            var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
            var stderr = await proc.StandardError.ReadToEndAsync(ct);

            // 等待进程退出，带超时
            var exited = await Task.Run(() => proc.WaitForExit(
                (request.DurationSeconds + 5) * 1000), ct);

            if (!exited)
            {
                try { proc.Kill(); } catch { }
                result.Success = false;
                result.ErrorMessage = "iPerf3 client timed out";
                return result;
            }

            if (proc.ExitCode != 0)
            {
                result.Success = false;
                result.ErrorMessage = stderr.Trim();
                _log.LogWarning("iPerf3 client exited {Code}: {Error}", proc.ExitCode, result.ErrorMessage);
                return result;
            }

            result.RawJson = stdout;
            ParseIperf3Json(result, stdout);
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _log.LogError(ex, "iPerf3 client error");
        }

        return result;
    }

    public async Task StopServerAsync(int port, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_runningServers.TryGetValue(port, out var proc))
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        proc.Kill();
                        _log.LogInformation("iPerf3 server on port {Port} killed", port);
                    }
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Failed to kill iPerf3 server on port {Port}", port);
                }
                _runningServers.Remove(port);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private void ParseIperf3Json(Iperf3Result result, string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // end.sum_sent or end.sum_received
            if (root.TryGetProperty("end", out var end))
            {
                var sum = end.TryGetProperty("sum_sent", out var sent) ? sent
                    : end.TryGetProperty("sum_received", out var recv) ? recv
                    : default;

                if (sum.ValueKind == JsonValueKind.Object)
                {
                    if (sum.TryGetProperty("bits_per_second", out var bps))
                        result.BitsPerSecond = bps.GetDouble();
                    if (sum.TryGetProperty("bytes", out var bytes))
                        result.BytesTransferred = bytes.GetInt64();
                    if (sum.TryGetProperty("retransmits", out var retx))
                        result.Retransmits = retx.GetDouble();
                }

                // UDP jitter
                if (end.TryGetProperty("sum", out var udpSum))
                {
                    if (udpSum.TryGetProperty("jitter_ms", out var jitter))
                        result.JitterMs = jitter.GetDouble();
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to parse iPerf3 JSON output");
        }
    }

    public void Dispose()
    {
        foreach (var (port, proc) in _runningServers)
        {
            try { if (!proc.HasExited) proc.Kill(); } catch { }
        }
        _runningServers.Clear();
        _lock.Dispose();
    }
}

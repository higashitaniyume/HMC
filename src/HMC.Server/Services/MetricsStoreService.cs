using System.Collections.Concurrent;
using System.Text.Json;
using HMC.Server.Data;
using HMC.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace HMC.Server.Services;

/// <summary>
/// 将实时指标聚合存储到 SQLite（每分钟聚合一条）。
/// </summary>
public class MetricsStoreService
{
    private readonly IServiceScopeFactory _scopeFactory;

    // 待聚合缓冲: DeviceId → 累积值
    private readonly ConcurrentDictionary<string, MetricsBuffer> _buffers = new();

    public MetricsStoreService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task StoreSnapshotAsync(MetricsSnapshot snapshot)
    {
        var buffer = _buffers.GetOrAdd(snapshot.DeviceId, _ => new MetricsBuffer());

        lock (buffer)
        {
            buffer.CpuSamples.Add(snapshot.Cpu.TotalPercent);
            buffer.MemUsedSamples.Add(snapshot.Memory.UsedMB);
            buffer.MemTotalSamples.Add(snapshot.Memory.TotalMB);
            buffer.DiskReadSamples.Add(snapshot.DiskIO.ReadBps);
            buffer.DiskWriteSamples.Add(snapshot.DiskIO.WriteBps);
            buffer.NetInSamples.Add(snapshot.Network.InBps);
            buffer.NetOutSamples.Add(snapshot.Network.OutBps);
            buffer.LastSnapshot = snapshot;

            // 每分钟聚合一次
            if (buffer.CpuSamples.Count >= 30) // 2s * 30 = 60s
            {
                FlushBuffer(snapshot.DeviceId, buffer);
            }
        }

        await Task.CompletedTask;
    }

    private void FlushBuffer(string deviceId, MetricsBuffer buffer)
    {
        var avgCpu = buffer.CpuSamples.Average();
        var avgMemUsed = buffer.MemUsedSamples.Average();
        var avgMemTotal = buffer.MemTotalSamples.Average();
        var avgDiskRead = buffer.DiskReadSamples.Average();
        var avgDiskWrite = buffer.DiskWriteSamples.Average();
        var avgNetIn = buffer.NetInSamples.Average();
        var avgNetOut = buffer.NetOutSamples.Average();

        var entity = new MetricsSnapshotEntity
        {
            DeviceId = deviceId,
            Timestamp = DateTime.UtcNow,
            CpuPercent = Math.Round(avgCpu, 2),
            MemoryUsedMB = Math.Round(avgMemUsed, 2),
            MemoryTotalMB = Math.Round(avgMemTotal, 2),
            DiskReadBps = avgDiskRead,
            DiskWriteBps = avgDiskWrite,
            NetInBps = avgNetIn,
            NetOutBps = avgNetOut,
            FullJson = JsonSerializer.Serialize(buffer.LastSnapshot)
        };

        // 异步写入 DB
        var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            db.MetricsSnapshots.Add(entity);
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            // Log via scope
            var log = scope.ServiceProvider.GetRequiredService<ILogger<MetricsStoreService>>();
            log.LogWarning(ex, "Failed to flush metrics buffer for {DeviceId}", deviceId);
        }

        buffer.CpuSamples.Clear();
        buffer.MemUsedSamples.Clear();
        buffer.MemTotalSamples.Clear();
        buffer.DiskReadSamples.Clear();
        buffer.DiskWriteSamples.Clear();
        buffer.NetInSamples.Clear();
        buffer.NetOutSamples.Clear();
    }

    public async Task<List<MetricsSnapshotEntity>> GetHistoryAsync(
        string deviceId, DateTime from, DateTime to)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.MetricsSnapshots
            .Where(m => m.DeviceId == deviceId && m.Timestamp >= from && m.Timestamp <= to)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
    }

    private class MetricsBuffer
    {
        public List<double> CpuSamples { get; } = [];
        public List<double> MemUsedSamples { get; } = [];
        public List<double> MemTotalSamples { get; } = [];
        public List<double> DiskReadSamples { get; } = [];
        public List<double> DiskWriteSamples { get; } = [];
        public List<double> NetInSamples { get; } = [];
        public List<double> NetOutSamples { get; } = [];
        public MetricsSnapshot? LastSnapshot { get; set; }
    }
}

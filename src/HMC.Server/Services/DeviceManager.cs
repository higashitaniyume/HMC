using System.Collections.Concurrent;
using System.Text.Json;
using HMC.Server.Data;
using HMC.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace HMC.Server.Services;

/// <summary>
/// 管理设备注册、在线状态、系统信息。
/// </summary>
public class DeviceManager
{
    private readonly IServiceScopeFactory _scopeFactory;

    // 内存缓存: DeviceId → DeviceInfo
    private readonly ConcurrentDictionary<string, CachedDevice> _devices = new();

    public DeviceManager(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task RegisterAsync(DeviceInfo info, SystemOverview overview, string connectionId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await db.Devices.FirstOrDefaultAsync(d => d.DeviceId == info.DeviceId);
        if (existing != null)
        {
            existing.Name = info.Name;
            existing.Hostname = info.Hostname;
            existing.IpAddress = info.IpAddress;
            existing.OsVersion = info.OsVersion;
            existing.AgentVersion = info.AgentVersion;
            existing.ConnectionId = connectionId;
            existing.IsOnline = true;
            existing.LastSeen = DateTime.UtcNow;
            existing.SystemInfoJson = JsonSerializer.Serialize(overview);
        }
        else
        {
            db.Devices.Add(new DeviceEntity
            {
                DeviceId = info.DeviceId,
                Name = info.Name,
                Hostname = info.Hostname,
                IpAddress = info.IpAddress,
                OsVersion = info.OsVersion,
                AgentVersion = info.AgentVersion,
                ConnectionId = connectionId,
                IsOnline = true,
                FirstSeen = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
                SystemInfoJson = JsonSerializer.Serialize(overview)
            });
        }

        await db.SaveChangesAsync();

        // 更新缓存
        _devices[info.DeviceId] = new CachedDevice
        {
            Info = info,
            Overview = overview,
            ConnectionId = connectionId,
            IsOnline = true,
            LastSeen = DateTime.UtcNow
        };
    }

    public async Task<List<DeviceEntity>> GetAllAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Devices.OrderBy(d => d.Name).ToListAsync();
    }

    public async Task<DeviceEntity?> GetByIdAsync(string deviceId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Devices.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
    }

    public async Task UpdateLastSeenAsync(string deviceId)
    {
        if (_devices.TryGetValue(deviceId, out var cached))
        {
            cached.LastSeen = DateTime.UtcNow;
            if (!cached.IsOnline)
            {
                cached.IsOnline = true;
                // 异步更新 DB
                _ = Task.Run(async () =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var entity = await db.Devices.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
                    if (entity != null)
                    {
                        entity.IsOnline = true;
                        entity.LastSeen = DateTime.UtcNow;
                        await db.SaveChangesAsync();
                    }
                });
            }
        }
    }

    public async Task SetOnlineStatusAsync(string deviceId, bool isOnline)
    {
        if (_devices.TryGetValue(deviceId, out var cached))
        {
            cached.IsOnline = isOnline;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = await db.Devices.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
        if (entity != null)
        {
            entity.IsOnline = isOnline;
            entity.LastSeen = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    public async Task UpdateSystemInfoAsync(string deviceId, SystemOverview overview)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = await db.Devices.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
        if (entity != null)
        {
            entity.SystemInfoJson = JsonSerializer.Serialize(overview);
            await db.SaveChangesAsync();
        }

        if (_devices.TryGetValue(deviceId, out var cached))
        {
            cached.Overview = overview;
        }
    }

    private class CachedDevice
    {
        public DeviceInfo Info { get; set; } = new();
        public SystemOverview Overview { get; set; } = new();
        public string ConnectionId { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public DateTime LastSeen { get; set; }
    }
}

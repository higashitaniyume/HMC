using HMC.Server.Data;
using HMC.Server.Services;
using HMC.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HMC.Server.Tests.Hubs;

public class AgentHubTests : IDisposable
{
    private readonly string _dbPath;

    public AgentHubTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"hmc-test-{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { }
    }

    private IServiceScopeFactory CreateScopeFactory()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={_dbPath}"));

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        return sp.GetRequiredService<IServiceScopeFactory>();
    }

    [Fact]
    public async Task DeviceManager_RegisterAndGetAll()
    {
        var scopeFactory = CreateScopeFactory();
        var manager = new DeviceManager(scopeFactory);

        var info = new DeviceInfo
        {
            DeviceId = "test-device-1",
            Name = "TestPC",
            Hostname = "test-pc",
            IpAddress = "192.168.1.100",
            OsVersion = "Windows 11",
            AgentVersion = "1.0.0"
        };

        await manager.RegisterAsync(info, new SystemOverview { DeviceId = "test-device-1" }, "conn-123");
        var devices = await manager.GetAllAsync();

        Assert.Single(devices);
        Assert.Equal("TestPC", devices[0].Name);
        Assert.True(devices[0].IsOnline);
    }

    [Fact]
    public async Task DeviceManager_SetOnlineStatus()
    {
        var scopeFactory = CreateScopeFactory();
        var manager = new DeviceManager(scopeFactory);

        var info = new DeviceInfo { DeviceId = "d1", Name = "D1", IpAddress = "10.0.0.1" };
        await manager.RegisterAsync(info, new SystemOverview(), "c1");

        await manager.SetOnlineStatusAsync("d1", false);
        var device = await manager.GetByIdAsync("d1");

        Assert.NotNull(device);
        Assert.False(device!.IsOnline);
    }

    [Fact]
    public async Task DeviceManager_UpdateLastSeen_KeepsOnline()
    {
        var scopeFactory = CreateScopeFactory();
        var manager = new DeviceManager(scopeFactory);

        var info = new DeviceInfo { DeviceId = "d2", Name = "D2", IpAddress = "10.0.0.2" };
        await manager.RegisterAsync(info, new SystemOverview(), "c2");
        await manager.UpdateLastSeenAsync("d2");

        var device = await manager.GetByIdAsync("d2");
        Assert.NotNull(device);
        Assert.True(device!.IsOnline);
        Assert.True(device.LastSeen > DateTime.UtcNow.AddSeconds(-10));
    }

    [Fact]
    public void MetricsSnapshot_SerializeRoundTrip()
    {
        var snapshot = new MetricsSnapshot
        {
            DeviceId = "dev-1",
            Timestamp = DateTime.UtcNow,
            Cpu = new CpuMetrics { TotalPercent = 45.5, PerCorePercent = [50.1, 40.9] },
            Memory = new MemoryMetrics { TotalMB = 16384, UsedMB = 8192, PercentUsed = 50.0 }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(snapshot);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<MetricsSnapshot>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("dev-1", deserialized!.DeviceId);
        Assert.Equal(45.5, deserialized.Cpu.TotalPercent);
        Assert.Equal(2, deserialized.Cpu.PerCorePercent.Count);
        Assert.Equal(16384, deserialized.Memory.TotalMB);
    }

}

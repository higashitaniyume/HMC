using HMC.Agent.Services;
using HMC.Agent.Workers;
using Serilog;
using Serilog.Events;

namespace HMC.Agent;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Parse command line args
        bool selfTest = args.Contains("--self-test");

        // Log path: %PROGRAMDATA%/HMC/Agent/logs/
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "HMC", "Agent", "logs");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("MachineName", Environment.MachineName)
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                Path.Combine(logDir, "hmc-agent-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            Log.Information("HMC Agent starting...");
            Log.Information("Log directory: {LogDir}", logDir);

            if (selfTest)
            {
                await RunSelfTest();
                return;
            }

            var host = Host.CreateDefaultBuilder(args)
                .UseWindowsService(options =>
                {
                    options.ServiceName = "HMC Agent";
                })
                .UseSerilog()
                .ConfigureServices((context, services) =>
                {
                    ConfigureServices(services, context.Configuration);
                })
                .Build();

            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Agent terminated unexpectedly");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        var deviceId = config.GetValue("Agent:DeviceId",
            string.IsNullOrWhiteSpace(config.GetValue<string>("Agent:DeviceId"))
                ? Guid.NewGuid().ToString("D")
                : config.GetValue<string>("Agent:DeviceId")!);

        if (string.IsNullOrWhiteSpace(deviceId))
            deviceId = Guid.NewGuid().ToString("D");

        var deviceName = config.GetValue("Agent:DeviceName", Environment.MachineName);
        if (string.IsNullOrWhiteSpace(deviceName))
            deviceName = Environment.MachineName;

        var serverUrl = config.GetValue("Agent:ServerUrl", "http://localhost:5000")!;
        var metricsIntervalMs = config.GetValue("Agent:MetricsIntervalMs", 2000);

        // Services
        services.AddSingleton<IPerformanceCollector, PerformanceCollector>();
        services.AddSingleton<INetworkMonitor, NetworkMonitor>();
        services.AddSingleton<ISystemInfoCollector, SystemInfoCollector>();
        services.AddSingleton<IPingTestService, PingTestService>();
        services.AddSingleton<IIperf3Service, Iperf3Service>();

        // SignalR client (wired up in callbacks below)
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SignalRClientService>>();
            var signalR = new SignalRClientService(
                logger,
                sp,
                serverUrl,
                deviceId,
                deviceName);

            // Wire up callbacks
            signalR.OnRunPingTest = async (targets) =>
            {
                var pingService = sp.GetRequiredService<IPingTestService>();
                return await pingService.RunPingAsync(targets);
            };

            signalR.OnStartIperf3Server = async (request) =>
            {
                var iperfService = sp.GetRequiredService<IIperf3Service>();
                await iperfService.StartServerAsync(request.Port);
            };

            signalR.OnRunIperf3Client = async (request) =>
            {
                var iperfService = sp.GetRequiredService<IIperf3Service>();
                return await iperfService.RunClientAsync(request);
            };

            signalR.OnStopIperf3Server = async (port) =>
            {
                var iperfService = sp.GetRequiredService<IIperf3Service>();
                await iperfService.StopServerAsync(port);
            };

            signalR.OnCollectSystemInfo = async () =>
            {
                var sysInfo = sp.GetRequiredService<ISystemInfoCollector>();
                return await sysInfo.CollectAsync();
            };

            return signalR;
        });

        // Worker
        services.AddHostedService(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<MetricsCollectionWorker>>();
            var signalR = sp.GetRequiredService<SignalRClientService>();
            var perf = sp.GetRequiredService<IPerformanceCollector>();
            var netMon = sp.GetRequiredService<INetworkMonitor>();
            var sysInfo = sp.GetRequiredService<ISystemInfoCollector>();
            return new MetricsCollectionWorker(logger, signalR, perf, netMon, sysInfo, deviceId, metricsIntervalMs);
        });
    }

    private static async Task RunSelfTest()
    {
        Log.Information("=== HMC Agent Self-Test ===");
        var testsPassed = 0;
        var testsFailed = 0;

        // Test iPerf3
        try
        {
            Log.Information("  [TEST] iPerf3 exists...");
            var path = Path.Combine(AppContext.BaseDirectory, "tools", "iperf3.exe");
            if (!File.Exists(path))
                throw new FileNotFoundException($"iperf3.exe not found at {path}");
            Log.Information("    Path: {Path}", path);
            Log.Information("  [PASS] iPerf3 exists");
            testsPassed++;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "  [FAIL] iPerf3 exists: {Error}", ex.Message);
            testsFailed++;
        }

        // Test WMI CPU
        try
        {
            Log.Information("  [TEST] WMI CPU query...");
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT Name FROM Win32_Processor");
            var results = searcher.Get().Cast<System.Management.ManagementObject>().ToList();
            Log.Information("    CPU: {Name}", results[0]["Name"]);
            Log.Information("  [PASS] WMI CPU query");
            testsPassed++;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "  [FAIL] WMI CPU query: {Error}", ex.Message);
            testsFailed++;
        }

        // Test Ping
        try
        {
            Log.Information("  [TEST] Ping 127.0.0.1...");
            using var ping = new System.Net.NetworkInformation.Ping();
            var reply = await ping.SendPingAsync("127.0.0.1", 1000);
            if (reply.Status != System.Net.NetworkInformation.IPStatus.Success)
                throw new Exception($"Ping failed: {reply.Status}");
            Log.Information("    Latency: {Ms}ms", reply.RoundtripTime);
            Log.Information("  [PASS] Ping 127.0.0.1");
            testsPassed++;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "  [FAIL] Ping 127.0.0.1: {Error}", ex.Message);
            testsFailed++;
        }

        Log.Information("=== Self-Test Complete: {Passed} passed, {Failed} failed ===",
            testsPassed, testsFailed);
    }
}

# HMC — Host Monitoring Center

> 局域网设备监控系统 · LAN Host Monitoring System

[中文版](#chinese) | [English](#english)

---

<span id="chinese"></span>
## 中文版

### 概述

HMC 是一个局域网设备监控系统，由一个中心服务器、多个 Windows Agent 和一个 Web 管理界面组成。支持实时性能监控、网络测试、文件操作等功能。

### 架构

```
┌──────────────────────────────────────────┐
│        Linux Server (Docker)              │
│  ┌────────────────────────────────────┐   │
│  │  ASP.NET Core WebAPI + SignalR Hub  │   │
│  │  + SQLite + iPerf3                 │   │
│  │  + React SPA (内置)                 │   │
│  └────────────────────────────────────┘   │
│               ↑ UDP 9556 (发现)            │
│               ↑ TCP 5000 (HTTP/WebSocket)  │
└───────────────┼──────────────────────────┘
                │
    ┌───────────┼───────────┬──────────────┐
    │           │           │              │
┌───▼──┐   ┌───▼──┐   ┌───▼──┐      ┌───▼──┐
│Agent │   │Agent │   │Agent │ ...  │Agent │  ≤10 台 Windows
│WinSvc│   │WinSvc│   │WinSvc│      │WinSvc│
└──────┘   └──────┘   └──────┘      └──────┘
```

### 功能

| 功能 | 说明 |
|------|------|
| **实时性能监控** | CPU 使用率（总体+每核心）、内存、磁盘 I/O、网络流量、GPU 信息 |
| **TCP 连接列表** | 所有 TCP 连接，含 PID、进程名、状态 |
| **进程列表** | 所有运行中的进程，含内存占用、CPU 时间 |
| **系统信息** | OS 版本、CPU 型号、主板/BIOS、硬盘、网卡 |
| **Ping 测试** | 设备间互 Ping、设备到互联网 Ping（Google DNS / Cloudflare / Google） |
| **iPerf3 测速** | 设备间双向带宽测试、设备到服务器测速（多线程/单线程） |
| **UDP 自动发现** | Agent 通过广播自动发现局域网中的 Server，无需手动配置 IP |
| **文件日志** | Agent 和 Server 均有滚动文件日志 |

### 技术栈

| 层 | 技术 |
|---|------|
| Agent | C# .NET 9, Windows Service, WMI, P/Invoke |
| Server | C# .NET 9, ASP.NET Core, SignalR, EF Core + SQLite |
| 前端 | TypeScript, React 18, Vite, Ant Design, Recharts |
| 通信 | SignalR (WebSocket) + HTTP REST |
| 部署 | Docker Compose (Server), MSI (Agent) |

---

### 快速开始

#### 1. 启动 Server（Linux / Docker）

```bash
git clone <repo> && cd HMC
HMC_SERVER_IP=192.168.31.2 docker-compose up -d
```

Server 启动后访问 `http://<服务器IP>:5000`，即可看到 Dashboard。

如果不用 Docker：

```bash
cd src/HMC.Server
dotnet run
```

#### 2. 安装 Agent（Windows）

**方式 A：开发调试**

```bash
cd src/HMC.Agent
# 编辑 appsettings.json，ServerUrl 留空即可自动发现
dotnet run
```

**方式 B：MSI 安装包**

```bash
cd installer/HMC.Agent.Installer
.\build.ps1
# 生成的 MSI 位于 output/HMC.Agent.msi
# 在目标 Windows 设备上以管理员身份运行安装
```

MSI 会自动注册 Windows Service `HMC Agent`，开机自启。

#### 3. 启动前端（开发模式）

```bash
cd src/HMC.Frontend
pnpm install
pnpm dev
```

打开 `http://localhost:3000`。

> 生产环境无需单独启动前端，Server 已内置 React 前端。

---

### 配置

#### Agent (`appsettings.json`)

```json
{
  "Agent": {
    "DeviceId": "",
    "DeviceName": "",
    "ServerUrl": "",
    "MetricsIntervalMs": 2000
  }
}
```

| 字段 | 说明 | 默认值 |
|------|------|--------|
| `DeviceId` | 设备唯一标识，留空自动生成并持久化 | 自动生成 |
| `DeviceName` | 设备显示名称 | 主机名 |
| `ServerUrl` | Server 地址，留空则通过 UDP 自动发现 | 自动发现 |
| `MetricsIntervalMs` | 性能采集间隔（毫秒） | 2000（2 秒） |

#### Server (环境变量 / docker-compose.yml)

| 变量 | 说明 | 默认值 |
|------|------|--------|
| `HMC_SERVER_IP` | 服务器局域网 IP，用于发现响应和 iPerf3 测试 | 自动检测 |
| `ASPNETCORE_ENVIRONMENT` | 运行环境 | Production |

---

### UDP 自动发现

Agent 启动时若未配置 `ServerUrl`，会自动通过 UDP 广播发现 Server。

```
Agent ──UDP广播 "HMC_DISCOVER"──► 255.255.255.255:9556
                                     │
Server ◄──────────────────────────────┘
Server ──UDP回复 "HMC_SERVER|192.168.31.2:5000"──► Agent
Agent ──SignalR WebSocket──► ws://192.168.31.2:5000/hub/agent
```

如果 Server 在 Docker 中运行且 UDP 广播不通，有三种解决方案：
1. 使用 `network_mode: host`（docker-compose.yml 中取消注释）
2. 在 Agent 配置中手动设置 `ServerUrl`
3. 设置环境变量 `HMC_SERVER_IP`

---

### API 文档

Base URL: `http://<server>:5000`

#### 设备管理

##### GET /api/devices

获取所有已注册设备列表。

**响应：**
```json
[
  {
    "id": 1,
    "deviceId": "83583b23-ee87-41be-ac63-564f6f7f7ee6",
    "name": "MY-PC",
    "hostname": "MY-PC",
    "ipAddress": "192.168.31.131",
    "osVersion": "Microsoft Windows 11 专业版",
    "agentVersion": "1.0.0",
    "systemInfoJson": "{...}",
    "connectionId": "abc123",
    "isOnline": true,
    "firstSeen": "2026-06-09T11:00:00Z",
    "lastSeen": "2026-06-09T12:00:00Z"
  }
]
```

##### GET /api/devices/{deviceId}

获取单个设备详情。

**参数：** `deviceId` - 设备 GUID

**响应：** 单个 DeviceEntity 对象，同上

---

#### 指标查询

##### GET /api/Metrics/{deviceId}/history

查询设备历史性能数据（每分钟聚合一条）。

**参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `deviceId` | string | 是 | 设备 GUID |
| `from` | DateTime? | 否 | 起始时间，默认 1 小时前 |
| `to` | DateTime? | 否 | 结束时间，默认当前时间 |

**响应：**
```json
[
  {
    "id": 1,
    "deviceId": "83583b23-...",
    "timestamp": "2026-06-09T12:00:00Z",
    "cpuPercent": 45.5,
    "memoryUsedMB": 8192.0,
    "memoryTotalMB": 16384.0,
    "diskReadBps": 1024000.0,
    "diskWriteBps": 512000.0,
    "netInBps": 2048000.0,
    "netOutBps": 512000.0
  }
]
```

---

#### 网络测试

##### POST /api/NetworkTest/ping-all

触发全量 Ping 测试。所有在线设备互相 Ping，并对以下互联网目标进行 Ping：

- `8.8.8.8` (Google DNS)
- `1.1.1.1` (Cloudflare DNS)
- `google.com` (Google)

**响应：** `{"status": "PingAll triggered"}`

**结果获取：** 通过 SignalR `networktestresult` 事件接收实时结果。

---

##### POST /api/NetworkTest/iperf3

触发 iPerf3 带宽测试。

**参数：**

| 参数 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `source` | string | 是 | - | 源设备 ID，或 `_server_` 表示服务器 |
| `target` | string | 是 | - | 目标设备 ID，或 `_server_` 表示服务器 |
| `threads` | int | 否 | 4 | 并行线程数（1-16） |
| `duration` | int | 否 | 10 | 测试时长秒数（1-60） |

**示例：** `POST /api/NetworkTest/iperf3?source=abc123&target=_server_&threads=4&duration=10`

**响应：** `{"status": "Iperf3 test triggered", "source": "abc123", "target": "_server_"}`

---

#### 健康检查

##### GET /health

```json
{"status": "Healthy", "timestamp": "2026-06-09T12:00:00Z"}
```

---

### SignalR Hub 协议

Hub URL: `ws://<server>:5000/hub/agent`

#### Agent → Server（Hub 方法）

| 方法名 | 参数 | 说明 |
|--------|------|------|
| `registerdevice` | `DeviceInfo`, `SystemOverview` | Agent 注册/上线 |
| `pushmetrics` | `MetricsSnapshot` | 推送实时性能快照 |
| `submitpingresults` | `string deviceId`, `List<PingResult>` | 提交 Ping 测试结果 |
| `submitiperf3result` | `Iperf3Result` | 提交 iPerf3 测试结果 |
| `submitsysteminfo` | `string deviceId`, `SystemOverview` | 按需提交系统信息 |

#### Server → Agent（客户端方法）

| 方法名 | 参数 | 说明 |
|--------|------|------|
| `runpingtest` | `List<PingTarget>` | 要求 Agent 执行 Ping |
| `startiperf3server` | `Iperf3TestRequest` | 要求 Agent 启动 iPerf3 Server |
| `runiperf3client` | `Iperf3TestRequest` | 要求 Agent 运行 iPerf3 Client |
| `stopiperf3server` | `int port` | 要求 Agent 停止 iPerf3 Server |
| `collectsysteminfo` | - | 要求 Agent 采集系统信息 |

#### Server → Frontend（客户端事件）

| 事件名 | 数据 | 说明 |
|--------|------|------|
| `devicesupdated` | `List<DeviceEntity>` | 设备列表变更（上线/下线/注册） |
| `metricsupdated` | `MetricsSnapshot` | 实时性能数据（每 2 秒） |
| `networktestresult` | `{ TestType, DeviceId?, Results?, Result? }` | 网络测试结果 |
| `systeminfoupdated` | `{ DeviceId, Overview }` | 系统信息更新 |

---

### 数据模型

#### MetricsSnapshot

```json
{
  "deviceId": "guid",
  "timestamp": "2026-06-09T12:00:00Z",
  "cpu": {
    "totalPercent": 45.5,
    "perCorePercent": [50.1, 40.9, 38.2, 55.3],
    "currentFrequencyMhz": 3200.0
  },
  "memory": {
    "totalMB": 16384.0,
    "usedMB": 8192.0,
    "availableMB": 8192.0,
    "percentUsed": 50.0,
    "swapUsedMB": 2048.0,
    "swapTotalMB": 32768.0
  },
  "diskIO": {
    "readBps": 1024000.0,
    "writeBps": 512000.0,
    "readIops": 120.0,
    "writeIops": 80.0,
    "avgQueueDepth": 0.5,
    "disks": [
      {
        "name": "0 C:",
        "driveLetter": "C:",
        "readBps": 1024000.0,
        "writeBps": 512000.0,
        "diskTimePercent": 15.0
      }
    ]
  },
  "network": {
    "inBps": 2048000.0,
    "outBps": 512000.0,
    "inPps": 1500.0,
    "outPps": 800.0,
    "nics": [
      {
        "name": "Ethernet",
        "description": "Realtek PCIe GbE",
        "inBps": 2048000.0,
        "outBps": 512000.0,
        "inPps": 1500.0,
        "outPps": 800.0,
        "totalInBytes": 1234567890123,
        "totalOutBytes": 987654321098
      }
    ]
  },
  "gpu": {
    "gpus": [
      {
        "name": "NVIDIA GeForce RTX 4060",
        "utilizationPercent": 35.0,
        "memoryUsedMB": 2048.0,
        "memoryTotalMB": 8192.0,
        "temperatureCelsius": 65.0
      }
    ]
  },
  "processes": [
    {
      "pid": 1234,
      "name": "chrome",
      "workingSetMB": 512.0,
      "cpuTimeSeconds": 3600.0
    }
  ],
  "tcpConnections": [
    {
      "localAddress": "192.168.31.131",
      "localPort": 52431,
      "remoteAddress": "142.250.80.14",
      "remotePort": 443,
      "state": "Established",
      "pid": 1234,
      "processName": "chrome"
    }
  ]
}
```

#### SystemOverview

```json
{
  "deviceId": "guid",
  "osName": "Microsoft Windows 11 专业版",
  "osVersion": "10.0.26200",
  "osArchitecture": "64-bit",
  "lastBootTime": "2026-06-08T08:00:00Z",
  "timeZone": "(UTC+08:00) 北京，重庆，香港特别行政区，乌鲁木齐",
  "cpuName": "AMD Ryzen 7 7735H w/ Radeon 680M Graphics",
  "cpuPhysicalCores": 8,
  "cpuLogicalCores": 16,
  "totalMemoryBytes": 14688436224,
  "motherboardManufacturer": "ASUSTeK COMPUTER INC.",
  "motherboardProduct": "FA507NV",
  "biosVersion": "FA507NV.310",
  "gpus": [
    {
      "name": "NVIDIA GeForce RTX 4060 Laptop GPU",
      "driverVersion": "32.0.15.6094",
      "adapterRamBytes": 8589934592
    }
  ],
  "disks": [
    {
      "model": "SAMSUNG MZVL21T0HCLR-00B00",
      "driveLetter": "",
      "totalSizeBytes": 1000204886016,
      "mediaType": "SSD"
    }
  ],
  "networkAdapters": [
    {
      "name": "Realtek 8852CE WiFi 6E PCI-E NIC",
      "macAddress": "AABBCCDDEEFF",
      "ipAddresses": ["192.168.31.131"],
      "speedBps": 1201000000
    }
  ]
}
```

#### PingResult

```json
{
  "address": "8.8.8.8",
  "label": "Google DNS",
  "success": true,
  "roundTripMs": 12,
  "errorMessage": "",
  "sent": 4,
  "received": 4,
  "lost": 0,
  "minMs": 10,
  "maxMs": 15,
  "avgMs": 12
}
```

#### Iperf3Result

```json
{
  "testId": "a1b2c3d4",
  "sourceDeviceId": "guid",
  "targetDeviceId": "_server_",
  "success": true,
  "errorMessage": "",
  "bitsPerSecond": 950000000.0,
  "retransmits": 2.0,
  "jitterMs": 0.5,
  "bytesTransferred": 1187500000,
  "rawJson": "{...}",
  "timestamp": "2026-06-09T12:00:00Z"
}
```

---

### 目录结构

```
HMC/
├── docker-compose.yml
├── src/
│   ├── HMC.Shared/         # 共享模型 + 常量
│   ├── HMC.Agent/          # Windows Agent
│   │   ├── Services/       # 采集/监控/测试服务
│   │   ├── Workers/        # 后台 Worker
│   │   ├── Native/         # P/Invoke
│   │   └── tools/          # iPerf3.exe + DLL 依赖
│   ├── HMC.Server/         # Linux Server
│   │   ├── Controllers/    # REST API
│   │   ├── Hubs/           # SignalR Hub
│   │   ├── Services/       # 设备管理/指标/测试编排
│   │   ├── Data/           # EF Core DbContext
│   │   └── Middleware/     # 异常处理
│   └── HMC.Frontend/       # React + Vite
│       ├── src/hooks/      # useSignalR, useMetrics, useDevices
│       ├── src/components/ # 图表/表格/面板
│       └── src/pages/      # Dashboard, DevicePage
├── test/
│   ├── HMC.Agent.Tests/
│   └── HMC.Server.Tests/
├── installer/
│   └── HMC.Agent.Installer/ # WiX MSI 打包
└── scripts/
    └── deploy-server.ps1    # 一键部署到远程 Linux
```

---

### 常用命令

```bash
# === 开发 ===
cd src/HMC.Server && dotnet run          # 启动 Server
cd src/HMC.Agent && dotnet run           # 启动 Agent
cd src/HMC.Frontend && pnpm dev          # 启动前端 (开发模式)

# === 自检 ===
cd src/HMC.Agent && dotnet run -- --self-test

# === Docker 部署 ===
docker-compose up -d                     # 启动 Server + 前端
docker-compose logs -f server            # 查看日志

# === 远程部署 ===
.\scripts\deploy-server.ps1              # 发布+上传到 192.168.31.2

# === 构建 MSI ===
cd installer/HMC.Agent.Installer
.\build.ps1 -Version "1.0.0.0"

# === 安装 iPerf3 (Linux) ===
apt install iperf3 -y

# === 测试 ===
dotnet test
```

---

### 日志

| 组件 | 位置 |
|------|------|
| Agent | `%PROGRAMDATA%\HMC\Agent\logs\hmc-agent-{Date}.log` |
| Server (本地) | `src/HMC.Server/logs/server-{Date}.log` |
| Server (Docker) | Docker volume `server-logs` |
| Server (Linux) | `/var/log/hmc/server-{Date}.log` |

保留 30 天滚动日志。

---

### iPerf3 依赖

| 组件 | 依赖 | 安装方式 |
|------|------|----------|
| Agent | `iperf3.exe` + `cygwin1.dll` + `cygcrypto-3.dll` + `cygz.dll` | 已内置于 `src/HMC.Agent/tools/`，MSI 自动安装 |
| Server (Docker) | `iperf3` | Dockerfile 中 `apt install iperf3` |
| Server (裸机 Linux) | `iperf3` | `apt install iperf3 -y` |

---

<span id="english"></span>
## English

### Overview

HMC is a LAN host monitoring system consisting of a central server, multiple Windows agents, and a web dashboard. It provides real-time performance monitoring, network testing, and system information collection.

### Architecture

```
┌──────────────────────────────────────────┐
│        Linux Server (Docker)              │
│  ASP.NET Core WebAPI + SignalR Hub        │
│  + SQLite + iPerf3 + React SPA            │
│         ↑ UDP 9556 (discovery)            │
│         ↑ TCP 5000 (HTTP/WebSocket)       │
└─────────┼────────────────────────────────┘
          │
  ┌───────┼───────┬──────────────┐
  │       │       │              │
┌─▼──┐ ┌─▼──┐ ┌─▼──┐      ┌─▼──┐
│Agent│ │Agent│ │Agent│ ...  │Agent│  ≤10 Windows
└────┘ └────┘ └────┘      └────┘
```

### Features

| Feature | Description |
|---------|-------------|
| **Real-time Monitoring** | CPU (total+per-core), Memory, Disk I/O, Network, GPU |
| **TCP Connections** | Full connection table with PID/process name/state |
| **Process List** | All running processes with memory/CPU time |
| **System Info** | OS, CPU, Motherboard/BIOS, Disks, Network adapters |
| **Ping Test** | Device-to-device, device-to-internet (Google/Cloudflare) |
| **iPerf3 Speed Test** | Agent↔Agent bidirectional, Agent↔Server |
| **UDP Auto-Discovery** | Agents find server via broadcast, no manual IP config |
| **File Logging** | Rolling file logs for both Agent and Server |

### Tech Stack

| Layer | Technology |
|-------|------------|
| Agent | C# .NET 9, Windows Service, WMI, P/Invoke |
| Server | C# .NET 9, ASP.NET Core, SignalR, EF Core + SQLite |
| Frontend | TypeScript, React 18, Vite, Ant Design, Recharts |
| Communication | SignalR (WebSocket) + HTTP REST |
| Deployment | Docker Compose (Server), MSI (Agent) |

### Quick Start

#### 1. Start Server

```bash
git clone <repo> && cd HMC
HMC_SERVER_IP=192.168.31.2 docker-compose up -d
```

Visit `http://<server-ip>:5000`.

Without Docker:

```bash
cd src/HMC.Server && dotnet run
```

#### 2. Install Agent

**Development:**

```bash
cd src/HMC.Agent
# Leave ServerUrl empty in appsettings.json for auto-discovery
dotnet run
```

**MSI Package:**

```bash
cd installer/HMC.Agent.Installer
.\build.ps1
# MSI at output/HMC.Agent.msi — run as Administrator on target PC
```

#### 3. Frontend (Dev Mode)

```bash
cd src/HMC.Frontend
pnpm install && pnpm dev
# → http://localhost:3000
```

### API Reference

Base URL: `http://<server>:5000`

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/devices` | List all registered devices |
| GET | `/api/devices/{deviceId}` | Get single device |
| GET | `/api/Metrics/{deviceId}/history?from=&to=` | Query historical metrics |
| POST | `/api/NetworkTest/ping-all` | Trigger full Ping test |
| POST | `/api/NetworkTest/iperf3?source=&target=&threads=&duration=` | Trigger iPerf3 speed test |
| GET | `/health` | Health check |

### SignalR Hub

Hub URL: `ws://<server>:5000/hub/agent`

**Agent → Server methods:** `registerdevice`, `pushmetrics`, `submitpingresults`, `submitiperf3result`, `submitsysteminfo`

**Server → Agent methods:** `runpingtest`, `startiperf3server`, `runiperf3client`, `stopiperf3server`, `collectsysteminfo`

**Server → Frontend events:** `devicesupdated`, `metricsupdated`, `networktestresult`, `systeminfoupdated`

See the [Chinese section](#chinese) above for full data model schemas.

### Deployment Script

```bash
# One-click deploy to remote Linux server
.\scripts\deploy-server.ps1
```

The script: builds frontend → copies to server wwwroot → publishes for linux-x64 → SCP upload → installs systemd service → starts server.

### Logging

| Component | Path |
|-----------|------|
| Agent | `%PROGRAMDATA%\HMC\Agent\logs\hmc-agent-{Date}.log` |
| Server (local) | `src/HMC.Server/logs/server-{Date}.log` |
| Server (Docker) | Docker volume `server-logs` |
| Server (Linux) | `/var/log/hmc/server-{Date}.log` |

30-day retention, rolling files.

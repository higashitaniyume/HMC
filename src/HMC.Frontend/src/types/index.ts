// ===== Device =====
export interface DeviceInfo {
  deviceId: string;
  name: string;
  hostname: string;
  ipAddress: string;
  osVersion: string;
  agentVersion: string;
}

export interface SystemOverview {
  deviceId: string;
  osName: string;
  osVersion: string;
  osArchitecture: string;
  lastBootTime: string;
  timeZone: string;
  cpuName: string;
  cpuPhysicalCores: number;
  cpuLogicalCores: number;
  totalMemoryBytes: number;
  motherboardManufacturer: string;
  motherboardProduct: string;
  biosVersion: string;
  gpus: GpuInfo[];
  disks: DiskInfo[];
  networkAdapters: NetworkAdapterInfo[];
}

export interface GpuInfo {
  name: string;
  driverVersion: string;
  adapterRamBytes: number;
}

export interface DiskInfo {
  model: string;
  driveLetter: string;
  totalSizeBytes: number;
  mediaType: string;
}

export interface NetworkAdapterInfo {
  name: string;
  macAddress: string;
  ipAddresses: string[];
  speedBps: number;
}

// ===== Metrics =====
export interface MetricsSnapshot {
  deviceId: string;
  timestamp: string;
  cpu: CpuMetrics;
  memory: MemoryMetrics;
  diskIO: DiskIOMetrics;
  network: NetworkMetrics;
  gpu: GpuMetrics | null;
  processes: ProcessSnapshot[];
  tcpConnections: TcpConnectionInfo[];
}

export interface CpuMetrics {
  totalPercent: number;
  perCorePercent: number[];
  currentFrequencyMhz: number;
}

export interface MemoryMetrics {
  totalMB: number;
  usedMB: number;
  availableMB: number;
  percentUsed: number;
  swapUsedMB: number;
  swapTotalMB: number;
}

export interface DiskIOMetrics {
  readBps: number;
  writeBps: number;
  readIops: number;
  writeIops: number;
  avgQueueDepth: number;
  disks: PerDiskMetrics[];
}

export interface PerDiskMetrics {
  name: string;
  driveLetter: string;
  readBps: number;
  writeBps: number;
  diskTimePercent: number;
}

export interface NetworkMetrics {
  inBps: number;
  outBps: number;
  inPps: number;
  outPps: number;
  nics: PerNicMetrics[];
}

export interface PerNicMetrics {
  name: string;
  description: string;
  inBps: number;
  outBps: number;
  inPps: number;
  outPps: number;
  totalInBytes: number;
  totalOutBytes: number;
}

export interface GpuMetrics {
  gpus: PerGpuMetrics[];
}

export interface PerGpuMetrics {
  name: string;
  utilizationPercent: number;
  memoryUsedMB: number;
  memoryTotalMB: number;
  temperatureCelsius: number;
}

export interface ProcessSnapshot {
  pid: number;
  name: string;
  workingSetMB: number;
  cpuTimeSeconds: number;
}

export interface TcpConnectionInfo {
  localAddress: string;
  localPort: number;
  remoteAddress: string;
  remotePort: number;
  state: string;
  pid: number;
  processName: string;
}

// ===== Network Test =====
export interface PingTarget {
  address: string;
  label: string;
}

export interface PingResult {
  address: string;
  label: string;
  success: boolean;
  roundTripMs: number;
  errorMessage: string;
  sent: number;
  received: number;
  lost: number;
  minMs: number;
  maxMs: number;
  avgMs: number;
}

export interface Iperf3Result {
  testId: string;
  sourceDeviceId: string;
  targetDeviceId: string;
  success: boolean;
  errorMessage: string;
  bitsPerSecond: number;
  retransmits: number;
  jitterMs: number;
  bytesTransferred: number;
  rawJson: string;
  timestamp: string;
}

// ===== Device Entity (from DB) =====
export interface DeviceEntity {
  id: number;
  deviceId: string;
  name: string;
  hostname: string;
  ipAddress: string;
  osVersion: string;
  agentVersion: string;
  systemInfoJson: string;
  connectionId: string;
  isOnline: boolean;
  firstSeen: string;
  lastSeen: string;
}

// ===== History Metrics =====
export interface MetricsHistoryItem {
  id: number;
  deviceId: string;
  timestamp: string;
  cpuPercent: number;
  memoryUsedMB: number;
  memoryTotalMB: number;
  diskReadBps: number;
  diskWriteBps: number;
  netInBps: number;
  netOutBps: number;
}

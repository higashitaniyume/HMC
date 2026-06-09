const BASE_URL = '/api';

export async function fetchDevices(): Promise<any[]> {
  const res = await fetch(`${BASE_URL}/devices`);
  return res.json();
}

export async function fetchDevice(deviceId: string): Promise<any> {
  const res = await fetch(`${BASE_URL}/devices/${deviceId}`);
  if (!res.ok) throw new Error(`Device ${deviceId} not found`);
  return res.json();
}

export async function fetchMetricsHistory(
  deviceId: string,
  from?: Date,
  to?: Date
): Promise<any[]> {
  const params = new URLSearchParams();
  if (from) params.set('from', from.toISOString());
  if (to) params.set('to', to.toISOString());
  const res = await fetch(
    `${BASE_URL}/metrics/${deviceId}/history?${params}`
  );
  return res.json();
}

export async function triggerPingAll(): Promise<void> {
  await fetch(`${BASE_URL}/network-test/ping-all`, { method: 'POST' });
}

export async function triggerIperf3(
  source: string,
  target: string,
  threads = 4,
  duration = 10
): Promise<void> {
  const params = new URLSearchParams({
    source,
    target,
    threads: String(threads),
    duration: String(duration),
  });
  await fetch(`${BASE_URL}/network-test/iperf3?${params}`, { method: 'POST' });
}

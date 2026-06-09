import { useState, useEffect, useRef } from 'react';
import { Card, Button, Select, Space, Table, Tag, message, InputNumber, Typography, Collapse } from 'antd';
import { ThunderboltOutlined, WifiOutlined, ClearOutlined } from '@ant-design/icons';
import { useSignalRContext } from '../hooks/SignalRContext';
import * as api from '../api/client';
import type { DeviceEntity, PingResult, Iperf3Result } from '../types';

interface Props {
  devices: DeviceEntity[];
}

interface PingTestRun {
  deviceId: string;
  deviceName: string;
  timestamp: Date;
  results: PingResult[];
}

interface Iperf3TestRun {
  sourceName: string;
  targetName: string;
  timestamp: Date;
  result: Iperf3Result;
}

export default function NetworkTestPanel({ devices }: Props) {
  const { on } = useSignalRContext();
  const [pingResults, setPingResults] = useState<PingTestRun[]>([]);
  const [iperfResults, setIperfResults] = useState<Iperf3TestRun[]>([]);
  const [pingLoading, setPingLoading] = useState(false);
  const [iperfLoading, setIperfLoading] = useState(false);
  const [iperfSource, setIperfSource] = useState<string>('');
  const [iperfTarget, setIperfTarget] = useState<string>('');
  const [threads, setThreads] = useState(4);
  const [duration, setDuration] = useState(10);
  const resultsEndRef = useRef<HTMLDivElement>(null);

  const onlineDevices = devices.filter((d) => d.isOnline);

  useEffect(() => {
    const cleanup = on('networktestresult', (result: any) => {
      if (result.testType === 'Ping') {
        const deviceName = devices.find((d) => d.deviceId === result.deviceId)?.name || result.deviceId;
        setPingResults((prev) => [
          {
            deviceId: result.deviceId,
            deviceName,
            timestamp: new Date(),
            results: result.results,
          },
          ...prev,
        ]);
        setPingLoading(false);
      } else if (result.testType === 'Iperf3') {
        const srcName = devices.find((d) => d.deviceId === result.result.sourceDeviceId)?.name || '?';
        const tgtName = devices.find((d) => d.deviceId === result.result.targetDeviceId)?.name || '?';
        setIperfResults((prev) => [
          {
            sourceName: srcName,
            targetName: tgtName,
            timestamp: new Date(),
            result: result.result,
          },
          ...prev,
        ]);
        setIperfLoading(false);
      }
    });
    return cleanup;
  }, [on, devices]);

  const handlePingAll = async () => {
    setPingLoading(true);
    try {
      await api.triggerPingAll();
    } catch {
      setPingLoading(false);
      message.error('Failed to trigger ping test');
    }
  };

  const handleIperf3 = async () => {
    if (!iperfSource || !iperfTarget) {
      message.warning('Select source and target devices');
      return;
    }
    setIperfLoading(true);
    try {
      await api.triggerIperf3(iperfSource, iperfTarget, threads, duration);
    } catch {
      setIperfLoading(false);
      message.error('Failed to trigger iPerf3 test');
    }
  };

  return (
    <div>
      <Card size="small" style={{ marginBottom: 16 }}>
        <Space direction="vertical" style={{ width: '100%' }} size="middle">
          <div>
            <Typography.Text strong>
              <WifiOutlined /> Ping Test
            </Typography.Text>
            <br />
            <Button
              type="primary"
              onClick={handlePingAll}
              loading={pingLoading}
              disabled={onlineDevices.length === 0}
              style={{ marginTop: 8 }}
            >
              Ping All ({onlineDevices.length} devices)
            </Button>
          </div>

          <div>
            <Typography.Text strong>
              <ThunderboltOutlined /> iPerf3 Speed Test
            </Typography.Text>
            <div style={{ marginTop: 8 }}>
              <Space wrap>
                <Select
                  placeholder="Source device"
                  style={{ width: 180 }}
                  value={iperfSource || undefined}
                  onChange={setIperfSource}
                  options={onlineDevices.map((d) => ({
                    value: d.deviceId,
                    label: `${d.name} (${d.ipAddress})`,
                  }))}
                />
                <span>→</span>
                <Select
                  placeholder="Target device"
                  style={{ width: 180 }}
                  value={iperfTarget || undefined}
                  onChange={setIperfTarget}
                  options={onlineDevices.map((d) => ({
                    value: d.deviceId,
                    label: `${d.name} (${d.ipAddress})`,
                  }))}
                />
                <span>Threads:</span>
                <InputNumber min={1} max={16} value={threads} onChange={(v) => setThreads(v || 4)} />
                <span>Duration (s):</span>
                <InputNumber min={1} max={60} value={duration} onChange={(v) => setDuration(v || 10)} />
                <Button type="primary" onClick={handleIperf3} loading={iperfLoading}>
                  Run iPerf3
                </Button>
              </Space>
            </div>
          </div>
        </Space>
      </Card>

      {/* Ping Results */}
      {pingResults.length > 0 && (
        <Card
          title={`Ping Results (${pingResults.length})`}
          size="small"
          style={{ marginBottom: 16 }}
          extra={
            <Button size="small" icon={<ClearOutlined />} onClick={() => setPingResults([])}>
              Clear
            </Button>
          }
        >
          <Collapse size="small" items={pingResults.map((r, i) => ({
            key: i,
            label: `${r.deviceName} — ${r.timestamp.toLocaleTimeString()}`,
            children: <PingResultTable results={r.results} />,
          }))} />
          <div ref={resultsEndRef} />
        </Card>
      )}

      {/* iPerf3 Results */}
      {iperfResults.length > 0 && (
        <Card
          title={`Speed Test Results (${iperfResults.length})`}
          size="small"
          extra={
            <Button size="small" icon={<ClearOutlined />} onClick={() => setIperfResults([])}>
              Clear
            </Button>
          }
        >
          {iperfResults.map((r, i) => (
            <div key={i} style={{ marginBottom: 8 }}>
              <Typography.Text strong>
                {r.sourceName} → {r.targetName}
                {' — '}{r.timestamp.toLocaleTimeString()}
              </Typography.Text>
              <Iperf3ResultCard result={r.result} />
            </div>
          ))}
        </Card>
      )}
    </div>
  );
}

function PingResultTable({ results }: { results: PingResult[] }) {
  const columns = [
    { title: 'Target', dataIndex: 'label', key: 'label', width: 140 },
    { title: 'Address', dataIndex: 'address', key: 'address', width: 140 },
    { title: 'Status', dataIndex: 'success', key: 'success', width: 80,
      render: (s: boolean) => <Tag color={s ? 'green' : 'red'}>{s ? 'OK' : 'FAIL'}</Tag> },
    { title: 'Avg', dataIndex: 'avgMs', key: 'avg', width: 70,
      render: (v: number, r: PingResult) => r.success ? `${v}ms` : '-' },
    { title: 'Min/Max', key: 'minmax', width: 100,
      render: (_: any, r: PingResult) => r.success ? `${r.minMs}/${r.maxMs}ms` : '-' },
    { title: 'Loss', key: 'loss', width: 80,
      render: (_: any, r: PingResult) => `${r.lost}/${r.sent}` },
  ];
  return <Table dataSource={results} columns={columns} rowKey="address" size="small" pagination={false} />;
}

function Iperf3ResultCard({ result }: { result: Iperf3Result }) {
  if (!result.success) {
    return <div style={{ padding: 4 }}><Tag color="red">Failed</Tag> {result.errorMessage}</div>;
  }
  return (
    <div style={{ padding: 4 }}>
      <Space size="large">
        <span><strong>Speed:</strong> {(result.bitsPerSecond / 1_000_000).toFixed(2)} Mbps</span>
        <span><strong>Transferred:</strong> {(result.bytesTransferred / 1_000_000).toFixed(1)} MB</span>
        {result.jitterMs > 0 && <span><strong>Jitter:</strong> {result.jitterMs.toFixed(2)} ms</span>}
        {result.retransmits > 0 && <span><strong>Retransmits:</strong> {result.retransmits}</span>}
      </Space>
    </div>
  );
}

import { useState, useEffect, useRef } from 'react';
import { Card, Button, Select, Space, Table, Tag, message, InputNumber, Typography } from 'antd';
import { ThunderboltOutlined, WifiOutlined } from '@ant-design/icons';
import { useSignalR } from '../hooks/useSignalR';
import * as api from '../api/client';
import type { DeviceEntity, PingResult, Iperf3Result } from '../types';

interface Props {
  devices: DeviceEntity[];
}

interface TestResult {
  type: 'Ping' | 'Iperf3';
  deviceId: string;
  timestamp: Date;
  data: PingResult[] | Iperf3Result;
}

export default function NetworkTestPanel({ devices }: Props) {
  const { on } = useSignalR('/hub/agent');
  const [results, setResults] = useState<TestResult[]>([]);
  const [testing, setTesting] = useState(false);
  const [iperfSource, setIperfSource] = useState<string>('');
  const [iperfTarget, setIperfTarget] = useState<string>('');
  const [threads, setThreads] = useState(4);
  const [duration, setDuration] = useState(10);
  const resultsEndRef = useRef<HTMLDivElement>(null);

  const onlineDevices = devices.filter((d) => d.isOnline);

  useEffect(() => {
    const cleanup1 = on('NetworkTestResult', (result: any) => {
      if (result.testType === 'Ping') {
        setResults((prev) => [
          ...prev,
          {
            type: 'Ping',
            deviceId: result.deviceId,
            timestamp: new Date(),
            data: result.results,
          },
        ]);
      } else if (result.testType === 'Iperf3') {
        setResults((prev) => [
          ...prev,
          {
            type: 'Iperf3',
            deviceId: result.result.sourceDeviceId,
            timestamp: new Date(),
            data: result.result,
          },
        ]);
      }
      setTesting(false);
    });

    return cleanup1;
  }, [on]);

  useEffect(() => {
    resultsEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [results.length]);

  const handlePingAll = async () => {
    setTesting(true);
    try {
      await api.triggerPingAll();
      message.info('Ping test dispatched to all devices');
    } catch {
      setTesting(false);
      message.error('Failed to trigger ping test');
    }
  };

  const handleIperf3 = async () => {
    if (!iperfSource || !iperfTarget) {
      message.warning('Select source and target devices');
      return;
    }
    setTesting(true);
    try {
      await api.triggerIperf3(iperfSource, iperfTarget, threads, duration);
      message.info(`iPerf3 test: ${iperfSource} → ${iperfTarget}`);
    } catch {
      setTesting(false);
      message.error('Failed to trigger iPerf3 test');
    }
  };

  return (
    <div>
      <Card size="small" style={{ marginBottom: 16 }}>
        <Space direction="vertical" style={{ width: '100%' }} size="middle">
          {/* Ping Section */}
          <div>
            <Typography.Text strong>
              <WifiOutlined /> Ping Test
            </Typography.Text>
            <br />
            <Button
              type="primary"
              icon={<ThunderboltOutlined />}
              onClick={handlePingAll}
              loading={testing}
              disabled={onlineDevices.length === 0}
              style={{ marginTop: 8 }}
            >
              Ping All ({onlineDevices.length} devices)
            </Button>
          </div>

          {/* iPerf3 Section */}
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
                <Button
                  type="primary"
                  onClick={handleIperf3}
                  loading={testing}
                >
                  Run iPerf3
                </Button>
              </Space>
            </div>
          </div>
        </Space>
      </Card>

      {/* Results */}
      {results.length > 0 && (
        <Card title="Test Results" size="small">
          {results.map((r, i) => (
            <div key={i} style={{ marginBottom: 16 }}>
              <Typography.Text strong>
                [{r.type}] {devices.find((d) => d.deviceId === r.deviceId)?.name || r.deviceId}
                {' — '}
                {r.timestamp.toLocaleTimeString()}
              </Typography.Text>
              {r.type === 'Ping' ? (
                <PingResultTable results={r.data as PingResult[]} />
              ) : (
                <Iperf3ResultCard result={r.data as Iperf3Result} />
              )}
            </div>
          ))}
          <div ref={resultsEndRef} />
        </Card>
      )}
    </div>
  );
}

function PingResultTable({ results }: { results: PingResult[] }) {
  const columns = [
    { title: 'Target', dataIndex: 'label', key: 'label', width: 140 },
    { title: 'Address', dataIndex: 'address', key: 'address', width: 140 },
    {
      title: 'Status',
      dataIndex: 'success',
      key: 'success',
      width: 80,
      render: (s: boolean) => (
        <Tag color={s ? 'green' : 'red'}>{s ? 'OK' : 'FAIL'}</Tag>
      ),
    },
    {
      title: 'Avg',
      dataIndex: 'avgMs',
      key: 'avg',
      width: 70,
      render: (v: number, r: PingResult) => r.success ? `${v}ms` : '-',
    },
    {
      title: 'Min/Max',
      key: 'minmax',
      width: 100,
      render: (_: any, r: PingResult) =>
        r.success ? `${r.minMs}/${r.maxMs}ms` : '-',
    },
    {
      title: 'Loss',
      key: 'loss',
      width: 80,
      render: (_: any, r: PingResult) =>
        `${r.lost}/${r.sent} (${((r.lost / r.sent) * 100).toFixed(0)}%)`,
    },
  ];

  return (
    <Table
      dataSource={results}
      columns={columns}
      rowKey="address"
      size="small"
      pagination={false}
    />
  );
}

function Iperf3ResultCard({ result }: { result: Iperf3Result }) {
  if (!result.success) {
    return (
      <div style={{ padding: 8 }}>
        <Tag color="red">Failed</Tag>
        <span style={{ marginLeft: 8 }}>{result.errorMessage}</span>
      </div>
    );
  }

  return (
    <div style={{ padding: 8 }}>
      <Space size="large">
        <span>
          <strong>Speed:</strong>{' '}
          {(result.bitsPerSecond / 1_000_000).toFixed(2)} Mbps
        </span>
        <span>
          <strong>Transferred:</strong>{' '}
          {(result.bytesTransferred / (1000 * 1000)).toFixed(1)} MB
        </span>
        {result.jitterMs > 0 && (
          <span>
            <strong>Jitter:</strong> {result.jitterMs.toFixed(2)} ms
          </span>
        )}
        {result.retransmits > 0 && (
          <span>
            <strong>Retransmits:</strong> {result.retransmits}
          </span>
        )}
      </Space>
    </div>
  );
}

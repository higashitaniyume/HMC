import { Card, Statistic, Row, Col } from 'antd';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import type { MetricsSnapshot } from '../types';
import { useMetrics } from '../hooks/useMetrics';

interface Props { metrics: MetricsSnapshot }

export default function NetworkChart({ metrics }: Props) {
  const { getChartData } = useMetrics();
  const buf = getChartData(metrics.deviceId);

  const data = buf
    ? buf.timestamps.map((t, i) => ({
        time: t,
        in: buf.netIn[i],
        out: buf.netOut[i],
      }))
    : [{ time: new Date(metrics.timestamp).toLocaleTimeString(), in: metrics.network.inBps, out: metrics.network.outBps }];

  return (
    <Card title="Network" size="small" style={{ marginBottom: 16 }}>
      <Row gutter={16} style={{ marginBottom: 12 }}>
        <Col span={8}>
          <Statistic title="Download" value={formatBytes(metrics.network.inBps)} suffix="/s" valueStyle={{ color: '#1677ff' }} />
        </Col>
        <Col span={8}>
          <Statistic title="Upload" value={formatBytes(metrics.network.outBps)} suffix="/s" valueStyle={{ color: '#fa8c16' }} />
        </Col>
        <Col span={8}>
          <Statistic title="NICs" value={metrics.network.nics.length} />
        </Col>
      </Row>
      <ResponsiveContainer width="100%" height={200}>
        <LineChart data={data}>
          <CartesianGrid strokeDasharray="3 3" stroke="#333" />
          <XAxis dataKey="time" stroke="#666" fontSize={11} />
          <YAxis stroke="#666" fontSize={11} tickFormatter={(v: number) => formatBytes(v)} />
          <Tooltip
            contentStyle={{ background: '#1f1f1f', border: '1px solid #333' }}
            formatter={(value: number) => [formatBytes(value) + '/s']}
          />
          <Line type="monotone" dataKey="in" stroke="#1677ff" dot={false} strokeWidth={2} name="In" />
          <Line type="monotone" dataKey="out" stroke="#fa8c16" dot={false} strokeWidth={2} name="Out" />
        </LineChart>
      </ResponsiveContainer>
    </Card>
  );
}

function formatBytes(bytes: number): string {
  if (bytes >= 1_000_000) return (bytes / 1_000_000).toFixed(1) + ' MB';
  if (bytes >= 1_000) return (bytes / 1_000).toFixed(1) + ' KB';
  return bytes.toFixed(0) + ' B';
}

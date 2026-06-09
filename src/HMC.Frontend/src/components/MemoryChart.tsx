import { Card, Progress, Statistic, Row, Col } from 'antd';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import type { MetricsSnapshot } from '../types';
import { useMetrics } from '../hooks/useMetrics';

interface Props { metrics: MetricsSnapshot }

export default function MemoryChart({ metrics }: Props) {
  const { getChartData } = useMetrics();
  const buf = getChartData(metrics.deviceId);

  const data = buf
    ? buf.timestamps.map((t, i) => ({ time: t, mem: buf.mem[i] }))
    : [{ time: new Date(metrics.timestamp).toLocaleTimeString(), mem: metrics.memory.percentUsed }];

  return (
    <Card title="Memory" size="small" style={{ marginBottom: 16 }}>
      <Row gutter={16} style={{ marginBottom: 12 }}>
        <Col span={6}>
          <Statistic title="Used" value={metrics.memory.usedMB.toFixed(0)} suffix="MB" />
        </Col>
        <Col span={6}>
          <Statistic title="Total" value={metrics.memory.totalMB.toFixed(0)} suffix="MB" />
        </Col>
        <Col span={6}>
          <Statistic title="Available" value={metrics.memory.availableMB.toFixed(0)} suffix="MB" />
        </Col>
        <Col span={6}>
          <Progress type="circle" percent={Math.round(metrics.memory.percentUsed)} size={60} />
        </Col>
      </Row>
      <ResponsiveContainer width="100%" height={160}>
        <LineChart data={data}>
          <CartesianGrid strokeDasharray="3 3" stroke="#333" />
          <XAxis dataKey="time" stroke="#666" fontSize={11} />
          <YAxis domain={[0, 100]} unit="%" stroke="#666" fontSize={11} />
          <Tooltip contentStyle={{ background: '#1f1f1f', border: '1px solid #333' }} />
          <Line type="monotone" dataKey="mem" stroke="#52c41a" dot={false} strokeWidth={2} name="Memory %" />
        </LineChart>
      </ResponsiveContainer>
    </Card>
  );
}

import { Card, Statistic, Row, Col } from 'antd';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import type { MetricsSnapshot } from '../types';

interface Props { metrics: MetricsSnapshot }

export default function DiskIOChart({ metrics }: Props) {
  const diskData = metrics.diskIO.disks.map((d) => ({
    name: d.name.replace('_Total', 'All'),
    read: d.readBps,
    write: d.writeBps,
  }));

  return (
    <Card title="Disk I/O" size="small" style={{ marginBottom: 16 }}>
      <Row gutter={16} style={{ marginBottom: 12 }}>
        <Col span={8}>
          <Statistic title="Read" value={formatBytes(metrics.diskIO.readBps)} suffix="/s" />
        </Col>
        <Col span={8}>
          <Statistic title="Write" value={formatBytes(metrics.diskIO.writeBps)} suffix="/s" />
        </Col>
        <Col span={8}>
          <Statistic title="Queue Depth" value={metrics.diskIO.avgQueueDepth.toFixed(2)} />
        </Col>
      </Row>
      {diskData.length > 0 && (
        <ResponsiveContainer width="100%" height={200}>
          <BarChart data={diskData}>
            <CartesianGrid strokeDasharray="3 3" stroke="#333" />
            <XAxis dataKey="name" stroke="#666" fontSize={11} />
            <YAxis stroke="#666" fontSize={11} tickFormatter={(v: number) => formatBytes(v)} />
            <Tooltip
              contentStyle={{ background: '#1f1f1f', border: '1px solid #333' }}
              formatter={(value: number) => [formatBytes(value) + '/s']}
            />
            <Bar dataKey="read" fill="#1677ff" name="Read" />
            <Bar dataKey="write" fill="#fa8c16" name="Write" />
          </BarChart>
        </ResponsiveContainer>
      )}
    </Card>
  );
}

function formatBytes(bytes: number): string {
  if (bytes >= 1_000_000) return (bytes / 1_000_000).toFixed(1) + ' MB';
  if (bytes >= 1_000) return (bytes / 1_000).toFixed(1) + ' KB';
  return bytes.toFixed(0) + ' B';
}

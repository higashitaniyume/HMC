import { Card } from 'antd';
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from 'recharts';
import type { MetricsSnapshot } from '../types';
import { useMetrics } from '../hooks/useMetrics';

interface Props {
  metrics: MetricsSnapshot;
}

export default function CpuChart({ metrics }: Props) {
  const { getChartData } = useMetrics();
  const buf = getChartData(metrics.deviceId);

  const data = buf
    ? buf.timestamps.map((t, i) => ({
        time: t,
        cpu: buf.cpu[i],
      }))
    : [{ time: new Date(metrics.timestamp).toLocaleTimeString(), cpu: metrics.cpu.totalPercent }];

  return (
    <Card title="CPU Usage" size="small" style={{ marginBottom: 16 }}>
      <ResponsiveContainer width="100%" height={200}>
        <LineChart data={data}>
          <CartesianGrid strokeDasharray="3 3" stroke="#333" />
          <XAxis dataKey="time" stroke="#666" fontSize={11} />
          <YAxis domain={[0, 100]} unit="%" stroke="#666" fontSize={11} />
          <Tooltip
            contentStyle={{ background: '#1f1f1f', border: '1px solid #333' }}
          />
          <Line
            type="monotone"
            dataKey="cpu"
            stroke="#1677ff"
            dot={false}
            strokeWidth={2}
            name="CPU %"
          />
        </LineChart>
      </ResponsiveContainer>
      <div style={{ fontSize: 11, color: '#888', marginTop: 4 }}>
        {metrics.cpu.perCorePercent.length} cores ·{' '}
        {metrics.cpu.currentFrequencyMhz} MHz
      </div>
    </Card>
  );
}

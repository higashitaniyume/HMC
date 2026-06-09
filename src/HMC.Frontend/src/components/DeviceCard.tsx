import { Card, Progress, Tag, Space, Typography } from 'antd';
import { useNavigate } from 'react-router-dom';
import {
  DesktopOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
} from '@ant-design/icons';
import type { DeviceEntity, MetricsSnapshot } from '../types';

interface Props {
  device: DeviceEntity;
  metrics: MetricsSnapshot | null;
}

export default function DeviceCard({ device, metrics }: Props) {
  const navigate = useNavigate();

  return (
    <Card
      hoverable
      size="small"
      onClick={() => navigate(`/device/${device.deviceId}`)}
      title={
        <Space>
          <DesktopOutlined />
          <span>{device.name}</span>
          {device.isOnline ? (
            <Tag color="green" icon={<CheckCircleOutlined />}>
              Online
            </Tag>
          ) : (
            <Tag color="red" icon={<CloseCircleOutlined />}>
              Offline
            </Tag>
          )}
        </Space>
      }
      style={{ height: '100%' }}
    >
      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
        {device.ipAddress} · {device.hostname}
      </Typography.Text>

      {metrics && device.isOnline ? (
        <div style={{ marginTop: 12 }}>
          <div style={{ marginBottom: 8 }}>
            <Typography.Text style={{ fontSize: 12 }}>CPU</Typography.Text>
            <Progress
              percent={Math.round(metrics.cpu.totalPercent)}
              size="small"
              status={metrics.cpu.totalPercent > 90 ? 'exception' : 'active'}
            />
          </div>
          <div style={{ marginBottom: 8 }}>
            <Typography.Text style={{ fontSize: 12 }}>Memory</Typography.Text>
            <Progress
              percent={Math.round(metrics.memory.percentUsed)}
              size="small"
              status={metrics.memory.percentUsed > 90 ? 'exception' : 'active'}
            />
          </div>
          <Space style={{ fontSize: 11 }} split="·">
            <Typography.Text type="secondary">
              ↓ {formatBytes(metrics.network.inBps)}/s
            </Typography.Text>
            <Typography.Text type="secondary">
              ↑ {formatBytes(metrics.network.outBps)}/s
            </Typography.Text>
          </Space>
        </div>
      ) : (
        <div style={{ marginTop: 12 }}>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {device.isOnline ? 'Waiting for metrics...' : 'Last seen: ' + new Date(device.lastSeen).toLocaleString()}
          </Typography.Text>
        </div>
      )}
    </Card>
  );
}

function formatBytes(bytes: number): string {
  if (bytes >= 1_000_000) return (bytes / 1_000_000).toFixed(1) + ' MB';
  if (bytes >= 1_000) return (bytes / 1_000).toFixed(1) + ' KB';
  return bytes.toFixed(0) + ' B';
}

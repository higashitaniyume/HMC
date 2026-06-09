import { useParams, useNavigate } from 'react-router-dom';
import { Button, Spin, Typography, Tabs, Descriptions, Tag, Card } from 'antd';
import { ArrowLeftOutlined } from '@ant-design/icons';
import { useEffect, useState } from 'react';
import * as api from '../api/client';
import { useMetrics } from '../hooks/useMetrics';
import type { DeviceEntity, SystemOverview } from '../types';
import CpuChart from '../components/CpuChart';
import MemoryChart from '../components/MemoryChart';
import DiskIOChart from '../components/DiskIOChart';
import NetworkChart from '../components/NetworkChart';
import ConnectionsTable from '../components/ConnectionsTable';
import ProcessTable from '../components/ProcessTable';
import SystemOverviewPanel from '../components/SystemOverview';

export default function DevicePage() {
  const { deviceId } = useParams<{ deviceId: string }>();
  const navigate = useNavigate();
  const { latestMetrics } = useMetrics();

  const [device, setDevice] = useState<DeviceEntity | null>(null);
  const [overview, setOverview] = useState<SystemOverview | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!deviceId) return;
    api.fetchDevice(deviceId).then((d) => {
      setDevice(d);
      try {
        setOverview(JSON.parse(d.systemInfoJson));
      } catch {
        setOverview(null);
      }
      setLoading(false);
    });
  }, [deviceId]);

  if (loading) return <Spin size="large" style={{ display: 'block', margin: '100px auto' }} />;
  if (!device) return <Typography.Text>Device not found</Typography.Text>;

  const metrics = latestMetrics.get(device.deviceId) || null;

  const tabItems = [
    {
      key: 'overview',
      label: 'Performance',
      children: (
        <div>
          <Card size="small" style={{ marginBottom: 16 }}>
            <Descriptions size="small" column={4}>
              <Descriptions.Item label="Hostname">{device.hostname}</Descriptions.Item>
              <Descriptions.Item label="IP">{device.ipAddress}</Descriptions.Item>
              <Descriptions.Item label="OS">{device.osVersion}</Descriptions.Item>
              <Descriptions.Item label="Agent">{device.agentVersion}</Descriptions.Item>
              <Descriptions.Item label="Status">
                <Tag color={device.isOnline ? 'green' : 'red'}>
                  {device.isOnline ? 'Online' : 'Offline'}
                </Tag>
              </Descriptions.Item>
              <Descriptions.Item label="Last Seen">
                {new Date(device.lastSeen).toLocaleString()}
              </Descriptions.Item>
            </Descriptions>
          </Card>

          {metrics ? (
            <>
              <CpuChart metrics={metrics} />
              <MemoryChart metrics={metrics} />
              <DiskIOChart metrics={metrics} />
              <NetworkChart metrics={metrics} />
            </>
          ) : (
            <Typography.Text type="secondary">No metrics data yet</Typography.Text>
          )}
        </div>
      ),
    },
    {
      key: 'system',
      label: 'System Info',
      children: overview ? (
        <SystemOverviewPanel overview={overview} />
      ) : (
        <Typography.Text type="secondary">System info not available</Typography.Text>
      ),
    },
    {
      key: 'connections',
      label: `TCP Connections (${metrics?.tcpConnections?.length || 0})`,
      children: <ConnectionsTable connections={metrics?.tcpConnections || []} />,
    },
    {
      key: 'processes',
      label: `Processes (${metrics?.processes?.length || 0})`,
      children: <ProcessTable processes={metrics?.processes || []} />,
    },
  ];

  return (
    <div>
      <Button
        icon={<ArrowLeftOutlined />}
        onClick={() => navigate('/')}
        style={{ marginBottom: 16 }}
      >
        Back to Dashboard
      </Button>
      <Typography.Title level={3}>{device.name}</Typography.Title>
      <Tabs defaultActiveKey="overview" items={tabItems} />
    </div>
  );
}

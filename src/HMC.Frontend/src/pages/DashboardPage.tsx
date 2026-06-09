import { Row, Col, Spin, Empty, Typography } from 'antd';
import { useDevices } from '../hooks/useDevices';
import { useMetrics } from '../hooks/useMetrics';
import DeviceCard from '../components/DeviceCard';
import NetworkTestPanel from '../components/NetworkTestPanel';

export default function DashboardPage() {
  const { devices, loading } = useDevices();
  const { latestMetrics, isConnected } = useMetrics();

  if (loading) {
    return (
      <div style={{ textAlign: 'center', padding: 100 }}>
        <Spin size="large" />
      </div>
    );
  }

  if (devices.length === 0) {
    return (
      <Empty
        description="No devices registered yet. Start an agent to see it here."
        style={{ marginTop: 100 }}
      />
    );
  }

  return (
    <div>
      <Typography.Title level={3} style={{ marginBottom: 16 }}>
        Devices ({devices.length})
      </Typography.Title>
      <Row gutter={[16, 16]}>
        {devices.map((device) => (
          <Col xs={24} sm={12} lg={8} xl={6} key={device.deviceId}>
            <DeviceCard
              device={device}
              metrics={latestMetrics.get(device.deviceId) || null}
            />
          </Col>
        ))}
      </Row>

      <Typography.Title level={3} style={{ marginTop: 32, marginBottom: 16 }}>
        Network Tests
      </Typography.Title>
      <NetworkTestPanel devices={devices} />
    </div>
  );
}

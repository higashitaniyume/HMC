import { Descriptions, Card, Table, Tag } from 'antd';
import type { SystemOverview } from '../types';

interface Props {
  overview: SystemOverview;
}

export default function SystemOverviewPanel({ overview }: Props) {
  return (
    <div>
      <Card title="Operating System" size="small" style={{ marginBottom: 16 }}>
        <Descriptions size="small" column={3}>
          <Descriptions.Item label="OS">{overview.osName}</Descriptions.Item>
          <Descriptions.Item label="Version">{overview.osVersion}</Descriptions.Item>
          <Descriptions.Item label="Architecture">{overview.osArchitecture}</Descriptions.Item>
          <Descriptions.Item label="Last Boot">
            {new Date(overview.lastBootTime).toLocaleString()}
          </Descriptions.Item>
          <Descriptions.Item label="Time Zone">{overview.timeZone}</Descriptions.Item>
        </Descriptions>
      </Card>

      <Card title="CPU" size="small" style={{ marginBottom: 16 }}>
        <Descriptions size="small" column={2}>
          <Descriptions.Item label="Name">{overview.cpuName}</Descriptions.Item>
          <Descriptions.Item label="Physical Cores">{overview.cpuPhysicalCores}</Descriptions.Item>
          <Descriptions.Item label="Logical Cores">{overview.cpuLogicalCores}</Descriptions.Item>
          <Descriptions.Item label="Total RAM">
            {(overview.totalMemoryBytes / (1024 * 1024 * 1024)).toFixed(1)} GB
          </Descriptions.Item>
        </Descriptions>
      </Card>

      <Card title="Motherboard & BIOS" size="small" style={{ marginBottom: 16 }}>
        <Descriptions size="small" column={2}>
          <Descriptions.Item label="MB Manufacturer">
            {overview.motherboardManufacturer || '-'}
          </Descriptions.Item>
          <Descriptions.Item label="MB Product">
            {overview.motherboardProduct || '-'}
          </Descriptions.Item>
          <Descriptions.Item label="BIOS">{overview.biosVersion || '-'}</Descriptions.Item>
        </Descriptions>
      </Card>

      {overview.gpus.length > 0 && (
        <Card title="GPUs" size="small" style={{ marginBottom: 16 }}>
          <Table
            dataSource={overview.gpus}
            rowKey="name"
            size="small"
            pagination={false}
            columns={[
              { title: 'Name', dataIndex: 'name', key: 'name' },
              { title: 'Driver', dataIndex: 'driverVersion', key: 'driver' },
              {
                title: 'VRAM',
                dataIndex: 'adapterRamBytes',
                key: 'ram',
                render: (v: number) =>
                  v > 0 ? `${(v / (1024 * 1024 * 1024)).toFixed(1)} GB` : '-',
              },
            ]}
          />
        </Card>
      )}

      {overview.disks.length > 0 && (
        <Card title="Disks" size="small" style={{ marginBottom: 16 }}>
          <Table
            dataSource={overview.disks}
            rowKey="model"
            size="small"
            pagination={false}
            columns={[
              { title: 'Model', dataIndex: 'model', key: 'model' },
              {
                title: 'Drive',
                dataIndex: 'driveLetter',
                key: 'drive',
                width: 80,
              },
              {
                title: 'Size',
                dataIndex: 'totalSizeBytes',
                key: 'size',
                render: (v: number) =>
                  v > 0 ? `${(v / (1000 * 1000 * 1000)).toFixed(0)} GB` : '-',
              },
              {
                title: 'Type',
                dataIndex: 'mediaType',
                key: 'type',
                width: 80,
                render: (t: string) => <Tag>{t}</Tag>,
              },
            ]}
          />
        </Card>
      )}

      {overview.networkAdapters.length > 0 && (
        <Card title="Network Adapters" size="small">
          <Table
            dataSource={overview.networkAdapters}
            rowKey="name"
            size="small"
            pagination={false}
            columns={[
              { title: 'Name', dataIndex: 'name', key: 'name' },
              { title: 'MAC', dataIndex: 'macAddress', key: 'mac' },
              {
                title: 'IPs',
                dataIndex: 'ipAddresses',
                key: 'ips',
                render: (ips: string[]) => ips.join(', ') || '-',
              },
              {
                title: 'Speed',
                dataIndex: 'speedBps',
                key: 'speed',
                render: (v: number) =>
                  v > 0 ? `${(v / 1_000_000_000).toFixed(1)} Gbps` : '-',
              },
            ]}
          />
        </Card>
      )}
    </div>
  );
}

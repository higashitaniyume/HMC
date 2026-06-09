import { Table, Tag, Input } from 'antd';
import { useState, useMemo } from 'react';
import type { TcpConnectionInfo } from '../types';

interface Props {
  connections: TcpConnectionInfo[];
}

const stateColors: Record<string, string> = {
  Established: 'green',
  Listen: 'blue',
  TimeWait: 'orange',
  CloseWait: 'red',
  SynSent: 'cyan',
  FinWait1: 'volcano',
  FinWait2: 'volcano',
  Closed: 'default',
};

export default function ConnectionsTable({ connections }: Props) {
  const [search, setSearch] = useState('');

  const filtered = useMemo(() => {
    if (!search) return connections;
    const s = search.toLowerCase();
    return connections.filter(
      (c) =>
        c.localAddress.includes(s) ||
        c.remoteAddress.includes(s) ||
        c.processName.toLowerCase().includes(s) ||
        c.localPort.toString().includes(s) ||
        c.remotePort.toString().includes(s)
    );
  }, [connections, search]);

  const columns = [
    {
      title: 'State',
      dataIndex: 'state',
      key: 'state',
      width: 100,
      render: (s: string) => (
        <Tag color={stateColors[s] || 'default'}>{s}</Tag>
      ),
    },
    { title: 'Local', dataIndex: 'localAddress', key: 'local', width: 140,
      render: (_: string, r: TcpConnectionInfo) => `${r.localAddress}:${r.localPort}` },
    { title: 'Remote', dataIndex: 'remoteAddress', key: 'remote', width: 160,
      render: (_: string, r: TcpConnectionInfo) => `${r.remoteAddress}:${r.remotePort}` },
    { title: 'PID', dataIndex: 'pid', key: 'pid', width: 60 },
    { title: 'Process', dataIndex: 'processName', key: 'process' },
  ];

  return (
    <div>
      <Input.Search
        placeholder="Filter by IP, port, or process name..."
        allowClear
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        style={{ marginBottom: 12, maxWidth: 400 }}
      />
      <Table
        dataSource={filtered}
        columns={columns}
        rowKey={(r, i) => `${r.localAddress}:${r.localPort}-${r.remoteAddress}:${r.remotePort}-${i}`}
        size="small"
        pagination={{ pageSize: 50, showSizeChanger: true }}
        scroll={{ y: 400 }}
      />
    </div>
  );
}

import { Table, Input } from 'antd';
import { useState, useMemo } from 'react';
import type { ProcessSnapshot } from '../types';

interface Props {
  processes: ProcessSnapshot[];
}

export default function ProcessTable({ processes }: Props) {
  const [search, setSearch] = useState('');

  const filtered = useMemo(() => {
    if (!search) return processes;
    const s = search.toLowerCase();
    return processes.filter(
      (p) =>
        p.name.toLowerCase().includes(s) || p.pid.toString().includes(s)
    );
  }, [processes, search]);

  const columns = [
    { title: 'PID', dataIndex: 'pid', key: 'pid', width: 80, sorter: (a: ProcessSnapshot, b: ProcessSnapshot) => a.pid - b.pid },
    { title: 'Name', dataIndex: 'name', key: 'name', sorter: (a: ProcessSnapshot, b: ProcessSnapshot) => a.name.localeCompare(b.name) },
    {
      title: 'Memory',
      dataIndex: 'workingSetMB',
      key: 'mem',
      width: 120,
      sorter: (a: ProcessSnapshot, b: ProcessSnapshot) => a.workingSetMB - b.workingSetMB,
      render: (v: number) => `${v.toFixed(1)} MB`,
    },
    {
      title: 'CPU Time',
      dataIndex: 'cpuTimeSeconds',
      key: 'cpu',
      width: 120,
      sorter: (a: ProcessSnapshot, b: ProcessSnapshot) => a.cpuTimeSeconds - b.cpuTimeSeconds,
      render: (v: number) => `${v.toFixed(0)}s`,
    },
  ];

  return (
    <div>
      <Input.Search
        placeholder="Filter by name or PID..."
        allowClear
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        style={{ marginBottom: 12, maxWidth: 400 }}
      />
      <Table
        dataSource={filtered}
        columns={columns}
        rowKey="pid"
        size="small"
        pagination={{ pageSize: 50, showSizeChanger: true }}
        scroll={{ y: 500 }}
      />
    </div>
  );
}

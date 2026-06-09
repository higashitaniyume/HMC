import { Outlet, useNavigate, useLocation } from 'react-router-dom';
import {
  Layout as AntLayout,
  Menu,
  Typography,
} from 'antd';
import {
  DashboardOutlined,
  ApiOutlined,
} from '@ant-design/icons';
import { useDevices } from '../hooks/useDevices';

const { Header, Content } = AntLayout;

export default function Layout() {
  const navigate = useNavigate();
  const location = useLocation();
  const { isConnected, onlineDevices } = useDevices();

  const menuItems = [
    {
      key: '/',
      icon: <DashboardOutlined />,
      label: 'Dashboard',
    },
  ];

  return (
    <AntLayout style={{ minHeight: '100vh' }}>
      <Header
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '0 24px',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 24 }}>
          <Typography.Title
            level={4}
            style={{ color: '#fff', margin: 0, cursor: 'pointer' }}
            onClick={() => navigate('/')}
          >
            <ApiOutlined /> HMC
          </Typography.Title>
          <Menu
            theme="dark"
            mode="horizontal"
            selectedKeys={[location.pathname]}
            items={menuItems}
            onClick={({ key }) => navigate(key)}
            style={{ flex: 1, minWidth: 200 }}
          />
        </div>
        <div style={{ color: '#aaa', fontSize: 13 }}>
          {isConnected ? (
            <span style={{ color: '#52c41a' }}>● Connected</span>
          ) : (
            <span style={{ color: '#ff4d4f' }}>● Disconnected</span>
          )}
          <span style={{ marginLeft: 12 }}>
            {onlineDevices.length} online
          </span>
        </div>
      </Header>
      <Content style={{ padding: 24, background: '#141414' }}>
        <Outlet />
      </Content>
    </AntLayout>
  );
}

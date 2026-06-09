import { Routes, Route } from 'react-router-dom';
import Layout from './components/Layout';
import DashboardPage from './pages/DashboardPage';
import DevicePage from './pages/DevicePage';

export default function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/" element={<DashboardPage />} />
        <Route path="/device/:deviceId" element={<DevicePage />} />
      </Route>
    </Routes>
  );
}

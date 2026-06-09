import { useState, useEffect, useCallback } from 'react';
import { useSignalR } from './useSignalR';
import * as api from '../api/client';
import type { DeviceEntity } from '../types';

export function useDevices() {
  const { isConnected, on } = useSignalR('/hub/agent');
  const [devices, setDevices] = useState<DeviceEntity[]>([]);
  const [loading, setLoading] = useState(true);

  const loadDevices = useCallback(async () => {
    try {
      const data = await api.fetchDevices();
      setDevices(data);
    } catch (err) {
      console.error('Failed to load devices:', err);
    } finally {
      setLoading(false);
    }
  }, []);

  // Initial load
  useEffect(() => {
    loadDevices();
  }, [loadDevices]);

  // Real-time updates
  useEffect(() => {
    const cleanup = on('DevicesUpdated', (updated: DeviceEntity[]) => {
      setDevices(updated);
    });
    return cleanup;
  }, [on]);

  const onlineDevices = devices.filter((d) => d.isOnline);
  const offlineDevices = devices.filter((d) => !d.isOnline);

  return { devices, onlineDevices, offlineDevices, loading, isConnected };
}

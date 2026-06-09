import { useState, useEffect, useCallback } from 'react';
import { useSignalRContext } from './SignalRContext';
import * as api from '../api/client';
import type { DeviceEntity } from '../types';

export function useDevices() {
  const { isConnected, on } = useSignalRContext();
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

  useEffect(() => {
    loadDevices();
  }, [loadDevices]);

  useEffect(() => {
    const cleanup = on('DevicesUpdated', (updated: DeviceEntity[]) => {
      if (Array.isArray(updated)) {
        setDevices(updated);
      }
    });
    return cleanup;
  }, [on]);

  const onlineDevices = devices.filter((d) => d.isOnline);
  const offlineDevices = devices.filter((d) => !d.isOnline);

  return { devices, onlineDevices, offlineDevices, loading, isConnected };
}

import { useState, useEffect, useCallback, useRef } from 'react';
import { useSignalRContext } from './SignalRContext';
import type { MetricsSnapshot } from '../types';

interface MetricsBuffer {
  cpu: number[];
  mem: number[];
  netIn: number[];
  netOut: number[];
  timestamps: string[];
  lastSnapshot: MetricsSnapshot | null;
}

const MAX_POINTS = 60;

export function useMetrics() {
  const { isConnected, on } = useSignalRContext();
  const [latestMetrics, setLatestMetrics] = useState<Map<string, MetricsSnapshot>>(new Map());
  const buffersRef = useRef<Map<string, MetricsBuffer>>(new Map());

  useEffect(() => {
    const cleanup = on('MetricsUpdated', (snapshot: MetricsSnapshot) => {
      if (!snapshot?.deviceId) return;
      const dt = snapshot.deviceId;

      setLatestMetrics((prev) => {
        const next = new Map(prev);
        next.set(dt, snapshot);
        return next;
      });

      const buffers = buffersRef.current;
      if (!buffers.has(dt)) {
        buffers.set(dt, { cpu: [], mem: [], netIn: [], netOut: [], timestamps: [], lastSnapshot: null });
      }
      const buf = buffers.get(dt)!;
      buf.cpu.push(snapshot?.cpu?.totalPercent ?? 0);
      buf.mem.push(snapshot?.memory?.percentUsed ?? 0);
      buf.netIn.push(snapshot?.network?.inBps ?? 0);
      buf.netOut.push(snapshot?.network?.outBps ?? 0);
      buf.timestamps.push(new Date(snapshot.timestamp).toLocaleTimeString());
      buf.lastSnapshot = snapshot;

      if (buf.cpu.length > MAX_POINTS) {
        buf.cpu = buf.cpu.slice(-MAX_POINTS);
        buf.mem = buf.mem.slice(-MAX_POINTS);
        buf.netIn = buf.netIn.slice(-MAX_POINTS);
        buf.netOut = buf.netOut.slice(-MAX_POINTS);
        buf.timestamps = buf.timestamps.slice(-MAX_POINTS);
      }
    });

    return cleanup;
  }, [on]);

  const getChartData = useCallback((deviceId: string) => {
    return buffersRef.current.get(deviceId) || null;
  }, []);

  return { latestMetrics, isConnected, getChartData };
}

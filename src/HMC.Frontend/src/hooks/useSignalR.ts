import { useEffect, useRef, useState, useCallback } from 'react';
import {
  HubConnectionBuilder,
  HubConnection,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';

export function useSignalR(hubUrl: string) {
  const [connection, setConnection] = useState<HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    const conn = new HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    conn.onreconnecting(() => setIsConnected(false));
    conn.onreconnected(() => setIsConnected(true));
    conn.onclose(() => setIsConnected(false));

    conn
      .start()
      .then(() => {
        setIsConnected(true);
        console.log('SignalR connected:', conn.connectionId);
      })
      .catch((err) => console.error('SignalR connect failed:', err));

    connectionRef.current = conn;
    setConnection(conn);

    return () => {
      conn.stop();
    };
  }, [hubUrl]);

  const on = useCallback(
    (method: string, handler: (...args: any[]) => void) => {
      if (connectionRef.current) {
        connectionRef.current.on(method, handler);
      }
      // Return cleanup
      return () => {
        connectionRef.current?.off(method, handler);
      };
    },
    [connection]
  );

  return { connection, isConnected, on };
}

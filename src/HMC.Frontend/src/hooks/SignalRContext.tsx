import React, {
  createContext,
  useContext,
  useEffect,
  useRef,
  useState,
  useCallback,
  ReactNode,
} from 'react';
import {
  HubConnectionBuilder,
  HubConnection,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';

interface SignalRContextValue {
  connection: HubConnection | null;
  isConnected: boolean;
  on: (method: string, handler: (...args: any[]) => void) => () => void;
}

const SignalRContext = createContext<SignalRContextValue>({
  connection: null,
  isConnected: false,
  on: () => () => {},
});

export function SignalRProvider({ children }: { children: ReactNode }) {
  const [isConnected, setIsConnected] = useState(false);
  const connectionRef = useRef<HubConnection | null>(null);
  // Store cleanup functions keyed by method name
  const handlersRef = useRef<Map<string, Set<(...args: any[]) => void>>>(
    new Map()
  );

  useEffect(() => {
    const conn = new HubConnectionBuilder()
      .withUrl('/hub/agent')
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

    return () => {
      conn.stop();
    };
  }, []);

  const on = useCallback(
    (method: string, handler: (...args: any[]) => void) => {
      const conn = connectionRef.current;
      if (!conn) return () => {};

      // Track handler set
      if (!handlersRef.current.has(method)) {
        handlersRef.current.set(method, new Set());
      }
      handlersRef.current.get(method)!.add(handler);

      // Register on the actual connection
      conn.on(method, handler);

      // Return cleanup
      return () => {
        handlersRef.current.get(method)?.delete(handler);
        conn.off(method, handler);
      };
    },
    []
  );

  return (
    <SignalRContext.Provider
      value={{ connection: connectionRef.current, isConnected, on }}
    >
      {children}
    </SignalRContext.Provider>
  );
}

export function useSignalRContext() {
  return useContext(SignalRContext);
}

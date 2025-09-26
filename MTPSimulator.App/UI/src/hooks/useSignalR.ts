import { useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';

interface LiveValue {
  nodeId: string;
  value: any;
  timestamp: string;
}

export const useSignalR = () => {
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
  const [liveValues, setLiveValues] = useState<Map<string, LiveValue>>(new Map());
  const [isConnected, setIsConnected] = useState(false);

  useEffect(() => {
    const newConnection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5000/simulatorHub')
      .withAutomaticReconnect()
      .build();

    newConnection.start()
      .then(() => {
        console.log('SignalR Connected');
        setIsConnected(true);
        setConnection(newConnection);
        
        // Join the live-data group
        newConnection.invoke('JoinGroup', 'live-data');
      })
      .catch((err) => {
        console.error('SignalR Connection Error:', err);
        setIsConnected(false);
      });

    newConnection.on('ValueUpdated', (data: LiveValue) => {
      setLiveValues(prev => {
        const newMap = new Map(prev);
        newMap.set(data.nodeId, data);
        return newMap;
      });
    });

    newConnection.onclose(() => {
      console.log('SignalR Disconnected');
      setIsConnected(false);
    });

    return () => {
      newConnection.stop();
    };
  }, []);

  return {
    connection,
    liveValues,
    isConnected
  };
};

import { useState, useEffect } from "react";
import { Circle, Users, Clock, Server } from "lucide-react";

interface StatusBarProps {
  serverStatus: "running" | "stopped" | "error";
  connectedClients: number;
  lastUpdate: Date;
}

export function StatusBar({ 
  serverStatus = "stopped", 
  connectedClients = 0, 
  lastUpdate = new Date() 
}: Partial<StatusBarProps>) {
  const [currentTime, setCurrentTime] = useState(new Date());

  useEffect(() => {
    const timer = setInterval(() => setCurrentTime(new Date()), 1000);
    return () => clearInterval(timer);
  }, []);

  const getStatusColor = (status: string) => {
    switch (status) {
      case "running": return "text-success";
      case "error": return "text-destructive";
      default: return "text-muted-foreground";
    }
  };

  const getStatusText = (status: string) => {
    switch (status) {
      case "running": return "Server Running";
      case "error": return "Server Error";
      default: return "Server Stopped";
    }
  };

  return (
    <div className="flex items-center justify-between px-6 py-3 bg-card border-t border-border text-sm">
      <div className="flex items-center gap-6">
        {/* Server Status */}
        <div className="flex items-center gap-2">
          <Circle className={`h-3 w-3 ${getStatusColor(serverStatus)} ${serverStatus === 'running' ? 'animate-pulse' : ''}`} fill="currentColor" />
          <span className="font-medium">{getStatusText(serverStatus)}</span>
        </div>

        {/* Connected Clients */}
        <div className="flex items-center gap-2">
          <Users className="h-4 w-4 text-muted-foreground" />
          <span>{connectedClients} Client{connectedClients !== 1 ? 's' : ''}</span>
        </div>

        {/* Last Update */}
        <div className="flex items-center gap-2">
          <Clock className="h-4 w-4 text-muted-foreground" />
          <span>Last Update: {lastUpdate.toLocaleTimeString()}</span>
        </div>
      </div>

      <div className="flex items-center gap-2">
        <Server className="h-4 w-4 text-muted-foreground" />
        <span className="text-muted-foreground">{currentTime.toLocaleString()}</span>
      </div>
    </div>
  );
}
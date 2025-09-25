import { SidebarProvider, SidebarTrigger } from "@/components/ui/sidebar";
import { AppSidebar } from "@/components/AppSidebar";
import { StatusBar } from "@/components/StatusBar";
import { useState } from "react";

interface LayoutProps {
  children: React.ReactNode;
}

export function Layout({ children }: LayoutProps) {
  const [serverStatus] = useState<"running" | "stopped" | "error">("stopped");
  const [connectedClients] = useState(0);
  const [lastUpdate] = useState(new Date());

  return (
    <SidebarProvider>
      <div className="flex min-h-screen w-full bg-background">
        <AppSidebar />
        
        <div className="flex-1 flex flex-col">
          {/* Header */}
          <header className="flex h-14 items-center justify-between border-b border-border bg-card px-6">
            <div className="flex items-center gap-4">
              <SidebarTrigger className="h-8 w-8" />
              <h1 className="text-lg font-semibold text-foreground">OPC UA MTP Simulator</h1>
            </div>
            
            <div className="flex items-center gap-2">
              <span className="status-indicator running">v1.0.0</span>
            </div>
          </header>

          {/* Main Content */}
          <main className="flex-1 overflow-auto">
            <div className="container mx-auto p-6">
              {children}
            </div>
          </main>

          {/* Status Bar */}
          <StatusBar 
            serverStatus={serverStatus}
            connectedClients={connectedClients}
            lastUpdate={lastUpdate}
          />
        </div>
      </div>
    </SidebarProvider>
  );
}
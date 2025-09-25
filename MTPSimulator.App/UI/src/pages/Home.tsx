import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { 
  Server, 
  FileText, 
  Network, 
  Play, 
  TrendingUp,
  Clock,
  Users,
  CheckCircle2
} from "lucide-react";
import { Link } from "react-router-dom";

export default function Home() {
  const stats = [
    { 
      title: "Server Status", 
      value: "Stopped", 
      icon: Server, 
      status: "stopped" 
    },
    { 
      title: "Loaded MTP Files", 
      value: "0", 
      icon: FileText, 
      status: "neutral" 
    },
    { 
      title: "Active Nodes", 
      value: "0", 
      icon: Network, 
      status: "neutral" 
    },
    { 
      title: "Running Simulations", 
      value: "0", 
      icon: TrendingUp, 
      status: "neutral" 
    }
  ];

  const recentActivities = [
    { time: "10:30", action: "System started", status: "info" },
    { time: "10:29", action: "Configuration loaded", status: "success" },
    { time: "10:28", action: "OPC UA server initialized", status: "info" }
  ];

  const quickActions = [
    { 
      title: "Load MTP File", 
      description: "Upload and parse a new MTP XML file",
      icon: FileText,
      path: "/mtp-loader",
      primary: true 
    },
    { 
      title: "Browse Nodes", 
      description: "Explore OPC UA node structure",
      icon: Network,
      path: "/node-browser" 
    },
    { 
      title: "Start Simulation", 
      description: "Control simulation parameters",
      icon: Play,
      path: "/simulation" 
    }
  ];

  return (
    <div className="space-y-8">
      {/* Welcome Section */}
      <div>
        <h1 className="text-3xl font-bold text-foreground mb-2">
          Welcome to OPC UA MTP Simulator
        </h1>
        <p className="text-lg text-muted-foreground">
          Industrial automation simulation and testing environment
        </p>
      </div>

      {/* Statistics Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        {stats.map((stat) => (
          <Card key={stat.title} className="industrial-card">
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-sm font-medium text-muted-foreground">
                {stat.title}
              </CardTitle>
              <stat.icon className="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold text-foreground">{stat.value}</div>
              <div className={`text-xs mt-1 status-indicator ${stat.status}`}>
                {stat.status === "stopped" && "Not Active"}
                {stat.status === "neutral" && "Ready"}
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Quick Actions */}
        <div className="lg:col-span-2">
          <Card className="industrial-card">
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <CheckCircle2 className="h-5 w-5" />
                Quick Actions
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              {quickActions.map((action) => (
                <div key={action.title} className="flex items-center justify-between p-4 rounded-lg border border-border hover:bg-accent/50 transition-colors">
                  <div className="flex items-center gap-3">
                    <div className="h-10 w-10 rounded-lg bg-primary/10 flex items-center justify-center">
                      <action.icon className="h-5 w-5 text-primary" />
                    </div>
                    <div>
                      <h3 className="font-medium text-foreground">{action.title}</h3>
                      <p className="text-sm text-muted-foreground">{action.description}</p>
                    </div>
                  </div>
                  <Button asChild variant={action.primary ? "default" : "outline"} size="sm">
                    <Link to={action.path}>
                      {action.primary ? "Get Started" : "Open"}
                    </Link>
                  </Button>
                </div>
              ))}
            </CardContent>
          </Card>
        </div>

        {/* Recent Activity */}
        <Card className="industrial-card">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Clock className="h-5 w-5" />
              Recent Activity
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {recentActivities.map((activity, index) => (
              <div key={index} className="flex items-center gap-3 text-sm">
                <span className="text-muted-foreground font-mono text-xs w-12">
                  {activity.time}
                </span>
                <div className={`h-2 w-2 rounded-full ${
                  activity.status === "success" ? "bg-success" : 
                  activity.status === "info" ? "bg-info" : "bg-muted-foreground"
                }`} />
                <span className="text-foreground">{activity.action}</span>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
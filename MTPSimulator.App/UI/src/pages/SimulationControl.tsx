import { useState } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { 
  Play, 
  Pause, 
  Square, 
  Settings, 
  TrendingUp,
  BarChart3,
  Zap
} from "lucide-react";

type SimulationState = "stopped" | "running" | "paused";
type SimulationPattern = "constant" | "sine" | "random" | "ramp";

interface SimulationConfig {
  pattern: SimulationPattern;
  baseValue: number;
  amplitude: number;
  frequency: number;
  noiseLevel: number;
  updateInterval: number;
}

export default function SimulationControl() {
  const [simulationState, setSimulationState] = useState<SimulationState>("stopped");
  const [config, setConfig] = useState<SimulationConfig>({
    pattern: "constant",
    baseValue: 50,
    amplitude: 10,
    frequency: 1,
    noiseLevel: 0.1,
    updateInterval: 1000
  });

  const startSimulation = () => {
    setSimulationState("running");
  };

  const pauseSimulation = () => {
    setSimulationState("paused");
  };

  const stopSimulation = () => {
    setSimulationState("stopped");
  };

  const updateConfig = (key: keyof SimulationConfig, value: string | number) => {
    setConfig(prev => ({ ...prev, [key]: value }));
  };

  const getPatternDescription = (pattern: SimulationPattern) => {
    switch (pattern) {
      case "constant": return "Fixed value with optional noise";
      case "sine": return "Sinusoidal wave pattern"; 
      case "random": return "Random walk simulation";
      case "ramp": return "Linear increase/decrease";
      default: return "";
    }
  };

  const simulationNodes = [
    { name: "Temperature Sensor", nodeId: "ns=2;s=Temp1", enabled: true, value: 45.2 },
    { name: "Pressure Gauge", nodeId: "ns=2;s=Press1", enabled: true, value: 2.3 },
    { name: "Flow Rate", nodeId: "ns=2;s=Flow1", enabled: false, value: 125.7 },
    { name: "Motor Speed", nodeId: "ns=2;s=Motor1", enabled: true, value: 1500 }
  ];

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-3xl font-bold text-foreground mb-2">Simulation Control</h1>
        <p className="text-lg text-muted-foreground">
          Configure and control OPC UA node value simulation
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Control Panel */}
        <Card className="industrial-card">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Play className="h-5 w-5" />
              Simulation Control
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-6">
            {/* Status */}
            <div className="text-center">
              <div className={`inline-flex items-center gap-2 px-4 py-2 rounded-full text-sm font-medium ${
                simulationState === "running" ? "bg-success/10 text-success border border-success/20" :
                simulationState === "paused" ? "bg-warning/10 text-warning border border-warning/20" :
                "bg-muted text-muted-foreground border border-border"
              }`}>
                <div className={`h-2 w-2 rounded-full ${
                  simulationState === "running" ? "bg-success animate-pulse" :
                  simulationState === "paused" ? "bg-warning" :
                  "bg-muted-foreground"
                }`} />
                {simulationState === "running" ? "Running" : 
                 simulationState === "paused" ? "Paused" : "Stopped"}
              </div>
            </div>

            {/* Control Buttons */}
            <div className="grid grid-cols-3 gap-2">
              <Button 
                onClick={startSimulation}
                disabled={simulationState === "running"}
                variant="default"
                size="sm"
              >
                <Play className="h-4 w-4" />
              </Button>
              <Button 
                onClick={pauseSimulation}
                disabled={simulationState === "stopped"}
                variant="outline"
                size="sm"
              >
                <Pause className="h-4 w-4" />
              </Button>
              <Button 
                onClick={stopSimulation}
                disabled={simulationState === "stopped"}
                variant="outline" 
                size="sm"
              >
                <Square className="h-4 w-4" />
              </Button>
            </div>

            {/* Update Interval */}
            <div className="space-y-2">
              <Label className="text-sm font-medium">Update Interval (ms)</Label>
              <Input
                type="number"
                value={config.updateInterval}
                onChange={(e) => updateConfig("updateInterval", parseInt(e.target.value))}
                min={100}
                max={10000}
                step={100}
              />
            </div>
          </CardContent>
        </Card>

        {/* Pattern Configuration */}
        <Card className="industrial-card">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Settings className="h-5 w-5" />
              Pattern Configuration
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {/* Pattern Selection */}
            <div className="space-y-2">
              <Label className="text-sm font-medium">Simulation Pattern</Label>
              <Select 
                value={config.pattern} 
                onValueChange={(value: SimulationPattern) => updateConfig("pattern", value)}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="constant">
                    <div className="flex items-center gap-2">
                      <BarChart3 className="h-4 w-4" />
                      Constant
                    </div>
                  </SelectItem>
                  <SelectItem value="sine">
                    <div className="flex items-center gap-2">
                      <TrendingUp className="h-4 w-4" />
                      Sine Wave
                    </div>
                  </SelectItem>
                  <SelectItem value="random">
                    <div className="flex items-center gap-2">
                      <Zap className="h-4 w-4" />
                      Random Walk
                    </div>
                  </SelectItem>
                  <SelectItem value="ramp">
                    <div className="flex items-center gap-2">
                      <TrendingUp className="h-4 w-4" />
                      Ramp
                    </div>
                  </SelectItem>
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                {getPatternDescription(config.pattern)}
              </p>
            </div>

            {/* Base Value */}
            <div className="space-y-2">
              <Label className="text-sm font-medium">Base Value</Label>
              <Input
                type="number"
                value={config.baseValue}
                onChange={(e) => updateConfig("baseValue", parseFloat(e.target.value))}
                step={0.1}
              />
            </div>

            {/* Amplitude (for sine/random patterns) */}
            {(config.pattern === "sine" || config.pattern === "random") && (
              <div className="space-y-2">
                <Label className="text-sm font-medium">Amplitude</Label>
                <Input
                  type="number"
                  value={config.amplitude}
                  onChange={(e) => updateConfig("amplitude", parseFloat(e.target.value))}
                  min={0}
                  step={0.1}
                />
              </div>
            )}

            {/* Frequency (for sine pattern) */}
            {config.pattern === "sine" && (
              <div className="space-y-2">
                <Label className="text-sm font-medium">Frequency (Hz)</Label>
                <Input
                  type="number"
                  value={config.frequency}
                  onChange={(e) => updateConfig("frequency", parseFloat(e.target.value))}
                  min={0.01}
                  max={10}
                  step={0.01}
                />
              </div>
            )}

            {/* Noise Level */}
            <div className="space-y-2">
              <Label className="text-sm font-medium">Noise Level</Label>
              <Input
                type="number"
                value={config.noiseLevel}
                onChange={(e) => updateConfig("noiseLevel", parseFloat(e.target.value))}
                min={0}
                max={1}
                step={0.01}
              />
            </div>
          </CardContent>
        </Card>

        {/* Active Nodes */}
        <Card className="industrial-card">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <BarChart3 className="h-5 w-5" />
              Simulated Nodes
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              {simulationNodes.map((node, index) => (
                <div key={index} className="flex items-center justify-between p-3 border border-border rounded-lg">
                  <div className="flex-1">
                    <div className="font-medium text-foreground text-sm">{node.name}</div>
                    <div className="text-xs text-muted-foreground font-mono">{node.nodeId}</div>
                    <div className="text-sm text-primary font-medium mt-1">{node.value}</div>
                  </div>
                  <Switch checked={node.enabled} />
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
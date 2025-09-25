import { useState } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { 
  Network, 
  ChevronRight, 
  ChevronDown, 
  Search, 
  Filter,
  Copy,
  Eye
} from "lucide-react";

interface TreeNode {
  id: string;
  name: string;
  nodeId: string;
  dataType: string;
  value: string | number | boolean;
  children?: TreeNode[];
  expanded?: boolean;
}

const mockNodes: TreeNode[] = [
  {
    id: "1",
    name: "Production Line A",
    nodeId: "ns=2;s=ProductionLineA",
    dataType: "Object",
    value: "",
    expanded: true,
    children: [
      {
        id: "1.1",
        name: "Temperature Control",
        nodeId: "ns=2;s=ProductionLineA.TempControl",
        dataType: "Object", 
        value: "",
        expanded: true,
        children: [
          {
            id: "1.1.1",
            name: "Current Temperature",
            nodeId: "ns=2;s=ProductionLineA.TempControl.Current",
            dataType: "Double",
            value: 45.2
          },
          {
            id: "1.1.2", 
            name: "Setpoint",
            nodeId: "ns=2;s=ProductionLineA.TempControl.Setpoint",
            dataType: "Double",
            value: 50.0
          }
        ]
      },
      {
        id: "1.2",
        name: "Motor Control",
        nodeId: "ns=2;s=ProductionLineA.MotorControl", 
        dataType: "Object",
        value: "",
        children: [
          {
            id: "1.2.1",
            name: "Speed",
            nodeId: "ns=2;s=ProductionLineA.MotorControl.Speed",
            dataType: "Int32",
            value: 1500
          },
          {
            id: "1.2.2",
            name: "Running",
            nodeId: "ns=2;s=ProductionLineA.MotorControl.Running", 
            dataType: "Boolean",
            value: true
          }
        ]
      }
    ]
  }
];

export default function NodeBrowser() {
  const [nodes, setNodes] = useState<TreeNode[]>(mockNodes);
  const [selectedNode, setSelectedNode] = useState<TreeNode | null>(null);
  const [searchTerm, setSearchTerm] = useState("");

  const toggleNodeExpansion = (nodeId: string) => {
    const updateNodes = (nodes: TreeNode[]): TreeNode[] => {
      return nodes.map(node => {
        if (node.id === nodeId) {
          return { ...node, expanded: !node.expanded };
        }
        if (node.children) {
          return { ...node, children: updateNodes(node.children) };
        }
        return node;
      });
    };
    setNodes(updateNodes(nodes));
  };

  const renderTreeNode = (node: TreeNode, level: number = 0) => {
    const hasChildren = node.children && node.children.length > 0;
    const isExpanded = node.expanded;

    return (
      <div key={node.id}>
        <div 
          className={`flex items-center gap-2 p-2 rounded-lg hover:bg-accent/50 cursor-pointer ${
            selectedNode?.id === node.id ? "bg-primary/10 border border-primary/20" : ""
          }`}
          style={{ paddingLeft: `${level * 20 + 8}px` }}
          onClick={() => setSelectedNode(node)}
        >
          {hasChildren && (
            <Button
              variant="ghost"
              size="sm"
              className="h-6 w-6 p-0"
              onClick={(e) => {
                e.stopPropagation();
                toggleNodeExpansion(node.id);
              }}
            >
              {isExpanded ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
            </Button>
          )}
          
          {!hasChildren && <div className="w-6" />}
          
          <Network className="h-4 w-4 text-primary flex-shrink-0" />
          <span className="font-medium text-foreground flex-1">{node.name}</span>
          
          <Badge variant="outline" className="text-xs">
            {node.dataType}
          </Badge>
        </div>
        
        {hasChildren && isExpanded && (
          <div>
            {node.children!.map(child => renderTreeNode(child, level + 1))}
          </div>
        )}
      </div>
    );
  };

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-3xl font-bold text-foreground mb-2">Node Browser</h1>
        <p className="text-lg text-muted-foreground">
          Explore and inspect OPC UA nodes from loaded MTP files
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Node Tree */}
        <Card className="industrial-card">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Network className="h-5 w-5" />
              OPC UA Node Tree
            </CardTitle>
            <div className="flex gap-2">
              <div className="relative flex-1">
                <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                <Input
                  placeholder="Search nodes..."
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  className="pl-10"
                />
              </div>
              <Button variant="outline" size="sm">
                <Filter className="h-4 w-4" />
              </Button>
            </div>
          </CardHeader>
          <CardContent className="max-h-96 overflow-y-auto">
            <div className="space-y-1">
              {nodes.map(node => renderTreeNode(node))}
            </div>
          </CardContent>
        </Card>

        {/* Node Details */}
        <Card className="industrial-card">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Eye className="h-5 w-5" />
              Node Details
            </CardTitle>
          </CardHeader>
          <CardContent>
            {selectedNode ? (
              <div className="space-y-4">
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <Label className="text-sm font-medium text-muted-foreground">Node Name</Label>
                    <div className="text-foreground font-medium">{selectedNode.name}</div>
                  </div>
                  <div>
                    <Label className="text-sm font-medium text-muted-foreground">Data Type</Label>
                    <Badge className="mt-1">{selectedNode.dataType}</Badge>
                  </div>
                </div>

                <div>
                  <Label className="text-sm font-medium text-muted-foreground">Node ID</Label>
                  <div className="flex items-center gap-2 mt-1">
                    <code className="flex-1 p-2 bg-muted rounded font-mono text-sm">
                      {selectedNode.nodeId}
                    </code>
                    <Button variant="outline" size="sm">
                      <Copy className="h-4 w-4" />
                    </Button>
                  </div>
                </div>

                <div>
                  <Label className="text-sm font-medium text-muted-foreground">Current Value</Label>
                  <div className="mt-1">
                    {selectedNode.value !== "" ? (
                      <div className="p-3 bg-primary/5 border border-primary/20 rounded-lg">
                        <span className="text-lg font-mono text-primary">
                          {typeof selectedNode.value === "boolean" 
                            ? (selectedNode.value ? "TRUE" : "FALSE")
                            : selectedNode.value}
                        </span>
                      </div>
                    ) : (
                      <div className="p-3 bg-muted/50 rounded-lg text-muted-foreground">
                        No value (Object type)
                      </div>
                    )}
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <Label className="text-sm font-medium text-muted-foreground">Access Level</Label>
                    <div className="text-foreground">Read/Write</div>
                  </div>
                  <div>
                    <Label className="text-sm font-medium text-muted-foreground">Quality</Label>
                    <span className="status-indicator running">Good</span>
                  </div>
                </div>
              </div>
            ) : (
              <div className="flex items-center justify-center h-64 text-muted-foreground">
                <div className="text-center">
                  <Network className="h-12 w-12 mx-auto mb-4 opacity-50" />
                  <p>Select a node from the tree to view details</p>
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

const Label: React.FC<{ className?: string; children: React.ReactNode }> = ({ 
  className = "", 
  children 
}) => {
  return <label className={className}>{children}</label>;
};
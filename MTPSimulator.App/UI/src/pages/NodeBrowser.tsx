import { useEffect, useMemo, useState } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectGroup, SelectItem, SelectLabel, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Badge } from "@/components/ui/badge";
import * as signalR from "@microsoft/signalr";
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

const REST_BASE = import.meta.env.VITE_API_BASE || "http://localhost:5288";
const HUB_URL = `${REST_BASE}/hub/values`;

export default function NodeBrowser() {
  const [nodes, setNodes] = useState<TreeNode[]>([]);
  const [selectedNode, setSelectedNode] = useState<TreeNode | null>(null);
  const [searchTerm, setSearchTerm] = useState("");
  const [dataTypeFilter, setDataTypeFilter] = useState<string>("All");

  // build index for fast updates
  const nodeIndex = useMemo(() => new Map<string, TreeNode>(), [nodes]);
  useEffect(() => {
    nodeIndex.clear();
    const normId = (id: string): string => {
      if (!id) return id;
      let s = id.trim();
      // Convert UaExpert style: NS2|String|R0002 -> ns=2;s=R0002
      if (/^NS\d+\|/i.test(s)) {
        const m = /^NS(\d+)\|[^|]*\|(.*)$/i.exec(s);
        if (m) s = `ns=${m[1]};s=${m[2]}`;
      }
      if (!/^ns=/i.test(s)) s = `ns=2;s=${s}`;
      return s;
    };
    const walk = (n: TreeNode) => {
      nodeIndex.set(n.nodeId, n);
      nodeIndex.set(normId(n.nodeId), n);
      n.children?.forEach(walk);
    };
    nodes.forEach(walk);
  }, [nodes]);

  useEffect(() => {
    let connection: signalR.HubConnection | null = null;

    const fetchVariables = async () => {
      const res = await fetch(`${REST_BASE}/api/variables`);
      const vars: { nodeId: string; displayName: string; dataType: string; value: any, access?: number, description?: string }[] = await res.json();
      const normId = (id: string) => {
        if (!id) return id;
        let s = id.trim();
        if (/^NS\d+\|/i.test(s)) {
          const m = /^NS(\d+)\|[^|]*\|(.*)$/i.exec(s);
          if (m) s = `ns=${m[1]};s=${m[2]}`;
        }
        if (!/^ns=/i.test(s)) s = `ns=2;s=${s}`;
        return s;
      };
      // flat list -> simple tree (group by first path segment)
      const tree: TreeNode[] = [
        {
          id: "root",
          name: "MTP",
          nodeId: "MTP",
          dataType: "Folder",
          value: "",
          expanded: true,
          children: vars.map((v, i) => ({
            id: `${i}`,
            name: v.displayName,
            nodeId: normId(v.nodeId),
            dataType: v.dataType,
            value: v.value ?? ""
          }))
        }
      ];
      setNodes(tree);
    };

    const startHub = async () => {
      connection = new signalR.HubConnectionBuilder()
        .withUrl(HUB_URL)
        .withAutomaticReconnect()
        .build();
      await connection.start();
      const normalizeIncoming = (id: string) => {
        let s = id;
        if (/^NS\d+\|/i.test(s)) {
          const m = /^NS(\d+)\|[^|]*\|(.*)$/i.exec(s);
          if (m) s = `ns=${m[1]};s=${m[2]}`;
        }
        return s;
      };

      connection.on("value", (nodeId: string, value: any) => {
        nodeId = normalizeIncoming(nodeId);
        const node = nodeIndex.get(nodeId);
        if (node) {
          node.value = value;
          setNodes(n => [...n]);
          // do not force details panel on sim updates; only refresh if it's currently selected
          setSelectedNode(prev => (prev && prev.nodeId === nodeId ? { ...node } : prev));
        }
      });

      connection.on("externalWrite", (nodeId: string, value: any) => {
        nodeId = normalizeIncoming(nodeId);
        const node = nodeIndex.get(nodeId);
        if (node) {
          node.value = value;
          setNodes(n => [...n]);
          setSelectedNode(prev => (prev && prev.nodeId === nodeId ? { ...node } : prev));
        }
      });
    };

    fetchVariables().catch(console.error);
    startHub().catch(console.error);

    return () => {
      if (connection) connection.stop().catch(() => {});
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

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

  // Fetch latest value for the selected node using the /api/read endpoint
  useEffect(() => {
    const nodeId = selectedNode?.nodeId;
    if (!nodeId) return;
    const controller = new AbortController();
    const load = async () => {
      try {
        const res = await fetch(`${REST_BASE}/api/read?nodeId=${encodeURIComponent(nodeId)}`, { signal: controller.signal });
        if (!res.ok) return; // leave as-is if not found
        const data = await res.json();
        setSelectedNode(prev => (prev && prev.nodeId === nodeId ? { ...prev, value: data.value } : prev));
        // also reflect in the tree cache
        const n = nodeIndex.get(nodeId);
        if (n) {
          n.value = data.value;
          setNodes(v => [...v]);
        }
      } catch { /* ignore */ }
    };
    load();
    return () => controller.abort();
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedNode?.nodeId]);

  // Filtering
  const filtered = useMemo(() => {
    const term = searchTerm.trim().toLowerCase();
    const wanted = dataTypeFilter.toLowerCase();

    const normalizeDataType = (dataType: string): string => {
      if (!dataType) return "";
      let normalized = dataType.toLowerCase().trim();
      
      // Remove XML schema prefixes
      if (normalized.startsWith("xs:")) normalized = normalized.substring(3);
      if (normalized.startsWith("xsd:")) normalized = normalized.substring(4);
      
      // Normalize common variations
      switch (normalized) {
        case "bool":
        case "boolean":
          return "boolean";
        case "string":
        case "normalizedstring":
        case "token":
          return "string";
        case "int":
        case "integer":
        case "int32":
          return "int32";
        case "float":
        case "single":
          return "float";
        case "double":
          return "double";
        case "byte":
        case "unsignedbyte":
          return "byte";
        case "short":
        case "int16":
          return "int16";
        case "long":
        case "int64":
          return "int64";
        case "datetime":
        case "date":
        case "time":
          return "datetime";
        default:
          return normalized;
      }
    };

    const matches = (n: TreeNode): boolean => {
      const byTerm = term.length === 0 || n.name.toLowerCase().includes(term) || n.nodeId.toLowerCase().includes(term);
      if (n.children && n.children.length > 0) return byTerm; // don't type-filter folders
      
      const normalizedNodeType = normalizeDataType(n.dataType);
      const normalizedWanted = normalizeDataType(wanted);
      
      const byType = wanted === "all" || normalizedNodeType === normalizedWanted;
      return byTerm && byType;
    };

    const filterTree = (nodes: TreeNode[]): TreeNode[] =>
      nodes.map(n => {
        const kids = n.children ? filterTree(n.children) : undefined;
        const selfOk = matches(n);
        const anyKid = kids && kids.length > 0;
        if (selfOk || anyKid) {
          return { ...n, children: kids };
        }
        return null as any;
      }).filter(Boolean);

    return filterTree(nodes);
  }, [nodes, searchTerm, dataTypeFilter]);

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
              <Select value={dataTypeFilter} onValueChange={setDataTypeFilter}>
                <SelectTrigger className="w-[160px]">
                  <SelectValue placeholder="All types" />
                </SelectTrigger>
                <SelectContent>
                  <SelectGroup>
                    <SelectLabel>Data Type</SelectLabel>
                    <SelectItem value="All">All</SelectItem>
                    <SelectItem value="Boolean">Boolean</SelectItem>
                    <SelectItem value="String">String</SelectItem>
                    <SelectItem value="Int32">Int32</SelectItem>
                    <SelectItem value="Int16">Int16</SelectItem>
                    <SelectItem value="Int64">Int64</SelectItem>
                    <SelectItem value="Byte">Byte</SelectItem>
                    <SelectItem value="Float">Float</SelectItem>
                    <SelectItem value="Double">Double</SelectItem>
                    <SelectItem value="DateTime">DateTime</SelectItem>
                  </SelectGroup>
                </SelectContent>
              </Select>
            </div>
          </CardHeader>
          <CardContent className="max-h-96 overflow-y-auto">
            <div className="space-y-1">
              {filtered.map(node => renderTreeNode(node))}
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
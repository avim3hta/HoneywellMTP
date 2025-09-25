import { useState } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { 
  Upload, 
  FileText, 
  CheckCircle2, 
  AlertCircle, 
  FileX,
  Download
} from "lucide-react";
import { useToast } from "@/hooks/use-toast";

export default function MTPLoader() {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [parseStatus, setParseStatus] = useState<"idle" | "success" | "error">("idle");
  const { toast } = useToast();

  const handleFileSelect = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (file) {
      if (file.type === "text/xml" || file.name.endsWith(".xml")) {
        setSelectedFile(file);
        setParseStatus("idle");
      } else {
        toast({
          title: "Invalid File Type",
          description: "Please select a valid XML file.",
          variant: "destructive",
        });
      }
    }
  };

  const handleFileParse = async () => {
    if (!selectedFile) return;

    setIsLoading(true);
    try {
      // Simulate parsing delay
      await new Promise(resolve => setTimeout(resolve, 2000));
      setParseStatus("success");
      toast({
        title: "MTP File Parsed Successfully",
        description: `${selectedFile.name} has been loaded and parsed.`,
      });
    } catch (error) {
      setParseStatus("error");
      toast({
        title: "Parse Error",
        description: "Failed to parse the MTP XML file. Please check the file format.",
        variant: "destructive",
      });
    } finally {
      setIsLoading(false);
    }
  };

  const clearFile = () => {
    setSelectedFile(null);
    setParseStatus("idle");
  };

  const mockMtpFiles = [
    { name: "Production_Line_A.xml", size: "1.2 MB", nodes: 145, lastModified: "2024-01-15" },
    { name: "Reactor_System_B.xml", size: "2.8 MB", nodes: 302, lastModified: "2024-01-12" },
    { name: "Packaging_Unit.xml", size: "950 KB", nodes: 87, lastModified: "2024-01-10" }
  ];

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-3xl font-bold text-foreground mb-2">MTP Loader</h1>
        <p className="text-lg text-muted-foreground">
          Upload and parse Module Type Package (MTP) XML files
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* File Upload Section */}
        <Card className="industrial-card">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Upload className="h-5 w-5" />
              Upload MTP File
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-6">
            {!selectedFile ? (
              <div className="border-2 border-dashed border-border rounded-lg p-8 text-center hover:border-primary/50 transition-colors">
                <div className="space-y-4">
                  <div className="flex justify-center">
                    <FileText className="h-12 w-12 text-muted-foreground" />
                  </div>
                  <div>
                    <Label htmlFor="file-upload" className="cursor-pointer">
                      <div className="text-lg font-medium text-foreground mb-2">
                        Select MTP XML File
                      </div>
                      <div className="text-sm text-muted-foreground">
                        Click to browse or drag and drop your MTP file here
                      </div>
                    </Label>
                  </div>
                  <Input
                    id="file-upload"
                    type="file"
                    accept=".xml"
                    onChange={handleFileSelect}
                    className="sr-only"
                  />
                  <Button asChild variant="outline">
                    <label htmlFor="file-upload" className="cursor-pointer">
                      Browse Files
                    </label>
                  </Button>
                </div>
              </div>
            ) : (
              <div className="space-y-4">
                <div className="flex items-center justify-between p-4 border border-border rounded-lg">
                  <div className="flex items-center gap-3">
                    <FileText className="h-8 w-8 text-primary" />
                    <div>
                      <div className="font-medium text-foreground">{selectedFile.name}</div>
                      <div className="text-sm text-muted-foreground">
                        {(selectedFile.size / (1024 * 1024)).toFixed(2)} MB
                      </div>
                    </div>
                  </div>
                  <Button variant="outline" size="sm" onClick={clearFile}>
                    <FileX className="h-4 w-4" />
                  </Button>
                </div>

                <div className="flex gap-2">
                  <Button 
                    onClick={handleFileParse} 
                    disabled={isLoading}
                    className="flex-1"
                  >
                    {isLoading ? "Parsing..." : "Parse MTP File"}
                  </Button>
                </div>

                {parseStatus === "success" && (
                  <div className="flex items-center gap-2 p-3 bg-success/10 border border-success/20 rounded-lg">
                    <CheckCircle2 className="h-5 w-5 text-success" />
                    <span className="text-success font-medium">File parsed successfully!</span>
                  </div>
                )}

                {parseStatus === "error" && (
                  <div className="flex items-center gap-2 p-3 bg-destructive/10 border border-destructive/20 rounded-lg">
                    <AlertCircle className="h-5 w-5 text-destructive" />
                    <span className="text-destructive font-medium">Parse failed. Check file format.</span>
                  </div>
                )}
              </div>
            )}
          </CardContent>
        </Card>

        {/* Recent Files */}
        <Card className="industrial-card">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <FileText className="h-5 w-5" />
              Recent MTP Files
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              {mockMtpFiles.map((file, index) => (
                <div key={index} className="flex items-center justify-between p-3 border border-border rounded-lg hover:bg-accent/50 transition-colors">
                  <div className="flex items-center gap-3">
                    <FileText className="h-6 w-6 text-primary" />
                    <div>
                      <div className="font-medium text-foreground text-sm">{file.name}</div>
                      <div className="text-xs text-muted-foreground">
                        {file.size} • {file.nodes} nodes • {file.lastModified}
                      </div>
                    </div>
                  </div>
                  <Button variant="outline" size="sm">
                    <Download className="h-4 w-4" />
                  </Button>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
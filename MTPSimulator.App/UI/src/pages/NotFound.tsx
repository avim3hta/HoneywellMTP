import { useLocation } from "react-router-dom";
import { useEffect } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Home, AlertTriangle } from "lucide-react";

const NotFound = () => {
  const location = useLocation();

  useEffect(() => {
    console.error("404 Error: User attempted to access non-existent route:", location.pathname);
  }, [location.pathname]);

  return (
    <div className="flex min-h-[60vh] items-center justify-center">
      <Card className="industrial-card max-w-md mx-auto">
        <CardContent className="text-center p-8">
          <div className="flex justify-center mb-6">
            <div className="h-16 w-16 rounded-full bg-warning/10 flex items-center justify-center">
              <AlertTriangle className="h-8 w-8 text-warning" />
            </div>
          </div>
          
          <h1 className="text-4xl font-bold text-foreground mb-2">404</h1>
          <h2 className="text-xl font-semibold text-foreground mb-4">Page Not Found</h2>
          <p className="text-muted-foreground mb-6">
            The page you're looking for doesn't exist or has been moved.
          </p>
          
          <Button asChild className="flex items-center gap-2">
            <a href="/">
              <Home className="h-4 w-4" />
              Return to Home
            </a>
          </Button>
        </CardContent>
      </Card>
    </div>
  );
};

export default NotFound;

using System;
using System.Threading.Tasks;
using System.Net;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== OPC UA Server Test ===");
        
        try
        {
            Console.WriteLine("1. Creating configuration...");
            var config = new ApplicationConfiguration
            {
                ApplicationName = "Test OPC UA Server",
                ApplicationType = ApplicationType.Server,
                ApplicationUri = $"urn:{Dns.GetHostName()}:TestServer",
                ProductUri = "urn:test-server",
                ServerConfiguration = new ServerConfiguration
                {
                    BaseAddresses = { "opc.tcp://localhost:4840" },
                    MinRequestThreadCount = 2,
                    MaxRequestThreadCount = 10
                },
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = CertificateStoreType.X509Store,
                        StorePath = "CurrentUser\\My",
                        SubjectName = "CN=Test OPC UA Server"
                    },
                    AutoAcceptUntrustedCertificates = true,
                    AddAppCertToTrustedStore = true,
                    RejectSHA1SignedCertificates = false
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 },
                DisableHiResClock = true
            };

            Console.WriteLine("2. Validating configuration...");
            await config.Validate(ApplicationType.Server);

            Console.WriteLine("3. Creating application instance...");
            var application = new ApplicationInstance
            {
                ApplicationName = config.ApplicationName,
                ApplicationType = ApplicationType.Server,
                ApplicationConfiguration = config
            };

            Console.WriteLine("4. Checking certificate...");
            await application.CheckApplicationInstanceCertificate(false, 0);

            Console.WriteLine("5. Creating server...");
            var server = new StandardServer();

            Console.WriteLine("6. Starting server...");
            await application.Start(server);

            Console.WriteLine("✅ SUCCESS: OPC UA Server started on opc.tcp://localhost:4840");
            Console.WriteLine("Press any key to stop...");
            Console.ReadKey();

            server.Stop();
            Console.WriteLine("Server stopped.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERROR: {ex.Message}");
            Console.WriteLine($"Type: {ex.GetType().Name}");
            Console.WriteLine($"Inner Exception: {ex.InnerException?.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
        
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}
// See https://aka.ms/new-console-template for more information
//Console.WriteLine("Hello, World!");
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serel.MCPServer.SqlServer.Handlers;
using Serel.MCPServer.SqlServer.Services;
using System.Text.Json;

namespace SqlServerMcpServer;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // Configurar configuração básica
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            // Verificar connection string
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING");

            if (string.IsNullOrEmpty(connectionString))
            {
                // Se não há connection string, usar um valor padrão para evitar crash
                Environment.SetEnvironmentVariable("SQL_CONNECTION_STRING",
                    "Server=localhost;Database=tempdb;Integrated Security=true;TrustServerCertificate=true;");
            }

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<DatabaseService>();
                    services.AddSingleton<McpHandler>();
                })
                .UseConsoleLifetime()
                .Build();

            var mcpHandler = host.Services.GetRequiredService<McpHandler>();
            await mcpHandler.RunAsync();
        }
        catch (Exception ex)
        {
            // Log para arquivo em caso de erro crítico na inicialização
            try
            {
                var logPath = Path.Combine(Path.GetTempPath(), "mcp-sqlserver-startup-error.log");
                await File.WriteAllTextAsync(logPath, $"{DateTime.Now}: Startup Error: {ex}\n");
            }
            catch
            {
                // Se não conseguir escrever log, apenas sair
            }

            Environment.Exit(1);
        }
    }
}

using System.Text.Json;
using Serel.MCPServer.SqlServer.Models;
using Serel.MCPServer.SqlServer.Services;

namespace Serel.MCPServer.SqlServer.Handlers
{
    public class McpHandler
    {
        private readonly DatabaseService _databaseService;
        private readonly JsonSerializerOptions _jsonOptions;

        public McpHandler(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false // Mudado para false para evitar problemas
            };
        }

        public async Task RunAsync()
        {
            try
            {
                // Redirecionar stderr para evitar que apareça no stdout
                Console.SetError(TextWriter.Null);

                string? line;
                while ((line = Console.ReadLine()) != null)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        var request = JsonSerializer.Deserialize<McpRequest>(line, _jsonOptions);
                        if (request != null)
                        {
                            var response = await ProcessRequest(request);
                            var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
                            Console.WriteLine(responseJson);
                            Console.Out.Flush();
                        }
                    }
                    catch (JsonException)
                    {
                        // Ignorar linhas que não são JSON válido
                        continue;
                    }
                    catch (Exception ex)
                    {
                        var errorResponse = new McpResponse
                        {
                            Jsonrpc = "2.0",
                            Id = JsonDocument.Parse("0").RootElement,
                            Error = new McpError
                            {
                                Code = -32603,
                                Message = $"Internal error: {ex.Message}"
                            }
                        };
                        var errorJson = JsonSerializer.Serialize(errorResponse, _jsonOptions);
                        Console.WriteLine(errorJson);
                        Console.Out.Flush();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log para arquivo em caso de erro crítico
                try
                {
                    var logPath = Path.Combine(Path.GetTempPath(), "mcp-sqlserver-error.log");
                    await File.WriteAllTextAsync(logPath, $"{DateTime.Now}: {ex}\n");
                }
                catch
                {
                    // Ignorar se não conseguir escrever log
                }
            }
        }

        private async Task<McpResponse> ProcessRequest(McpRequest request)
        {
            try
            {
                return request.Method switch
                {
                    "initialize" => HandleInitialize(request),
                    "tools/list" => HandleListTools(request),
                    "tools/call" => await HandleCallTool(request),
                    "resources/list" => HandleListResources(request),
                    "resources/read" => await HandleReadResource(request),
                    _ => new McpResponse
                    {
                        Jsonrpc = "2.0",
                        Id = GetValidId(request.Id),
                        Error = new McpError
                        {
                            Code = -32601,
                            Message = $"Method not found: {request.Method}"
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                return new McpResponse
                {
                    Jsonrpc = "2.0",
                    Id = GetValidId(request.Id),
                    Error = new McpError
                    {
                        Code = -32603,
                        Message = $"Internal error: {ex.Message}"
                    }
                };
            }
        }

        private McpResponse HandleInitialize(McpRequest request)
        {
            var capabilities = new Dictionary<string, object>
            {
                ["tools"] = new Dictionary<string, object>(),
                ["resources"] = new Dictionary<string, object>()
            };

            var serverInfo = new Dictionary<string, object>
            {
                ["name"] = "sql-server-mcp",
                ["version"] = "1.0.0"
            };

            var result = new Dictionary<string, object>
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = capabilities,
                ["serverInfo"] = serverInfo
            };

            return new McpResponse
            {
                Jsonrpc = "2.0",
                Id = GetValidId(request.Id),
                Result = result
            };
        }

        private McpResponse HandleListTools(McpRequest request)
        {
            var tools = new List<Dictionary<string, object>>
        {
            new()
            {
                ["name"] = "list_tables",
                ["description"] = "Lista todas as tabelas do banco de dados",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>(),
                    ["required"] = new List<string>()
                }
            },
            new()
            {
                ["name"] = "describe_table",
                ["description"] = "Descreve a estrutura de uma tabela específica",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["tableName"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Nome da tabela a ser descrita"
                        }
                    },
                    ["required"] = new List<string> { "tableName" }
                }
            },
            new()
            {
                ["name"] = "execute_query",
                ["description"] = "Executa uma query SQL SELECT",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["query"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Query SQL a ser executada"
                        }
                    },
                    ["required"] = new List<string> { "query" }
                }
            },
            new()
            {
                ["name"] = "list_stored_procedures",
                ["description"] = "Lista todas as stored procedures do banco",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>(),
                    ["required"] = new List<string>()
                }
            },
            new()
            {
                ["name"] = "describe_stored_procedure",
                ["description"] = "Descreve uma stored procedure específica",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["procedureName"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Nome da stored procedure"
                        }
                    },
                    ["required"] = new List<string> { "procedureName" }
                }
            },
            new()
            {
                ["name"] = "list_views",
                ["description"] = "Lista todas as views do banco de dados",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>(),
                    ["required"] = new List<string>()
                }
            },
            new()
            {
                ["name"] = "describe_view",
                ["description"] = "Descreve uma view específica",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["viewName"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Nome da view"
                        }
                    },
                    ["required"] = new List<string> { "viewName" }
                }
            },
            new()
            {
                ["name"] = "list_triggers",
                ["description"] = "Lista todos os triggers do banco de dados",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>(),
                    ["required"] = new List<string>()
                }
            },
            new()
            {
                ["name"] = "describe_trigger",
                ["description"] = "Descreve um trigger específico",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["triggerName"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Nome do trigger"
                        }
                    },
                    ["required"] = new List<string> { "triggerName" }
                }
            },
            new()
            {
                ["name"] = "generate_crud_endpoints",
                ["description"] = "Gera código para endpoints CRUD de uma tabela",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["tableName"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Nome da tabela para gerar CRUD"
                        },
                        ["framework"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Framework (aspnet, fastapi, express)",
                            ["enum"] = new List<string> { "aspnet", "fastapi", "express" }
                        }
                    },
                    ["required"] = new List<string> { "tableName", "framework" }
                }
            }
        };

            var result = new Dictionary<string, object>
            {
                ["tools"] = tools
            };

            return new McpResponse
            {
                Jsonrpc = "2.0",
                Id = GetValidId(request.Id),
                Result = result
            };
        }

        private async Task<McpResponse> HandleCallTool(McpRequest request)
        {
            try
            {
                if (!request.Params.HasValue)
                {
                    throw new InvalidOperationException("Params is required for tool calls");
                }

                var paramsElement = request.Params.Value;
                var toolName = paramsElement.GetProperty("name").GetString() ?? "";

                JsonElement argumentsElement = default;
                if (paramsElement.TryGetProperty("arguments", out argumentsElement))
                {
                    // Arguments exist
                }

                var result = toolName switch
                {
                    "list_tables" => await _databaseService.ListTablesAsync(),
                    "describe_table" => await _databaseService.DescribeTableAsync(
                        GetStringArgument(argumentsElement, "tableName")),
                    "execute_query" => await _databaseService.ExecuteQueryAsync(
                        GetStringArgument(argumentsElement, "query")),
                    "list_stored_procedures" => await _databaseService.ListStoredProceduresAsync(),
                    "describe_stored_procedure" => await _databaseService.DescribeStoredProcedureAsync(
                        GetStringArgument(argumentsElement, "procedureName")),
                    "list_views" => await _databaseService.ListViewsAsync(),
                    "describe_view" => await _databaseService.DescribeViewAsync(
                        GetStringArgument(argumentsElement, "viewName")),
                    "list_triggers" => await _databaseService.ListTriggersAsync(),
                    "describe_trigger" => await _databaseService.DescribeTriggerAsync(
                        GetStringArgument(argumentsElement, "triggerName")),
                    "generate_crud_endpoints" => await _databaseService.GenerateCrudEndpointsAsync(
                        GetStringArgument(argumentsElement, "tableName"),
                        GetStringArgument(argumentsElement, "framework")),
                    _ => throw new InvalidOperationException($"Unknown tool: {toolName}")
                };

                var content = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["type"] = "text",
                    ["text"] = result
                }
            };

                var responseResult = new Dictionary<string, object>
                {
                    ["content"] = content
                };

                return new McpResponse
                {
                    Jsonrpc = "2.0",
                    Id = GetValidId(request.Id),
                    Result = responseResult
                };
            }
            catch (Exception ex)
            {
                return new McpResponse
                {
                    Jsonrpc = "2.0",
                    Id = GetValidId(request.Id),
                    Error = new McpError
                    {
                        Code = -32603,
                        Message = $"Tool execution error: {ex.Message}"
                    }
                };
            }
        }

        private McpResponse HandleListResources(McpRequest request)
        {
            var resources = new List<Dictionary<string, object>>
        {
            new()
            {
                ["uri"] = "database://schema",
                ["name"] = "Database Schema",
                ["description"] = "Complete database schema information",
                ["mimeType"] = "application/json"
            }
        };

            var result = new Dictionary<string, object>
            {
                ["resources"] = resources
            };

            return new McpResponse
            {
                Jsonrpc = "2.0",
                Id = GetValidId(request.Id),
                Result = result
            };
        }

        private async Task<McpResponse> HandleReadResource(McpRequest request)
        {
            try
            {
                if (!request.Params.HasValue)
                {
                    throw new InvalidOperationException("Params is required for resource read");
                }

                var uri = request.Params.Value.GetProperty("uri").GetString() ?? "";

                var content = uri switch
                {
                    "database://schema" => await _databaseService.GetCompleteSchemaAsync(),
                    _ => throw new InvalidOperationException($"Unknown resource: {uri}")
                };

                var contents = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["uri"] = uri,
                    ["mimeType"] = "application/json",
                    ["text"] = content
                }
            };

                var result = new Dictionary<string, object>
                {
                    ["contents"] = contents
                };

                return new McpResponse
                {
                    Jsonrpc = "2.0",
                    Id = GetValidId(request.Id),
                    Result = result
                };
            }
            catch (Exception ex)
            {
                return new McpResponse
                {
                    Jsonrpc = "2.0",
                    Id = GetValidId(request.Id),
                    Error = new McpError
                    {
                        Code = -32603,
                        Message = $"Resource read error: {ex.Message}"
                    }
                };
            }
        }

        private string GetStringArgument(JsonElement argumentsElement, string key)
        {
            if (argumentsElement.ValueKind == JsonValueKind.Undefined ||
                !argumentsElement.TryGetProperty(key, out var property))
            {
                return "";
            }
            return property.GetString() ?? "";
        }

        private JsonElement GetValidId(JsonElement? requestId)
        {
            if (requestId.HasValue && requestId.Value.ValueKind != JsonValueKind.Null)
            {
                return requestId.Value;
            }

            // Retornar um ID padrão válido
            return JsonDocument.Parse("1").RootElement;
        }
    }
}

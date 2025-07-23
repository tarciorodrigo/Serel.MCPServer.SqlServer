tarciorodrigo: Refaça o texto para que fique com uma estrutura de Markdown

GitHub Copilot: # SQL Server MCP Server

## Funcionalidades Principais

### Tools (Ferramentas) Disponíveis

- `list_tables` - Lista todas as tabelas
- `describe_table` - Estrutura detalhada de tabelas  
- `execute_query` - Execução de queries SELECT (seguro)
- `list_stored_procedures` - Lista procedures
- `describe_stored_procedure` - Detalhes de procedures
- `list_views` - Lista views
- `describe_view` - Detalhes de views
- `list_triggers` - Lista triggers
- `describe_trigger` - Detalhes de triggers
- `generate_crud_endpoints` - Gera código CRUD para ASP.NET, FastAPI ou Express.js

### Resources

- `database://schema` - Schema completo em JSON

## Arquitetura

- Program.cs - Entry point com DI
- McpHandler.cs - Processa mensagens MCP via stdio
- DatabaseService.cs - Abstração de acesso ao SQL Server
- `Models` - Modelos para MCP e entidades da base de dados

## Como Usar

1. Configure a connection string no `appsettings.json` ou variável de ambiente
2. Build e execute: `dotnet run`
3. Configure no seu cliente MCP para comunicar via stdio

## Segurança

- Apenas queries SELECT permitidas
- Operações de metadata são read-only 
- Use credenciais com privilégios mínimos

## Geração de Código

O servidor pode gerar endpoints CRUD completos para:

- ASP.NET Core (C#)
- FastAPI (Python) 
- Express.js (Node.js)
# SQL Server MCP Server

Um servidor MCP (Model Context Protocol) para integração com SQL Server, desenvolvido em .NET 9. Este servidor permite que agentes de IA acessem e interajam com bases de dados SQL Server através de ferramentas padronizadas.

## Funcionalidades

### Tools (Ferramentas) Disponíveis

1. **list_tables** - Lista todas as tabelas da base de dados
2. **describe_table** - Descreve a estrutura de uma tabela específica
3. **execute_query** - Executa queries SQL SELECT (apenas leitura)
4. **list_stored_procedures** - Lista todas as stored procedures
5. **describe_stored_procedure** - Descreve uma stored procedure específica
6. **list_views** - Lista todas as views da base de dados
7. **describe_view** - Descreve uma view específica
8. **list_triggers** - Lista todos os triggers
9. **describe_trigger** - Descreve um trigger específico
10. **generate_crud_endpoints** - Gera código para endpoints CRUD

### Resources (Recursos) Disponíveis

- **database://schema** - Schema completo da base de dados em formato JSON

## Configuração

### 1. Connection String

Configure a connection string no `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=YourDatabase;Integrated Security=true;TrustServerCertificate=true;"
  }
}
```

Ou defina a variável de ambiente:
```bash
export SQL_CONNECTION_STRING="Server=localhost;Database=YourDatabase;Integrated Security=true;TrustServerCertificate=true;"
```

### 2. Exemplos de Connection String

**SQL Server Authentication:**
```
Server=localhost;Database=YourDatabase;User Id=sa;Password=YourPassword;TrustServerCertificate=true;
```

**Windows Authentication:**
```
Server=localhost;Database=YourDatabase;Integrated Security=true;TrustServerCertificate=true;
```

**Azure SQL:**
```
Server=tcp:yourserver.database.windows.net,1433;Initial Catalog=yourdatabase;Persist Security Info=False;User ID=yourusername;Password=yourpassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

## Instalação e Uso

### 1. Clonar e Build

```bash
git clone <seu-repositorio>
cd SqlServerMcpServer
dotnet build
```

### 2. Executar

```bash
dotnet run
```

### 3. Uso como MCP Server

Configure seu cliente MCP para usar este servidor via stdio. Exemplo de configuração:

```json
{
  "mcpServers": {
    "sqlserver": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/SqlServerMcpServer"],
      "env": {
        "SQL_CONNECTION_STRING": "sua-connection-string-aqui"
      }
    }
  }
}
```

## Exemplos de Uso

### 1. Listar Tabelas

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "list_tables",
    "arguments": {}
  }
}
```

### 2. Descrever Tabela

```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/call",
  "params": {
    "name": "describe_table",
    "arguments": {
      "tableName": "Users"
    }
  }
}
```

### 3. Executar Query

```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/call",
  "params": {
    "name": "execute_query",
    "arguments": {
      "query": "SELECT TOP 10 * FROM Users WHERE IsActive = 1"
    }
  }
}
```

### 4. Gerar Endpoints CRUD

```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "method": "tools/call",
  "params": {
    "name": "generate_crud_endpoints",
    "arguments": {
      "tableName": "Users",
      "framework": "aspnet"
    }
  }
}
```

## Frameworks Suportados para Geração de Código

- **aspnet** - ASP.NET Core Web API
- **fastapi** - FastAPI (Python)
- **express** - Express.js (Node.js)

## Segurança

- Apenas queries SELECT são permitidas na ferramenta `execute_query`
- Todas as outras operações são apenas de leitura (metadata)
- Use connection strings com privilégios mínimos necessários

## Estrutura do Projeto

```
SqlServerMcpServer/
├── Program.cs                    # Entry point
├── McpHandler.cs                # Handler principal MCP
├── Models/
│   └── McpModels.cs             # Modelos MCP e base de dados
├── Services/
│   └── DatabaseService.cs       # Serviço de acesso à base de dados
├── SqlServerMcpServer.csproj    # Arquivo do projeto
├── appsettings.json             # Configurações
└── README.md                    # Esta documentação
```

## Troubleshooting

### Connection Issues

1. Verifique se o SQL Server está a aceitar ligações
2. Confirme se as credenciais estão corretas
3. Verifique se a base de dados existe
4. Para Azure SQL, certifique-se de que o firewall permite a ligação

### Permission Issues

Certifique-se de que o utilizador tem permissões para:
- SELECT nas tabelas system (INFORMATION_SCHEMA, sys.*)
- SELECT nas tabelas, views, procedures que pretende aceder

## Contribuição

1. Fork o projeto
2. Crie uma branch para sua funcionalidade
3. Commit suas mudanças
4. Push para a branch
5. Abra um Pull Request

## Licença

MIT License
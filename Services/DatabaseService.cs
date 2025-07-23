using Microsoft.Extensions.Configuration;
using System.Data.SqlClient;
using System.Text;
using System.Text.Json;
using System.Data;
using Serel.MCPServer.SqlServer.Models;

namespace Serel.MCPServer.SqlServer.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;
        private readonly JsonSerializerOptions _jsonOptions;

        public DatabaseService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING")
                ?? "Server=localhost;Database=tempdb;Integrated Security=true;TrustServerCertificate=true;";

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
        }

        public async Task<string> ListTablesAsync()
        {
            try
            {
                const string query = @"
                SELECT 
                    TABLE_SCHEMA,
                    TABLE_NAME,
                    TABLE_TYPE
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE'
                ORDER BY TABLE_SCHEMA, TABLE_NAME";

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                var tables = new List<object>();
                while (await reader.ReadAsync())
                {
                    tables.Add(new
                    {
                        Schema = reader.GetString("TABLE_SCHEMA"),
                        TableName = reader.GetString("TABLE_NAME"),
                        Type = reader.GetString("TABLE_TYPE")
                    });
                }

                return JsonSerializer.Serialize(tables, _jsonOptions);
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions);
            }
        }

        public async Task<string> DescribeTableAsync(string tableName)
        {
            var parts = tableName.Split('.');
            var schema = parts.Length > 1 ? parts[0] : "dbo";
            var table = parts.Length > 1 ? parts[1] : tableName;

            const string query = @"
            SELECT 
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.IS_NULLABLE,
                c.COLUMN_DEFAULT,
                c.CHARACTER_MAXIMUM_LENGTH,
                c.NUMERIC_PRECISION,
                c.NUMERIC_SCALE,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PRIMARY_KEY,
                CASE WHEN COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') = 1 THEN 1 ELSE 0 END AS IS_IDENTITY
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT ku.TABLE_NAME, ku.COLUMN_NAME, ku.TABLE_SCHEMA
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS tc
                INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS ku
                    ON tc.CONSTRAINT_TYPE = 'PRIMARY KEY' AND tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
            ) pk ON c.TABLE_NAME = pk.TABLE_NAME AND c.COLUMN_NAME = pk.COLUMN_NAME AND c.TABLE_SCHEMA = pk.TABLE_SCHEMA
            WHERE c.TABLE_NAME = @TableName AND c.TABLE_SCHEMA = @Schema
            ORDER BY c.ORDINAL_POSITION";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@TableName", table);
            command.Parameters.AddWithValue("@Schema", schema);

            using var reader = await command.ExecuteReaderAsync();

            var columns = new List<DatabaseColumn>();
            while (await reader.ReadAsync())
            {
                columns.Add(new DatabaseColumn
                {
                    ColumnName = reader.GetString("COLUMN_NAME"),
                    DataType = reader.GetString("DATA_TYPE"),
                    IsNullable = reader.GetString("IS_NULLABLE") == "YES",
                    IsPrimaryKey = reader.GetInt32("IS_PRIMARY_KEY") == 1,
                    IsIdentity = reader.GetInt32("IS_IDENTITY") == 1,
                    MaxLength = reader.IsDBNull("CHARACTER_MAXIMUM_LENGTH") ? null : reader.GetInt32("CHARACTER_MAXIMUM_LENGTH"),
                    DefaultValue = reader.IsDBNull("COLUMN_DEFAULT") ? null : reader.GetString("COLUMN_DEFAULT")
                });
            }

            var tableInfo = new DatabaseTable
            {
                TableName = table,
                Schema = schema,
                Columns = columns
            };

            return JsonSerializer.Serialize(tableInfo, _jsonOptions);
        }

        public async Task<string> ExecuteQueryAsync(string query)
        {
            // Verificação de segurança - apenas SELECT
            if (!query.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Apenas queries SELECT são permitidas");
            }

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            var results = new List<Dictionary<string, object?>>();
            var columns = new List<string>();

            // Obter nomes das colunas
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                results.Add(row);
            }

            return JsonSerializer.Serialize(new { columns, data = results }, _jsonOptions);
        }

        public async Task<string> ListStoredProceduresAsync()
        {
            const string query = @"
            SELECT 
                ROUTINE_SCHEMA,
                ROUTINE_NAME,
                ROUTINE_TYPE,
                CREATED,
                LAST_ALTERED
            FROM INFORMATION_SCHEMA.ROUTINES
            WHERE ROUTINE_TYPE = 'PROCEDURE'
            ORDER BY ROUTINE_SCHEMA, ROUTINE_NAME";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            var procedures = new List<object>();
            while (await reader.ReadAsync())
            {
                procedures.Add(new
                {
                    Schema = reader.GetString("ROUTINE_SCHEMA"),
                    Name = reader.GetString("ROUTINE_NAME"),
                    Type = reader.GetString("ROUTINE_TYPE"),
                    Created = reader.GetDateTime("CREATED"),
                    LastAltered = reader.GetDateTime("LAST_ALTERED")
                });
            }

            return JsonSerializer.Serialize(procedures, _jsonOptions);
        }

        public async Task<string> DescribeStoredProcedureAsync(string procedureName)
        {
            var parts = procedureName.Split('.');
            var schema = parts.Length > 1 ? parts[0] : "dbo";
            var procName = parts.Length > 1 ? parts[1] : procedureName;

            // Query para obter definição da procedure
            const string definitionQuery = @"
            SELECT ROUTINE_DEFINITION
            FROM INFORMATION_SCHEMA.ROUTINES
            WHERE ROUTINE_NAME = @ProcName AND ROUTINE_SCHEMA = @Schema";

            // Query para obter parâmetros
            const string parametersQuery = @"
            SELECT 
                PARAMETER_NAME,
                DATA_TYPE,
                PARAMETER_MODE,
                IS_RESULT,
                CHARACTER_MAXIMUM_LENGTH
            FROM INFORMATION_SCHEMA.PARAMETERS
            WHERE SPECIFIC_NAME = @ProcName AND SPECIFIC_SCHEMA = @Schema
            ORDER BY ORDINAL_POSITION";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Obter definição
            using var defCommand = new SqlCommand(definitionQuery, connection);
            defCommand.Parameters.AddWithValue("@ProcName", procName);
            defCommand.Parameters.AddWithValue("@Schema", schema);

            var definition = await defCommand.ExecuteScalarAsync() as string ?? "";

            // Obter parâmetros
            using var paramCommand = new SqlCommand(parametersQuery, connection);
            paramCommand.Parameters.AddWithValue("@ProcName", procName);
            paramCommand.Parameters.AddWithValue("@Schema", schema);

            using var paramReader = await paramCommand.ExecuteReaderAsync();

            var parameters = new List<ProcedureParameter>();
            while (await paramReader.ReadAsync())
            {
                if (!paramReader.IsDBNull("PARAMETER_NAME"))
                {
                    parameters.Add(new ProcedureParameter
                    {
                        Name = paramReader.GetString("PARAMETER_NAME"),
                        DataType = paramReader.GetString("DATA_TYPE"),
                        IsOutput = paramReader.GetString("PARAMETER_MODE") == "OUT"
                    });
                }
            }

            var procedureInfo = new StoredProcedure
            {
                Name = procName,
                Schema = schema,
                Definition = definition,
                Parameters = parameters
            };

            return JsonSerializer.Serialize(procedureInfo, _jsonOptions);
        }

        public async Task<string> ListViewsAsync()
        {
            const string query = @"
            SELECT 
                TABLE_SCHEMA,
                TABLE_NAME
            FROM INFORMATION_SCHEMA.VIEWS
            ORDER BY TABLE_SCHEMA, TABLE_NAME";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            var views = new List<object>();
            while (await reader.ReadAsync())
            {
                views.Add(new
                {
                    Schema = reader.GetString("TABLE_SCHEMA"),
                    ViewName = reader.GetString("TABLE_NAME")
                });
            }

            return JsonSerializer.Serialize(views, _jsonOptions);
        }

        public async Task<string> DescribeViewAsync(string viewName)
        {
            var parts = viewName.Split('.');
            var schema = parts.Length > 1 ? parts[0] : "dbo";
            var view = parts.Length > 1 ? parts[1] : viewName;

            const string query = @"
            SELECT VIEW_DEFINITION
            FROM INFORMATION_SCHEMA.VIEWS
            WHERE TABLE_NAME = @ViewName AND TABLE_SCHEMA = @Schema";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@ViewName", view);
            command.Parameters.AddWithValue("@Schema", schema);

            var definition = await command.ExecuteScalarAsync() as string ?? "";

            var viewInfo = new DatabaseView
            {
                ViewName = view,
                Schema = schema,
                Definition = definition
            };

            return JsonSerializer.Serialize(viewInfo, _jsonOptions);
        }

        public async Task<string> ListTriggersAsync()
        {
            const string query = @"
            SELECT 
                t.name AS TriggerName,
                OBJECT_NAME(t.parent_id) AS TableName,
                OBJECT_SCHEMA_NAME(t.parent_id) AS SchemaName,
                t.type_desc AS TriggerType,
                t.is_disabled,
                STRING_AGG(te.type_desc, ', ') AS Events
            FROM sys.triggers t
            INNER JOIN sys.trigger_events te ON t.object_id = te.object_id
            WHERE t.parent_id > 0
            GROUP BY t.name, t.parent_id, t.type_desc, t.is_disabled
            ORDER BY OBJECT_SCHEMA_NAME(t.parent_id), OBJECT_NAME(t.parent_id), t.name";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            var triggers = new List<object>();
            while (await reader.ReadAsync())
            {
                triggers.Add(new
                {
                    TriggerName = reader.GetString("TriggerName"),
                    TableName = reader.GetString("TableName"),
                    Schema = reader.GetString("SchemaName"),
                    TriggerType = reader.GetString("TriggerType"),
                    IsDisabled = reader.GetBoolean("is_disabled"),
                    Events = reader.GetString("Events")
                });
            }

            return JsonSerializer.Serialize(triggers, _jsonOptions);
        }

        public async Task<string> DescribeTriggerAsync(string triggerName)
        {
            const string query = @"
            SELECT 
                t.name AS TriggerName,
                OBJECT_NAME(t.parent_id) AS TableName,
                OBJECT_SCHEMA_NAME(t.parent_id) AS SchemaName,
                t.type_desc AS TriggerType,
                STRING_AGG(te.type_desc, ', ') AS Events,
                m.definition AS Definition
            FROM sys.triggers t
            INNER JOIN sys.trigger_events te ON t.object_id = te.object_id
            INNER JOIN sys.sql_modules m ON t.object_id = m.object_id
            WHERE t.name = @TriggerName AND t.parent_id > 0
            GROUP BY t.name, t.parent_id, t.type_desc, m.definition";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@TriggerName", triggerName);

            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var triggerInfo = new DatabaseTrigger
                {
                    TriggerName = reader.GetString("TriggerName"),
                    TableName = reader.GetString("TableName"),
                    Schema = reader.GetString("SchemaName"),
                    TriggerType = reader.GetString("TriggerType"),
                    Events = reader.GetString("Events"),
                    Definition = reader.GetString("Definition")
                };

                return JsonSerializer.Serialize(triggerInfo, _jsonOptions);
            }

            throw new InvalidOperationException($"Trigger '{triggerName}' não encontrado");
        }

        public async Task<string> GenerateCrudEndpointsAsync(string tableName, string framework)
        {
            var tableDescription = await DescribeTableAsync(tableName);
            var table = JsonSerializer.Deserialize<DatabaseTable>(tableDescription, _jsonOptions);

            if (table == null)
                throw new InvalidOperationException($"Tabela '{tableName}' não encontrada");

            return framework.ToLower() switch
            {
                "aspnet" => GenerateAspNetCrud(table),
                "fastapi" => GenerateFastApiCrud(table),
                "express" => GenerateExpressCrud(table),
                _ => throw new InvalidOperationException($"Framework '{framework}' não suportado")
            };
        }

        public async Task<string> GetCompleteSchemaAsync()
        {
            var schema = new
            {
                Tables = JsonSerializer.Deserialize<object>(await ListTablesAsync()),
                StoredProcedures = JsonSerializer.Deserialize<object>(await ListStoredProceduresAsync()),
                Views = JsonSerializer.Deserialize<object>(await ListViewsAsync()),
                Triggers = JsonSerializer.Deserialize<object>(await ListTriggersAsync())
            };

            return JsonSerializer.Serialize(schema, _jsonOptions);
        }

        private string GenerateAspNetCrud(DatabaseTable table)
        {
            var sb = new StringBuilder();
            var className = ToPascalCase(table.TableName);
            var primaryKey = table.Columns.FirstOrDefault(c => c.IsPrimaryKey);

            sb.AppendLine($"// ASP.NET Core Controller para {table.TableName}");
            sb.AppendLine($"using Microsoft.AspNetCore.Mvc;");
            sb.AppendLine($"using System.Data.SqlClient;");
            sb.AppendLine($"using Dapper;");
            sb.AppendLine();
            sb.AppendLine($"[ApiController]");
            sb.AppendLine($"[Route(\"api/[controller]\")]");
            sb.AppendLine($"public class {className}Controller : ControllerBase");
            sb.AppendLine("{");
            sb.AppendLine($"    private readonly string _connectionString;");
            sb.AppendLine();
            sb.AppendLine($"    public {className}Controller(IConfiguration configuration)");
            sb.AppendLine("    {");
            sb.AppendLine($"        _connectionString = configuration.GetConnectionString(\"DefaultConnection\");");
            sb.AppendLine("    }");
            sb.AppendLine();

            // GET All
            sb.AppendLine($"    [HttpGet]");
            sb.AppendLine($"    public async Task<IActionResult> GetAll()");
            sb.AppendLine("    {");
            sb.AppendLine($"        using var connection = new SqlConnection(_connectionString);");
            sb.AppendLine($"        var query = \"SELECT * FROM {table.Schema}.{table.TableName}\";");
            sb.AppendLine($"        var results = await connection.QueryAsync(query);");
            sb.AppendLine($"        return Ok(results);");
            sb.AppendLine("    }");
            sb.AppendLine();

            if (primaryKey != null)
            {
                // GET By ID
                sb.AppendLine($"    [HttpGet(\"{{id}}\")]");
                sb.AppendLine($"    public async Task<IActionResult> GetById({GetCSharpType(primaryKey.DataType)} id)");
                sb.AppendLine("    {");
                sb.AppendLine($"        using var connection = new SqlConnection(_connectionString);");
                sb.AppendLine($"        var query = \"SELECT * FROM {table.Schema}.{table.TableName} WHERE {primaryKey.ColumnName} = @Id\";");
                sb.AppendLine($"        var result = await connection.QueryFirstOrDefaultAsync(query, new {{ Id = id }});");
                sb.AppendLine($"        return result != null ? Ok(result) : NotFound();");
                sb.AppendLine("    }");
                sb.AppendLine();
            }

            // POST
            sb.AppendLine($"    [HttpPost]");
            sb.AppendLine($"    public async Task<IActionResult> Create([FromBody] dynamic entity)");
            sb.AppendLine("    {");
            sb.AppendLine($"        using var connection = new SqlConnection(_connectionString);");

            var insertColumns = table.Columns.Where(c => !c.IsIdentity).Select(c => c.ColumnName);
            var insertValues = table.Columns.Where(c => !c.IsIdentity).Select(c => $"@{c.ColumnName}");

            sb.AppendLine($"        var query = \"INSERT INTO {table.Schema}.{table.TableName} ({string.Join(", ", insertColumns)}) VALUES ({string.Join(", ", insertValues)})\";");
            sb.AppendLine($"        var result = await connection.ExecuteAsync(query, entity);");
            sb.AppendLine($"        return result > 0 ? Ok() : BadRequest();");
            sb.AppendLine("    }");
            sb.AppendLine();

            if (primaryKey != null)
            {
                // PUT
                sb.AppendLine($"    [HttpPut(\"{{id}}\")]");
                sb.AppendLine($"    public async Task<IActionResult> Update({GetCSharpType(primaryKey.DataType)} id, [FromBody] dynamic entity)");
                sb.AppendLine("    {");
                sb.AppendLine($"        using var connection = new SqlConnection(_connectionString);");

                var updateColumns = table.Columns.Where(c => !c.IsPrimaryKey && !c.IsIdentity)
                    .Select(c => $"{c.ColumnName} = @{c.ColumnName}");

                sb.AppendLine($"        var query = \"UPDATE {table.Schema}.{table.TableName} SET {string.Join(", ", updateColumns)} WHERE {primaryKey.ColumnName} = @Id\";");
                sb.AppendLine($"        var result = await connection.ExecuteAsync(query, entity);");
                sb.AppendLine($"        return result > 0 ? Ok() : NotFound();");
                sb.AppendLine("    }");
                sb.AppendLine();

                // DELETE
                sb.AppendLine($"    [HttpDelete(\"{{id}}\")]");
                sb.AppendLine($"    public async Task<IActionResult> Delete({GetCSharpType(primaryKey.DataType)} id)");
                sb.AppendLine("    {");
                sb.AppendLine($"        using var connection = new SqlConnection(_connectionString);");
                sb.AppendLine($"        var query = \"DELETE FROM {table.Schema}.{table.TableName} WHERE {primaryKey.ColumnName} = @Id\";");
                sb.AppendLine($"        var result = await connection.ExecuteAsync(query, new {{ Id = id }});");
                sb.AppendLine($"        return result > 0 ? Ok() : NotFound();");
                sb.AppendLine("    }");
            }

            sb.AppendLine("}");

            return sb.ToString();
        }

        private string GenerateFastApiCrud(DatabaseTable table)
        {
            var sb = new StringBuilder();
            var className = table.TableName.ToLower();
            var primaryKey = table.Columns.FirstOrDefault(c => c.IsPrimaryKey);

            sb.AppendLine($"# FastAPI endpoints para {table.TableName}");
            sb.AppendLine($"from fastapi import APIRouter, HTTPException");
            sb.AppendLine($"from typing import List, Optional");
            sb.AppendLine($"import pyodbc");
            sb.AppendLine();
            sb.AppendLine($"router = APIRouter(prefix=\"/{className}\", tags=[\"{className}\"])");
            sb.AppendLine();

            // GET All
            sb.AppendLine($"@router.get(\"/\")");
            sb.AppendLine($"async def get_all_{className}():");
            sb.AppendLine($"    connection = pyodbc.connect(CONNECTION_STRING)");
            sb.AppendLine($"    cursor = connection.cursor()");
            sb.AppendLine($"    cursor.execute(\"SELECT * FROM {table.Schema}.{table.TableName}\")");
            sb.AppendLine($"    results = cursor.fetchall()");
            sb.AppendLine($"    connection.close()");
            sb.AppendLine($"    return results");
            sb.AppendLine();

            if (primaryKey != null)
            {
                // GET By ID
                sb.AppendLine($"@router.get(\"/{{id}}\")");
                sb.AppendLine($"async def get_{className}_by_id(id: {GetPythonType(primaryKey.DataType)}):");
                sb.AppendLine($"    connection = pyodbc.connect(CONNECTION_STRING)");
                sb.AppendLine($"    cursor = connection.cursor()");
                sb.AppendLine($"    cursor.execute(\"SELECT * FROM {table.Schema}.{table.TableName} WHERE {primaryKey.ColumnName} = ?\", id)");
                sb.AppendLine($"    result = cursor.fetchone()");
                sb.AppendLine($"    connection.close()");
                sb.AppendLine($"    if not result:");
                sb.AppendLine($"        raise HTTPException(status_code=404, detail=\"Item not found\")");
                sb.AppendLine($"    return result");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string GenerateExpressCrud(DatabaseTable table)
        {
            var sb = new StringBuilder();
            var routeName = table.TableName.ToLower();
            var primaryKey = table.Columns.FirstOrDefault(c => c.IsPrimaryKey);

            sb.AppendLine($"// Express.js routes para {table.TableName}");
            sb.AppendLine($"const express = require('express');");
            sb.AppendLine($"const sql = require('mssql');");
            sb.AppendLine($"const router = express.Router();");
            sb.AppendLine();

            // GET All
            sb.AppendLine($"// GET /{routeName}");
            sb.AppendLine($"router.get('/', async (req, res) => {{");
            sb.AppendLine($"    try {{");
            sb.AppendLine($"        const request = new sql.Request();");
            sb.AppendLine($"        const result = await request.query('SELECT * FROM {table.Schema}.{table.TableName}');");
            sb.AppendLine($"        res.json(result.recordset);");
            sb.AppendLine($"    }} catch (err) {{");
            sb.AppendLine($"        res.status(500).json({{ error: err.message }});");
            sb.AppendLine($"    }}");
            sb.AppendLine($"}});");
            sb.AppendLine();

            if (primaryKey != null)
            {
                // GET By ID
                sb.AppendLine($"// GET /{routeName}/:id");
                sb.AppendLine($"router.get('/:id', async (req, res) => {{");
                sb.AppendLine($"    try {{");
                sb.AppendLine($"        const request = new sql.Request();");
                sb.AppendLine($"        request.input('id', sql.{GetSqlJsType(primaryKey.DataType)}, req.params.id);");
                sb.AppendLine($"        const result = await request.query('SELECT * FROM {table.Schema}.{table.TableName} WHERE {primaryKey.ColumnName} = @id');");
                sb.AppendLine($"        if (result.recordset.length === 0) {{");
                sb.AppendLine($"            return res.status(404).json({{ error: 'Item not found' }});");
                sb.AppendLine($"        }}");
                sb.AppendLine($"        res.json(result.recordset[0]);");
                sb.AppendLine($"    }} catch (err) {{");
                sb.AppendLine($"        res.status(500).json({{ error: err.message }});");
                sb.AppendLine($"    }}");
                sb.AppendLine($"}});");
                sb.AppendLine();
            }

            sb.AppendLine($"module.exports = router;");

            return sb.ToString();
        }

        private string GetCSharpType(string sqlType)
        {
            return sqlType.ToLower() switch
            {
                "int" => "int",
                "bigint" => "long",
                "smallint" => "short",
                "tinyint" => "byte",
                "bit" => "bool",
                "decimal" or "numeric" or "money" or "smallmoney" => "decimal",
                "float" => "double",
                "real" => "float",
                "varchar" or "nvarchar" or "char" or "nchar" or "text" or "ntext" => "string",
                "datetime" or "datetime2" or "smalldatetime" => "DateTime",
                "date" => "DateOnly",
                "time" => "TimeOnly",
                "uniqueidentifier" => "Guid",
                "varbinary" or "binary" or "image" => "byte[]",
                _ => "object"
            };
        }

        private string GetPythonType(string sqlType)
        {
            return sqlType.ToLower() switch
            {
                "int" or "bigint" or "smallint" or "tinyint" => "int",
                "bit" => "bool",
                "decimal" or "numeric" or "money" or "smallmoney" or "float" or "real" => "float",
                "varchar" or "nvarchar" or "char" or "nchar" or "text" or "ntext" => "str",
                "datetime" or "datetime2" or "smalldatetime" or "date" or "time" => "str",
                "uniqueidentifier" => "str",
                _ => "str"
            };
        }

        private string GetSqlJsType(string sqlType)
        {
            return sqlType.ToLower() switch
            {
                "int" => "Int",
                "bigint" => "BigInt",
                "smallint" => "SmallInt",
                "tinyint" => "TinyInt",
                "bit" => "Bit",
                "decimal" or "numeric" or "money" or "smallmoney" => "Decimal",
                "float" => "Float",
                "real" => "Real",
                "varchar" or "char" => "VarChar",
                "nvarchar" or "nchar" => "NVarChar",
                "text" => "Text",
                "ntext" => "NText",
                "datetime" or "smalldatetime" => "DateTime",
                "datetime2" => "DateTime2",
                "date" => "Date",
                "time" => "Time",
                "uniqueidentifier" => "UniqueIdentifier",
                "varbinary" or "binary" => "VarBinary",
                "image" => "Image",
                _ => "VarChar"
            };
        }

        private string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var words = input.Split('_');
            var result = new StringBuilder();

            foreach (var word in words)
            {
                if (word.Length > 0)
                {
                    result.Append(char.ToUpper(word[0]));
                    if (word.Length > 1)
                        result.Append(word.Substring(1).ToLower());
                }
            }

            return result.ToString();
        }
    }
}

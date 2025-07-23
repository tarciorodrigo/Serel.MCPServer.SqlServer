using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Serel.MCPServer.SqlServer.Models
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class McpRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string Jsonrpc { get; set; } = "2.0";

        [JsonPropertyName("id")]
        public JsonElement? Id { get; set; }

        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("params")]
        public JsonElement? Params { get; set; }
    }

    public class McpResponse
    {
        [JsonPropertyName("jsonrpc")]
        public string Jsonrpc { get; set; } = "2.0";

        [JsonPropertyName("id")]
        public JsonElement Id { get; set; } // Removido nullable para garantir que sempre tenha valor

        [JsonPropertyName("result")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Result { get; set; }

        [JsonPropertyName("error")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public McpError? Error { get; set; }
    }

    public class McpError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public object? Data { get; set; }
    }

    public class DatabaseTable
    {
        public string TableName { get; set; } = string.Empty;
        public string Schema { get; set; } = string.Empty;
        public List<DatabaseColumn> Columns { get; set; } = new();
    }

    public class DatabaseColumn
    {
        public string ColumnName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsIdentity { get; set; }
        public int? MaxLength { get; set; }
        public string? DefaultValue { get; set; }
    }

    public class StoredProcedure
    {
        public string Name { get; set; } = string.Empty;
        public string Schema { get; set; } = string.Empty;
        public string Definition { get; set; } = string.Empty;
        public List<ProcedureParameter> Parameters { get; set; } = new();
    }

    public class ProcedureParameter
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsOutput { get; set; }
        public string? DefaultValue { get; set; }
    }

    public class DatabaseView
    {
        public string ViewName { get; set; } = string.Empty;
        public string Schema { get; set; } = string.Empty;
        public string Definition { get; set; } = string.Empty;
        public List<DatabaseColumn> Columns { get; set; } = new();
    }

    public class DatabaseTrigger
    {
        public string TriggerName { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string Schema { get; set; } = string.Empty;
        public string Definition { get; set; } = string.Empty;
        public string TriggerType { get; set; } = string.Empty;
        public string Events { get; set; } = string.Empty;
    }
}

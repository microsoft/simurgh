using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using ChatApp.Server.Models.Options;
using Microsoft.Azure.Cosmos;
using System.Data;
using System.Text;

namespace ChatApp.Server.Plugins
{
    public class SqlDbPlugin
    {
        private readonly SqlConnection _sqlConn;
        private readonly string query = @"
            SELECT 
                t.TABLE_SCHEMA, 
                t.TABLE_NAME, 
                c.COLUMN_NAME, 
                c.DATA_TYPE, 
                c.IS_NULLABLE
            FROM 
                INFORMATION_SCHEMA.TABLES t
            INNER JOIN 
                INFORMATION_SCHEMA.COLUMNS c ON t.TABLE_NAME = c.TABLE_NAME AND t.TABLE_SCHEMA = c.TABLE_SCHEMA
            WHERE 
                t.TABLE_TYPE = 'BASE TABLE'
            ORDER BY 
                t.TABLE_SCHEMA, t.TABLE_NAME, c.ORDINAL_POSITION;
        ";

        private readonly string metadataQuery = @"
            SELECT     
        ";

        public SqlDbPlugin(IOptions<SqlOptions> sqlOptions)
        {
            _sqlConn = new SqlConnection(sqlOptions.Value.ConnectionString);
        }

        [KernelFunction(nameof(ExecuteSqlQueryAsync))]
        [Description("Execute a query against the Azure SQL Database")]
        [return: Description("The result of the query")]
        public async Task<List<dynamic>> ExecuteSqlQueryAsync([Description("The query to run")] string query)
        {
            var sqlCommand = new SqlCommand(query, _sqlConn);
            _sqlConn.Open();

            List<dynamic> results = new List<dynamic>();

            using SqlDataReader reader = sqlCommand.ExecuteReader();

            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    results.Add((IDataRecord)reader);
                }
            }

            return results;
        }

        [KernelFunction(nameof(GetTablesDataSchemaAsync))]
        [Description("Get tables schema from Azure SQL Database")]
        [return: Description("The schema of tables as a string")]
        public async Task<string> GetTablesDataSchemaAsync()
        {
            var tableSchemas = new Dictionary<string, List<string>>();

            _sqlConn.Open();

            using (SqlCommand command = new SqlCommand(query, _sqlConn))
            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string schema = reader["TABLE_SCHEMA"].ToString();
                    string tableName = reader["TABLE_NAME"].ToString();
                    string columnName = reader["COLUMN_NAME"].ToString();
                    string dataType = reader["DATA_TYPE"].ToString() == "uniqueidentifier" ? "guid" : reader["DATA_TYPE"].ToString();
                    string isNullable = reader["IS_NULLABLE"].ToString();

                    // Store schema info in a dictionary by table name
                    string tableKey = $"{schema}.{tableName}";
                    string columnDetails = $"{columnName} ({dataType})";

                    if (!tableSchemas.ContainsKey(tableKey))
                    {
                        tableSchemas[tableKey] = new List<string>();
                    }
                    tableSchemas[tableKey].Add(columnDetails);
                }
            }

            StringBuilder tableSchemasString = new StringBuilder();

            foreach (var table in tableSchemas)
            {
                tableSchemasString.AppendLine($"Table: {table.Key}");
                foreach (var column in table.Value)
                {
                    tableSchemasString.AppendLine($"- {column}");
                }
            }

            return tableSchemasString.ToString();
        }

        [KernelFunction(nameof(GetTablesDataSchemaAsync))]
        [Description("Get metadata of data stored in SQL table")]
        [return: Description("The metadata of data as a string")]
        public async Task<string> GetDataMetadataAsync(Guid SurveyId)
        {
            string metadataQuery = @$"SELECT Id, Question, [Description]
                FROM[dbo].[SurveyQuestion]
                WHERE SurveyId = '{SurveyId}'
            ";

            _sqlConn.Open();

            StringBuilder dataMetadataString = new StringBuilder();

            using (SqlCommand command = new SqlCommand(metadataQuery, _sqlConn))
            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    dataMetadataString.AppendLine($"- {reader["Id"]}|\"{reader["Question"]}\"|{reader["Description"]}");
                }
            }

            return dataMetadataString.ToString();
        }
    }
}
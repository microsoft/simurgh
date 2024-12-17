using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using ChatApp.Server.Models.Options;
using System.Data;
using System.Text;
using Microsoft.SemanticKernel.ChatCompletion;
using ChatApp.Server.Services;

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

        private string sqlSchema = string.Empty;
        private string questionMetadata = string.Empty;
 
        public SqlDbPlugin(IOptions<SqlOptions> sqlOptions)
        {
            _sqlConn = new SqlConnection(sqlOptions.Value.ConnectionString);
        }

        [KernelFunction(nameof(SqlQueryGeneration))]
        [Description("Generates a SQL query based on a SQL tables schema and questions metadata that are cross referenced to a user's question.")]
        [return: Description("The SQL query")]
        public async Task<string> SqlQueryGeneration(
            [Description("The intent of the query.")] string input,
            //[Description("The schema of SQL tables. Run GetTablesDataSchemaAsync function to get a value.")] string schema,
            //[Description("The questions metadata. Run GetDataMetadataAsync function to get a value.")] string metadata,
            Kernel kernel)
        {
            sqlSchema = await GetTablesDataSchemaAsync();
            questionMetadata = await GetDataMetadataAsync(new Guid("3382c772-fa56-464f-97b9-ea9ff5dc3bbf"));

            var chatService = kernel.GetRequiredService<ChatCompletionService>();

            return string.Empty;
        }

        [KernelFunction(nameof(ExecuteSqlQueryAsync))]
        [Description("Execute a query against the SQL Database.")]
        [return: Description("The result of the query")]
        public async Task<List<dynamic>> ExecuteSqlQueryAsync([Description("The query to run")] string query)
        {
            if(_sqlConn.State != ConnectionState.Open)
                _sqlConn.Open();

            var sqlCommand = new SqlCommand(query, _sqlConn);

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

        //[KernelFunction(nameof(GetTablesDataSchemaAsync))]
        //[Description("Get schema of tables from SQL Database")]
        //[return: Description("The schema of tables as a string")]
        public async Task<string> GetTablesDataSchemaAsync()
        {
            var tableSchemas = new Dictionary<string, List<string>>();

            if (_sqlConn.State != ConnectionState.Open)
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

        //[KernelFunction(nameof(GetDataMetadataAsync))]
        //[Description("Get metadata of data stored in SQL table")]
        //[return: Description("The metadata of data as a string")]
        public async Task<string> GetDataMetadataAsync(Guid SurveyId)
        {
            string metadataQuery = @$"SELECT Id, Question, [Description]
                FROM[dbo].[SurveyQuestion]
                WHERE SurveyId = '{SurveyId}'
            ";

            StringBuilder dataMetadataString = new StringBuilder();

            if (_sqlConn.State != ConnectionState.Open)
                _sqlConn.Open();

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
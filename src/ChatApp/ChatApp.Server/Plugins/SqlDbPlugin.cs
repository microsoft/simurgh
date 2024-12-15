using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using ChatApp.Server.Models.Options;
using Microsoft.Azure.Cosmos;
using System.Data;

namespace ChatApp.Server.Plugins
{
    public class SqlDbPlugin
    {
        private readonly SqlConnection _sqlConn;

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
        [Description("Get the column names of the Cosmos DB container")]
        [return: Description("The column names of the container")]
        public async Task<List<dynamic>> GetTablesDataSchemaAsync()
        {
            _sqlConn.Open();
            DataTable table = _sqlConn.GetSchema("Tables");

            List<dynamic> tablesSchema = new List<dynamic>();

            foreach (DataRow row in table.Rows)
            {
                tablesSchema.Add(row);
            }

            return tablesSchema;
        }
    }
}

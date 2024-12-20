using ChatApp.Server.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text;

namespace ChatApp.Server.Services;

public class SurveyService
{
    private readonly string _connectionString;

    public SurveyService(string connectionString)
    {

        _connectionString = connectionString;
    }

    public async Task<IEnumerable<Survey>> GetSurveysAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Filename, Version FROM Survey";

        await using var reader = await command.ExecuteReaderAsync();
        var surveys = new List<Survey>();


        while (await reader.ReadAsync())
        {
            surveys.Add(new Survey
            {
                Id = reader.GetSqlGuid(0).Value,
                Filename = reader.GetString(1),
                Version = reader.GetString(2)
            });
        }

        return surveys;
    }

    public async Task<List<SurveyQuestion>> GetSurveyQuestionsAsync(Guid surveyId)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT 
                Id, 
                SurveyId, 
                Question,
                DataType,
                Description
            FROM SurveyQuestion
            WHERE SurveyId = @surveyId
            """;
        command.Parameters.AddWithValue("@surveyId", surveyId);

        await using var reader = await command.ExecuteReaderAsync();

        var questions = new List<SurveyQuestion>();

        while (await reader.ReadAsync())
        {
            questions.Add(new SurveyQuestion
            {
                Id = reader.GetSqlGuid(0).Value,
                SurveyId = reader.GetSqlGuid(1).Value,
                Question = reader.GetString(2),
                DataType = reader.GetString(3), //Enum.TryParse<DataType>(reader.GetString(3), out var dataType) ? dataType : null,
                Description = reader.GetString(4)
            });
        }

        return questions;
    }


    public async Task<List<dynamic>> ExecuteSqlQueryAsync(string query)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        var sqlCommand = new SqlCommand(query, connection);

        var results = new List<dynamic>();

        using SqlDataReader reader = await sqlCommand.ExecuteReaderAsync();

        if (!reader.HasRows)
            return results;

        //getting name of columns from reulting data set in case if there is more then one column
        var columnNames = new List<string>();
        DataTable schemaTable = reader.GetSchemaTable();

        foreach (DataRow row in schemaTable.Rows)
        {
            foreach (DataColumn column in schemaTable.Columns)
            {
                if (column.ColumnName != "ColumnName")
                    continue;

                var val = row[column.ColumnName].ToString();

                if (val != null)
                    columnNames.Add(val);
            }
        }

        // consider yield?
        while (reader.Read())
        {
            var val = reader[0].ToString();

            if (val != null)
                results.Add(val);
        }

        return results;
    }

    public async Task<string> GetSurveyMetadataAsync(Guid SurveyId)
    {
        string metadataQuery = @$"SELECT Id, Question, [Description]
                FROM[dbo].[SurveyQuestion]
                WHERE SurveyId = '{SurveyId}'
            ";

        var dataMetadataString = new StringBuilder();

        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var command = new SqlCommand(metadataQuery, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (reader.Read())
        {
            dataMetadataString.AppendLine($"- {reader["Id"]}|\"{reader["Question"]}\"|\"{reader["Description"]}\"");
        }

        return dataMetadataString.ToString();
    }
    internal async Task<string> GetTablesDataSchemaAsync()
    {
        var tableSchemas = new Dictionary<string, List<string>>();

        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        var query = @"
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

        using var command = new SqlCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (reader.Read())
        {
            string schema = reader["TABLE_SCHEMA"].ToString() ?? string.Empty;
            string tableName = reader["TABLE_NAME"].ToString() ?? string.Empty;
            string columnName = reader["COLUMN_NAME"].ToString() ?? string.Empty;
            string dataType = reader["DATA_TYPE"].ToString() == "uniqueidentifier" ? "guid" : reader["DATA_TYPE"].ToString() ?? string.Empty;
            string isNullable = reader["IS_NULLABLE"].ToString() ?? string.Empty;

            // Store schema info in a dictionary by table name
            string tableKey = $"{schema}.{tableName}";
            string columnDetails = $"{columnName} ({dataType})";

            if (!tableSchemas.ContainsKey(tableKey))
            {
                tableSchemas[tableKey] = new List<string>();
            }
            tableSchemas[tableKey].Add(columnDetails);
        }

        var tableSchemasString = new StringBuilder();

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




}

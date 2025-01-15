using Azure.Core;
using Azure.Identity;
using ChatApp.Server.Models;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using System.Data;
using System.Text;

namespace ChatApp.Server.Services;

public class SurveyService
{
    private readonly string _connectionString;
    private readonly bool _useManagedIdentity = false;
    private readonly string? _tenantId = null;

    public SurveyService(string connectionString, string? tenantId)
    {
        var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);

        if (connectionStringBuilder.Authentication == SqlAuthenticationMethod.ActiveDirectoryManagedIdentity)
        {
            _tenantId = tenantId;
            _useManagedIdentity = true;
            // this is really goofy but managed identity doesn't work with SqlConnectionStringBuilder
            // for local settings unless you set the token yourself using DefaultAzureCredential
            // see GetAccessTokenAsync, but you cannot set the AccessToken property on the connection
            // if the auth method has been set on the connection string, so we start by saying it is
            // managed identity and then set it to not specified so we can set the token later
            connectionStringBuilder.Authentication = SqlAuthenticationMethod.NotSpecified;
        }

        _connectionString = connectionStringBuilder.ConnectionString;
    }

    private async Task<string> GetAccessTokenAsync()
    {
        var defaultAzureCredential = string.IsNullOrWhiteSpace(_tenantId)
            ? new DefaultAzureCredential()
            : new DefaultAzureCredential(new DefaultAzureCredentialOptions() { TenantId = _tenantId });
        var tokenResult = await defaultAzureCredential.GetTokenAsync(new TokenRequestContext(scopes: ["https://database.windows.net/.default"]));
        return tokenResult.Token;
    }

    public async Task<IEnumerable<Survey>> GetSurveysAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        if (_useManagedIdentity) connection.AccessToken = await GetAccessTokenAsync();
        await connection.OpenAsync();
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
        if (_useManagedIdentity) connection.AccessToken = await GetAccessTokenAsync();
        await connection.OpenAsync();
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

    /// <summary>
    /// Find the most relevant SurveyQuestion based on the user's question
    /// </summary>
    /// <param name="surveyId"></param>
    /// <param name="embeddedUserQuestion"></param>
    /// <returns>SurveyQuestion ID</returns>
    public async Task<List<SurveyQuestion>> VectorSearchQuestionAsync(Guid surveyId, string userQuestion, ReadOnlyMemory<float> embeddedUserQuestion)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            if (_useManagedIdentity) connection.AccessToken = await GetAccessTokenAsync();
            await connection.OpenAsync();

            var embeddingJson = JsonConvert.SerializeObject(embeddedUserQuestion.ToArray());

            // todo: filter on surveyId
            var vectorSearch = $"""
            DECLARE @k INT = @kParam;
            DECLARE @q NVARCHAR(4000) = @qParam;
            DECLARE @e VECTOR(1536) = CAST(@eParam AS VECTOR(1536));
            WITH keyword_search AS (
                SELECT TOP(@k)
                    id,
                    RANK() OVER (ORDER BY rank) AS rank,
                    question,
                    description,
                    datatype,
                    surveyid
                FROM
                    (
                        SELECT TOP(@k)
                            sd.id,
                            ftt.[RANK] AS rank,
                            sd.question,
                            sd.description,
                            sd.datatype,
                            sd.surveyid
                        FROM 
                            dbo.surveyquestion AS sd
                        INNER JOIN 
                            FREETEXTTABLE(dbo.surveyquestion, *, @q) AS ftt ON sd.id = ftt.[KEY]
                    ) AS t
                ORDER BY
                    rank
            ),
            semantic_search AS
            (
                SELECT TOP(@k)
                    id,
                    RANK() OVER (ORDER BY distance) AS rank,
                    question,
                    description,
                    datatype,
                    surveyid
                FROM
                    (
                        SELECT TOP(@k)
                            id, 
                            VECTOR_DISTANCE('cosine', embedding, @e) AS distance,
                            question,
                            description,
                            datatype,
                            surveyid
                        FROM 
                            dbo.surveyquestion
                        ORDER BY
                            distance
                    ) AS t
                ORDER BY
                    rank
            )
            SELECT TOP(@k)
                COALESCE(ss.id, ks.id) AS id,
                COALESCE(1.0 / (@k + ss.rank), 0.0) +
                COALESCE(1.0 / (@k + ks.rank), 0.0) AS score, -- Reciprocal Rank Fusion (RRF)
                COALESCE(ss.question, ks.question) AS question,
                COALESCE(ss.description, ks.description) AS description,
                ss.rank AS semantic_rank,
                ks.rank AS keyword_rank,
                COALESCE(ss.surveyid, ks.surveyid) AS surveyid,
                COALESCE(ss.datatype, ks.datatype) AS datatype,
                COALESCE(ss.surveyid, ks.surveyid) AS surveyid
            FROM
                semantic_search ss
            FULL OUTER JOIN
                keyword_search ks ON ss.id = ks.id
            ORDER BY 
                score DESC
            """;
            // todo: add datatype, surveyid


            //var vectorSearch = $"""
            //DECLARE @e VECTOR(1536) = CAST(@eParam AS VECTOR(1536));

            //SELECT TOP(3)
            //    Id,
            //    SurveyId,
            //    Question,
            //    DataType,
            //    Description,
            //    VECTOR_DISTANCE('cosine', embedding, @e) AS distance
            //FROM
            //    dbo.SurveyQuestion
            //ORDER BY
            //    distance
            //""";
            /*
            DECLARE @k INT = @kParam;
            DECLARE @s uniqueidentifier = @sParam;
             
            WHERE
                SurveyId = @s and
                Embedding is not null
             
             */

            //  VECTOR_DISTANCE('cosine', embedding, @e) AS distance,
            var command = new SqlCommand(vectorSearch, connection);
            command.Parameters.AddWithValue("@qParam", userQuestion);
            command.Parameters.AddWithValue("@eParam", embeddingJson);
            command.Parameters.AddWithValue("@kParam", 3);
            //command.Parameters.AddWithValue("@sParam", surveyId);
            await using var reader = await command.ExecuteReaderAsync();

            /*
             
               0. COALESCE(ss.id, ks.id) AS id,
               1. COALESCE(1.0 / (@k + ss.rank), 0.0) +
                COALESCE(1.0 / (@k + ks.rank), 0.0) AS score, -- Reciprocal Rank Fusion (RRF)
               2. COALESCE(ss.question, ks.question) AS question,
               3. COALESCE(ss.description, ks.description) AS description,
               4. ss.rank AS semantic_rank,
               5. ks.rank AS keyword_rank,
               6. COALESCE(ss.surveyid, ks.surveyid) AS surveyid,
               7. COALESCE(ss.datatype, ks.datatype) AS datatype,
               8. COALESCE(ss.surveyid, ks.surveyid) AS surveyid
             */


            var questions = new List<SurveyQuestion>();

            while (await reader.ReadAsync())
            {
                questions.Add(new SurveyQuestion
                {
                    Id = reader.GetSqlGuid(0).Value,
                    SurveyId = reader.GetSqlGuid(8).Value,
                    Question = reader.GetString(2),
                    DataType = reader.GetString(7),
                    Description = reader.GetString(3)
                });
            }

            return questions;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
        }
    }

    public async Task<List<VectorSearchResult>> VectorSearchAsync(ReadOnlyMemory<float> embeddedQuestion, VectorSearchOptions options)
    {
        using var connection = new SqlConnection(_connectionString);
        if (_useManagedIdentity) connection.AccessToken = await GetAccessTokenAsync();
        await connection.OpenAsync();

        var embeddingJson = JsonConvert.SerializeObject(embeddedQuestion.ToArray());

        //var hybridSearch = $"""
        //    DECLARE @k INT = @kParam;
        //    DECLARE @q NVARCHAR(4000) = @qParam;
        //    DECLARE @e VECTOR(384) = CAST(@eParam AS VECTOR(384));

        //    WITH
        //        keyword_search
        //        AS

        //        (
        //            SELECT TOP(@k)
        //                Id,
        //                RANK() OVER(ORDER BY rank) AS rank,
        //                TextAnswer
        //            FROM
        //                (
        //             SELECT TOP(@k)
        //                    sd.Id,
        //                    ftt.[RANK] AS rank,
        //                    sd.TextAnswer
        //                FROM
        //                    dbo.VectorSearchResult AS sd
        //                    INNER JOIN
        //                    FREETEXTTABLE(dbo.VectorSearchResult, *, @q) AS ftt ON sd.Id = ftt.[KEY]
        //         ) AS t
        //            ORDER BY
        //         rank
        //        ),
        //        semantic_search
        //        AS
        //        (
        //            SELECT TOP(@k)
        //                Id,
        //                RANK() OVER (ORDER BY distance) AS rank,
        //                TextAnswer
        //            FROM
        //                (
        //                     SELECT TOP(@k)
        //                    Id,
        //                    VECTOR_DISTANCE('cosine', embedding, @e) AS distance,
        //                    TextAnswer
        //                FROM
        //                    dbo.VectorSearchResult
        //                ORDER BY
        //                         distance
        //                 ) AS t
        //            ORDER BY
        //                 rank
        //        )
        //    SELECT TOP(@k)
        //        COALESCE(ss.Id, ks.Id) AS Id,
        //        COALESCE(1.0 / (@k + ss.rank), 0.0) +
        //         COALESCE(1.0 / (@k + ks.rank), 0.0) AS score, --Reciprocal Rank Fusion(RRF)
        //        COALESCE(ss.TextAnswer, ks.TextAnswer) AS TextAnswer,
        //        ss.rank AS SemanticRank,
        //        ks.rank AS keyword_rank
        //    FROM
        //        semantic_search ss
        //        FULL OUTER JOIN
        //        keyword_search ks ON ss.Id = ks.Id
        //    ORDER BY
        //         score DESC
        //    """;

        var vectorSearch = $"""
            DECLARE @k INT = @kParam;
            DECLARE @e VECTOR(1536) = CAST(@eParam AS VECTOR(1536));

            SELECT TOP(@k)
                Id,
                SurveyResponseId,
                SurveyQuestionId,
                TextAnswer,
                PositiveSentimentConfidenceScore,
                NeutralSentimentConfidenceScore,
                NegativeSentimentConfidenceScore,
                RANK() OVER (ORDER BY distance) AS rank
            FROM 
            (
                SELECT TOP(@k)
                    Id,
                    SurveyResponseId,
                    SurveyQuestionId,
                    TextAnswer,
                    PositiveSentimentConfidenceScore,
                    NeutralSentimentConfidenceScore,
                    NegativeSentimentConfidenceScore,
                    VECTOR_DISTANCE('cosine', embedding, @e) AS distance
                FROM
                    dbo.SurveyQuestionAnswer
                WHERE
                    SurveyId = @surveyId and
                    TextAnswer is not null and
                    Embedding is not null
                    {options.GetWhereClauseString()}
                ORDER BY
                    distance
                    {options.GetOrderByClauseString()}
            ) AS t
            ORDER BY
                rank
            """;
        //  VECTOR_DISTANCE('cosine', embedding, @e) AS distance,
        var command = new SqlCommand(vectorSearch, connection);
        command.Parameters.AddWithValue("@eParam", embeddingJson);
        command.Parameters.AddWithValue("@kParam", options.TopK);
        command.Parameters.AddWithValue("@surveyId", options.SurveyId);

        await using var reader = await command.ExecuteReaderAsync();

        var answers = new List<VectorSearchResult>();

        while (await reader.ReadAsync())
        {
            var id = reader.GetSqlGuid(0).Value;
            answers.Add(new VectorSearchResult()
            {
                Id = reader.GetGuid(0),
                SurveyResponseId = reader.GetGuid(1),
                SurveyQuestionId = reader.GetGuid(2),
                TextAnswer = reader.GetString(3),
                PositiveSentimentConfidenceScore = reader.GetDouble(4),
                NeutralSentimentConfidenceScore = reader.GetDouble(5),
                NegativeSentimentConfidenceScore = reader.GetDouble(6),
            });
            // todo: how to handle null values more robustly
        }

        return answers;
    }


    public async Task<List<dynamic>> ExecuteSqlQueryAsync(string query)
    {
        using var connection = new SqlConnection(_connectionString);
        if (_useManagedIdentity) connection.AccessToken = await GetAccessTokenAsync();
        await connection.OpenAsync();

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
        string metadataQuery = @$"SELECT Id, Question, [DataType], [Description]
                FROM[dbo].[SurveyQuestion]
                WHERE SurveyId = '{SurveyId}'
            ";

        var dataMetadataString = new StringBuilder();

        using var connection = new SqlConnection(_connectionString);
        if (_useManagedIdentity) connection.AccessToken = await GetAccessTokenAsync();
        await connection.OpenAsync();

        using var command = new SqlCommand(metadataQuery, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (reader.Read())
        {
            dataMetadataString.AppendLine($"- {reader["Id"]}|\"{reader["Question"]}\"|\"{reader["DataType"]}\"|\"{reader["Description"]}\"");
        }

        return dataMetadataString.ToString();
    }
    internal async Task<string> GetTablesDataSchemaAsync()
    {
        var tableSchemas = new Dictionary<string, List<string>>();

        using var connection = new SqlConnection(_connectionString);
        if (_useManagedIdentity) connection.AccessToken = await GetAccessTokenAsync();
        await connection.OpenAsync();

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

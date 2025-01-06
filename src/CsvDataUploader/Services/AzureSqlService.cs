using Azure.Core;
using Azure.Identity;
using CsvDataUploader.Models;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using System.Data;

namespace CsvDataUploader.Services;

internal class AzureSqlService
{
    private readonly string _connectionString;
    private readonly bool _useManagedIdentity = false;
    private readonly string? _tenantId = null;

    internal AzureSqlService(SqlConnectionStringBuilder connectionStringBuilder, string? tenantId = null)
    {
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

    internal async Task<bool> TestConnectionAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        if (_useManagedIdentity) connection.AccessToken = await GetAccessTokenAsync();
        await connection.OpenAsync();
        return connection.State == ConnectionState.Open;
    }

    internal async Task ClearDatabaseDataAsync()
    {
        Console.WriteLine("Are you sure you want to clear the database? y/N");
        Console.WriteLine("note: cancelling does not stop uploader from trying to upload - just prevents clearing the database");
        var input = Console.ReadLine();

        if (!string.Equals(input, "Y", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Operation canceled. Database data left as is.");
            return;
        }

        using var connection = new SqlConnection(_connectionString);
        if (_useManagedIdentity) connection.AccessToken = await GetAccessTokenAsync();
        await connection.OpenAsync();

        var clearDatabaseCommand = new SqlCommand("DELETE FROM SurveyQuestionAnswer; DELETE FROM SurveyQuestion; DELETE FROM SurveyResponse; DELETE FROM Survey;", connection);
        await clearDatabaseCommand.ExecuteNonQueryAsync();
    }

    internal async Task<Guid> UploadSurveyAsync(string surveyName, string version = "1.0")
    {
        using var connection = new SqlConnection(_connectionString);
        if (_useManagedIdentity) connection.AccessToken = await GetAccessTokenAsync();
        await connection.OpenAsync();

        var surveyId = Guid.NewGuid();

        var insertSurveyCommand = new SqlCommand("INSERT INTO Survey (Id, Filename, Version) VALUES (@Id, @Filename, @Version)", connection);
        insertSurveyCommand.Parameters.AddWithValue("@Id", surveyId);
        insertSurveyCommand.Parameters.AddWithValue("@Filename", surveyName);
        insertSurveyCommand.Parameters.AddWithValue("@Version", version);   // pull this from designated column which should have the samcross all CSVs
        await insertSurveyCommand.ExecuteNonQueryAsync();

        return surveyId;
    }

    // we provide IDs here instead of generating them because 
    // we want to be able to match the survey responses
    // to the survey questions outside scope of this function
    internal async Task UploadSurveyResponsesAsync(Guid surveyId, List<Guid> responseIds)
    {
        using var connection = new SqlConnection(_connectionString);
        if (_useManagedIdentity) connection.AccessToken = await GetAccessTokenAsync();
        await connection.OpenAsync();

        var surveyResponseTable = new DataTable();
        surveyResponseTable.Columns.Add("Id", typeof(Guid));
        surveyResponseTable.Columns.Add("SurveyId", typeof(Guid));

        foreach (var surveyResponseId in responseIds)
        {
            var row = surveyResponseTable.NewRow();
            row["Id"] = surveyResponseId;
            row["SurveyId"] = surveyId;
            surveyResponseTable.Rows.Add(row);
        }

        using var surveyResponseBulkCopy = new SqlBulkCopy(connection);

        surveyResponseBulkCopy.DestinationTableName = "SurveyResponse";
        surveyResponseBulkCopy.ColumnMappings.Add("Id", "Id");
        surveyResponseBulkCopy.ColumnMappings.Add("SurveyId", "SurveyId");

        await surveyResponseBulkCopy.WriteToServerAsync(surveyResponseTable);
    }

    internal async Task UploadSurveyQuestionsAsync(Guid surveyId, List<Question> questions)
    {
        using var connection = new SqlConnection(_connectionString);
        if (_useManagedIdentity) connection.AccessToken = await GetAccessTokenAsync();
        await connection.OpenAsync();

        var questionTable = new DataTable();
        questionTable.Columns.Add("Id", typeof(Guid));
        questionTable.Columns.Add("SurveyId", typeof(Guid));
        questionTable.Columns.Add("Question", typeof(string));
        questionTable.Columns.Add("DataType", typeof(string));
        questionTable.Columns.Add("Description", typeof(string));
        questionTable.Columns.Add("Embedding", typeof(string));

        foreach (var question in questions)
        {
            var row = questionTable.NewRow();
            row["Id"] = question.Id;
            row["SurveyId"] = question.SurveyId;
            row["Question"] = question.Text;
            row["DataType"] = question.DataType;
            row["Description"] = question.Description;
            row["Embedding"] = question.Embedding.HasValue
                ? JsonConvert.SerializeObject(question.Embedding.Value.ToArray())
                : DBNull.Value;
            questionTable.Rows.Add(row);
        }

        using var questionBulkCopy = new SqlBulkCopy(connection);

        questionBulkCopy.DestinationTableName = "SurveyQuestion";
        questionBulkCopy.ColumnMappings.Add("Id", "Id");
        questionBulkCopy.ColumnMappings.Add("SurveyId", "SurveyId");
        questionBulkCopy.ColumnMappings.Add("Question", "Question");
        questionBulkCopy.ColumnMappings.Add("DataType", "DataType");
        questionBulkCopy.ColumnMappings.Add("Description", "Description");
        questionBulkCopy.ColumnMappings.Add("Embedding", "Embedding");

        await questionBulkCopy.WriteToServerAsync(questionTable);
    }

    internal async Task UploadSurveyQuestionAnswersAsync(List<Answer> answers)
    {
        using var connection = new SqlConnection(_connectionString);
        if (_useManagedIdentity) connection.AccessToken = await GetAccessTokenAsync();
        await connection.OpenAsync();

        var answerTable = new DataTable();
        answerTable.Columns.Add("Id", typeof(Guid));
        answerTable.Columns.Add("SurveyId", typeof(Guid));
        answerTable.Columns.Add("SurveyResponseId", typeof(Guid));
        answerTable.Columns.Add("SurveyQuestionId", typeof(Guid));
        answerTable.Columns.Add("TextAnswer", typeof(string));
        answerTable.Columns.Add("NumericAnswer", typeof(decimal));
        answerTable.Columns.Add("SentimentAnalysisJson", typeof(string));
        answerTable.Columns.Add("PositiveSentimentConfidenceScore", typeof(double));
        answerTable.Columns.Add("NeutralSentimentConfidenceScore", typeof(double));
        answerTable.Columns.Add("NegativeSentimentConfidenceScore", typeof(double));
        answerTable.Columns.Add("Embedding", typeof(string));

        foreach (var answer in answers)
        {
            var row = answerTable.NewRow();
            row["Id"] = answer.Id;
            row["SurveyId"] = answer.SurveyId;
            row["SurveyResponseId"] = answer.SurveyResponseId;
            row["SurveyQuestionId"] = answer.SurveyQuestionId;
            row["TextAnswer"] = answer.TextAnswer ?? (object)DBNull.Value;
            row["NumericAnswer"] = answer.NumericAnswer ?? (object)DBNull.Value;
            row["SentimentAnalysisJson"] = answer.SentimentAnalysisJson ?? (object)DBNull.Value;
            row["PositiveSentimentConfidenceScore"] = answer.PositiveSentimentConfidenceScore ?? (object)DBNull.Value;
            row["NeutralSentimentConfidenceScore"] = answer.NeutralSentimentConfidenceScore ?? (object)DBNull.Value;
            row["NegativeSentimentConfidenceScore"] = answer.NegativeSentimentConfidenceScore ?? (object)DBNull.Value;
            row["Embedding"] = answer.Embedding.HasValue
                ? JsonConvert.SerializeObject(answer.Embedding.Value.ToArray())
                : DBNull.Value;

            answerTable.Rows.Add(row);
        }

        using var answerBulkCopy = new SqlBulkCopy(connection);

        answerBulkCopy.DestinationTableName = "SurveyQuestionAnswer";
        answerBulkCopy.ColumnMappings.Add("Id", "Id");
        answerBulkCopy.ColumnMappings.Add("SurveyId", "SurveyId");
        answerBulkCopy.ColumnMappings.Add("SurveyResponseId", "SurveyResponseId");
        answerBulkCopy.ColumnMappings.Add("SurveyQuestionId", "SurveyQuestionId");
        answerBulkCopy.ColumnMappings.Add("TextAnswer", "TextAnswer");
        answerBulkCopy.ColumnMappings.Add("NumericAnswer", "NumericAnswer");
        answerBulkCopy.ColumnMappings.Add("SentimentAnalysisJson", "SentimentAnalysisJson");
        answerBulkCopy.ColumnMappings.Add("PositiveSentimentConfidenceScore", "PositiveSentimentConfidenceScore");
        answerBulkCopy.ColumnMappings.Add("NeutralSentimentConfidenceScore", "NeutralSentimentConfidenceScore");
        answerBulkCopy.ColumnMappings.Add("NegativeSentimentConfidenceScore", "NegativeSentimentConfidenceScore");
        answerBulkCopy.ColumnMappings.Add("Embedding", "Embedding");

        await answerBulkCopy.WriteToServerAsync(answerTable);
    }
}

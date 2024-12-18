using Microsoft.Data.SqlClient;
using System.Data;

namespace CsvDataUploader.Services;

internal class AzureSqlService
{
    private readonly string _connectionString;
    internal AzureSqlService(string connectionString)
    {
        _connectionString = connectionString;
    }

    internal async Task<bool> TestConnectionAsync()
    {
        using var connection = new SqlConnection(_connectionString);
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
        await connection.OpenAsync();

        var clearDatabaseCommand = new SqlCommand("DELETE FROM SurveyQuestionAnswer; DELETE FROM SurveyQuestion; DELETE FROM SurveyResponse; DELETE FROM Survey;", connection);
        await clearDatabaseCommand.ExecuteNonQueryAsync();
    }

    internal async Task<Guid> UploadSurveyAsync(string surveyName, string version = "1.0")
    {
        using var connection = new SqlConnection(_connectionString);
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
        await connection.OpenAsync();

        var questionTable = new DataTable();
        questionTable.Columns.Add("Id", typeof(Guid));
        questionTable.Columns.Add("SurveyId", typeof(Guid));
        questionTable.Columns.Add("Question", typeof(string));
        questionTable.Columns.Add("DataType", typeof(string));
        questionTable.Columns.Add("Description", typeof(string));

        foreach (var question in questions)
        {
            var row = questionTable.NewRow();
            row["Id"] = question.Id;
            row["SurveyId"] = question.SurveyId;
            row["Question"] = question.Text;
            row["DataType"] = question.DataType;
            row["Description"] = question.Description;
            questionTable.Rows.Add(row);
        }

        using var questionBulkCopy = new SqlBulkCopy(connection);

        questionBulkCopy.DestinationTableName = "SurveyQuestion";
        questionBulkCopy.ColumnMappings.Add("Id", "Id");
        questionBulkCopy.ColumnMappings.Add("SurveyId", "SurveyId");
        questionBulkCopy.ColumnMappings.Add("Question", "Question");
        questionBulkCopy.ColumnMappings.Add("DataType", "DataType");
        questionBulkCopy.ColumnMappings.Add("Description", "Description");

        await questionBulkCopy.WriteToServerAsync(questionTable);
    }

    internal async Task UploadSurveyQuestionAnswersAsync(List<Answer> answers)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var answerTable = new DataTable();
        answerTable.Columns.Add("Id", typeof(Guid));
        answerTable.Columns.Add("SurveyId", typeof(Guid));
        answerTable.Columns.Add("QuestionId", typeof(Guid));
        answerTable.Columns.Add("SurveyResponseId", typeof(Guid));
        answerTable.Columns.Add("TextAnswer", typeof(string));
        answerTable.Columns.Add("NumericAnswer", typeof(decimal));
        answerTable.Columns.Add("SentimentAnalysisJson", typeof(string));
        
        foreach (var answer in answers)
        {
            var row = answerTable.NewRow();
            row["Id"] = answer.Id;
            row["SurveyId"] = answer.SurveyId;
            row["SurveyResponseId"] = answer.SurveyResponseId;
            row["QuestionId"] = answer.QuestionId;
            row["TextAnswer"] = answer.TextAnswer ?? (object)DBNull.Value;
            row["NumericAnswer"] = answer.NumericAnswer ?? (object)DBNull.Value;
            row["SentimentAnalysisJson"] = answer.SentimentAnalysisJson ?? (object)DBNull.Value;
            answerTable.Rows.Add(row);
        }

        using var answerBulkCopy = new SqlBulkCopy(connection);

        answerBulkCopy.DestinationTableName = "SurveyQuestionAnswer";
        answerBulkCopy.ColumnMappings.Add("Id", "Id");
        answerBulkCopy.ColumnMappings.Add("SurveyId", "SurveyId");
        answerBulkCopy.ColumnMappings.Add("QuestionId", "QuestionId");
        answerBulkCopy.ColumnMappings.Add("SurveyResponseId", "SurveyResponseId");
        answerBulkCopy.ColumnMappings.Add("TextAnswer", "TextAnswer");
        answerBulkCopy.ColumnMappings.Add("NumericAnswer", "NumericAnswer");
        answerBulkCopy.ColumnMappings.Add("SentimentAnalysisJson", "SentimentAnalysisJson");

        await answerBulkCopy.WriteToServerAsync(answerTable);
    }
}

using ChatApp.Server.Models;
using Microsoft.Data.SqlClient;

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
}

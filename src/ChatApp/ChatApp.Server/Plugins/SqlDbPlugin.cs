using ChatApp.Server.Services;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.ComponentModel;

namespace ChatApp.Server.Plugins;

public class SqlDbPlugin
{
    private readonly IChatCompletionService _chatService;
    private readonly SurveyService _surveyService;
    public SqlDbPlugin(IChatCompletionService chatService, SurveyService surveyService)
    {
        _chatService = chatService;
        _surveyService = surveyService;
    }

    // todo: play with using sys prompt to organize cot on getting schema + metadata (optionally filtering metadata down)
    // then generate and execute query


    // todo: include data type for deciding on what to include in aggregates
    //[KernelFunction(nameof(GetSurveyQuestionsAsync))]
    //[Description("Get metadatada about the questions in the format of ID | Question | Description")]
    //[return: Description("A pipe delimited string of question metadata")]
    //public async Task<string> GetSurveyQuestionsAsync([Description("The ID of the survey")] Guid surveyId)
    //{
    //    return await _surveyService.GetSurveyMetadataAsync(surveyId);
    //}

    //[KernelFunction(nameof(GetTableSchemaAsync))]
    //[Description("The schema of the tables available in Azure SQL")]
    //[return: Description("A list of tables and their properties as text")]
    //public async Task<string> GetTableSchemaAsync()
    //{
    //    return await _surveyService.GetTablesDataSchemaAsync();
    //}

    [KernelFunction(nameof(SqlQueryGeneration))]
    [Description("Generates a SQL query based on a SQL tables schema and questions metadata that are cross referenced to a user's question.")]
    [return: Description("The SQL query")]
    public async Task<string> SqlQueryGeneration(
        [Description("The intent of the query.")] string input,
        //[Description("A list of tables and their properties in the SQL database")] string sqlSchema,
        //[Description("A comma delimited string of question metadata")] string questionMetadata,
        [Description("The ID of the survey")] Guid surveyId,
        Kernel kernel)
    {
        var sqlSchema = await _surveyService.GetTablesDataSchemaAsync();
        var questionMetadata = await _surveyService.GetSurveyMetadataAsync(surveyId);

        var systemPrompt = $"""
            Given the follow SQL schema containing survey data
            {sqlSchema}
            with values for the SurveyQuestion table in format Id|Question|Description. Use Description as a hint to identify best relevant question. Values for the SurveyQuestion table:
            {questionMetadata}
            generate a syntactically correct SQL Server query in Transact-SQL dialect to answer the user question: "{input}". Only use tables (except the Survey table) and columns form schema description.
            The SurveyResponse table has one to many relationship to SurveyQuestionAnswer where SurveyResponses represent individual responses to a given survey with the answers to the SurveyQuestions being records in the SurveyQuestionAnswer table.
            Question "Name" is for actual account name. Use "NPS Score" question a score calculation. 
            Use parameterized queries to hadle Question values with special characters, like quotes. Only provide the SQL query. Do not encapsulate it in markdown.
            """;

        var history = new ChatHistory(systemPrompt);

        history.AddUserMessage(input);

        var response = await _chatService.GetChatMessageContentAsync(history);

        return response.ToString();
    }

    [KernelFunction(nameof(ExecuteSqlQueryAsync))]
    [Description("Execute a query against the SQL Database.")]
    [return: Description("The result of the query")]
    //public async Task<List<dynamic>> ExecuteSqlQueryAsync([Description("The query to run")] string query)
    public async Task<List<dynamic>> ExecuteSqlQueryAsync([Description("The query to run")] string query)
    {
        // reason for abstraction is for improved
        // dependency injection and lifetime of sql connection pooling
        return await _surveyService.ExecuteSqlQueryAsync(query);

        //return query;
    }
}
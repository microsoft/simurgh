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

    //[KernelFunction(nameof(SqlQueryGeneration))]
    //[Description("Generates a SQL query based on a SQL tables schema and questions metadata that are cross referenced to a user's question.")]
    //[return: Description("The SQL query")]
    //public async Task<string> SqlQueryGeneration(
    //    [Description("The intent of the query.")] string input,
    //    //[Description("A list of tables and their properties in the SQL database")] string sqlSchema,
    //    //[Description("A comma delimited string of question metadata")] string questionMetadata,
    //    [Description("The ID of the survey")] Guid surveyId,
    //    Kernel kernel)
    //{
    //    //var sqlSchema = await _surveyService.GetTablesDataSchemaAsync();
    //    var questionMetadata = await _surveyService.GetSurveyMetadataAsync(surveyId);

    //    var systemPrompt = $"""
    //        Given the follow SQL schema containing survey data

    //        Table: dbo.SurveyQuestion contains a definition for questions in the survey
    //        - Id (guid)
    //        - SurveyId (guid)
    //        - Question (nvarchar)
    //        - DataType (varchar) numeric or text
    //        - Description (nvarchar) nullable
    //        Table: dbo.SurveyQuestionAnswer contains individual answers to the survey question with the SurveyResponseId grouping the answers for a givem response to the survey
    //        - Id (guid)
    //        - SurveyId (guid)
    //        - SurveyResponseId (guid)
    //        - SurveyQuestionId (guid)
    //        - TextAnswer (nvarchar) nullable, contains the answer for text questions
    //        - NumericAnswer (numeric) nullable, contains the answer for numeric questions
    //        - PositiveSentimentConfidenceScore (float) nullable, contains the confidence score for positive sentiment for text answers
    //        - NeutralSentimentConfidenceScore (float) nullable, contains the confidence score for neutral sentiment for text answers
    //        - NegativeSentimentConfidenceScore (float) nullable, contains the confidence score for negative sentiment for text answers

    //        and these values for the SurveyQuestion records the format of Id | Question | DataType | Description

    //        {questionMetadata}

    //        generate a syntactically correct SQL Server query in Transact-SQL dialect to answer the user question: "{input}". Only use tables (except the Survey table) and columns form schema description.
    //        The SurveyResponse table has one to many relationship to SurveyQuestionAnswer where SurveyResponses represent individual responses to a given survey with the answers to the SurveyQuestions being records in the SurveyQuestionAnswer table.
    //        Only provide the SQL query. Do not encapsulate it in markdown.
    //        """;
    //    // Use Description as a hint to identify best relevant question >>>changed
    //    // Question "Name" is for actual account name. Use "NPS Score" question a score calculation. 
    //    //Use parameterized queries to hadle Question values with special characters, like quotes.

    //    var history = new ChatHistory(systemPrompt);

    //    history.AddUserMessage(input);

    //    var response = await _chatService.GetChatMessageContentAsync(history);

    //    return response.ToString();
    //}



    [KernelFunction(nameof(SplitUserQuestionAsync))]
    [Description("Splits a user question into any subqueries.")]
    [return: Description("String containing new line delimited components to the user's query")]
    public async Task<string> SplitUserQuestionAsync(
      [Description("The intent of the query.")] string input,
      Kernel kernel)
    {
        var systemPrompt = $"""
            Break down the following user question into component parts. Each part should be on a new line.
            """;

        // The parts will be used in prompt engineering to generate a SQL query.

        var history = new ChatHistory(systemPrompt);

        history.AddUserMessage(input);

        var response = await _chatService.GetChatMessageContentAsync(history);

        return response.ToString();
    }


    [KernelFunction(nameof(CombineSqlQueriesAsync))]
    [Description("Splits a user question into any subqueries.")]
    [return: Description("String containing new line delimited components to the user's query")]
    public async Task<string> CombineSqlQueriesAsync(
      [Description("User question")] string input,
      [Description("SQL query representing a component of the user's quetsion.")] string sqlQuery1,
      [Description("SQL query representing a component of the user's quetsion.")] string sqlQuery2,
      Kernel kernel)
    {
        var systemPrompt = $"""
            Given the follow SQL schema containing survey data
            Table: dbo.SurveyQuestion contains a definition for questions in the survey
            - Id (guid)
            - SurveyId (guid)
            - Question (nvarchar)
            - DataType (varchar) numeric or text
            - Description (nvarchar) nullable
            Table: dbo.SurveyQuestionAnswer contains individual answers to the survey question with the SurveyResponseId grouping the answers for a givem response to the survey
            - Id (guid)
            - SurveyId (guid)
            - SurveyResponseId (guid)
            - SurveyQuestionId (guid)
            - TextAnswer (nvarchar) nullable, contains the answer for text questions
            - NumericAnswer (numeric) nullable, contains the answer for numeric questions
            - PositiveSentimentConfidenceScore (float) nullable, contains the confidence score for positive sentiment for text answers
            - NeutralSentimentConfidenceScore (float) nullable, contains the confidence score for neutral sentiment for text answers
            - NegativeSentimentConfidenceScore (float) nullable, contains the confidence score for negative sentiment for text answers
            Combine the following SQL queries into a single query that answers the user question: "{input}".

            {sqlQuery1}

            {sqlQuery2}
            """;

        // The parts will be used in prompt engineering to generate a SQL query.

        var history = new ChatHistory(systemPrompt);

        history.AddUserMessage(input);

        var response = await _chatService.GetChatMessageContentAsync(history);

        return response.ToString();
    }

    [KernelFunction(nameof(GenerateSqlAggregateOfSurveyQuestionAsync))]
    [Description("Generates a SQL query based to find an aggregate for a given SurveyQuestionId.")]
    [return: Description("The SQL query")]
    public async Task<string> GenerateSqlAggregateOfSurveyQuestionAsync(
      [Description("The intent of the query.")] string input,
      [Description("The ID of the survey")] Guid surveyId,
      [Description("SurveyQuestionId")] Guid surveyQuestiondId,
      Kernel kernel)
    {
        var systemPrompt = $"""
            Given the follow SQL schema containing survey data
            Table: dbo.SurveyQuestion contains a definition for questions in the survey
            - Id (guid)
            - SurveyId (guid)
            - Question (nvarchar)
            - DataType (varchar) numeric or text
            - Description (nvarchar) nullable
            Table: dbo.SurveyQuestionAnswer contains individual answers to the survey question with the SurveyResponseId grouping the answers for a givem response to the survey
            - Id (guid)
            - SurveyId (guid)
            - SurveyResponseId (guid)
            - SurveyQuestionId (guid)
            - TextAnswer (nvarchar) nullable, contains the answer for text questions
            - NumericAnswer (numeric) nullable, contains the answer for numeric questions
            - PositiveSentimentConfidenceScore (float) nullable, contains the confidence score for positive sentiment for text answers
            - NeutralSentimentConfidenceScore (float) nullable, contains the confidence score for neutral sentiment for text answers
            - NegativeSentimentConfidenceScore (float) nullable, contains the confidence score for negative sentiment for text answers
            and the most relevant survey question based on the user question has an Id of {surveyQuestiondId}
            generate a syntactically correct SQL Server query in Transact-SQL dialect to answer the user question: "{input}".
            Only provide the SQL query. Do not encapsulate it in markdown.
            For aggregates, ignore null or negative answers.
            Only aggregate results where surveyId equals {surveyId}.
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
       return await _surveyService.ExecuteSqlQueryAsync(query);
    }
}
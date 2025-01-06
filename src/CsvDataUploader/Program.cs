using CsvDataUploader;
using CsvDataUploader.Models;
using CsvDataUploader.Options;
using CsvDataUploader.Services;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Globalization;

Console.ResetColor();

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json")
    .AddUserSecrets<Program>()
    .Build();

var sqlOptions = new SqlOptions();
configuration.GetSection(nameof(SqlOptions)).Bind(sqlOptions);
var csvOptions = new CsvOptions();
configuration.GetSection(nameof(CsvOptions)).Bind(csvOptions);
var textAnalyticsServiceOptions = new TextAnalyticsServiceOptions();
configuration.GetSection(nameof(TextAnalyticsServiceOptions)).Bind(textAnalyticsServiceOptions);
var vectorizationOptions = new VectorizationOptions();
configuration.GetSection(nameof(VectorizationOptions)).Bind(vectorizationOptions);

var timeStart = DateTime.UtcNow;
Console.WriteLine($"Initialized at {timeStart}.");

Console.WriteLine(Environment.NewLine);

var sqlConnectionStringBuilder = new SqlConnectionStringBuilder();

if (string.IsNullOrWhiteSpace(sqlOptions.ConnectionString))
{
    ArgumentException.ThrowIfNullOrWhiteSpace(sqlOptions.Endpoint, nameof(sqlOptions.Endpoint));
    ArgumentException.ThrowIfNullOrWhiteSpace(sqlOptions.DatabaseName, nameof(sqlOptions.DatabaseName));

    Console.WriteLine("Building a connection string using provided endpoint and database name.");
    sqlConnectionStringBuilder.DataSource = sqlOptions.Endpoint;
    sqlConnectionStringBuilder.InitialCatalog = sqlOptions.DatabaseName;

    if (string.IsNullOrWhiteSpace(sqlOptions.UserId) || string.IsNullOrWhiteSpace(sqlOptions.Password))
    {
        Console.WriteLine("Using managed identity to authenticate to Azure SQL Database.");
        sqlConnectionStringBuilder.Authentication = SqlAuthenticationMethod.ActiveDirectoryManagedIdentity;
        sqlConnectionStringBuilder.Encrypt = true;
    }
    else
    {
        Console.WriteLine("Using provided user ID and password to authenticate to Azure SQL Database.");
        sqlConnectionStringBuilder.UserID = sqlOptions.UserId;
        sqlConnectionStringBuilder.Password = sqlOptions.Password;
    }
}
else
{
    Console.WriteLine("Using provided connection string.");
    sqlConnectionStringBuilder.ConnectionString = sqlOptions.ConnectionString;
}

var azureSqlHelper = new AzureSqlService(sqlConnectionStringBuilder, sqlOptions.TenantId);

if (!await azureSqlHelper.TestConnectionAsync())
{
    Console.WriteLine("Failed to connect to Azure SQL Database.");
    return;
}
else
    Console.WriteLine("Connected to Azure SQL Database successfully.");

if (!File.Exists(csvOptions.CsvFilePath))
{
    Console.WriteLine("The CSV file does not exist.");
    return;
}
else
    Console.WriteLine("Verified CSV file existence at path: " + csvOptions.CsvFilePath);

using var reader = new StreamReader(csvOptions.CsvFilePath);
using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

var headerDescriptions = new Dictionary<string, string>();
if (!string.IsNullOrWhiteSpace(csvOptions.CsvHeaderDescriptionsFilePath) && File.Exists(csvOptions.CsvFilePath))
{
    Console.WriteLine("Reading header descriptions from " + csvOptions.CsvHeaderDescriptionsFilePath);
    using var csvHeaderDescriptionsReader = new StreamReader(csvOptions.CsvHeaderDescriptionsFilePath);
    using var csvHeaderDescriptions = new CsvReader(csvHeaderDescriptionsReader,
        new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false
        });

    headerDescriptions = csvHeaderDescriptions.GetHeaderDescriptions();
}
Console.WriteLine($"Number of header-description pairs: {headerDescriptions.Count}");

if (sqlOptions.ClearDatabaseDataBeforeRun)
    await azureSqlHelper.ClearDatabaseDataAsync();

TextAnalyticsService? textAnalyticsService = null;
bool canPerformSentimentAnalysis = false;
if (!string.IsNullOrWhiteSpace(textAnalyticsServiceOptions.Endpoint) && headerDescriptions.Any())
{
    textAnalyticsService = new TextAnalyticsService(textAnalyticsServiceOptions);
    canPerformSentimentAnalysis = await textAnalyticsService.TestConnectionAsync();
    if (!canPerformSentimentAnalysis)
        Console.WriteLine("Failed to connect to Text Analytics service. Skipping");
    else
        Console.WriteLine($"{nameof(TextAnalyticsService)} successfully configured. Any columns marked as containing unstructured data will receive sentiment analysis.");
}

VectorizationService? vectorizationService = null;
// todo: verify connection
if (!string.IsNullOrWhiteSpace(vectorizationOptions.Endpoint) && !string.IsNullOrWhiteSpace(vectorizationOptions.Deployment))
{
    vectorizationService = new VectorizationService(vectorizationOptions);
    Console.WriteLine("Azure Open AI service is configured. Will use it to vectorize text data.");
}
Console.WriteLine(Environment.NewLine);

var rowsToUpload = csv.GetRecords<dynamic>()
    .OfType<IDictionary<string, object>>()
    .ToList() ?? [];

Console.WriteLine("Processing rows...");

var filename = Path.GetFileName(csvOptions.CsvFilePath);
var version = "1.0"; // Assuming version is 1.0 for all rows; todo: read this from appropriate column

var surveyId = await azureSqlHelper.UploadSurveyAsync(filename, version);

Console.WriteLine($"Survey inserted... {surveyId}");

var surveyResponse = new List<Guid>();
// use literal question text (the CSV column name) as the dictionary key
var questions = new Dictionary<string, Question>();

ConsoleUtility.WriteProgressBar(0);

var lastPercent = 0;
foreach (var row in rowsToUpload)
{
    var surveyResponseId = Guid.NewGuid();
    surveyResponse.Add(surveyResponseId);

    foreach (var key in row.Keys)
    {
        var stringVal = row[key] as string;

        // skip empty answers so as not to skew question data type; this assumes no questions go completely unaswered
        if (string.IsNullOrWhiteSpace(stringVal))
            continue;

        // parsing helper will try to parse the string value to a numeric value and handle special cases
        decimal? numericVal = ParsingHelper.TryGetNumericValue(stringVal, out var decimalLiteral) ? decimalLiteral : null;

        // converting not applicables to -1
        if (string.Equals(stringVal, "Not Applicable", StringComparison.InvariantCultureIgnoreCase))
            numericVal = -1;

        var questionDescription = headerDescriptions[key] ?? string.Empty;

        if (!questions.TryGetValue(key, out var question))
        {
            // todo: potentially move header descriptions into the save file as a separate special row?
            question = new(surveyId, key, numericVal.HasValue ? "numeric" : "string", headerDescriptions[key] ?? string.Empty);
            if (vectorizationService != null) // generating embedding off a combination of the question and its description if available; description provides optional context
                question.Embedding = await vectorizationService.GetEmbeddingAsync(key + Environment.NewLine + headerDescriptions[key] ?? string.Empty);
            questions.Add(key, question);
        }

        Answer answer = question.DataType switch
        {
            "string" => new(surveyId, surveyResponseId, question.Id, textAnswer: stringVal),
            "numeric" => new(surveyId, surveyResponseId, question.Id, numericAnswer: numericVal),
            _ => throw new NotSupportedException($"Data type '{question.DataType}' is not supported.")
        };

        // todo: determine whether a question requires a sentiment analysis...
        // for now we'll just check for the presence of the phrase "unstructured data" in the question description
        if (question.Description.Contains("unstructured data", StringComparison.InvariantCultureIgnoreCase))
        {
            if (vectorizationService != null)
                answer.Embedding = await vectorizationService.GetEmbeddingAsync(stringVal);

            if (textAnalyticsService != null)
            {
                var analysis = await textAnalyticsService.AnalyzeSentimentAsync(stringVal);

                if (analysis is not null)
                {
                    answer.SentimentAnalysisJson = JsonConvert.SerializeObject(analysis);
                    answer.PositiveSentimentConfidenceScore = analysis.ConfidenceScores.Positive;
                    answer.NeutralSentimentConfidenceScore = analysis.ConfidenceScores.Neutral;
                    answer.NegativeSentimentConfidenceScore = analysis.ConfidenceScores.Negative;
                }
            }
        }

        var currentPercent = rowsToUpload.IndexOf(row) * 100 / rowsToUpload.Count;
        if (lastPercent != currentPercent)
            ConsoleUtility.WriteProgressBar(currentPercent, true);
        lastPercent = currentPercent;
        question.Answers.Add(answer);
    }
}
ConsoleUtility.WriteProgressBar(100, true);

Console.WriteLine(Environment.NewLine);
Console.WriteLine($"Questions and answers processed... {questions.Count} questions and {surveyResponse.Count} responses");

await azureSqlHelper.UploadSurveyResponsesAsync(surveyId, surveyResponse);
Console.WriteLine($"Survey responses inserted... {surveyResponse.Count}");

await azureSqlHelper.UploadSurveyQuestionsAsync(surveyId, questions.Values.ToList());
Console.WriteLine($"Questions inserted... {questions.Count}");

var allAnswers = questions.Values.SelectMany(q => q.Answers);
await azureSqlHelper.UploadSurveyQuestionAnswersAsync(allAnswers.ToList());
Console.WriteLine($"Answers inserted... {allAnswers.Count()}");

Console.WriteLine(Environment.NewLine);
var timeEnd = DateTime.UtcNow;
Console.WriteLine($"Finished at {timeEnd}");
Console.WriteLine($"Total time taken: {timeEnd - timeStart}");
Console.WriteLine("Press any key to exit...");
Console.ReadKey();

Console.ResetColor();

using Azure.Core;
using Azure.Identity;
using CsvDataUploader;
using CsvHelper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Globalization;

var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .AddUserSecrets<Program>()
            .Build();

var options = new UploaderOptions();
configuration.GetSection(nameof(UploaderOptions)).Bind(options);

#region SQL

var sqlConnectionStringBuilder = new SqlConnectionStringBuilder
{
    DataSource = options.SqlServerEndpoint,
    InitialCatalog = options.SqlDatabaseName
};

var defaultAzureCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions() { TenantId = options.TenantId });
var tokenResult = await defaultAzureCredential.GetTokenAsync(new TokenRequestContext(scopes: ["https://database.windows.net/.default"]));

using var sqlConnection = new SqlConnection(sqlConnectionStringBuilder.ConnectionString);
sqlConnection.AccessToken = tokenResult.Token;

try
{
    await sqlConnection.OpenAsync();
    Console.WriteLine("Connected to Azure SQL Database successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to connect to Azure SQL Database: {ex.Message}");
}

if (!File.Exists(options.CsvFilePath)) throw new ArgumentException("CsvFilePath does not exist.");

using var reader = new StreamReader(options.CsvFilePath);
using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

// todo: clear out existing data for this CSV file for idempotency

var surveyId = Guid.NewGuid();
var filename = Path.GetFileName(options.CsvFilePath);
var version = "1.0"; // Assuming version is 1.0 for all rows

var insertSurveyCommand = new SqlCommand("INSERT INTO Survey (Id, Filename, Version) VALUES (@Id, @Filename, @Version)", sqlConnection);
insertSurveyCommand.Parameters.AddWithValue("@Id", surveyId);
insertSurveyCommand.Parameters.AddWithValue("@Filename", filename);
insertSurveyCommand.Parameters.AddWithValue("@Version", version);   // pull this from designated column which should have the samcross all CSVs
await insertSurveyCommand.ExecuteNonQueryAsync();

var rowsToUpload = csv.GetRecords<dynamic>()
    .OfType<IDictionary<string, object>>()
    .ToList() ?? [];

// use literal question text (the CSV column name) as the dictionary key
var surveyResponse = new List<Guid>();
var questions = new Dictionary<string, Question>();

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

        decimal? numericVal = int.TryParse(stringVal, out var intLiteral)
               ? Convert.ToDecimal(intLiteral)
               : decimal.TryParse(stringVal, out var decimalLiteral) ? decimalLiteral : null;

        // todo: handle colums with mixed data types e.g. 10 - Extremely Satisfied

        if (!questions.TryGetValue(key, out var question))
        {
            question = new(surveyId, key, numericVal.HasValue ? "numeric" : "string", string.Empty); // todo: generate description later?
            questions.Add(key, question);
        }

        Answer answer = question.DataType switch
        {
            "string" => new(surveyId, surveyResponseId, question.Id, textAnswer: stringVal),
            "numeric" => new(surveyId, surveyResponseId, question.Id, numericAnswer: numericVal),
            _ => throw new NotSupportedException($"Data type '{question.DataType}' is not supported.")
        };

        question.Answers.Add(answer);
    }
}

var surveyResponseTable = new DataTable();
surveyResponseTable.Columns.Add("Id", typeof(Guid));
surveyResponseTable.Columns.Add("SurveyId", typeof(Guid));

foreach (var surveyResponseId in surveyResponse)
{
    var row = surveyResponseTable.NewRow();
    row["Id"] = surveyResponseId;
    row["SurveyId"] = surveyId;
    surveyResponseTable.Rows.Add(row);
}

using var surveyResponseBulkCopy = new SqlBulkCopy(sqlConnection);

surveyResponseBulkCopy.DestinationTableName = "SurveyResponse";
surveyResponseBulkCopy.ColumnMappings.Add("Id", "Id");
surveyResponseBulkCopy.ColumnMappings.Add("SurveyId", "SurveyId");

await surveyResponseBulkCopy.WriteToServerAsync(surveyResponseTable);

var questionTable = new DataTable();
questionTable.Columns.Add("Id", typeof(Guid));
questionTable.Columns.Add("SurveyId", typeof(Guid));
questionTable.Columns.Add("Question", typeof(string));
questionTable.Columns.Add("DataType", typeof(string));
questionTable.Columns.Add("Description", typeof(string));

foreach (var question in questions.Values)
{
    var row = questionTable.NewRow();
    row["Id"] = question.Id;
    row["SurveyId"] = question.SurveyId;
    row["Question"] = question.Text;
    row["DataType"] = question.DataType;
    row["Description"] = question.Description;
    questionTable.Rows.Add(row);
}

using var questionBulkCopy = new SqlBulkCopy(sqlConnection);

questionBulkCopy.DestinationTableName = "SurveyQuestion";
questionBulkCopy.ColumnMappings.Add("Id", "Id");
questionBulkCopy.ColumnMappings.Add("SurveyId", "SurveyId");
questionBulkCopy.ColumnMappings.Add("Question", "Question");
questionBulkCopy.ColumnMappings.Add("DataType", "DataType");
questionBulkCopy.ColumnMappings.Add("Description", "Description");

await questionBulkCopy.WriteToServerAsync(questionTable);

var answerTable = new DataTable();
answerTable.Columns.Add("Id", typeof(Guid));
answerTable.Columns.Add("SurveyId", typeof(Guid));
answerTable.Columns.Add("QuestionId", typeof(Guid));
answerTable.Columns.Add("SurveyResponseId", typeof(Guid));
answerTable.Columns.Add("TextAnswer", typeof(string));
answerTable.Columns.Add("NumericAnswer", typeof(decimal));

var allAnswers = questions.Values.SelectMany(q => q.Answers);

foreach (var answer in allAnswers)
{
    var row = answerTable.NewRow();
    row["Id"] = answer.Id;
    row["SurveyId"] = answer.SurveyId;
    row["SurveyResponseId"] = answer.SurveyResponseId;
    row["QuestionId"] = answer.QuestionId;
    row["TextAnswer"] = answer.TextAnswer ?? (object)DBNull.Value;
    row["NumericAnswer"] = answer.NumericAnswer ?? (object)DBNull.Value;
    answerTable.Rows.Add(row);
}

using var answerBulkCopy = new SqlBulkCopy(sqlConnection);

answerBulkCopy.DestinationTableName = "SurveyQuestionAnswer";
answerBulkCopy.ColumnMappings.Add("Id", "Id");
answerBulkCopy.ColumnMappings.Add("SurveyId", "SurveyId");
answerBulkCopy.ColumnMappings.Add("QuestionId", "QuestionId");
answerBulkCopy.ColumnMappings.Add("SurveyResponseId", "SurveyResponseId");
answerBulkCopy.ColumnMappings.Add("TextAnswer", "TextAnswer");
answerBulkCopy.ColumnMappings.Add("NumericAnswer", "NumericAnswer");

await answerBulkCopy.WriteToServerAsync(answerTable);






Console.WriteLine("Finished...");

#endregion




#region Cosmos

//CosmosClient cosmosClient;

//if (!string.IsNullOrWhiteSpace(options.ConnectionString))
//{
//    cosmosClient = new CosmosClient(options.ConnectionString);
//}
//else if (!string.IsNullOrWhiteSpace(options.Endpoint))
//{
//    if (string.IsNullOrWhiteSpace(options.Key))
//    {
//        var defaultAzureCredential =
//            string.IsNullOrWhiteSpace(options.TenantId) ? new DefaultAzureCredential() :
//            new DefaultAzureCredential(new DefaultAzureCredentialOptions { TenantId = options.TenantId });

//        cosmosClient = new CosmosClient(options.Endpoint, defaultAzureCredential);
//    }
//    else
//    {
//        cosmosClient = new CosmosClient(options.Endpoint, options.Key);
//    }
//}
//else
//    throw new ArgumentException("Either ConnectionString or Endpoint must be provided.");

//var container = cosmosClient.GetContainer(options.DatabaseName, options.ContainerName);

//if (!File.Exists(options.CsvFilePath)) throw new ArgumentException("CsvFilePath does not exist.");

//using var reader = new StreamReader(options.CsvFilePath);
//using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

//var rowsToUpload = csv.GetRecords<dynamic>()
//    .OfType<IDictionary<string, object>>()
//    .ToList() ?? [];

//var tasks = new List<Task>();

//foreach (var row in rowsToUpload)
//{
//    var rowToUpload = row.Where(r => r.Key is not null && (r.Value is string || r.Value is int))
//      .ToDictionary();

//    var partitionKey = Path.GetFileNameWithoutExtension(options.CsvFilePath);

//    rowToUpload["id"] = Guid.NewGuid().ToString();
//    rowToUpload[options.PartitionKey] = partitionKey;

//    tasks.Add(container.CreateItemAsync(rowToUpload, new PartitionKey(partitionKey)));

//    // Process in batches of 10
//    if (tasks.Count >= options.BatchSize)
//    {
//        await Task.WhenAll(tasks);
//        tasks.Clear();
//    }
//}

//// Process any remaining tasks
//if (tasks.Count != 0)
//    await Task.WhenAll(tasks);

//Console.WriteLine("Finished...");

#endregion

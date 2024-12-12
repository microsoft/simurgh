using Azure.Identity;
using CsvDataUploader;
using CsvHelper;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System.Globalization;

var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .AddUserSecrets<Program>()
            .Build();

var options = new UploaderOptions();
configuration.GetSection(nameof(UploaderOptions)).Bind(options);

CosmosClient cosmosClient;

if (!string.IsNullOrWhiteSpace(options.ConnectionString))
{
    cosmosClient = new CosmosClient(options.ConnectionString);
}
else if (!string.IsNullOrWhiteSpace(options.Endpoint))
{
    if (string.IsNullOrWhiteSpace(options.Key))
    {
        var defaultAzureCredential =
            string.IsNullOrWhiteSpace(options.TenantId) ? new DefaultAzureCredential() :
            new DefaultAzureCredential(new DefaultAzureCredentialOptions { TenantId = options.TenantId });

        cosmosClient = new CosmosClient(options.Endpoint, defaultAzureCredential);
    }
    else
    {
        cosmosClient = new CosmosClient(options.Endpoint, options.Key);
    }
}
else
    throw new ArgumentException("Either ConnectionString or Endpoint must be provided.");

var container = cosmosClient.GetContainer(options.DatabaseName, options.ContainerName);

if (!File.Exists(options.CsvFilePath)) throw new ArgumentException("CsvFilePath does not exist.");

using var reader = new StreamReader(options.CsvFilePath);
using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

var rowsToUpload = csv.GetRecords<dynamic>()
    .OfType<IDictionary<string, object>>()
    .ToList() ?? [];

var tasks = new List<Task>();

foreach (var row in rowsToUpload)
{
    var rowToUpload = row.Where(r => r.Key is not null && (r.Value is string || r.Value is int))
      .ToDictionary();

    var partitionKey = Path.GetFileNameWithoutExtension(options.CsvFilePath);

    rowToUpload["id"] = Guid.NewGuid().ToString();
    rowToUpload[options.PartitionKey] = partitionKey;

    tasks.Add(container.CreateItemAsync(rowToUpload, new PartitionKey(partitionKey)));

    // Process in batches of 10
    if (tasks.Count >= options.BatchSize)
    {
        await Task.WhenAll(tasks);
        tasks.Clear();
    }
}

// Process any remaining tasks
if (tasks.Count != 0)
    await Task.WhenAll(tasks);

Console.WriteLine("Finished...");

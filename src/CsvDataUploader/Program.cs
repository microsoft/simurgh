using CsvHelper;
using System.Globalization;
using System.IO;
using Microsoft.Azure.Cosmos;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;


var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .Build();

// Access values from the configuration
string connectionString = configuration["CosmosDB:ConnectionString"];
string databaseName = configuration["CosmosDB:DatabaseName"];
string conatainerName = configuration["CosmosDB:ContainerName"];
string filePath = configuration["CsvFilePath"];

try
{
    CosmosClient cosmosClient = new CosmosClient(connectionString);
    Container container = cosmosClient.GetContainer(databaseName, conatainerName);

    using var reader = new StreamReader(filePath);
    using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
    var records = csv.GetRecords<dynamic>().ToList();

    List<Task> tasks = new List<Task>();
    foreach (var record in records)
    {
        IDictionary<string, object> recordDict = (IDictionary<string, object>)record;
        recordDict["id"] = Guid.NewGuid().ToString();
        recordDict["partitionKey"] = "2024 1H NPS Individual Responses";

        foreach (var key in recordDict.Keys.ToList())
        {
            if (recordDict[key] is string strValue &&
                int.TryParse(strValue, out int intValue))
            {
                recordDict[key] = intValue;
            }
        }

        tasks.Add(container.CreateItemAsync(recordDict, new PartitionKey(recordDict["partitionKey"].ToString())));

        // Process in batches of 10
        if (tasks.Count >= 10)
        {
            await Task.WhenAll(tasks);
            tasks.Clear();
        }
    }

    if (tasks.Any())
    {
        await Task.WhenAll(tasks);
    }
}
catch (Exception ex)
{
    throw new Exception($"Error uploading CSV to Cosmos DB: {ex.Message}", ex);
}

Console.WriteLine("Finished...");

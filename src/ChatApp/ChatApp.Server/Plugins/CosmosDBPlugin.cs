using Microsoft.SemanticKernel;
using Microsoft.Azure.Cosmos;
using System.ComponentModel;
using Microsoft.Extensions.Options;
using ChatApp.Server.Models.Options;
using System.Dynamic;

namespace ChatApp.Server.Plugins;

public class CosmosDBPlugin
{
    private readonly CosmosClient _cosmosClient;
    private Database _database;
    private Microsoft.Azure.Cosmos.Container _container;
    private readonly string _databaseId;
    private readonly string _containerId;

    public CosmosDBPlugin(CosmosClient cosmosClient, IOptions<CosmosOptions> cosmosOptions)
    {
        _cosmosClient = cosmosClient;

        _databaseId = cosmosOptions.Value.CosmosDatabaseId;
        _containerId = cosmosOptions.Value.CosmosStructuredDataContainerId;

        var dbResponse = _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseId).Result;
        _database = dbResponse.Database;

        ContainerResponse containerResponse;
        if (cosmosOptions.Value.CosmosStructuredDataContainerRUs.HasValue)
        {
            if (cosmosOptions.Value.CosmosStructuredDataContainerRUs.Value < 400)
                throw new Exception("Cannot create a container with less than 400 RUs.");

            containerResponse = _database.CreateContainerIfNotExistsAsync(
                    _containerId,
                    cosmosOptions.Value.CosmosStructuredDataContainerPartitionKey,
                    cosmosOptions.Value.CosmosStructuredDataContainerRUs.Value).Result;
        }
        else
            containerResponse = _database.CreateContainerIfNotExistsAsync(
                _containerId,
                cosmosOptions.Value.CosmosStructuredDataContainerPartitionKey).Result;

        _container = containerResponse.Container;
    }

    [KernelFunction("cosmos_query")]
    [Description("Execute a query against the Cosmos DB")]
    [return: Description("The result of the query")]
    public async Task<List<dynamic>> ExecuteCosmosQueryAsync([Description("The query to run")] string query)
    {
        var queryDefinition = new QueryDefinition(query);

        // TODO: Double check to make sure the query is safe
        var queryResultSetIterator = _container.GetItemQueryIterator<ExpandoObject>(queryDefinition);

        List<dynamic> results = new List<dynamic>();

        while (queryResultSetIterator.HasMoreResults)
        {
            FeedResponse<ExpandoObject> currentResultSet = await queryResultSetIterator.ReadNextAsync();
            foreach (var item in currentResultSet)
            {
                results.Add(item);
            }
        }

        return results;
    }

    // function for fetching metadata names of columns
    [KernelFunction("cosmos_get_column_names")]
    [Description("Get the column names of the Cosmos DB container")]
    [return: Description("The column names of the container")]
    public async Task<List<string>> GetColumnNamesAsync()
    {
        var query = "SELECT * FROM c";
        var iterator = _container.GetItemQueryIterator<dynamic>(query, requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        var response = await iterator.ReadNextAsync();

        if (response.Count > 0)
        {
            var firstItem = response.First();
            var columnNames = new List<string>();

            foreach (var property in firstItem.GetType().GetProperties())
            {
                columnNames.Add(property.Name);
            }

            return columnNames;
        }

        return new List<string>();
    }

    // generate a query based on the user's question
    [KernelFunction("cosmos_generate_query")]
    [Description("Generate a query based on the user's question and available columns")]
    [return: Description("The generated query")]
    public string GenerateCosmosQuery(string userQuestion, List<string> availableColumns)
    {
        if (string.IsNullOrEmpty(userQuestion))
        {
            throw new ArgumentException("User question cannot be null or empty", nameof(userQuestion));
        }

        if (availableColumns == null || availableColumns.Count == 0)
        {
            throw new ArgumentException("Available columns list cannot be null or empty", nameof(availableColumns));
        }

        // TODO: Use the ChatCompletionService to extract relevant column names from the user's question
        var selectedColumns = new List<string>();
        foreach (var column in availableColumns)
        {
            if (userQuestion.Contains(column, StringComparison.OrdinalIgnoreCase))
            {
                selectedColumns.Add(column);
            }
        }

        // If no columns are found in the user's question, select all available columns
        if (selectedColumns.Count == 0)
        {
            selectedColumns = availableColumns;
        }

        // Join the column names with commas to form the SELECT clause
        var selectClause = string.Join(", ", selectedColumns.Select(name => $"c.{name}"));

        // Construct the query
        var query = $"SELECT {selectClause} FROM c";

        return query;
    }

}

using ChatApp.Server.Models.Options;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Dynamic;

namespace ChatApp.Server.Plugins;

public class AggregatesPlugin
{
    private readonly IChatCompletionService _chatCompletionService;
    private readonly string _promptDirectory;
    private readonly CosmosClient _cosmosClient;
    private Database _database;
    private Microsoft.Azure.Cosmos.Container _container;
    private readonly string _databaseId;
    private readonly string _containerId;

    public AggregatesPlugin(IChatCompletionService chatCompletionService, CosmosClient cosmosClient, IOptions<CosmosOptions> cosmosOptions)
    {
        _chatCompletionService = chatCompletionService;

        _promptDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Plugins");

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

    [KernelFunction(nameof(GetAggregateAsync))]
    [Description("Get the aggregate of the data")]
    public async Task<string> GetAggregateAsync([Description("User request for aggregate of a column")] string userQuery)
    {
        var query = "SELECT * FROM c";
        var iterator = _container.GetItemQueryIterator<JObject>(query, requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        var response = await iterator.ReadNextAsync();

        if (response.Count == 0)
            throw new Exception("Panic at the disco");

        var firstItem = response.First();
        var columnNames = string.Join(',', firstItem.Properties().Select(p => p.Name));

        /// ORIGINAL WORKING PROMPT FROM SK FUNCTION
        //var systemPrompt = """
        //    Write a syntactically correct CosmosDB SQL query to answer this question: {{$queryintent}}. 
        //    Your response should be the raw SQL query without any encapsulating markdown.
        //    Do not provide explanation or context. 
        //    When using aggregate functions, include a where clause to filter out null values or empty values.
        //    Pay attention to use only the field names that you can see in the schema description. 
        //    Only use column names from the following list:
        //    {{$columnnames}}
        //    """;

        // 

        var systemPrompt = $"""
                ### Objective ###
                Write a syntactically correct T-SQL query to answer questions.
                
                ### Format ###
                Your response should be the raw SQL query without any encapsulating markdown.
                Do not provide explanation or context. 
                
                ### Context ###
                Use only the exact column names from the following list when constructing the query. If none of the given column names can be used to answer the question, respond "Not found".
                {columnNames}

                ### Tips ###
                - Column names should be wrapped in quotes and their references need to have a table reference before them. e.g. select c['column name'] from c
                - When using aggregate functions, include a where clause to filter out null values or empty values e.g. where c['column name'] != '' AND c['column name'] != null
                """;

        var chatHistory = new ChatHistory(systemPrompt);

        chatHistory.AddUserMessage(userQuery);

        var cosmosQuery = await _chatCompletionService.GetChatMessageContentAsync(chatHistory);
        // consider logging after each step and potentially breaking out into separate private fx


        try
        {
            // TODO: Double check to make sure the query is safe
            var queryDefinition = new QueryDefinition(cosmosQuery.ToString());

            var queryResultSetIterator = _container.GetItemQueryIterator<ExpandoObject>(queryDefinition);

            var results = new List<dynamic>();

            var currentResultSet = await queryResultSetIterator.ReadNextAsync();

            var firstResult = currentResultSet.First();

            return JsonConvert.SerializeObject(firstResult);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex?.Message);
            throw;
        }
    }
}

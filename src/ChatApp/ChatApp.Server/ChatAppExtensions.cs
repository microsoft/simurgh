using Azure;
using Azure.Identity;
using ChatApp.Server.Models.Options;
using ChatApp.Server.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using System.Text.Json;

namespace ChatApp.Server;

internal static class ChatAppExtensions
{
    // this should happen before AddChatServices...
    internal static void AddOptions(this IServiceCollection services, IConfiguration config)
    {
        // FrontendSettings class needs work on json serialization before this is useful...
        services.AddOptions<FrontendOptions>().Bind(config.GetSection(nameof(FrontendOptions)));
        services.AddOptions<AzureOpenAIOptions>().Bind(config.GetSection(nameof(AzureOpenAIOptions)));
        var stuff = config.GetSection(nameof(CosmosOptions));
        services.AddOptions<CosmosOptions>().Bind(config.GetSection(nameof(CosmosOptions)));
    }

    internal static void AddChatAppServices(this IServiceCollection services, IConfiguration config)
    {
        var frontendSettings = config.GetSection(nameof(FrontendOptions)).Get<FrontendOptions>();

        var defaultAzureCreds = string.IsNullOrEmpty(config["AZURE_TENANT_ID"]) ? new DefaultAzureCredential()
            : new DefaultAzureCredential(new DefaultAzureCredentialOptions { TenantId = config["AZURE_TENANT_ID"] });

        services.AddScoped<ChatCompletionService>();
        services.AddSingleton(services => new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        services.AddScoped(sp =>
        {
            var connStr = config.GetConnectionString("SurveysDatabase") ?? throw new ArgumentNullException("ConnectionStrings:SurveysDatabase");
            return new SurveyService(connStr);
        });

        var isChatEnabled = frontendSettings?.HistoryEnabled ?? false;

        if (isChatEnabled)
        {
            services.AddSingleton(services =>
            {
                var cosmosOptions = services.GetRequiredService<IOptionsMonitor<CosmosOptions>>() ?? throw new Exception($"{nameof(CosmosOptions)} is required in settings.");

                if (!string.IsNullOrWhiteSpace(cosmosOptions.CurrentValue.ConnectionString))
                {
                    // use conn str
                    return new CosmosClientBuilder(cosmosOptions.CurrentValue.ConnectionString)
                  .WithSerializerOptions(new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase })
                  .WithConnectionModeGateway()
                  .Build();
                }

                ArgumentException.ThrowIfNullOrWhiteSpace(cosmosOptions.CurrentValue.CosmosEndpoint);

                if (!string.IsNullOrWhiteSpace(cosmosOptions.CurrentValue.CosmosKey))
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(cosmosOptions.CurrentValue.CosmosEndpoint);
                    // use key base
                    return new CosmosClientBuilder(cosmosOptions.CurrentValue.CosmosEndpoint, new AzureKeyCredential(cosmosOptions.CurrentValue.CosmosKey))
                      .WithSerializerOptions(new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase })
                      .WithConnectionModeGateway()
                      .Build();
                }

                // use managed identity
                return new CosmosClientBuilder(cosmosOptions.CurrentValue.CosmosEndpoint, defaultAzureCreds)
                    .WithSerializerOptions(new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase })
                    .WithConnectionModeGateway()
                    .Build();
            });

            services.AddScoped<CosmosConversationService>();
        }

        services.AddScoped(services =>
        {
            // Get our dependencies
            var jsonOptions = services.GetRequiredService<JsonSerializerOptions>();
            var aoaiOpts = services.GetRequiredService<IOptionsMonitor<AzureOpenAIOptions>>();
            var cosmosOptions = services.GetRequiredService<IOptions<CosmosOptions>>();

            // Create the KernelBuilder
            var builder = Kernel.CreateBuilder();

            // register dependencies with Kernel services collection
            builder.Services.AddLogging(services => services.AddConsole().SetMinimumLevel(LogLevel.Trace));
            builder.Services.AddSingleton(config);
            builder.Services.AddSingleton(cosmosOptions);
            builder.Services.AddSingleton(aoaiOpts);
            builder.Services.AddSingleton(jsonOptions);
            builder.Services.AddScoped(sp =>
            {
                var connStr = config.GetConnectionString("SurveysDatabase") ?? throw new ArgumentNullException("ConnectionStrings:SurveysDatabase");
                return new SurveyService(connStr);
            });

            // Register the CosmosClient with the Kernel services collection
            builder.Services.AddSingleton(services =>
            {
                if (!string.IsNullOrWhiteSpace(cosmosOptions.Value.ConnectionString))
                {
                    // use conn str
                    return new CosmosClientBuilder(cosmosOptions.Value.ConnectionString)
                  .WithSerializerOptions(new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase })
                  .WithConnectionModeGateway()
                  .Build();
                }

                ArgumentException.ThrowIfNullOrWhiteSpace(cosmosOptions.Value.CosmosEndpoint);

                if (!string.IsNullOrWhiteSpace(cosmosOptions.Value.CosmosKey))
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(cosmosOptions.Value.CosmosEndpoint);
                    // use key base
                    return new CosmosClientBuilder(cosmosOptions.Value.CosmosEndpoint, new AzureKeyCredential(cosmosOptions.Value.CosmosKey))
                      .WithSerializerOptions(new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase })
                      .WithConnectionModeGateway()
                      .Build();
                }

                // use managed identity
                return new CosmosClientBuilder(cosmosOptions.Value.CosmosEndpoint, defaultAzureCreds)
                    .WithSerializerOptions(new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase })
                    .WithConnectionModeGateway()
                    .Build();
            });

            // Register the AzureOpenAIChatCompletion with the Kernel services collection
            if (string.IsNullOrWhiteSpace(aoaiOpts.CurrentValue.APIKey))
            {
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                builder.AddAzureOpenAIChatCompletion(aoaiOpts.CurrentValue.ChatDeployment, aoaiOpts.CurrentValue.Endpoint, defaultAzureCreds);
                builder.AddAzureOpenAITextEmbeddingGeneration(aoaiOpts.CurrentValue.EmbeddingsDeployment, aoaiOpts.CurrentValue.Endpoint, defaultAzureCreds);
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            }
            else
            {
                // api key
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                builder.AddAzureOpenAIChatCompletion(aoaiOpts.CurrentValue.ChatDeployment, aoaiOpts.CurrentValue.Endpoint, aoaiOpts.CurrentValue.APIKey);
                builder.AddAzureOpenAITextEmbeddingGeneration(aoaiOpts.CurrentValue.EmbeddingsDeployment, aoaiOpts.CurrentValue.Endpoint, aoaiOpts.CurrentValue.APIKey);
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            }

            // Register the native plugins with the primary kernel

            // because the ChatHistory CosmosClient is in the generic service collection, we will not accidentally
            // grab it here, instead we will get the copy of for the structured data which is only added to kernel
            // sic preventing the inverse scenario.. 
            //builder.Plugins.AddFromType<CosmosDBPlugin>();

            return builder.Build();
        });
    }
}

using Azure;
using Azure.Identity;
using ChatApp.Server.Models.Options;
using ChatApp.Server.Plugins;
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
        services.AddOptions<CosmosOptions>("ChatHistory").Bind(config.GetSection("ChatHistoryCosmosOptions"));
        services.AddOptions<CosmosOptions>("StructuredData").Bind(config.GetSection("StructuredDataCosmosOptions"));
        services.AddOptions<AzureOpenAIOptions>().Bind(config.GetSection(nameof(AzureOpenAIOptions)));
    }

    internal static void AddChatAppServices(this IServiceCollection services, IConfiguration config)
    {
        var frontendSettings = config.GetSection(nameof(FrontendOptions)).Get<FrontendOptions>();

        var defaultAzureCreds = string.IsNullOrEmpty(config["AZURE_TENANT_ID"]) ? new DefaultAzureCredential()
            : new DefaultAzureCredential(new DefaultAzureCredentialOptions { TenantId = config["AZURE_TENANT_ID"] });

        services.AddScoped<ChatCompletionService>();

        services.AddSingleton(services => new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var isChatEnabled = frontendSettings?.HistoryEnabled ?? false;

        if (isChatEnabled)
        {
            services.AddScoped(services =>
            {
                var optionsSnapshot = services.GetRequiredService<IOptionsSnapshot<CosmosOptions>>() ?? throw new Exception($"{nameof(CosmosOptions)} is required in settings.");

                var chatHistoryOptions = optionsSnapshot.Get("ChatHistory");

                return string.IsNullOrEmpty(chatHistoryOptions?.CosmosKey)
                    ? new CosmosClientBuilder(chatHistoryOptions!.CosmosEndpoint, defaultAzureCreds)
                        .WithSerializerOptions(new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase })
                        .WithConnectionModeGateway()
                        .Build()
                    : new CosmosClientBuilder(chatHistoryOptions.CosmosEndpoint, new AzureKeyCredential(chatHistoryOptions.CosmosKey))
                        .WithSerializerOptions(new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase })
                        .WithConnectionModeGateway()
                        .Build();
            });

            services.AddScoped<CosmosConversationService>();
        }

        // todo: create an inject service for structured data and ensure plugins etc added to kernel dependency injection service collection

        services.AddScoped(services =>
        {
            // Get our dependencies
            var jsonOptions = services.GetRequiredService<JsonSerializerOptions>();
            var aoaiOpts = services.GetRequiredService<IOptionsMonitor<AzureOpenAIOptions>>();
            var aoaiOptions = aoaiOpts.CurrentValue;
            // todo: add watcher to change out ChatCompletionService when options change

            // Create the KernelBuilder
            var builder = Kernel.CreateBuilder();

            // register dependencies with Kernel services collection
            builder.Services.AddLogging(services => services.AddConsole().SetMinimumLevel(LogLevel.Trace));
            builder.Services.AddSingleton(config);
            builder.Services.AddSingleton(jsonOptions);

            if(string.IsNullOrWhiteSpace(aoaiOptions.APIKey))
            {
                // managed identity
                var defaultAzureCreds = string.IsNullOrEmpty(config["AZURE_TENANT_ID"]) 
                ? new DefaultAzureCredential()
                : new DefaultAzureCredential(new DefaultAzureCredentialOptions { TenantId = config["AZURE_TENANT_ID"] });

                #pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                builder.AddAzureOpenAIChatCompletion(aoaiOptions.Deployment, aoaiOptions.Endpoint, defaultAzureCreds);
                #pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            }
            else
            {
                // api key
                #pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                builder.AddAzureOpenAIChatCompletion(aoaiOptions.Deployment, aoaiOptions.Endpoint, aoaiOptions.APIKey);
                #pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            }

            // Register the native plugins with the primary kernel
            builder.Plugins.AddFromType<LightsPlugin>();

            return builder.Build();
        });
    }
}

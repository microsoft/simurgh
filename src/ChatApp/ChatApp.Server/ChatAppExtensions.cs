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
        services.AddOptions<CosmosOptions>().Bind(config.GetSection(nameof(CosmosOptions)));
        services.AddOptions<AuthOptions>().Bind(config.GetSection(nameof(AuthOptions)));
    }

    internal static void AddChatAppServices(this IServiceCollection services, IConfiguration config)
    {
        var frontendSettings = config.GetSection(nameof(FrontendOptions)).Get<FrontendOptions>();

        var defaultAzureCreds = string.IsNullOrEmpty(config["AZURE_TENANT_ID"]) ? new DefaultAzureCredential()
            : new DefaultAzureCredential(new DefaultAzureCredentialOptions { TenantId = config["AZURE_TENANT_ID"] });

        services.AddScoped<ChatCompletionService>();

        services.AddScoped(services =>
        {
            // Get our dependencies
            var httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
            var jsonOptions = services.GetRequiredService<JsonSerializerOptions>();
            AuthOptions authOptions = services.GetRequiredService<IOptions<AuthOptions>>().Value ??
                throw new Exception("AuthOptions is required in settings.");

            // Create the KernelBuilder
            var builder = Kernel.CreateBuilder();

            // register dependencies with Kernel services collection
            builder.Services.AddLogging(services => services.AddConsole().SetMinimumLevel(LogLevel.Trace));
            builder.Services.AddSingleton(config);
            builder.Services.AddSingleton(jsonOptions);
            builder.Services.AddSingleton(httpClientFactory);
            builder.Services.AddSingleton(authOptions);

            // Register the native plugins with the primary kernel
            builder.Plugins.AddFromType<LightsPlugin>();

            return builder.Build();
        });

        services.AddSingleton(services => new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var isChatEnabled = frontendSettings?.HistoryEnabled ?? false;

        if (isChatEnabled)
        {
            services.AddSingleton(services =>
            {
                var options = services.GetRequiredService<IOptions<CosmosOptions>>().Value ?? throw new Exception($"{nameof(CosmosOptions)} is rquired in settings.");

                return string.IsNullOrEmpty(options?.CosmosKey)
                    ? new CosmosClientBuilder(options!.CosmosEndpoint, defaultAzureCreds)
                        .WithSerializerOptions(new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase })
                        .WithConnectionModeGateway()
                        .Build()
                    : new CosmosClientBuilder(options.CosmosEndpoint, new AzureKeyCredential(options.CosmosKey))
                        .WithSerializerOptions(new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase })
                        .WithConnectionModeGateway()
                        .Build();
            });

            services.AddSingleton<CosmosConversationService>();
        }
    }
}

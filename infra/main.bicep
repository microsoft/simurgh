targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the the environment which is used to generate a short unique hash used in all resources.')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

@description('SQL Server administrator login')
param sqlServerAdminLogin string

@description('SQL Server administrator password')
@secure()
param sqlServerAdminPassword string

@description('SQL Database name')
param sqlDBName string = 'chatbot'

@description('Whether to use the Cosmos DB serverless option, default is true')
param isCosmosServerless bool

param cosmosDbName string = 'chatbot'

param containerId string = 'conversations'
param partitionKey string = '/userId'
param RUs int = 400

param appServicePlanName string = ''
param backendServiceName string = ''
param resourceGroupName string = ''

param openAiResourceName string = ''
param openAiResourceGroupName string = ''
param openAiResourceGroupLocation string = location
param openAiSkuName string = ''

@description('Name of the chat completion model deployment')
param chatDeploymentName string = 'chat'

@description('Name of the chat completion model')
param chatModelName string = 'gpt-4o'
param chatModelVersion string = '2024-05-13'

@description('Name of the chat completion model deployment')
param embeddingsDeploymentName string = 'embeddings'

@description('Name of the chat completion model')
param embeddingsModelName string = 'text-embedding-ada-002'
param embeddingsModelVersion string = '2'

// Used for Cosmos DB
@description('Is chat history enabled')
param isHistoryEnabled bool = true
param cosmosAccountName string = ''

@description('Id of the user or app to assign application roles')
param principalId string = ''

@description('Name of the Azure Log Analytics workspace')
param logAnalyticsName string = ''

@description('Name of the Azure Application Insights dashboard')
param applicationInsightsDashboardName string = ''

@description('Name of the Azure Application Insights resource')
param applicationInsightsName string = ''

var abbrs = loadJsonContent('abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var tags = { 'azd-env-name': environmentName }

// Organize resources in a resource group
resource resourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: !empty(resourceGroupName) ? resourceGroupName : '${abbrs.resourcesResourceGroups}${environmentName}'
  location: location
  tags: tags
}

resource openAiResourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' existing = if (!empty(openAiResourceGroupName)) {
  name: !empty(openAiResourceGroupName) ? openAiResourceGroupName : resourceGroup.name
}

// Monitor application with Azure Monitor
module monitoring 'core/monitor/monitoring.bicep' = {
  name: 'monitoring'
  scope: resourceGroup
  params: {
    location: location
    tags: tags
    includeApplicationInsights: true
    logAnalyticsName: !empty(logAnalyticsName)
      ? logAnalyticsName
      : '${abbrs.operationalInsightsWorkspaces}${resourceToken}'
    applicationInsightsName: !empty(applicationInsightsName)
      ? applicationInsightsName
      : '${abbrs.insightsComponents}${resourceToken}'
    applicationInsightsDashboardName: !empty(applicationInsightsDashboardName)
      ? applicationInsightsDashboardName
      : '${abbrs.portalDashboards}${resourceToken}'
  }
}

// The application frontend
var appServiceName = !empty(backendServiceName)
  ? backendServiceName
  : '${abbrs.webSitesAppService}backend-${resourceToken}'

var cosmosSettings = union(
  (isHistoryEnabled)
    ? {
        CosmosOptions__CosmosEndpoint: cosmos.outputs.endpoint
        CosmosOptions__CosmosDatabaseId: cosmosDbName
        CosmosOptions__CosmosChatHistoryContainerId: containerId
        CosmosOptions__CosmosChatHistoryContainerPartitionKey: partitionKey
      }
    : {},
  (isCosmosServerless)
    ? {
        CosmosOptions__CosmosChatHistoryContainerRUs: RUs
      }
    : {}
)

var backendAppSettings = union(cosmosSettings, {
  // frontend settings
  FrontendSettings__auth_enabled: 'false'
  FrontendSettings__feedback_enabled: 'false'
  FrontendSettings__ui__title: 'Simurgh Chatbot'
  FrontendSettings__ui__chat_description: 'This chatbot is configured to answer your questions.'
  FrontendSettings__ui__show_share_button: true
  FrontendSettings__sanitize_answer: false
  FrontendSettings__history_enabled: isHistoryEnabled
  // Azure Open AI settings
  AzureOpenAI__Endpoint: openAi.outputs.endpoint
  AzureOpenAI__Deployment: chatDeploymentName
})

module backend 'app/backend.bicep' = {
  name: 'web'
  scope: resourceGroup
  params: {
    location: location
    tags: tags
    appServicePlanName: !empty(appServicePlanName)
      ? appServicePlanName
      : '${abbrs.webServerFarms}backend-${resourceToken}'
    appServiceName: appServiceName
    appSettings: backendAppSettings
  }
}

module openAi 'core/ai/cognitiveservices.bicep' = {
  name: 'openai'
  scope: openAiResourceGroup
  params: {
    name: !empty(openAiResourceName) ? openAiResourceName : '${abbrs.cognitiveServicesAccounts}${resourceToken}'
    location: openAiResourceGroupLocation
    tags: tags
    sku: {
      name: !empty(openAiSkuName) ? openAiSkuName : 'S0'
    }
    deployments: [
      {
        name: chatDeploymentName
        model: {
          format: 'OpenAI'
          name: chatModelName
          version: chatModelVersion
        }
        capacity: 10
      }
      {
        name: embeddingsDeploymentName
        model: {
          format: 'OpenAI'
          name: embeddingsModelName
          version: embeddingsModelVersion
        }
        capacity: 10
      }
    ]
  }
}

module languageService 'core/ai/languageservice.bicep' = {
  name: 'language-service'
  scope: resourceGroup
  params: {
    languageServiceName: '${abbrs.cognitiveServicesTextAnalytics}${resourceToken}'
    location: location
    skuName: 'S'
    tags: tags
  }
}

// The SQL chat history database
module sql 'core/database/sqldb/sqldb-server.bicep' = {
  name: 'sql'
  scope: resourceGroup
  params: {
    serverName: 'sql-${resourceToken}'
    sqlDBName: sqlDBName
    location: location
    administratorLogin: sqlServerAdminLogin
    administratorLoginPassword: sqlServerAdminPassword
    principalId: principalId
  }
}

// The chat history database
module cosmos './app/db.bicep' = if (isHistoryEnabled) {
  name: 'cosmos'
  scope: resourceGroup
  params: {
    accountName: !empty(cosmosAccountName) ? cosmosAccountName : '${abbrs.documentDBDatabaseAccounts}${resourceToken}'
    location: location
    tags: tags
    databaseName: cosmosDbName
    isCosmosServerless: isCosmosServerless
    containers: [
      {
        id: containerId
        name: containerId
        partitionKey: partitionKey
        rus: RUs
      }
    ]
  }
}

module permissions './app/permissions.bicep' = {
  name: 'permissions'
  scope: resourceGroup
  params: {
    isHistoryEnabled: isHistoryEnabled
    cosmosAccountName: cosmos.outputs.accountName
    userPrincipalId: principalId
    appPrincipalId: backend.outputs.identityPrincipalId
  }
}

output AzureOpenAIOptions__Endpoint string = openAi.outputs.endpoint
output AzureOpenAIOptions__ChatDeployment string = chatDeploymentName
output AzureOpenAIOptions__EmbeddingsDeployment string = embeddingsDeploymentName
output AZURE_TENANT_ID string = subscription().tenantId

// cosmos db
output CosmosOptions__CosmosEndpoint string = cosmos.outputs.endpoint
output CosmosOptions__CosmosDatabaseId string = cosmosDbName
// chat history container
output CosmosOptions__ContainerId string = containerId
output CosmosOptions__PartitionKey string = partitionKey
output CosmosOptions__ContainerRUs int = RUs

// can't output connection strings from submodules and subscription scope prevents referencing existing resources so we construct connection string
output ConnectionStrings__SurveysDatabase string = 'Server=tcp:${sql.outputs.sqlServerName}${environment().suffixes.sqlServerHostname},1433;Initial Catalog=${sql.outputs.sqlDBName};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity'

output APPLICATIONINSIGHTS_CONNECTION_STRING string = monitoring.outputs.applicationInsightsConnectionString

// once this feature is better developed we can remove the feature flag
output AzureOpenAIOptions__IncludeVectorSearchPlugin bool = true

// not needed for chat app server but will be used for the console app
output TextAnalyticsServiceOptions__Endpoint string = languageService.outputs.endpoint

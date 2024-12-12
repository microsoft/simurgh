targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the the environment which is used to generate a short unique hash used in all resources.')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

@description('Whether to use the Cosmos DB serverless option, default is true')
param isCosmosServerless bool

param cosmosDbName string = 'chatbot'

param structuredDataContainerId string = 'documents'
param structuredDataPartitionKey string = '/filename'
param structuredDataRUs int = 400

param chatHistoryContainerId string = 'conversations'
param chatHistoryPartitionKey string = '/userId'
param chatHistoryRUs int = 400

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
        CosmosOptions__CosmosStructuredDataContainerId: structuredDataContainerId
        CosmosOptions__CosmosStructuredDataContainerPartitionKey: structuredDataPartitionKey

        CosmosOptions__CosmosChatHistoryContainerId: chatHistoryContainerId
        CosmosOptions__CosmosChatHistoryContainerPartitionKey: chatHistoryPartitionKey
      }
    : {},
  (isCosmosServerless)
    ? {
        CosmosOptions__CosmosStructuredDataContainerRUs: structuredDataRUs
        CosmosOptions__CosmosChatHistoryContainerRUs: chatHistoryRUs
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
        capacity: 30
      }
    ]
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
        id: chatHistoryContainerId
        name: chatHistoryContainerId
        partitionKey: chatHistoryPartitionKey
        rus: chatHistoryRUs
      }
      {
        id: structuredDataContainerId
        name: structuredDataContainerId
        partitionKey: structuredDataPartitionKey
        rus: structuredDataRUs
      }
    ]
  }
}

module cosmosRoleAssign './app/db-rbac.bicep' = if (isHistoryEnabled) {
  name: 'cosmos-role-assign'
  scope: resourceGroup
  params: {
    accountName: cosmos.outputs.accountName
    principalIds: [principalId, backend.outputs.identityPrincipalId]
  }
}

module openAiRoleUser 'core/security/role.bicep' = {
  scope: openAiResourceGroup
  name: 'openai-role-user'
  params: {
    principalId: principalId
    roleDefinitionId: '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
    principalType: 'User'
  }
}

module aiDeveloperRoleUser 'core/security/role.bicep' = {
  scope: openAiResourceGroup
  name: 'ai-developer-role-user'
  params: {
    principalId: principalId
    roleDefinitionId: '64702f94-c441-49e6-a78b-ef80e0188fee'
    principalType: 'User'
  }
}

// SYSTEM IDENTITIES

module openAiRoleBackend 'core/security/role.bicep' = {
  scope: openAiResourceGroup
  name: 'openai-role-backend'
  params: {
    principalId: backend.outputs.identityPrincipalId
    roleDefinitionId: '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
    principalType: 'ServicePrincipal'
  }
}

module aiDeveloperRoleBackend 'core/security/role.bicep' = {
  scope: openAiResourceGroup
  name: 'ai-developer-role-backend'
  params: {
    principalId: backend.outputs.identityPrincipalId
    roleDefinitionId: '64702f94-c441-49e6-a78b-ef80e0188fee'
    principalType: 'ServicePrincipal'
  }
}

output AzureOpenAIOptions__Endpoint string = openAi.outputs.endpoint
output AzureOpenAIOptions__Deployment string = chatDeploymentName
output AZURE_TENANT_ID string = subscription().tenantId

// cosmos db
output CosmosOptions__CosmosEndpoint string = cosmos.outputs.endpoint
output CosmosOptions__CosmosDatabaseId string = cosmosDbName
// chat history container
output CosmosOptions__CosmosChatHistoryContainerId string = chatHistoryContainerId
output CosmosOptions__CosmosChatHistoryContainerPartitionKey string = chatHistoryPartitionKey
output CosmosOptions__CosmosChatHistoryContainerRUs int = chatHistoryRUs
// structured data container
output CosmosOptions__CosmosStructuredDataContainerId string = structuredDataContainerId
output CosmosOptions__CosmosStructuredDataContainerPartitionKey string = structuredDataPartitionKey
output CosmosOptions__CosmosStructuredDataContainerRUs int = structuredDataRUs

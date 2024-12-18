@description('Name of the Azure Language Service instance.')
param languageServiceName string = 'languageService-${uniqueString(resourceGroup().id)}'

@description('The kind of Azure Cognitive Service.')
param kind string = 'TextAnalytics'

@description('Location for all resources.')
param location string = resourceGroup().location

param tags object = {}
param customSubDomainName string = languageServiceName
param publicNetworkAccess string = 'Enabled'

@description('The pricing tier of the Azure Language Service.')
@allowed([ 'F0', 'S' ])
param skuName string = 'F0'

resource languagesrv 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: languageServiceName
  location: location
  kind: kind
  sku: {
    name: skuName
  }
  properties: {
    customSubDomainName: customSubDomainName
    publicNetworkAccess: publicNetworkAccess
  }
  tags: tags
}

output endpoint string = languagesrv.properties.endpoint

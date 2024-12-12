param accountName string
param location string = resourceGroup().location
param tags object = {}

/*

  [
    {
      id: '...'
      name: '...'
      partitionKey: '/id'
      rus: 400
    }
    ...
  ]

*/

param isCosmosServerless bool = true
param databaseName string = 'chatbot'
param containers array = [
  {
    id: 'conversations'
    name: 'conversations'
    partitionKey: '/userId'
    rus: 400
  }
  {
    id: 'documents'
    name: 'documents'
    partitionKey: '/filename'
    rus: 400
  }
]
param principalIds array = []

module cosmos '../core/database/cosmos/sql/cosmos-sql-db.bicep' = {
  name: 'cosmos-sql'
  params: {
    accountName: accountName
    isCosmosServerless: isCosmosServerless
    databaseName: databaseName
    containers: containers
    location: location
    tags: tags
    principalIds: principalIds
  }
}

output accountName string = cosmos.outputs.accountName
output endpoint string = cosmos.outputs.endpoint

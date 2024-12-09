metadata description = 'Creates an Azure Cosmos DB for NoSQL account with a database.'
param accountName string
param location string = resourceGroup().location
param tags object = {}

param principalIds array = []

/*

  [
    {
      name: '...'
      kind: 'GlobalDocumentDB'
      containers: [
        {
          name: '...'
          partitionKey: '/id'
        }
      ]
    }
    ...
]

*/
param databases array = []

module cosmos 'cosmos-sql-account.bicep' = {
  name: 'cosmos-sql-account'
  params: {
    name: accountName
    location: location
    tags: tags
  }
}

module database 'cosmos-single-sql-db.bicep' = [
  for database in databases: {
    name: 'cosmos-sql-db-${database.name}'
    params: {
      accountName: accountName
      databaseName: database.name
      containers: database.containers
    }
    dependsOn: [
      cosmos
    ]
  }
]

module roleDefinition 'cosmos-sql-role-def.bicep' = {
  name: 'cosmos-sql-role-definition'
  params: {
    accountName: accountName
  }
  dependsOn: [
    cosmos
    database
  ]
}

// We need batchSize(1) here because sql role assignments have to be done sequentially
@batchSize(1)
module userRole 'cosmos-sql-role-assign.bicep' = [
  for principalId in principalIds: if (!empty(principalId)) {
    name: 'cosmos-sql-user-role-${uniqueString(principalId)}'
    params: {
      accountName: accountName
      roleDefinitionId: roleDefinition.outputs.id
      principalId: principalId
    }
    dependsOn: [
      cosmos
      database
    ]
  }
]

output accountId string = cosmos.outputs.id
output accountName string = cosmos.outputs.name
output endpoint string = cosmos.outputs.endpoint
output roleDefinitionId string = roleDefinition.outputs.id

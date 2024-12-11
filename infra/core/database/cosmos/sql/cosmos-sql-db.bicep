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
param isCosmosServerless bool = true
param containers array = []
param databaseName string = 'chatbot'

module cosmos 'cosmos-sql-account.bicep' = {
  name: 'cosmos-sql-account'
  params: {
    name: accountName
    location: location
    tags: tags
    isCosmosServerless: isCosmosServerless
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2022-05-15' = {
  name: '${accountName}/${databaseName}'
  properties: {
    resource: { id: databaseName }
  }

  resource list 'containers' = [
    for container in containers: {
      name: container.name
      properties: {
        options: isCosmosServerless
          ? {}
          : {
              throughput: container.rus
            }
        resource: {
          id: container.id
          partitionKey: { paths: [container.partitionKey] }
        }
      }
    }
  ]

  dependsOn: [
    cosmos
  ]
}

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

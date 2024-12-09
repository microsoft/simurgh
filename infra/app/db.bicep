param accountName string
param location string = resourceGroup().location
param tags object = {}

/*

  [
    {
      name: '...'
      kind: 'GlobalDocumentDB'
      containers: [
        {
          id: '...'
          name: '...'
          partitionKey: '/id'
        }
      ]
    }
    ...
  ]

*/
param databases array = [
  {
    name: 'db_conversation_history'
    kind: 'GlobalDocumentDB'
    containers: [
      {
        id: 'conversations'
        name: 'conversations'
        partitionKey: '/userId'
      }
    ]
  }
  {
    name: 'db_structured_data'
    kind: 'GlobalDocumentDB'
    containers: [
      {
        id: 'documents'
        name: 'documents'
        partitionKey: '/id'
      }
    ]
  }
]
param principalIds array = []

module cosmos '../core/database/cosmos/sql/cosmos-sql-db.bicep' = {
  name: 'cosmos-sql'
  params: {
    accountName: accountName
    databases: databases
    location: location
    tags: tags
    principalIds: principalIds
  }
}


output accountName string = cosmos.outputs.accountName
output endpoint string = cosmos.outputs.endpoint

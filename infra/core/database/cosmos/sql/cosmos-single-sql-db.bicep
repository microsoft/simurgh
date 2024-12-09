param accountName string
param databaseName string

/*

  [
    {
      id: '...'
      name: '...'
      partitionKey: '/id'
    }
  ]

*/
param containers array = []

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2022-05-15' = {
  name: '${accountName}/${databaseName}'
  properties: {
    resource: { id: databaseName }
  }

  resource list 'containers' = [
    for container in containers: {
      name: container.name
      properties: {
        resource: {
          id: container.id
          partitionKey: { paths: [container.partitionKey] }
        }
        options: {}
      }
    }
  ]
}

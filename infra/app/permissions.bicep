param isHistoryEnabled bool
param cosmosAccountName string
param userPrincipalId string
param appPrincipalId string

var roles = [
  {
    name: 'Cognitive Services Open AI User'
    abbrev: 'cog-services-openai-user'
    id: '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
  }
  {
    name: 'Azure AI Developer'
    abbrev: 'az-ai-developer'
    id: '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
  }
  {
    name: 'SQL DB Contributor'
    abbrev: 'sql-db-contributor'
    id: '9b7fa17d-e63e-47b0-bb0a-15c516ac86ec'
  }
  {
    name: 'Cognitive Services Language Writer'
    abbrev: 'cog-services-lang-writer'
    id: 'f2310ca1-dc64-4889-bb49-c8e0fa3d47a8'
  }
]

module userRole '../core/security/role.bicep' = [
  for role in roles: {
    scope: resourceGroup()
    name: 'user-role-${role.abbrev}'
    params: {
      principalId: userPrincipalId
      roleDefinitionId: role.id
      principalType: 'User'
    }
  }
]

// technically the app doesn't need the language writer role
// but this is just an accelerator and we can keep simple loop here
module appRole '../core/security/role.bicep' = [
  for role in roles: {
    scope: resourceGroup()
    name: 'app-role-${role.abbrev}'
    params: {
      principalId: appPrincipalId
      roleDefinitionId: role.id
      principalType: 'ServicePrincipal'
    }
  }
]

module cosmosRoleAssign './db-rbac.bicep' = if (isHistoryEnabled) {
  name: 'cosmos-role-assign'
  scope: resourceGroup()
  params: {
    accountName: cosmosAccountName
    principalIds: [userPrincipalId, appPrincipalId]
  }
}

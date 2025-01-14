@description('The name of the SQL logical server.')
param serverName string = uniqueString('sql', resourceGroup().id)

@description('The name of the SQL Database.')
param sqlDBName string = 'SampleDB'

@description('Location for all resources.')
param location string = resourceGroup().location

@description('The administrator username of the SQL logical server.')
param administratorLogin string

@description('The administrator password of the SQL logical server.')
@secure()
param administratorLoginPassword string

@description('Allow Azure services to access the server.')
param allowAzureServices bool = true

@description('The principal ID of the user to be added as an administrator.')
@secure()
param principalId string

// resource deploymentScript 'Microsoft.Resources/deploymentScripts@2020-10-01' = {
//   name: 'runPowerShellScript'
//   location: resourceGroup().location
//   identity: {
//     type: 'UserAssigned'
//     userAssignedIdentities: {
//       '<your-managed-identity-id>': {}
//     }
//   }
//   kind: 'AzurePowerShell'
//   properties: {
//     azPowerShellVersion: '5.0'
//     scriptContent: '''
//       param (
//         [string]$PrincipalId
//       )

//       # Connect to Azure AD
//       Connect-AzureAD

//       # Get the user object using Principal ID
//       $user = Get-AzureADUser -ObjectId $PrincipalId

//       # Retrieve SID, Tenant ID, and login
//       $sid = $user.ObjectId
//       $tenantId = (Get-AzTenant).TenantId
//       $login = $user.UserPrincipalName

//       # Output the results
//       $results = @{
//         SID = $sid
//         TenantID = $tenantId
//         Login = $login
//       }
//       $results | ConvertTo-Json
//     '''
//     arguments: '-PrincipalId "${principalId}"'
//     timeout: 'PT30M'
//     cleanupPreference: 'OnSuccess'
//     retentionInterval: 'P1D'
//   }
// }

// using both sql server admin and entra admin
resource sqlServer 'Microsoft.Sql/servers@2022-05-01-preview' = {
  name: serverName
  location: location
  properties: {
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorLoginPassword
  }
}

resource allowAzureServicesRule 'Microsoft.Sql/servers/firewallRules@2021-02-01-preview' = if (allowAzureServices) {
  name: 'AllowAllWindowsAzureIps'
  parent: sqlServer
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDB 'Microsoft.Sql/servers/databases@2022-05-01-preview' = {
  parent: sqlServer
  name: sqlDBName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
}

output sqlServerName string = sqlServer.name
output sqlDBName string = sqlDB.name
output sqlDBId string = sqlDB.id

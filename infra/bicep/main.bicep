targetScope = 'resourceGroup'

@description('The environment name (dev, test, prod)')
param environmentName string = 'dev'

@description('The Azure region for resources')
param location string = resourceGroup().location

@description('Unique suffix for resource names')
param uniqueSuffix string = uniqueString(resourceGroup().id)

@description('GitHub repository owner (organization or username)')
param githubRepositoryOwner string

@description('GitHub repository name')
param githubRepositoryName string

@description('GitHub branch name for the deployments')
param githubBranch string = 'main'

@description('Container image for web app. If empty, a default public image will be used')
param containerImageName string = ''

var appName = 'docvault'
var uniqueAppName = '${appName}${uniqueSuffix}'
var tags = {
  application: 'Document Vault'
  environment: environmentName
}

// Define resource names upfront so they can be used in listKeys functions
var cosmosAccountName = 'cosmos-${uniqueAppName}'
var storageAccountName = 'st${uniqueAppName}'

// Container Registry for Docker images
module containerRegistry 'modules/containerregistry.bicep' = {
  name: 'containerRegistryDeploy'
  params: {
    registryName: 'acr${uniqueAppName}'
    location: location
    tags: tags
  }
}

module storageAccount 'modules/storage.bicep' = {
  name: 'storageAccountDeploy'
  params: {
    storageAccountName: storageAccountName
    location: location
    tags: tags
  }
}

module cosmosDb 'modules/cosmosdb.bicep' = {
  name: 'cosmosDbDeploy'
  params: {
    cosmosAccountName: cosmosAccountName
    location: location
    tags: tags
  }
}

module functionApp 'modules/function.bicep' = {
  name: 'functionAppDeploy'
  params: {
    functionAppName: 'func-${uniqueAppName}'
    storageAccountName: storageAccount.outputs.name
    storageAccountId: storageAccount.outputs.id
    appInsightsInstrumentationKey: appInsights.outputs.instrumentationKey
    cosmosDbEndpoint: cosmosDb.outputs.endpoint
    cosmosDbKey: listKeys(resourceId('Microsoft.DocumentDB/databaseAccounts', cosmosAccountName), '2022-08-15').primaryMasterKey
    cdnEndpointUrl: cdn.outputs.cdnEndpointUrl
    location: location
    tags: tags
  }
}

module containerApp 'modules/containerapp.bicep' = {
  name: 'containerAppDeploy'
  params: {
    containerAppName: 'app-${uniqueAppName}'
    containerAppEnvName: 'env-${uniqueAppName}'
    location: location
    containerRegistryLoginServer: containerRegistry.outputs.loginServer
    containerRegistryUsername: containerRegistry.outputs.adminUsername
    containerRegistryPassword: containerRegistry.outputs.adminPassword
    containerImageName: containerImageName
    appInsightsInstrumentationKey: appInsights.outputs.instrumentationKey
    cosmosDbEndpoint: cosmosDb.outputs.endpoint
    cosmosDbKey: listKeys(resourceId('Microsoft.DocumentDB/databaseAccounts', cosmosAccountName), '2022-08-15').primaryMasterKey
    storageAccountName: storageAccount.outputs.name
    storageAccountKey: listKeys(resourceId('Microsoft.Storage/storageAccounts', storageAccountName), '2022-09-01').keys[0].value
    functionAppHostName: functionApp.outputs.hostName
    tags: tags
  }
}

module cdn 'modules/cdn.bicep' = {
  name: 'cdnDeploy'
  params: {
    cdnProfileName: 'cdn-${uniqueAppName}'
    cdnEndpointName: 'endpoint-${uniqueAppName}'
    storageAccountHostName: storageAccount.outputs.primaryBlobEndpoint
    location: location
    tags: tags
  }
}

module appInsights 'modules/appinsights.bicep' = {
  name: 'appInsightsDeploy'
  params: {
    appInsightsName: 'ai-${uniqueAppName}'
    location: location
    tags: tags
  }
}

output storageAccountName string = storageAccount.outputs.name
output cosmosDbAccountName string = cosmosDb.outputs.name
output functionAppName string = functionApp.outputs.name
output containerAppName string = containerApp.outputs.name
output cdnEndpointUrl string = cdn.outputs.cdnEndpointUrl
output appInsightsName string = appInsights.outputs.name
output containerRegistryName string = containerRegistry.outputs.name
output containerRegistryLoginServer string = containerRegistry.outputs.loginServer

module functionAppFederatedIdentity 'modules/github-federated-identity.bicep' = {
  name: 'functionAppFederatedIdentityDeploy'
  params: {
    repositoryOwner: githubRepositoryOwner
    repositoryName: githubRepositoryName
    entityType: 'branch'
    entityName: githubBranch
    identitySuffix: 'function'
  }
}

module webAppFederatedIdentity 'modules/github-federated-identity.bicep' = {
  name: 'webAppFederatedIdentityDeploy'
  params: {
    repositoryOwner: githubRepositoryOwner
    repositoryName: githubRepositoryName
    entityType: 'branch'
    entityName: githubBranch
    identitySuffix: 'web'
  }
}

module functionAppRoleAssignment 'modules/role-assignment.bicep' = {
  name: 'functionAppRoleAssignmentDeploy'
  params: {
    principalId: functionAppFederatedIdentity.outputs.federatedIdentityPrincipalId
    resourceId: functionApp.outputs.id
    roleDefinitionId: 'b24988ac-6180-42a0-ab88-20f7382dd24c' // Contributor role
  }
}

module acrPushRoleAssignment 'modules/role-assignment.bicep' = {
  name: 'acrPushRoleAssignmentDeploy'
  params: {
    principalId: webAppFederatedIdentity.outputs.federatedIdentityPrincipalId
    resourceId: containerRegistry.outputs.id
    roleDefinitionId: '8311e382-0749-4cb8-b61a-304f252e45ec' // AcrPush role
  }
}

module containerAppRoleAssignment 'modules/role-assignment.bicep' = {
  name: 'containerAppRoleAssignmentDeploy'
  params: {
    principalId: webAppFederatedIdentity.outputs.federatedIdentityPrincipalId
    resourceId: containerApp.outputs.id
    roleDefinitionId: 'b24988ac-6180-42a0-ab88-20f7382dd24c' // Contributor role
  }
}

output functionAppFederatedIdentityClientId string = functionAppFederatedIdentity.outputs.federatedIdentityClientId
output webAppFederatedIdentityClientId string = webAppFederatedIdentity.outputs.federatedIdentityClientId

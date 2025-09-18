@description('App Service name')
param appServiceName string

@description('Application Insights instrumentation key')
param appInsightsInstrumentationKey string

@description('Cosmos DB endpoint')
param cosmosDbEndpoint string

@description('Cosmos DB key')
param cosmosDbKey string

@description('Storage account name')
param storageAccountName string

@description('Storage account key')
@secure()
param storageAccountKey string

@description('Function App host name')
param functionAppHostName string

@description('Location for all resources')
param location string

@description('Tags for the resources')
param tags object


resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: '${appServiceName}-plan'
  location: location
  tags: tags
  sku: {
    name: 'P1V3'
    tier: 'PremiumV3'
  }
  properties: {
    reserved: true
  }
}

resource appService 'Microsoft.Web/sites@2022-03-01' = {
  name: appServiceName
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|9.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      alwaysOn: true
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsightsInstrumentationKey
        }
        {
          name: 'CosmosDb__Endpoint'
          value: cosmosDbEndpoint
        }
        {
          name: 'CosmosDb__Key'
          value: cosmosDbKey
        }
        {
          name: 'CosmosDb__DatabaseName'
          value: 'DocumentVaultDB'
        }
        {
          name: 'CosmosDb__LinksContainerName'
          value: 'Links'
        }
        {
          name: 'CosmosDb__DocumentsContainerName'
          value: 'Documents'
        }
        {
          name: 'BlobStorage__ConnectionString'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccountKey}'
        }
        {
          name: 'BlobStorage__ContainerName'
          value: 'documents'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: 'InstrumentationKey=${appInsightsInstrumentationKey}'
        }
        {
          name: 'ApiSettings__FunctionBaseUrl'
          value: 'https://${functionAppHostName}'
        }
      ]
    }
    httpsOnly: true
  }
}

// Create test slot for the app service
resource testSlot 'Microsoft.Web/sites/slots@2022-03-01' = {
  name: 'test'
  parent: appService
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|9.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      alwaysOn: true
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Development'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsightsInstrumentationKey
        }
        {
          name: 'CosmosDb__Endpoint'
          value: cosmosDbEndpoint
        }
        {
          name: 'CosmosDb__Key'
          value: cosmosDbKey
        }
        {
          name: 'CosmosDb__DatabaseName'
          value: 'DocumentVaultDB'
        }
        {
          name: 'CosmosDb__LinksContainerName'
          value: 'Links'
        }
        {
          name: 'CosmosDb__DocumentsContainerName'
          value: 'Documents'
        }
        {
          name: 'BlobStorage__ConnectionString'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccountKey}'
        }
        {
          name: 'BlobStorage__ContainerName'
          value: 'documents'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: 'InstrumentationKey=${appInsightsInstrumentationKey}'
        }
        {
          name: 'ApiSettings__FunctionBaseUrl'
          value: 'https://${functionAppHostName}'
        }
      ]
    }
    httpsOnly: true
  }
}

// Create staging slot for the app service
resource stagingSlot 'Microsoft.Web/sites/slots@2022-03-01' = {
  name: 'staging'
  parent: appService
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|9.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      alwaysOn: true
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Staging'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsightsInstrumentationKey
        }
        {
          name: 'CosmosDb__Endpoint'
          value: cosmosDbEndpoint
        }
        {
          name: 'CosmosDb__Key'
          value: cosmosDbKey
        }
        {
          name: 'CosmosDb__DatabaseName'
          value: 'DocumentVaultDB'
        }
        {
          name: 'CosmosDb__LinksContainerName'
          value: 'Links'
        }
        {
          name: 'CosmosDb__DocumentsContainerName'
          value: 'Documents'
        }
        {
          name: 'BlobStorage__ConnectionString'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccountKey}'
        }
        {
          name: 'BlobStorage__ContainerName'
          value: 'documents'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: 'InstrumentationKey=${appInsightsInstrumentationKey}'
        }
        {
          name: 'ApiSettings__FunctionBaseUrl'
          value: 'https://${functionAppHostName}'
        }
      ]
    }
    httpsOnly: true
  }
}

resource storageAccountResource 'Microsoft.Storage/storageAccounts@2022-09-01' existing = {
  name: storageAccountName
}

// Grant app service access to storage account
resource appServiceStorageRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(resourceGroup().id, appService.name, 'StorageBlobDataContributor')
  scope: storageAccountResource
  properties: {
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe') // Storage Blob Data Contributor
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Grant test slot access to storage account
resource testSlotStorageRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(resourceGroup().id, testSlot.name, 'StorageBlobDataContributor')
  scope: storageAccountResource
  properties: {
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe') // Storage Blob Data Contributor
    principalId: testSlot.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Grant staging slot access to storage account
resource stagingSlotStorageRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(resourceGroup().id, stagingSlot.name, 'StorageBlobDataContributor')
  scope: storageAccountResource
  properties: {
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe') // Storage Blob Data Contributor
    principalId: stagingSlot.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output id string = appService.id
output name string = appService.name
output hostName string = appService.properties.defaultHostName
output testSlotName string = testSlot.name
output testSlotHostName string = testSlot.properties.defaultHostName
output stagingSlotName string = stagingSlot.name
output stagingSlotHostName string = stagingSlot.properties.defaultHostName
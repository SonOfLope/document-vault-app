@description('Function App name')
param functionAppName string

@description('Storage account name for the Function App')
param storageAccountName string

@description('Storage account resource ID')
param storageAccountId string

@description('Application Insights instrumentation key')
param appInsightsInstrumentationKey string

@description('Cosmos DB endpoint')
param cosmosDbEndpoint string

@description('Cosmos DB key')
param cosmosDbKey string

@description('CDN endpoint URL')
param cdnEndpointUrl string

@description('Location for all resources')
param location string

@description('Tags for the resources')
param tags object

resource hostingPlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: '${functionAppName}-plan'
  location: location
  tags: tags
  sku: {
    name: 'EP1' // Must use a SKU that supports Linux and dotnet-isolated
    tier: 'ElasticPremium'
  }
  properties: {
    reserved: true
    maximumElasticWorkerCount: 20
  }
}

resource functionApp 'Microsoft.Web/sites@2022-03-01' = {
  name: functionAppName
  location: location
  tags: tags
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: hostingPlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|9.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      cors: {
        allowedOrigins: [
          '*'
        ]
      }
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};EndpointSuffix=${environment().suffixes.storage};AccountKey=${listKeys(storageAccountId, '2022-09-01').keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};EndpointSuffix=${environment().suffixes.storage};AccountKey=${listKeys(storageAccountId, '2022-09-01').keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(functionAppName)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
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
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};EndpointSuffix=${environment().suffixes.storage};AccountKey=${listKeys(storageAccountId, '2022-09-01').keys[0].value}'
        }
        {
          name: 'BlobStorage__ContainerName'
          value: 'documents'
        }
        {
          name: 'CdnEndpoint'
          value: cdnEndpointUrl
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: 'InstrumentationKey=${appInsightsInstrumentationKey}'
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~2'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'AzureFunctionsJobHost__logging__logLevel__default'
          value: 'Information'
        }
      ]
    }
    httpsOnly: true
  }
}

resource storageAccountResource 'Microsoft.Storage/storageAccounts@2022-09-01' existing = {
  name: storageAccountName
}

// identity access to storage account
resource functionAppStorageRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(resourceGroup().id, functionApp.name, 'StorageBlobDataContributor')
  scope: storageAccountResource
  properties: {
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe') // Storage Blob Data Contributor
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output id string = functionApp.id
output name string = functionApp.name
output hostName string = functionApp.properties.defaultHostName

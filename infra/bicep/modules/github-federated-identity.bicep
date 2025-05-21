@description('The repository owner, e.g., "contoso"')
param repositoryOwner string

@description('The repository name, e.g., "document-vault-app"')
param repositoryName string

@description('The GitHub entity that can use the federated identity. Options: environment, branch, pull_request, tag')
@allowed([
  'environment'
  'branch'
  'pull_request'
  'tag'
])
param entityType string = 'branch'

@description('The name of the GitHub entity that can use the federated credential')
param entityName string = 'main'

var federatedCredentialName = '${repositoryName}-${entityType}-${entityName}'

resource federatedIdentityCredentials 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  name: federatedCredentialName
  parent: managedIdentity
  properties: {
    audiences: [
      'api://AzureADTokenExchange'
    ]
    issuer: 'https://token.actions.githubusercontent.com'
    subject: entityType == 'environment' 
      ? 'repo:${repositoryOwner}/${repositoryName}:environment:${entityName}'
      : entityType == 'pull_request'
        ? 'repo:${repositoryOwner}/${repositoryName}:pull_request'
        : entityType == 'tag'
          ? 'repo:${repositoryOwner}/${repositoryName}:ref:refs/tags/${entityName}'
          : 'repo:${repositoryOwner}/${repositoryName}:ref:refs/heads/${entityName}'
  }
}

@description('Optional suffix to make the identity name unique')
param identitySuffix string = 'default'

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${replace(repositoryName, '-', '')}${entityName}-${identitySuffix}'
  location: resourceGroup().location
}

output federatedIdentityName string = managedIdentity.name
output federatedIdentityClientId string = managedIdentity.properties.clientId
output federatedIdentityPrincipalId string = managedIdentity.properties.principalId

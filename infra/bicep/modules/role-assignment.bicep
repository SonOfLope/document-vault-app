@description('Principal ID who gets the role assignment')
param principalId string

@description('The resource ID for the resource getting the role assignment')
param resourceId string

@description('The role definition ID to assign')
param roleDefinitionId string

@description('A unique identifier for the role assignment')
param nameGuid string = guid(principalId, resourceId, roleDefinitionId)

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: nameGuid
  scope: resourceGroup()
  properties: {
    principalId: principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionId)
    principalType: 'ServicePrincipal'
  }
}

output id string = roleAssignment.id
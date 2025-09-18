@description('CDN profile name')
param cdnProfileName string

@description('CDN endpoint name')
param cdnEndpointName string

@description('Hostname for the storage account')
param storageAccountHostName string


@description('Tags for the resources')
param tags object

resource cdnProfile 'Microsoft.Cdn/profiles@2022-05-01-preview' = {
  name: cdnProfileName
  location: 'global'
  tags: tags
  sku: {
    name: 'Standard_AzureFrontDoor'
  }
}

resource originGroup 'Microsoft.Cdn/profiles/originGroups@2022-05-01-preview' = {
  parent: cdnProfile
  name: '${cdnEndpointName}-origin-group'
  properties: {
    loadBalancingSettings: {
      sampleSize: 4
      successfulSamplesRequired: 3
      additionalLatencyInMilliseconds: 50
    }
    healthProbeSettings: {
      probePath: '/'
      probeRequestType: 'HEAD'
      probeProtocol: 'Https'
      probeIntervalInSeconds: 100
    }
  }
}

resource origin 'Microsoft.Cdn/profiles/originGroups/origins@2022-05-01-preview' = {
  parent: originGroup
  name: replace(replace(replace(storageAccountHostName, 'https://', ''), '.blob.${environment().suffixes.storage}/', ''), '.', '-')
  properties: {
    hostName: replace(replace(storageAccountHostName, 'https://', ''), '/', '')
    httpPort: 80
    httpsPort: 443
    originHostHeader: replace(replace(storageAccountHostName, 'https://', ''), '/', '')
    priority: 1
    weight: 1000
    enabledState: 'Enabled'
  }
}

resource cdnEndpoint 'Microsoft.Cdn/profiles/afdEndpoints@2022-05-01-preview' = {
  parent: cdnProfile
  name: cdnEndpointName
  location: 'global'
  tags: tags
  properties: {
    enabledState: 'Enabled'
  }
}

resource route 'Microsoft.Cdn/profiles/afdEndpoints/routes@2022-05-01-preview' = {
  parent: cdnEndpoint
  name: 'default-route'
  dependsOn: [
    origin
  ]
  properties: {
    customDomains: []
    originGroup: {
      id: originGroup.id
    }
    ruleSets: []
    supportedProtocols: [
      'Http'
      'Https'
    ]
    patternsToMatch: [
      '/*'
    ]
    forwardingProtocol: 'HttpsOnly'
    linkToDefaultDomain: 'Enabled'
    httpsRedirect: 'Enabled'
    enabledState: 'Enabled'
  }
}

output profileId string = cdnProfile.id
output endpointId string = cdnEndpoint.id
output cdnEndpointUrl string = 'https://${cdnEndpoint.properties.hostName}'

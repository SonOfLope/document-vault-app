@description('CDN profile name')
param cdnProfileName string

@description('CDN endpoint name')
param cdnEndpointName string

@description('Hostname for the storage account')
param storageAccountHostName string

@description('Location for the resources')
param location string

@description('Tags for the resources')
param tags object

resource cdnProfile 'Microsoft.Cdn/profiles@2022-05-01-preview' = {
  name: cdnProfileName
  location: location
  tags: tags
  sku: {
    name: 'Standard_Microsoft'
  }
}

resource cdnEndpoint 'Microsoft.Cdn/profiles/endpoints@2022-05-01-preview' = {
  parent: cdnProfile
  name: cdnEndpointName
  location: location
  tags: tags
  properties: {
    originHostHeader: replace(replace(storageAccountHostName, 'https://', ''), '/', '')
    isHttpAllowed: false
    isHttpsAllowed: true
    queryStringCachingBehavior: 'IgnoreQueryString'
    contentTypesToCompress: [
      'application/eot'
      'application/font'
      'application/font-sfnt'
      'application/javascript'
      'application/json'
      'application/opentype'
      'application/otf'
      'application/pkcs7-mime'
      'application/truetype'
      'application/ttf'
      'application/vnd.ms-fontobject'
      'application/xhtml+xml'
      'application/xml'
      'application/xml+rss'
      'application/x-font-opentype'
      'application/x-font-truetype'
      'application/x-font-ttf'
      'application/x-httpd-cgi'
      'application/x-javascript'
      'application/x-mpegurl'
      'application/x-opentype'
      'application/x-otf'
      'application/x-perl'
      'application/x-ttf'
      'font/eot'
      'font/ttf'
      'font/otf'
      'font/opentype'
      'image/svg+xml'
      'text/css'
      'text/csv'
      'text/html'
      'text/javascript'
      'text/js'
      'text/plain'
      'text/richtext'
      'text/tab-separated-values'
      'text/xml'
      'text/x-script'
      'text/x-component'
      'text/x-java-source'
    ]
    isCompressionEnabled: true
    origins: [
      {
        name: replace(replace(replace(storageAccountHostName, 'https://', ''), '.blob.${environment().suffixes.storage}/', ''), '.', '-')
        properties: {
          hostName: replace(replace(storageAccountHostName, 'https://', ''), '/', '')
          originHostHeader: replace(replace(storageAccountHostName, 'https://', ''), '/', '')
          enabled: true
          priority: 1
          weight: 1000
          httpPort: 80
          httpsPort: 443
        }
      }
    ]
    deliveryPolicy: {
      rules: [
        {
          name: 'EnforceHTTPS'
          order: 1
          conditions: [
            {
              name: 'RequestScheme'
              parameters: {
                typeName: 'DeliveryRuleRequestSchemeConditionParameters'
                matchValues: [
                  'HTTP'
                ]
                operator: 'Equal'
                negateCondition: false
                transforms: []
              }
            }
          ]
          actions: [
            {
              name: 'UrlRedirect'
              parameters: {
                typeName: 'DeliveryRuleUrlRedirectActionParameters'
                redirectType: 'Found'
                destinationProtocol: 'Https'
              }
            }
          ]
        }
        {
          name: 'SetCachingTTL'
          order: 2
          conditions: [
            {
              name: 'UrlFileExtension'
              parameters: {
                typeName: 'DeliveryRuleUrlFileExtensionMatchConditionParameters'
                operator: 'Any'
                transforms: []
                negateCondition: false
              }
            }
          ]
          actions: [
            {
              name: 'CacheExpiration'
              parameters: {
                typeName: 'DeliveryRuleCacheExpirationActionParameters'
                cacheBehavior: 'Override'
                cacheType: 'All'
                cacheDuration: '1.00:00:00' // 1 day
              }
            }
          ]
        }
      ]
    }
  }
}

output profileId string = cdnProfile.id
output endpointId string = cdnEndpoint.id
output cdnEndpointUrl string = 'https://${cdnEndpoint.properties.hostName}'

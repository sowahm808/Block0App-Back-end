@description('Deployment location')
param location string = resourceGroup().location
@description('Environment name')
param environmentName string
@description('Application name prefix')
param appName string = 'mindunlocking'

var name = '${appName}-${environmentName}'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${name}-log'
  location: location
  properties: { sku: { name: 'PerGB2018' } retentionInDays: 30 }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${name}-appi'
  location: location
  kind: 'web'
  properties: { Application_Type: 'web'; WorkspaceResourceId: logAnalytics.id }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: replace('${appName}${environmentName}st', '-', '')
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: { allowBlobPublicAccess: false; minimumTlsVersion: 'TLS1_2'; supportsHttpsTrafficOnly: true }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: '${name}-kv'
  location: location
  properties: { tenantId: tenant().tenantId; sku: { family: 'A'; name: 'standard' }; enableRbacAuthorization: true }
}

@description('Base name for all resources (lowercase, no spaces).')
param name string = 'voice-ai'

@description('Azure region. Must support Azure AI Services with Voice Live API. See: https://learn.microsoft.com/azure/ai-services/openai/concepts/models#model-summary-table-and-region-availability')
param location string = resourceGroup().location

@description('Object ID of the user or service principal that will run the app locally. Used to assign the Cognitive Services User role. Auto-detected by azd, or run: az ad signed-in-user show --query id -o tsv')
param currentUserObjectId string

// Azure AI Services — provides the Voice Live API endpoint
resource aiServices 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: name
  location: location
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: name
    publicNetworkAccess: 'Enabled'
  }
}

// Role assignment — Cognitive Services User (allows DefaultAzureCredential to call the API)
var cognitiveServicesUserRole = 'a97b65f3-24c7-4388-baec-2e87135dc908'

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiServices.id, currentUserObjectId, cognitiveServicesUserRole)
  scope: aiServices
  properties: {
    principalId: currentUserObjectId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesUserRole)
    principalType: 'User'
  }
}

@description('The endpoint URL to set as AZURE_VOICELIVE_ENDPOINT.')
output AZURE_VOICELIVE_ENDPOINT string = aiServices.properties.endpoint

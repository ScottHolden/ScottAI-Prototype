param location string = 'AustraliaEast'
param acsDataLocation string = 'Australia'
param prefix string = 'scottai'

var uniqueSuffix = uniqueString(resourceGroup().id, prefix, location)
var uniqueNameFormat = '${prefix}-{0}-${uniqueSuffix}'

resource openai 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: format(uniqueNameFormat, 'openai')
  location: location
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: format(uniqueNameFormat, 'openai')
  }

  resource gpt35 'deployments@2023-05-01' = {
    name: 'gpt-35-turbo-16k'
    sku: {
      name: 'Standard'
      capacity: 20
    }
    properties: {
      model: {
        format: 'OpenAI'
        name: 'gpt-35-turbo-16k'
      }
      versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
    }
  }
}

resource speech 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: format(uniqueNameFormat, 'speech')
  location: location
  kind: 'SpeechServices'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: format(uniqueNameFormat, 'speech')
  }
}

resource acs 'Microsoft.Communication/communicationServices@2023-04-01-preview' = {
  name: format(uniqueNameFormat, 'acs')
  location: 'global'
  properties: {
    dataLocation: acsDataLocation
  }
}

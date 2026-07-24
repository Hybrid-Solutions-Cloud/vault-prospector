param location string
param vmName string
param keyVaultName string

var bootstrapScript = loadTextContent('../scripts/Initialize-WindowsRunner.ps1')

resource buildVm 'Microsoft.Compute/virtualMachines@2024-03-01' existing = {
  name: vmName
}

resource runnerBootstrap 'Microsoft.Compute/virtualMachines/runCommands@2024-03-01' = {
  parent: buildVm
  name: 'vault-prospector-runner-bootstrap'
  location: location
  properties: {
    source: {
      script: bootstrapScript
    }
    parameters: [
      {
        name: 'KeyVaultName'
        value: keyVaultName
      }
    ]
    asyncExecution: false
    timeoutInSeconds: 10800
    treatFailureAsDeploymentFailure: true
  }
}

output extensionName string = runnerBootstrap.name

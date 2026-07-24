param location string
param vmName string
param keyVaultName string

var bootstrapScript = loadTextContent('../scripts/Initialize-WindowsRunner.ps1')

resource buildVm 'Microsoft.Compute/virtualMachines@2024-03-01' existing = {
  name: vmName
}

resource systemPreparation 'Microsoft.Compute/virtualMachines/runCommands@2024-03-01' = {
  parent: buildVm
  name: 'vault-prospector-runner-system-preparation-v2'
  location: location
  properties: {
    source: {
      script: '''
        $ErrorActionPreference = 'Stop'
        Set-Service -Name 'seclogon' -StartupType Manual
        Stop-Service -Name 'seclogon' -Force -ErrorAction SilentlyContinue
        & sc.exe sdset seclogon 'D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWRPDTLOCRRC;;;IU)(A;;CCLCSWDTLOCRRC;;;SU)(A;;CCLCSWRPDTLOCRRC;;;AU)S:(AU;FA;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;WD)'
        if ($LASTEXITCODE -ne 0) {
          throw "Secondary Logon DACL repair failed with exit code $LASTEXITCODE."
        }
        Start-Service -Name 'seclogon'
        '''
    }
    asyncExecution: false
    timeoutInSeconds: 300
    treatFailureAsDeploymentFailure: true
  }
}

resource runnerBootstrap 'Microsoft.Compute/virtualMachines/runCommands@2024-03-01' = {
  parent: buildVm
  name: 'vault-prospector-runner-bootstrap-v7'
  location: location
  dependsOn: [
    systemPreparation
  ]
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

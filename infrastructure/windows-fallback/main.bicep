targetScope = 'subscription'

@description('Azure region for the ephemeral HCS Windows build environment.')
param location string = 'eastus2'

@description('Temporary local administrator password resolved from HCS Key Vault.')
@secure()
param adminPassword string

@description('HCS Key Vault name used by the VM managed identity.')
param keyVaultName string = 'kv-hcs-vault-01'

@description('Resource group containing the HCS Key Vault.')
param keyVaultResourceGroupName string = 'rg-hcs-kv-mgmt-eus-01'

var buildResourceGroupName = 'rg-hcs-vp-winbuild-eus2-01'
var tags = {
  Owner: 'kris@hybridsolutions.cloud'
  Project: 'vault-prospector'
  Environment: 'prod'
  CostCenter: 'hcs-internal'
  ManagedBy: 'bicep'
  Workload: 'windows-build-fallback'
  Lifecycle: 'ephemeral'
}

resource buildResourceGroup 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: buildResourceGroupName
  location: location
  tags: tags
}

module buildVm 'modules/windows-build-vm.bicep' = {
  name: 'vault-prospector-windows-build-vm'
  scope: buildResourceGroup
  params: {
    location: location
    tags: tags
    adminPassword: adminPassword
  }
}

resource keyVaultResourceGroup 'Microsoft.Resources/resourceGroups@2023-07-01' existing = {
  name: keyVaultResourceGroupName
}

module buildVmSecretAccess 'modules/key-vault-access.bicep' = {
  name: 'vault-prospector-windows-build-key-vault-access'
  scope: keyVaultResourceGroup
  params: {
    keyVaultName: keyVaultName
    principalId: buildVm.outputs.principalId
  }
}

module runnerBootstrap 'modules/windows-runner-bootstrap.bicep' = {
  name: 'vault-prospector-windows-runner-bootstrap'
  scope: buildResourceGroup
  dependsOn: [
    buildVmSecretAccess
  ]
  params: {
    location: location
    vmName: buildVm.outputs.vmName
    keyVaultName: keyVaultName
    runId: deployment().name
  }
}

output resourceGroupName string = buildResourceGroup.name
output vmName string = buildVm.outputs.vmName
output runnerBootstrapName string = runnerBootstrap.outputs.extensionName

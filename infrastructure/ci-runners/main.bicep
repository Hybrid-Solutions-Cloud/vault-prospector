targetScope = 'resourceGroup'

@description('Azure region for the HCS Container Apps job.')
param location string = resourceGroup().location

@description('Resource ID of the existing HCS Container Apps managed environment.')
param containerAppsEnvironmentId string

@description('GitHub credential used by KEDA and the ephemeral runner.')
@secure()
param githubPat string

@description('Maximum concurrent Vault Prospector Linux runners.')
@minValue(1)
@maxValue(5)
param maxRunners int = 3

var repositoryOwner = 'Hybrid-Solutions-Cloud'
var repositoryName = 'vault-prospector'
var jobName = 'caj-hcs-vp-gh-runner-eus2-01'
var runnerLabels = 'self-hosted,linux,ubuntu-22.04,hcs'
var tags = {
  Owner: 'kris@hybridsolutions.cloud'
  Project: 'vault-prospector'
  Environment: 'prod'
  CostCenter: 'hcs-internal'
  ManagedBy: 'bicep'
  Workload: 'ci-runners'
}

resource runnerJob 'Microsoft.App/jobs@2024-03-01' = {
  name: jobName
  location: location
  tags: tags
  properties: {
    environmentId: containerAppsEnvironmentId
    configuration: {
      triggerType: 'Event'
      replicaTimeout: 7200
      replicaRetryLimit: 0
      secrets: [
        {
          name: 'github-pat'
          value: githubPat
        }
      ]
      eventTriggerConfig: {
        replicaCompletionCount: 1
        parallelism: 1
        scale: {
          minExecutions: 0
          maxExecutions: maxRunners
          pollingInterval: 30
          rules: [
            {
              name: 'vault-prospector-github-runner'
              type: 'github-runner'
              metadata: {
                githubApiURL: 'https://api.github.com'
                owner: repositoryOwner
                runnerScope: 'repo'
                repos: repositoryName
                targetWorkflowQueueLength: '1'
              }
              auth: [
                {
                  secretRef: 'github-pat'
                  triggerParameter: 'personalAccessToken'
                }
              ]
            }
          ]
        }
      }
    }
    template: {
      containers: [
        {
          name: 'vault-prospector-github-runner'
          image: 'myoung34/github-runner:ubuntu-jammy'
          resources: {
            cpu: json('4')
            memory: '8Gi'
          }
          env: [
            {
              name: 'RUNNER_SCOPE'
              value: 'repo'
            }
            {
              name: 'REPO_URL'
              value: 'https://github.com/${repositoryOwner}/${repositoryName}'
            }
            {
              name: 'LABELS'
              value: runnerLabels
            }
            {
              name: 'EPHEMERAL'
              value: '1'
            }
            {
              name: 'DISABLE_AUTO_UPDATE'
              value: '1'
            }
            {
              name: 'ACCESS_TOKEN'
              secretRef: 'github-pat'
            }
          ]
        }
      ]
    }
  }
}

output jobName string = runnerJob.name
output runnerLabels string = runnerLabels

# ACR-SyncTool

[![Nuget](https://img.shields.io/nuget/vpre/ACR-SyncTool.svg?style=flat-square)](https://www.nuget.org/packages/ACR-SyncTool)
[![Nuget)](https://img.shields.io/nuget/dt/ACR-SyncTool.svg?style=flat-square)](https://www.nuget.org/packages/ACR-SyncTool)

## What is this for?

It is common practice to have the Azure Container Registry behind a firewall and inaccessible from the outside world. Additionally, it is common practice to prevent AKS from loading images from public Docker Repositories. These two practices make it difficult to deploy images to the AKS cluster.

This tool aims to make this process easier by allowing you to sync images from a Docker Registry to the private Azure Container Registries.

## How it works

This tool is split into 3 different steps:

- ExportExistingImages
  - The first step is meant to be ran on your private Azure DevOps agents which have access to the Azure Container Registry. This step will export all the image names and tags to a json file. Your CI/CD pipeline should save this file to the Pipeline Artifacts and make it accessible to the next step.
- ExportMissingImages
  - The second step is meant to be ran on the Microsoft Hosted Azure DevOps agents and will pull all the images and tags from the Docker Registries that you are missing and save them to a json file. Your CI/CD pipeline should save this file to the Pipeline Artifacts and make it accessible to the next step.
- ImportMissingImages
  - This final mode is meant to be ran on your private Azure DevOps agents. It will load the image json from the previous step and import them to your private Azure Container Registry.

Utilizes the [Azure Container Registry Import feature](https://learn.microsoft.com/en-us/azure/container-registry/container-registry-import-images?).

## How to use

- [Install .Net 10](https://dotnet.microsoft.com/download/dotnet/10.0/runtime)
- dotnet tool install --global acr-synctool
- Create [appsettings.json](appsettings.json) and fill out the details
  - MaxSyncSizeGB
    - Max total image size to sync. Once reached the rest will be skipped.
  - AzureContainerRegistries
    - List of Azure Container Registries with Service Principle Credentials
  - Registries
    - List of Docker Registries and credentials
    - If a registry doesn't require credentials, you can exclude it from this list
    - AuthType can be Basic, PasswordOAuth or AnonymousOAuth
  - SyncedImages
    - List of Docker Images to sync
    - Image
      - Full image name ie registry.hub.docker.com/library/busybox
    - Semver
      - Semver rule, if it doesn't match, the Tag will not be synced
    - Regex
      - Regex rule, if it doesn't match, the Tag will not be synced
    - Tags
      - Array of specific tags to sync

- ```json
  {
    "MaxSyncSizeGB": "5",
    "AzureContainerRegistries": [
      {
        "Host": "ijtestacr.azurecr.io",
        "TenantId": "f4ba3f29-303f-4c8b-a487-991dc21962c0",
        "ClientId": "7c06291c-13bc-4321-ae27-34948ecc1eec",
        "Secret": "mysecret"
      }
    ],
    "Registries": [
      {
        "Host": "ghcr.io",
        "AuthType": "PasswordOAuth", 
        "Username": "ivanjosipovic",
        "Password": "Pat Token"
      },
      {
        "Host": "registry.hub.docker.com",
        "AuthType": "PasswordOAuth",
        "Username": "ivanjosipovic",
        "Password": "Access Tokens"
      },
      {
        "Host": "xpkg.upbound.io",
        "AuthType": "AnonymousOAuth"
      }
    ],
    "SyncedImages": [
      {
        "Image": "ghcr.io/fluxcd/helm-controller",
        "Semver": ">=0.11.0"
      },
      {
        "Image": "ghcr.io/fluxcd/image-automation-controller",
        "Semver": ">=0.14.0"
      },
      {
        "Image": "ghcr.io/fluxcd/image-reflector-controller",
        "Semver": ">=0.11.0"
      },
      {
        "Image": "ghcr.io/fluxcd/kustomize-controller",
        "Semver": ">=0.14.0"
      },
      {
        "Image": "ghcr.io/fluxcd/notification-controller",
        "Semver": ">=0.16.0"
      },
      {
        "Image": "ghcr.io/fluxcd/source-controller",
        "Semver": ">=0.15.0"
      },
      {
        "Image": "registry.hub.docker.com/nginx/nginx-ingress",
        "Tags": [
          "1.12.0"
        ]
      }
    ]
  }
  ```

## Example Command Lines (execute in folder containing appsettings.json)

- acr-synctool --Action ExportExistingImages --ACRHostName ijtestacr.azurecr.io --JsonExportExistingFilePath acr-export.json
- acr-synctool --Action ExportMissingImages --ImagesTarFilePath images.tar --JsonExportExistingFilePath acr-export.json --JsonExportMissingFilePath acr-missing.json
- acr-synctool --Action ImportMissingImages --ACRHostName ijtestacr.azurecr.io --JsonExportMissingFilePath acr-missing.json

## Local Testing

- dotnet pack
- dotnet tool install --global --add-source ./src/bin/Debug/ acr-synctool
- dotnet tool uninstall -g acr-synctool

# ACR-SyncTool

# What is this for?
It is common practice to have the Azure Container Registry behind a firewall and inaccessable from the outside world. Additionally it is common practice to prevent AKS from loading images from public Docker Repositories. These two practices make it difficult to deploy images to the AKS cluster.

This tool aims to make this process easier by allowing you to sync images from a Docker Registry to the private Azure Container Registries.

# How it works
This tool is split into 3 different steps:
- ExportExistingImages
  - The first step is meant to be ran on your private Azure DevOps agents which have access to the Azure Container Registry. This step will export all the image names and tags to a json file. Your CI/CD pipeline should save this file to the Pipeline Artifacts and make it accessible to the next step.
- PullAndSaveMissingImages
  - The second step is meant to be ran on the Microsoft Hosted Azure DevOps agents and will pull all the images and tags from the Docker Registries that you are missing and save them to a tar file. Your CI/CD pipeline should save this file to the Pipeline Artifacts and make it accessible to the next step.
- LoadAndPushImages
  - This final mode is meant to be ran on your private Azure DevOps agents. It will load the image tar from the previous step, re tag them and push them to your private Azure Container Registry.

# How to use
- Install .Net 6
- dotnet tool install --global acr-synctool
- Create [appsettings.json](appsettings.json) and fill out the details
  - AzureContainerRegistries
    - List of Azure Container Registries and Service Principle Credentials
  - Registries
    - List of Docker Registries and credentials
    - If a registry doesn't require credentials, you can exclude it from this list
    - AuthType can be Basic, PasswordOAuth or AnonymousOAuth
  - SyncedImaged
    - List of Docker Images to sync
    - Make sure to use the full image name ie registry.hub.docker.com/library/busybox
- ```
  {
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
        "AuthType": "PasswordOAuth", // Basic, PasswordOAuth, AnonymousOAuth
        "Username": "ivanjosipovic",
        "Password": "Pat Password"
      }
    ],
    "SyncedImaged": [
      "ghcr.io/fluxcd/helm-controller",
      "ghcr.io/fluxcd/image-automation-controller",
      "ghcr.io/fluxcd/image-reflector-controller",
      "ghcr.io/fluxcd/kustomize-controller",
      "ghcr.io/fluxcd/notification-controller",
      "ghcr.io/fluxcd/source-controller"
    ]
  }
  ```

## Example Command Lines (execute in folder containing appsettings.json)
- acr-synctool --Action ExportExistingImages --ACRHostName ijtestacr.azurecr.io --JsonExportFilePath acr-export.json
- acr-synctool --Action PullAndSaveMissingImages --ImagesTarFilePath images.tar --JsonExportFilePath acr-export.json
- acr-synctool --Action LoadAndPushImages --ACRHostName ijtestacr.azurecr.io --ImagesTarFilePath images.tar

# Local Testing
- dotnet pack
- dotnet tool install --global --add-source ./src/bin/Debug/ acr-synctool
- dotnet tool uninstall -g acr-synctool
Storage Acc:
az storage account create -n videofuncstorageaccount -g video_download -l centralindia --sku Standard_LRS
  
App Service plan
az appservice plan create -g video_download -n myFuncPlan --is-linux --sku F1 --location centralindia

Func Create
az functionapp create -g video_download -p myFuncPlan -n videofunc --storage-account videofuncstorageaccount --deployment-container-image-name videoregistry.azurecr.io/videofunc:v1

Az Container APP
================
az monitor log-analytics workspace create -g video_download -n myWorkspace

#Workspace ID from previous resource
WS_ID=$(az monitor log-analytics workspace show -g <RG> -n myWorkspace --query id -o tsv)


az containerapp env create -g video_download -n myEnv --location southindia --logs-workspace-id <ws_id>>

#When creating using managed identity
az containerapp create --name videofunc-app --resource-group video_download --environment managedEnvironment-videodownload-b4d5 --image videoregistry.azurecr.io/videofunc:v1  --registry-server videoregistry.azurecr.io  --registry-username videoregistry --registry-password <container registry pwd> --min-replicas 0 --max-replicas 5 --env-vars AzureWebJobsStorage="<Storage acc conn str>" FUNCTIONS_WORKER_RUNTIME="dotnet-isolated"


Envt Variables - Az Pipeline

FUNCTIONS_WORKER_RUNTIME = dotnet-isolated
AzureWebJobsStorage = DefaultEndpointsProtocol=https;AccountName=girimediastorage;AccountKey=;EndpointSuffix=core.windows.net


------------

Build commands:
docker build -t videofunc:v5 .
docker tag videofunc:v5 videoregistry.azurecr.io/videofunc:v5
docker push videoregistry.azurecr.io/videofunc:v5
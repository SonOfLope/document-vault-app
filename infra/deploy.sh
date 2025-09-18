#!/bin/bash
set -e

LOCATION="eastus"
ENVIRONMENT="prod"
RESOURCE_GROUP_NAME="documentvault-rg-as"
GITHUB_REPO_OWNER="SonOfLope" # case sensitive
GITHUB_REPO_NAME="document-vault-app"
GITHUB_BRANCH="main"

while [[ $# -gt 0 ]]; do
  case $1 in
    --github-owner)
      GITHUB_REPO_OWNER="$2"
      shift 2
      ;;
    --github-repo)
      GITHUB_REPO_NAME="$2"
      shift 2
      ;;
    --github-branch)
      GITHUB_BRANCH="$2"
      shift 2
      ;;
    --rg)
      RESOURCE_GROUP_NAME="$2"
      shift 2
      ;;
    --location)
      LOCATION="$2"
      shift 2
      ;;
    --env)
      ENVIRONMENT="$2"
      shift 2
      ;;
    *)
      echo "Unknown option: $1"
      exit 1
      ;;
  esac
done

# Validate required parameters
if [[ -z "$GITHUB_REPO_OWNER" || -z "$GITHUB_REPO_NAME" ]]; then
  echo "ERROR: GitHub repository owner and name are required."
  echo "Usage: $0 --github-owner <owner> --github-repo <repo-name> [--github-branch <branch>] [--rg <resource-group>] [--location <location>] [--env <environment>]"
  exit 1
fi

if ! az group show --name $RESOURCE_GROUP_NAME &>/dev/null; then
  echo "Creating resource group $RESOURCE_GROUP_NAME in $LOCATION..."
  az group create --name $RESOURCE_GROUP_NAME --location $LOCATION
else
  echo "Resource group $RESOURCE_GROUP_NAME already exists."
fi

echo "Deploying infrastructure..."
DEPLOYMENT_NAME="deployment-$(date +%Y%m%d%H%M%S)"

az deployment group create \
  --resource-group $RESOURCE_GROUP_NAME \
  --template-file bicep/main.bicep \
  --parameters environmentName=$ENVIRONMENT \
              location=$LOCATION \
              githubRepositoryOwner=$GITHUB_REPO_OWNER \
              githubRepositoryName=$GITHUB_REPO_NAME \
              githubBranch=$GITHUB_BRANCH \
  --name $DEPLOYMENT_NAME \
  --verbose

echo "Getting federated identity client IDs for GitHub Actions..."
FUNC_APP_CLIENT_ID=$(az deployment group show \
  --resource-group $RESOURCE_GROUP_NAME \
  --name $DEPLOYMENT_NAME \
  --query "properties.outputs.functionAppFederatedIdentityClientId.value" \
  --output tsv)

WEB_APP_PROD_CLIENT_ID=$(az deployment group show \
  --resource-group $RESOURCE_GROUP_NAME \
  --name $DEPLOYMENT_NAME \
  --query "properties.outputs.webAppFederatedIdentityProdClientId.value" \
  --output tsv)

WEB_APP_TEST_CLIENT_ID=$(az deployment group show \
  --resource-group $RESOURCE_GROUP_NAME \
  --name $DEPLOYMENT_NAME \
  --query "properties.outputs.webAppFederatedIdentityTestClientId.value" \
  --output tsv)

WEB_APP_STAGING_CLIENT_ID=$(az deployment group show \
  --resource-group $RESOURCE_GROUP_NAME \
  --name $DEPLOYMENT_NAME \
  --query "properties.outputs.webAppFederatedIdentityStagingClientId.value" \
  --output tsv)

APP_SERVICE_NAME=$(az deployment group show \
  --resource-group $RESOURCE_GROUP_NAME \
  --name $DEPLOYMENT_NAME \
  --query "properties.outputs.appServiceName.value" \
  --output tsv)

echo "======= GITHUB ACTIONS CONFIGURATION ======="
echo "Update your GitHub Actions workflows with the following values:"
echo
echo "For Function App Workflow (.github/workflows/function-app-cd.yml):"
echo "AZURE_TENANT_ID: $(az account show --query tenantId -o tsv)"
echo "AZURE_SUBSCRIPTION_ID: $(az account show --query id -o tsv)"
echo "AZURE_CLIENT_ID: $FUNC_APP_CLIENT_ID"
echo
echo "For Web App Workflow (.github/workflows/web-app-cd.yml):"
echo "AZURE_APP_SERVICE_NAME: $APP_SERVICE_NAME"
echo "AZURE_TENANT_ID: $(az account show --query tenantId -o tsv)"
echo "AZURE_SUBSCRIPTION_ID: $(az account show --query id -o tsv)"
echo
echo "GitHub Environment Secrets (set AZURE_CLIENT_ID_WEB for each environment):"
echo "prod environment: $WEB_APP_PROD_CLIENT_ID"
echo "test environment: $WEB_APP_TEST_CLIENT_ID"
echo "staging environment: $WEB_APP_STAGING_CLIENT_ID"
echo "================================================"
#!/bin/bash
set -e

LOCATION="eastus"
ENVIRONMENT="prod"
RESOURCE_GROUP_NAME="documentvault-rg-as-dc"
GITHUB_REPO_OWNER="SonOfLope" # case sensitive
GITHUB_REPO_NAME="document-vault-app"
GITHUB_BRANCH="feature/app-service-deployment-with-deployment-center"

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

echo "Getting deployment information..."

APP_SERVICE_NAME=$(az deployment group show \
  --resource-group $RESOURCE_GROUP_NAME \
  --name $DEPLOYMENT_NAME \
  --query "properties.outputs.appServiceName.value" \
  --output tsv)

echo "======= DEPLOYMENT CENTER CONFIGURATION ======="
echo "Azure Deployment Center has been configured for automatic CI/CD."
echo
echo "App Service Name: $APP_SERVICE_NAME"
echo "GitHub Repository: https://github.com/$GITHUB_REPO_OWNER/$GITHUB_REPO_NAME"
echo "Branch: $GITHUB_BRANCH"
echo
echo "Deployment center will automatically:"
echo "1. Generate a GitHub Actions workflow in your repository"
echo "2. Create a service principal for authentication"
echo "3. Deploy to production slot when you push to $GITHUB_BRANCH"
echo "4. Provide test and staging slots for manual deployment/swapping"
echo
echo "Available slots:"
echo "- Production: https://$APP_SERVICE_NAME.azurewebsites.net"
echo "- Staging: https://$APP_SERVICE_NAME-staging.azurewebsites.net"
echo "- Test: https://$APP_SERVICE_NAME-test.azurewebsites.net"
echo
echo "Use Azure Portal to manage slot swapping and deployments."
echo "================================================"
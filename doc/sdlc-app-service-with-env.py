#!/usr/bin/env python3
"""
SDLC Diagram: App Service with Environments (feature/app-service-deployment-with-environments branch)
Generates a visual diagram showing the SDLC for App Service deployment with multiple environments.
"""

from diagrams import Diagram, Cluster, Edge
from diagrams.onprem.vcs import Github
from diagrams.onprem.ci import GithubActions
from diagrams.azure.compute import AppServices, FunctionApps
from diagrams.azure.database import CosmosDb
from diagrams.azure.storage import StorageAccounts
from diagrams.azure.network import CDNProfiles
from diagrams.azure.identity import ManagedIdentities
from  diagrams.programming.language import Csharp

def create_diagram():
    with Diagram("SDLC: App Service with Environments", show=False, filename="sdlc-app-service-with-env"):

        with Cluster("Development"):
            function = Csharp("src/DocumentVault.Function")
            web = Csharp("src/DocumentVault.Web")

        with Cluster("GitHub"):
            github_repo = Github("Repository")

        with Cluster("Pipelines"):
            web_pipeline = GithubActions("web-app-cd-with-env.yml")
            function_pipeline = GithubActions("function-app-cd.yml")

        with Cluster("Azure tenant"):
            with Cluster("Federated Identities"):
                prod_identity = ManagedIdentities("prod-identity")
                test_identity = ManagedIdentities("test-identity")
                staging_identity = ManagedIdentities("staging-identity")
                function_identity = ManagedIdentities("function-identity")

            with Cluster("App Service"):
                prod_slot = AppServices("Production Slot")
                test_slot = AppServices("Test Slot")
                staging_slot = AppServices("Staging Slot")

            cosmos = CosmosDb("Cosmos DB")
            storage = StorageAccounts("Storage Account")
            cdn = CDNProfiles("CDN")
            function_app = FunctionApps("Function App")

        web >> Edge(label="code & commit") >> github_repo
        function >> Edge(label="code & commit") >> github_repo

        github_repo >> Edge(label="manual trigger, select environment") >> web_pipeline
        github_repo >> Edge(label="manual trigger") >> function_pipeline

        web_pipeline >> Edge(label="authenticate") >> [prod_identity, test_identity, staging_identity] 

        prod_identity >> Edge(label="deploy") >> prod_slot
        test_identity >> Edge(label="deploy") >> test_slot
        staging_identity >> Edge(label="deploy") >> staging_slot

        function_pipeline >> Edge(label="authenticate") >> function_identity >> Edge(label="deploy") >> function_app

        [prod_slot, test_slot, staging_slot] >> cosmos
        [prod_slot, test_slot, staging_slot] >> storage
        function_app >> cosmos
        function_app >> storage
        storage >> cdn

if __name__ == "__main__":
    create_diagram()
    print("generated: sdlc-app-service-with-env.png")
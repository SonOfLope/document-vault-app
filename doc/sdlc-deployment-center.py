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
from diagrams.azure.devops import Devops
from  diagrams.programming.language import Csharp

def create_diagram():
    with Diagram("SDLC: App Service with Deployment Center", show=False, filename="sdlc-deployment-center"):

        with Cluster("Development"):
            function = Csharp("src/DocumentVault.Function")
            web = Csharp("src/DocumentVault.Web")

        with Cluster("GitHub"):
            github_repo = Github("Repository")

        with Cluster("Pipelines"):
            web_pipeline = GithubActions("Deployment center auto provisioned yaml workflow")
            function_pipeline = GithubActions("function-app-cd.yml")

        with Cluster("Azure tenant"):
            with Cluster("Web App"):
                with Cluster("Production"):
                    prod_dc = Devops("Deployment Center")
                    prod_slot = AppServices("Production Slot")
                with Cluster("Staging"):
                    staging_dc = Devops("Deployment Center")
                    staging_slot = AppServices("Staging Slot")
                with Cluster("Test"):
                    test_dc = Devops("Deployment Center")
                    test_slot = AppServices("Test Slot")

            with Cluster("Function App Identity"):
                function_identity = ManagedIdentities("function-identity")

            cosmos = CosmosDb("Cosmos DB")
            storage = StorageAccounts("Storage Account")
            cdn = CDNProfiles("CDN")
            function_app = FunctionApps("Function App")

        web >> Edge(label="code & commit") >> github_repo
        function >> Edge(label="code & commit") >> github_repo

        github_repo >> Edge(label="push to target branch") >> web_pipeline
        github_repo >> Edge(label="manual trigger") >> function_pipeline

        web_pipeline >> Edge(label="push via Deployment Center") >> [prod_dc, staging_dc, test_dc]

        prod_dc >> Edge(label="deploy") >> prod_slot
        staging_dc >> Edge(label="deploy") >> staging_slot
        test_dc >> Edge(label="deploy") >> test_slot

        function_pipeline >> Edge(label="authenticate") >> function_identity >> Edge(label="deploy") >> function_app

        [prod_slot, staging_slot, test_slot] >> cosmos
        [prod_slot, staging_slot, test_slot] >> storage
        function_app >> cosmos
        function_app >> storage
        storage >> cdn

if __name__ == "__main__":
    create_diagram()
    print("generated: sdlc-deployment-center.png")
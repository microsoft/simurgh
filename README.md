# Azure RAG Architecture for NPS Survey Data

This repository is an accelerator for quickly getting started with using Semantic Kernel in .NET to in a [RAG pattern](https://learn.microsoft.com/en-us/semantic-kernel/concepts/plugins/using-data-retrieval-functions-for-rag) for querying structured and unstructured NPS survey data stored in Azure SQL.

The key challenge that this project seeks to overcome is providing ways to handle both structured and unstructured data using a combination of SQL query generation and hybrid search. Supporting the RAG application itself is a console application to provide a simple ETL process for preprocessing unstructured data with embeddings and sentiment analysis to reduce cost and performance impact by performing it on the fly in the chat application.

# Features
- [.NET 8](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8/overview) Backend API
    - Receives new user questions about the NPS data in the history/generate endpoint
    - Builds chat history and persists it to Cosmos DB
    - Uses Semantic Kernel to orchestrate plugins to answer user question on NPS data
    - Optionally generates suggested questions of the NPS data based on chat history
- [React](https://react.dev/) Front-End
    - Chat interface
    - List of past 'conversations'
- .NET Console ETL App
    - Reads CSV data into tables
    - Preprocesses unstructured data to generate embeddings and sentiment analysis

# Application Architecture

[Architecture Diagram](./docs/architecture-diagram.png)
- [Azure App Service](https://learn.microsoft.com/en-us/azure/app-service/overview): host frontend and backend
- [Application Insights](https://learn.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview): provides observability and logging across the stack
- [CosmosDB](https://learn.microsoft.com/en-us/azure/cosmos-db/): used for storing conversation history
- [Azure SQL](https://learn.microsoft.com/en-us/azure/azure-sql/database/sql-database-paas-overview?view=azuresql): stores survey data
- [Azure Open AI](https://learn.microsoft.com/en-us/azure/ai-services/openai/overview):
    - [text-embedding-ada-002](https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/models?tabs=global-standard%2Cstandard-chat-completions#embeddings): used for generating embeddings of comments, survey questions, and user queries
    - [gpt-4o](https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/models?tabs=global-standard%2Cstandard-chat-completions#gpt-4o-and-gpt-4-turbo): used for code generation, chat completion, and powering semantic kernel
- Azure Language Service (Azure Text Analytics): used for performing sentiment analysis on survey comments. Azure Open AI could also be used for this but the consistency and cost of the Azure Language service as well as avoiding token limitations 


The ETL process, currently represented by a .NET Console App reads the CSV data, and optionally another CSV with descriptions of the NPS questions, and loads them into tables with the following schema

[Database Schema](./docs/sql-schema-diagram.png)

The SurveyQuestion table receives the column headers, which are the questions from the NPS survey, from the original CSV as well as the descriptions of the questions and the data type (simply text or numeric) of the answers. The table also gets an embedding of the question and description which is leveraged by a Semantic Kernel plugin to hybrid search for the most relevant NPS questions to the chat user's prompt.

Answers are persisted to the SurveyQuestionAnswer table with answers that all belong to a single survey response from a survey taker tied together by a SurveyResponseId. Answers belonging to questions with a `text` DataType will be run through Azure Text Analytics to get sentiment analysis which is persisted to SentimentAnalysisJson with the overarching sentiment confidence of the text saved to `PositiveSentimentConfidenceScore`, `NeutralSentimentConfidenceScore`, and `NegativeSentimentConfidenceScore`. Further, the ETL process generated an embedding of `text` answers using Azure Open AI.

The RAG application is a .NET 8 minimal web API that hosts a React front-end. The user can select a survey to begin querying against. The chat page also shows what questions were in the survey and their data type to help guide the user on what kind of data is available to ask about.

Once a query about the data is submitted, the React application sends the message in the conversation to the `/history/generate` endpoint. The endpoint persists the latest query to the conversation history in Cosmos, generating a new conversation record if none exists, then passes the latest query along with the surveyId to Semantic Kernel.

[Logical Flow](./docs/logical-flow-diagram.png)

Semantic Kernel has plugins that it can choose from. It is not required to choose the plugins in the order above, however this often is the pattern that it would invoke: 
1. Use a semantic plugin to split more complex user questions up
2. For each identify which survey questions are most relevant to the user's question 
3. Invokes a plugin which injects those survey questions, the user's question into a prompt to generate SQL in order to try to find the answer
4. That SQL output is combined with results from other sub-questions until a final query is resolved and finally executed then those results returned to the user

# Getting Started

## Account Requirements
In order to deploy and run this example, you'll need

- Azure Account - If you're new to Azure, get an [Azure account for free](https://aka.ms/free) and you'll get some free Azure credits to get started.
- Azure subscription with access enabled for the Azure OpenAI service - [You can request access](https://aka.ms/oaiapply). You can also visit the Cognitive Search docs to get some free Azure credits to get you started.
- Azure account permissions - Your Azure Account must have Microsoft.Authorization/roleAssignments/write permissions, such as [User Access Administrator](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles#user-access-administrator) or [Owner](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles#owner).

## Cost Estimation
Pricing varies per region and usage, so it isn't possible to predict exact costs for your usage. However, you can try the [Azure pricing calculator](https://azure.microsoft.com/pricing/calculator/) for the resources below:

- [Azure App Service](https://azure.microsoft.com/en-us/pricing/details/app-service/linux/)
- [Azure OpenAI Service](https://azure.microsoft.com/pricing/details/cognitive-services/openai-service/)
- [Azure Cosmos DB](https://azure.microsoft.com/en-us/pricing/details/cosmos-db/)
- [Azure Monitor](https://azure.microsoft.com/pricing/details/monitor/)
- [Azure SQL](https://azure.microsoft.com/en-us/pricing/details/azure-sql-database/single)

# Deployment

This project supports azd for easy deployment of the complete application, as defined in the main.bicep resources.

If you choose not use azd then you can see [here](https://learn.microsoft.com/en-us/azure/app-service/quickstart-dotnetcore?tabs=net80&pivots=development-environment-vs) for instructions on deploying a .NET app to an Azure App Service.

# Running locally for Dev and Debug

As many cloud resources are required to run the client app and minimal API even locally, deployment to Azure first will provision all the necessary services. You can then configure your local user secrets to point to those required cloud resources before building and running locally for the purposes of debugging and development.

If you've run `azd up`, all of the secrets are in your .azure/{env name}/.env file. All of these settings can be applied in your appsettings file for both the ChatApp.Server application and CsvDataUploader. There is an example for both with comments on what each property of the settings is for and when it might be left blank. For any of the properties you deem sensitive, you can use .NET user secrets. You can learn how to manage user secrets in .NET [here](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets).

For authenticating to Azure resources, the code supports either managed identity or key based auth. For Azure SQL you can also construct a connection string and use that instead as another option. For key based auth you must include the keys in your user secrets. For managed identity, it relies on [DefaultAzureCredential](https://learn.microsoft.com/en-us/python/api/azure-identity/azure.identity.defaultazurecredential?view=azure-python). If you've run `azd up`, the bicep templates will grant you the necessary permissions. Otherwise, you will need the folllowing roles on the resource group (or their respective resources).
- [Azure AI Developer](https://learn.microsoft.com/en-us/azure/ai-studio/concepts/rbac-ai-studio#azure-ai-developer-role)
    - Azure Open AI
- Cognitive Services Open AI User
    - Azure Language Service
- SQL DB Contributor
    - Azure SQL
- Cognitive Services Language Writer
    - Azure Language Service
In addition you will need read/write permissions for Cosmos DB, [here](https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/security/how-to-grant-data-plane-role-based-access?tabs=built-in-definition%2Ccsharp&pivots=azure-interface-cli) is a way to grant those permissions to a user.

Finally for SQL server, you have to remaining steps. You need to also [add a network rule](https://learn.microsoft.com/en-us/azure/azure-sql/database/network-access-controls-overview?view=azuresql) to allow for local connection (which will be necessary to run the CsvDataUploader). For running in a deployed state, the bicep templates will ensure connectivity of services. 

In addition, you'll need to either grant yourself SQL permissions or create a SQL user with permissions. You can either use the SQL server admin that you created during `azd up` (for SQL user identity) or add yourself as the [entra admin](https://learn.microsoft.com/en-us/azure/azure-sql/database/authentication-aad-configure?view=azuresql&tabs=azure-portal#set-microsoft-entra-admin).

Before chatting, run the DropAndCreateallTables.sql script to create the tables then upload the data to it using the CsvDataUploader project. Once it completes, you can begin querying the ChatApp.Server about the data you've uploaded.

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft 
trademarks or logos is subject to and must follow 
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.

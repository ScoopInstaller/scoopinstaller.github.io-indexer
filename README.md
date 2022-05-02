
# ScoopInstaller Azure functions

This repository contains the Azure functions used to build and maintain the Scoop applications index used by https://scoopinstaller.github.io/

#### Prerequisites
- [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
- [Azure Functions Core Tools V4](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local#install-the-azure-functions-core-tools)

#### Configuration to build and debug
- Copy `local.settings.json-sample` to `local.settings.json`
- Create an *Azure Search* service
  - Retrieve the name (in *Properties*) and use it for `AzureSearchServiceName`
  - Retrieve the primary admin key (in *Keys*) and use it for `AzureSearchAdminApiKey`
- Create an *Azure Storage* account
  - Retrieve the connection string (in *Access keys*) and use it for `AzureWebJobsStorage`
- Create a [*GitHub access token*](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/creating-a-personal-access-token) without any scope and use it for `GitHubToken`

#### Azure Functions list
- `DispatchBucketsCrawler` - TimerTrigger, runs every 6 hours (as defined by `DispatchBucketsCrawlerCron`)
  - Create a list of buckets based on the configuration provided in `settings.json`:
    - Search on GitHub repositories for all possible buckets
    - Include official buckets
    - Include manually added buckets
    - Exclude manually excluded buckets
  - Remove non-existent buckets from the *Azure Search Service Index*
  - Queue all buckets to index in an *Azure Queue Storage*


- `BucketCrawler` - QueueTrigger, runs when a message *(bucket)* appears in the *Azure Queue Storage*
  - Clone bucket repository
  - Fetch all manifest files
  - Update the *Azure Search Service Index* based on manifests content (add/remove/update)


- `Version` - HttpTrigger, runs when accessing https://scoopsearch.azurewebsites.net/api/Version
  - Return the deployed version

# ScoopSearch Indexer

[![Indexer workflow](https://github.com/ScoopInstaller/scoopinstaller.github.io-indexer/actions/workflows/indexer.yml/badge.svg)](https://github.com/ScoopInstaller/scoopinstaller.github.io-indexer/actions/workflows/indexer.yml)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=ScoopInstaller_ScoopInstaller.scoopinstaller.github.io-indexer&metric=coverage)](https://sonarcloud.io/summary/new_code?id=ScoopInstaller_ScoopInstaller.AzureFunctions)


This repository contains the Indexer used to build and maintain the Scoop applications index used by https://scoopinstaller.github.io/

### Indexing
The indexer runs every 2 hours and search for buckets across the whole [GitHub site](https://github.com/ScoopInstaller/scoopinstaller.github.io-indexer/blob/main/src/ScoopSearch.Indexer/appsettings.json#L18-L25) + some additional [inclusions/exclusions](https://github.com/ScoopInstaller/scoopinstaller.github.io-indexer/blob/main/src/ScoopSearch.Indexer/appsettings.json#L28-L40).

### Configuration to build and debug the Indexer
- Install [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Create an *Azure Search* service
  - Retrieve the name (in *Properties*) and use it for `ServiceUrl`
  - Retrieve the primary admin key (in *Keys*) and use it for `AdminApiKey`
- Create a [*GitHub access token*](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/creating-a-personal-access-token) without any scope and use it for `Token`
- Create a file `src/ScoopSearch.Indexer.Console/appsettings.Production.json` with the following content
```
{
    "AzureSearch": {
        "ServiceUrl": "https://[SERVICENAME].search.windows.net",
        "AdminApiKey": "[ADMINAPIKEY]",
        "IndexName": "[INDEXNAME]"
    },

    "GitHub": {
        "Token": "[GITHUBTOKEN]"
    }
}
```
- Alternatively, you can declare environment variables
```
AzureSearch__ServiceUrl = "https://[SERVICENAME].search.windows.net"
AzureSearch__AdminApiKey = "[ADMINAPIKEY]"
AzureSearch__IndexName: "[INDEXNAME]"
GitHub__Token = "[GITHUBTOKEN]"
```

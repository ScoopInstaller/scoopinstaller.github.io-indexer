using ScoopSearch.Functions.Data;
using System;
using System.Collections.Generic;
using System.Threading;

namespace ScoopSearch.Functions.Manifest
{
    public interface IManifestCrawler
    {
        IEnumerable<ManifestInfo> GetManifestsFromRepository(Uri url, CancellationToken cancellationToken);
    }
}

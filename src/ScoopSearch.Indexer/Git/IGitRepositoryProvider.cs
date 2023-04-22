using System;
using System.Threading;

namespace ScoopSearch.Functions.Git
{
    public interface IGitRepositoryProvider
    {
        IGitRepository? Download(Uri uri, CancellationToken cancellationToken);
    }
}

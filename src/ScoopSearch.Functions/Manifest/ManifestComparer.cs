using System;
using System.Collections.Generic;
using ScoopSearch.Functions.Data;

namespace ScoopSearch.Functions.Manifest
{
    public class ManifestComparer : IEqualityComparer<ManifestInfo>
    {
        public static readonly IEqualityComparer<ManifestInfo> ManifestIdComparer = new ManifestComparer(false);
        public static readonly IEqualityComparer<ManifestInfo> ManifestExactComparer = new ManifestComparer(true);

        private readonly bool _exactComparer;

        private ManifestComparer(bool exactComparer)
        {
            _exactComparer = exactComparer;
        }

        public bool Equals(ManifestInfo x, ManifestInfo y)
        {
            if (x == null && y == null)
            {
                return true;
            }

            if (x == null || y == null)
            {
                return false;
            }

            if (_exactComparer)
            {
                return x.Id == y.Id
                       && x.Metadata.Sha == y.Metadata.Sha
                       && x.Metadata.RepositoryStars == y.Metadata.RepositoryStars;
            }
            else
            {
                return x.Id == y.Id;
            }
        }

        public int GetHashCode(ManifestInfo obj)
        {
            if (_exactComparer)
            {
                return HashCode.Combine(obj.Id, obj.Metadata.Sha, obj.Metadata.RepositoryStars);
            }
            else
            {
                return obj.Id.GetHashCode();
            }
        }
    }
}

using System;
using System.Security.Cryptography;
using System.Text;
using ScoopSearch.Functions.Data;

namespace ScoopSearch.Functions.Manifest
{
    internal class KeyGenerator : IKeyGenerator, IDisposable
    {
        private readonly SHA1Managed _sha1 = new SHA1Managed();

        public string Generate(ManifestMetadata manifestMetadata)
        {
            var key = $"{manifestMetadata.Repository}{manifestMetadata.BranchName}{manifestMetadata.FilePath}";

            var hash = _sha1.ComputeHash(Encoding.UTF8.GetBytes(key));
            var stringBuilder = new StringBuilder(hash.Length * 2);

            foreach (byte b in hash)
            {
                stringBuilder.Append(b.ToString("x2"));
            }

            return stringBuilder.ToString();
        }

        public void Dispose()
        {
            _sha1.Dispose();
        }
    }
}

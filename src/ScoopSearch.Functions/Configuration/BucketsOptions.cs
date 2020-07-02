using System;
using System.Collections.Generic;

namespace ScoopSearch.Functions.Configuration
{
    public class BucketsOptions
    {
        public const string Key = "Buckets";

        public Uri OfficialBucketsListUrl { get; set; }

        public List<string> GithubBucketsSearchQueries { get; set; }

        public HashSet<Uri> IgnoredBuckets { get; set; }

        public HashSet<Uri> ManualBuckets { get; set; }

    }
}

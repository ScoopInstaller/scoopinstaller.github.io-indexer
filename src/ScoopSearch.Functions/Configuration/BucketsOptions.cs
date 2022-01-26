using System;
using System.Collections.Generic;

namespace ScoopSearch.Functions.Configuration
{
    public class BucketsOptions
    {
        public const string Key = "Buckets";

        public Uri OfficialBucketsListUrl { get; set; }

        public List<Uri> GithubBucketsSearchQueries { get; set; } = new List<Uri>();

        public HashSet<Uri> IgnoredBuckets { get; set; } = new HashSet<Uri>();

        public Uri IgnoredBucketsListUrl { get; set; }

        public HashSet<Uri> ManualBuckets { get; set; } = new HashSet<Uri>();

        public Uri ManualBucketsListUrl { get; set; }
    }
}

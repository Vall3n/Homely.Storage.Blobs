using System;
using System.Collections.Generic;
using System.Text;

namespace Homely.Storage.Blobs
{
    public enum CacheControlType
    {
        None = 0,
        NoStore,
        NoCache
    }

    public static class CacheControlTypeHelper
    {
        public static string CacheControlToString(CacheControlType cacheControlType)
        {
            switch (cacheControlType)
            {
                case CacheControlType.NoStore:
                    return "no-store";
                case CacheControlType.NoCache:
                    return "no-cache";
                default:
                    return string.Empty;
            }
        }

        public static CacheControlType GetCacheControlType(string stringValue)
        {
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                throw new ArgumentNullException(nameof(stringValue));
            }

            if (stringValue.Equals("no-store", StringComparison.OrdinalIgnoreCase))
            {
                return CacheControlType.NoStore;
            }
            else if (stringValue.Equals("no-cache", StringComparison.OrdinalIgnoreCase))
            {
                return CacheControlType.NoCache;
            }

            return CacheControlType.None;
        }
    }
}

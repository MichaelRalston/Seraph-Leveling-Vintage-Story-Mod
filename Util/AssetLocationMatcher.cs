using System;
using System.Linq;
using Vintagestory.API.Common;

namespace SeraphLeveling.Util
{
    public interface IAssetLocationMatcher
    {
        private const string MATCH_ALL = "*";

        public enum MatcherType
        {
            PathPrefix = 0,
            PathExact,
            PathContains,
        }

        public bool Matches(AssetLocation code);

        /// <summary>
        /// Returns a simple asset location matcher with the given pattern and a path-prefix match type.
        /// </summary>
        public static IAssetLocationMatcher Simple(string pattern) => Simple(pattern, MatcherType.PathPrefix);

        public static IAssetLocationMatcher Simple(string pattern, MatcherType matchType)
        {
            return new SimpleInstance(pattern, matchType);
        }

        public static IAssetLocationMatcher And(params IAssetLocationMatcher[] subMatchers)
        {
            return new AndInstance(subMatchers);
        }

        public static IAssetLocationMatcher Or(params IAssetLocationMatcher[] subMatchers)
        {
            return new OrInstance(subMatchers);
        }

        public static IAssetLocationMatcher Any()
        {
            return Simple(MATCH_ALL);
        }

        public static IAssetLocationMatcher Not(IAssetLocationMatcher inner)
        {
            return new NotInstance(inner);
        }

        private record class SimpleInstance(string Pattern, MatcherType MatchType) : IAssetLocationMatcher
        {
            public bool Matches(AssetLocation code)
            {
                if (code == null || !code.Valid)
                {
                    return false;
                }
                else if (Pattern.Equals(MATCH_ALL, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                else
                {
                    return MatchType switch
                    {
                        MatcherType.PathPrefix => code.Path.StartsWith(Pattern, StringComparison.OrdinalIgnoreCase),
                        MatcherType.PathExact => code.Path.Equals(Pattern, StringComparison.OrdinalIgnoreCase),
                        MatcherType.PathContains => code.Path.Contains(Pattern, StringComparison.OrdinalIgnoreCase),
                        _ => false
                    };
                }
            }
        }

        private record class AndInstance(params IAssetLocationMatcher[] SubMatchers) : IAssetLocationMatcher
        {
            public bool Matches(AssetLocation code)
            {
                return SubMatchers.All(sub => sub?.Matches(code) ?? false);
            }
        }

        private record class OrInstance(params IAssetLocationMatcher[] SubMatchers) : IAssetLocationMatcher
        {
            public bool Matches(AssetLocation code)
            {
                return SubMatchers.Any(sub => sub?.Matches(code) ?? false);
            }
        }

        private record class NotInstance(IAssetLocationMatcher Inner) : IAssetLocationMatcher
        {
            public bool Matches(AssetLocation code)
            {
                return !Inner.Matches(code);
            }
        }
    }
}

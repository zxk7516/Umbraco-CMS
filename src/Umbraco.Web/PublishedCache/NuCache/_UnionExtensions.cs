using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Umbraco.Core.Models;

namespace Umbraco.Web.PublishedCache.NuCache
{
    static class UnionExtensions
    {
        // returns everything from source, then everything from other that has not been already returned,
        // either because it was in source, or because it was already in other.
        public static IEnumerable<IPublishedContent> UnionDistinct1(this IEnumerable<IPublishedContent> source, IEnumerable<IPublishedContent> other)
        {
            var ids = new HashSet<int>();
            foreach (var content in source)
            {
                ids.Add(content.Id);
                yield return content;
            }
            foreach (var content in other)
            {
                if (ids.Contains(content.Id)) continue;
                ids.Add(content.Id);
                yield return content;
            }
        }

        // returns everything from source, then everything from other that has not been already returned,
        // because it was in source, assuming it cannot be twice in other - so it's a slightly faster version
        // of UnionDistinct1 for when we know that other will not contain duplicates.
        public static IEnumerable<IPublishedContent> UnionDistinct2(this IEnumerable<IPublishedContent> source, IEnumerable<IPublishedContent> other)
        {
            var ids = new HashSet<int>();
            foreach (var content in source)
            {
                ids.Add(content.Id);
                yield return content;
            }
            foreach (var content in other.Where(content => ids.Contains(content.Id) == false))
                yield return content;
        }

        // returns everything non-null from source, then everything from other that is not in source,
        // including being in source but with a null value.
        public static IEnumerable<IPublishedContent> UnionDistinct(this IEnumerable<KeyValuePair<int, IPublishedContent>> source, IEnumerable<IPublishedContent> other)
        {
            var ids = new HashSet<int>();
            foreach (var kvp in source)
            {
                ids.Add(kvp.Key);
                if (kvp.Value == null) continue;
                yield return kvp.Value;
            }
            foreach (var content in other.Where(content => ids.Contains(content.Id) == false))
                yield return content;
        }
    }
}

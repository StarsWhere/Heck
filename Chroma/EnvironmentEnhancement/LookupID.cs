using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Chroma.EnvironmentEnhancement;

internal static class LookupID
{
    internal static List<GameObjectInfo> Get(
        List<GameObjectInfo> source,
        string[] gameObjectIds,
        string id,
        LookupMethod lookupMethod)
    {
        Func<GameObjectInfo, bool> predicate;
        switch (lookupMethod)
        {
            case LookupMethod.Regex:
                Regex regex = new(id, RegexOptions.CultureInvariant | RegexOptions.ECMAScript | RegexOptions.Compiled);
                predicate = n => regex.IsMatch(n.FullID);
                break;

            case LookupMethod.Exact:
                predicate = n => n.FullID == id;
                break;

            case LookupMethod.Contains:
                predicate = n => n.FullID.Contains(id);
                break;

            case LookupMethod.StartsWith:
                predicate = n => n.FullID.StartsWith(id);
                break;

            case LookupMethod.EndsWith:
                predicate = n => n.FullID.EndsWith(id);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(lookupMethod), "Invalid lookup method.");
        }

        return source.Where(predicate).ToList();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// The JSON shape of Resources/RegionMap.json, authored by AgentScripts/build_region_map.py.
[Serializable]
public class RegionEntry
{
    public string name, group, terrain;
    public float x, y;
    public List<string> adjacent = new();
}
[Serializable]
public class LandAlias { public string land, region; }
[Serializable]
public class RegionMapData
{
    public List<RegionEntry> regions = new();
    // Alternate land cards for one region ("Greenmarch Ruins" is still Greenmarch).
    public List<LandAlias> landAliases = new();
}

/// <summary>
/// Which regions border which. A region is a land card's name; every settlement sits in one. The
/// match reads it for one thing: how far a company travels, as borders crossed on the shortest route.
/// </summary>
public sealed class RegionMap
{
    public const int MinDistance = 1, MaxDistance = 5;
    readonly Dictionary<string, RegionEntry> regions = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, HashSet<string>> borders = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> aliases = new(StringComparer.OrdinalIgnoreCase);

    public RegionMap(RegionMapData data)
    {
        if (data == null) return;
        foreach (var region in data.regions.Where(r => r != null && !string.IsNullOrWhiteSpace(r.name)))
        {
            regions[region.name.Trim()] = region;
            borders[region.name.Trim()] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        // Borders are symmetric however they were written down.
        foreach (var region in regions.Values)
            foreach (var other in region.adjacent.Where(o => !string.IsNullOrWhiteSpace(o) && regions.ContainsKey(o.Trim())))
            { borders[region.name.Trim()].Add(other.Trim()); borders[other.Trim()].Add(region.name.Trim()); }
        foreach (var alias in data.landAliases.Where(a => a != null && !string.IsNullOrWhiteSpace(a.land) && !string.IsNullOrWhiteSpace(a.region)))
            aliases[alias.land.Trim()] = alias.region.Trim();
    }

    public static RegionMap Load()
    {
        var asset = Resources.Load<TextAsset>("RegionMap");
        return asset == null ? null : new RegionMap(JsonUtility.FromJson<RegionMapData>(asset.text));
    }

    public IEnumerable<RegionEntry> Regions => regions.Values;
    public bool Contains(string region) => !string.IsNullOrWhiteSpace(region) && regions.ContainsKey(region.Trim());
    /// <summary>The region a land card stands for: itself, unless it is an alternate card for another.</summary>
    public string RegionOfLand(string landName)
    {
        if (string.IsNullOrWhiteSpace(landName)) return landName;
        return aliases.TryGetValue(landName.Trim(), out var region) ? region : landName.Trim();
    }
    /// <summary>The ground of a region, from its land card. None for an unknown region.</summary>
    public TerrainEnum TerrainOf(string region) =>
        Contains(region) && Enum.TryParse(regions[region.Trim()].terrain, true, out TerrainEnum terrain) ? terrain : TerrainEnum.None;
    public IEnumerable<string> Neighbours(string region) =>
        Contains(region) ? borders[region.Trim()] : Enumerable.Empty<string>();
    public bool AreAdjacent(string a, string b) => Contains(a) && borders[a.Trim()].Contains(b?.Trim() ?? "");

    /// <summary>
    /// The shortest route from one region to another, both ends included. A one-entry list when they
    /// are the same region; null when either is unknown or no route exists.
    /// </summary>
    public List<string> Route(string from, string to)
    {
        if (!Contains(from) || !Contains(to)) return null;
        from = regions[from.Trim()].name; to = regions[to.Trim()].name;
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return new List<string> { from };
        var previous = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [from] = null };
        var queue = new Queue<string>(); queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in borders[current].OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                if (previous.ContainsKey(next)) continue;
                previous[next] = current;
                if (string.Equals(next, to, StringComparison.OrdinalIgnoreCase))
                {
                    var route = new List<string>();
                    for (var step = regions[next].name; step != null; step = previous[step]) route.Add(step);
                    route.Reverse(); return route;
                }
                queue.Enqueue(next);
            }
        }
        return null;
    }

    /// <summary>Borders crossed on the shortest route, or -1 when there is none.</summary>
    public int Distance(string from, string to) { var route = Route(from, to); return route == null ? -1 : route.Count - 1; }

    /// <summary>What a journey is worth: borders crossed, clamped to 1-5. Unknown routes count as the minimum.</summary>
    public static int Journey(int distance) => Math.Clamp(distance < 0 ? MinDistance : distance, MinDistance, MaxDistance);
}

using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Utils;

namespace GearTierColors;

public record GearTierColorsMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.oglok.geartiercolors";
    public string Name { get; init; } = "GearTierColors";
    public string Author { get; init; } = "oglok";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new("1.0.0");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.X");
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; } = "https://github.com/pCTHUWU/GearTierColors";
    public string? License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; }
}

internal sealed class ArmourTier { public int MinClass { get; set; } public string Colour { get; set; } = "default"; public string Label { get; set; } = ""; }
internal sealed class HeadsetTier { public int MinMetres { get; set; } public string Colour { get; set; } = "default"; public string Label { get; set; } = ""; }

internal sealed class Config
{
    public List<ArmourTier> ArmourTiers { get; set; } = new();
    public List<HeadsetTier> HeadsetTiers { get; set; } = new();
    public Dictionary<string, int> HeadsetMetres { get; set; } = new();
    public bool ColourCarriersByBestPlate { get; set; } = true;
    public bool RecolourArmour { get; set; } = true;
    public bool RecolourHelmets { get; set; } = true;
    public bool RecolourRigs { get; set; } = true;
    public bool RecolourPlates { get; set; } = true;
    public bool RecolourHeadsets { get; set; } = true;
}

/// <summary>
/// Paints armour, helmets, rigs, plates and headsets by tier, the way AmmoTierColors paints ammo
/// by penetration - one stat, one colour, so the two mods read as the same visual language.
///
/// Colour is one dimension. Folding weight in as well would make orange mean either "class 5" or
/// "class 6 but heavy", which you cannot tell apart at a glance, so class alone decides it.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.PostLoad)]
public class GearTierColors(
    TemplateTable templateTable,
    ItemHelper itemHelper,
    ISptLogger<GearTierColors> logger) : IOnLoad
{
    // Base classes we care about. Plates are their own class in 0.16.9.5 - armour is plate-based,
    // and the carrier is just the thing that holds them.
    private const string ArmourBase   = "5448e54d4bdc2dcc718b4568";
    private const string RigBase      = "5448e5284bdc2dcb718b4567";
    private const string HeadwearBase = "5a341c4086f77401f2541505";
    private const string PlateBase    = "644120aa86ffbe10ee032b6f";
    private const string HeadsetBase  = "5645bcb74bdc2ded0b8b4578";

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var config = LoadConfig();
        if (config is null)
            return Task.CompletedTask;

        var items = templateTable.Items!;
        var painted = 0;

        foreach (var (id, item) in items)
        {
            if (item.Properties is null || item.Type != "Item")
                continue;

            var colour = ColourFor(id, item, items, config);
            if (colour is null || item.Properties.BackgroundColor == colour)
                continue;

            item.Properties.BackgroundColor = colour;
            painted++;
        }

        logger.Info($"[GearTierColors] recoloured {painted} item(s) by tier");
        return Task.CompletedTask;
    }

    private string? ColourFor(MongoId id, TemplateItem item,
                              Dictionary<MongoId, TemplateItem> items, Config config)
    {
        if (config.RecolourHeadsets && itemHelper.IsOfBaseclass(id, HeadsetBase))
            return HeadsetColour(item, config);

        var isPlate   = itemHelper.IsOfBaseclass(id, PlateBase);
        var isArmour  = itemHelper.IsOfBaseclass(id, ArmourBase);
        var isRig     = itemHelper.IsOfBaseclass(id, RigBase);
        var isHelmet  = itemHelper.IsOfBaseclass(id, HeadwearBase);

        if (isPlate  && !config.RecolourPlates)  return null;
        if (isArmour && !config.RecolourArmour)  return null;
        if (isRig    && !config.RecolourRigs)    return null;
        if (isHelmet && !config.RecolourHelmets) return null;
        if (!isPlate && !isArmour && !isRig && !isHelmet) return null;

        var cls = item.Properties!.ArmorClass ?? 0;

        // A carrier reads armorClass 0 because the plates carry the class. Judged on its own it
        // would go grey, which is exactly backwards for a Slick. Ask what it can accept instead.
        if (cls == 0 && config.ColourCarriersByBestPlate)
            cls = BestPlateClass(item, items);

        return cls <= 0 ? null : TierColour(cls, config.ArmourTiers);
    }

    /// <summary>Highest armour class any plate this item will accept in any of its slots.</summary>
    private static int BestPlateClass(TemplateItem carrier, Dictionary<MongoId, TemplateItem> items)
    {
        var best = 0;
        foreach (var slot in carrier.Properties?.Slots ?? [])
        {
            foreach (var filter in slot.Properties?.Filters ?? [])
            {
                foreach (var candidate in filter.Filter ?? [])
                {
                    if (items.TryGetValue(candidate, out var plate))
                        best = Math.Max(best, plate.Properties?.ArmorClass ?? 0);
                }
            }
        }
        return best;
    }

    private static string? TierColour(int cls, List<ArmourTier> tiers)
    {
        foreach (var t in tiers.OrderByDescending(x => x.MinClass))
            if (cls >= t.MinClass) return t.Colour;
        return null;
    }

    /// <summary>
    /// Headsets are matched on name because the database carries no hearing stat at all - every
    /// one reads AmbientVolume -50 and DryVolume -60, the real differences being compressor and EQ
    /// curves. Longest key first: "ComTac V" is a prefix of "ComTac VI".
    /// </summary>
    private static string? HeadsetColour(TemplateItem item, Config config)
    {
        var name = item.Name ?? "";
        var metres = 0;
        foreach (var (frag, m) in config.HeadsetMetres.OrderByDescending(kv => kv.Key.Length))
        {
            if (name.Contains(frag, StringComparison.OrdinalIgnoreCase)) { metres = m; break; }
        }
        if (metres == 0)
            return null;    // unmeasured: leave it alone rather than imply a rating it never earned

        foreach (var t in config.HeadsetTiers.OrderByDescending(x => x.MinMetres))
            if (metres >= t.MinMetres) return t.Colour;
        return null;
    }

    private Config? LoadConfig()
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "user", "mods", "GearTierColors", "config.json");
        if (!File.Exists(path))
            path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(GearTierColors).Assembly.Location) ?? ".", "config.json");
        try
        {
            return JsonSerializer.Deserialize<Config>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip });
        }
        catch (Exception e)
        {
            // Fail visibly and change nothing. A colour mod is not worth taking the server down for.
            logger.Error($"[GearTierColors] could not read config.json ({e.Message}); no items recoloured");
            return null;
        }
    }
}

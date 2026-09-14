using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// Presentation wording for the rules in CardDisplayEnums. No combat rules are executed here.
public static class CardKeywordGlossary
{
    private static readonly Dictionary<string, string> descriptions = new()
    {
        ["ability:Ranged"] = "Can attack flying units. Attacks first; the defender deals damage back only if it survives.",
        ["ability:Mounted"] = "Can attack on the turn it is played. Gains +1 against tapped units.",
        ["ability:Poisoning"] = "Chance to poison the enemy target for one turn. Poison repeats half the damage dealt when applied, with a minimum of 1.",
        ["ability:Pyromancer"] = "Chance to burn the enemy target for one turn. Burning deals 2 damage next turn. Deals double damage to siege engines, ships and tree guardians.",
        ["ability:Cursing"] = "Chance to curse the enemy target, disabling its shared combat abilities while Cursed.",
        ["ability:Raiding"] = "After a successful attack, steals 1 material of any type from the opponent, once per turn.",
        ["ability:Pikemen"] = "+1 attack and -1 defense against cavalry.",
        ["ability:Shielded"] = "While untapped, must be targeted before unshielded units.",
        ["ability:Encouraging"] = "Chance to give units attacking alongside it Encouraged: +1 attack and +1 defense.",
        ["ability:Discouraging"] = "Chance to give the enemy target Discouraged: -1 attack and -1 defense.",
        ["ability:Berserker"] = "Chance to deal the same damage to another random enemy unit.",
        ["ability:Charging"] = "On its first attack after being played, gains +2 attack and -1 defense.",
        ["ability:Fearsome"] = "Chance to inflict Fear. A frightened unit cannot attack, but can defend.",
        ["ability:Freezing"] = "Chance to freeze the target. Frozen reduces defense by the damage dealt when applied.",
        ["ability:Blessing"] = "Chance to bless the target: roll twice when attacking and use the higher result. Deals double damage to undead and vampires.",
        ["ability:Hidden"] = "Cannot be targeted except by Rangers, who reveal it.",
        ["ability:Slaughter"] = "Chance to inflict Bleeding: a 25% chance to take double damage from an attack, once per turn.",
        ["ability:Flying"] = "Can only be targeted by flying units, siege engines or units with Ranged.",
        ["ability:Sightseeing"] = "Chance to reveal a random opponent card after a successful attack.",
        ["character:Scholar"] = "Receives a bonus when playing artifacts.",
        ["character:Lieutenant"] = "Chance to strengthen units attacking alongside it, giving them +2 attack.",
        ["character:Shieldbearer"] = "Chance to fortify units attacking alongside it, giving them +2 defense.",
        ["character:Ranger"] = "Can attack Hidden units and reveal them.",
        ["character:Wormtongue"] = "+1 to the effectiveness of emissary skills.",
        ["character:Friendly"] = "Receives a bonus when recruiting Factions and Allies.",
        ["character:Cruel"] = "+1 to the effectiveness of agent skills.",
        ["character:Warlock"] = "+1 to the effectiveness of mage spells and skills.",
        ["character:Leader"] = "+1 attack to your Common, Wildman and Skinchanger troops.",
        ["character:EasternerLeader"] = "+1 attack to your Easterner troops. Chance to grant Bonus vs Firstbloods to troops attacking alongside this character.",
        ["character:SouthernerLeader"] = "+1 attack to your Southerner troops. Chance to grant Bonus vs Firstbloods to troops attacking alongside this character.",
        ["character:OrcWarChief"] = "+1 attack to your Orc troops. Chance to grant Bonus vs Dwarves to troops attacking alongside this character.",
        ["character:WarTroll"] = "+1 attack to your Troll troops. Chance to grant Bonus vs Elves to troops attacking alongside this character.",
        ["status:Poisoned"] = "Repeats half the damage dealt when poison was applied, with a minimum of 1 damage.",
        ["status:Burning"] = "Takes 2 damage on the turn after Burning is applied.",
        ["status:Cursed"] = "Loses its shared combat abilities while this effect is active.",
        ["status:Encouraged"] = "Gains +1 attack and +1 defense while active.",
        ["status:Discouraged"] = "Loses 1 attack and 1 defense while active.",
        ["status:Fear"] = "Cannot attack, but can still defend.",
        ["status:Frozen"] = "Defense is reduced by the damage dealt when Frozen was applied.",
        ["status:Blessed"] = "Rolls twice when attacking and uses the higher result.",
        ["status:Bleeding"] = "Has a 25% chance to take double damage from an attack, once per turn.",
        ["status:Strenghtened"] = "Gains +2 attack while active.",
        ["status:Fortified"] = "Gains +2 defense while active.",
        ["status:Halted"] = "Does not untap during the owner's replenish stage.",
    };
    private static readonly Dictionary<string, (string title, string body)> icons = new()
    {
        ["attack"] = ("Attack", "The attack value used when this unit fights."),
        ["defense"] = ("Defense", "The defense value used when this unit withstands an attack."),
        ["commander"] = ("Commander", "Leadership class level. Used to meet commander requirements and command troops."),
        ["agent"] = ("Agent", "Agent class level. Used to meet agent requirements and perform agent skills."),
        ["emmissary"] = ("Emissary", "Diplomacy class level. Used to meet emissary requirements and perform emissary skills."),
        ["mage"] = ("Mage", "Magic class level. Used to meet mage requirements and cast spells or use mage skills."),
        ["leather"] = ("Leather", "Material used to pay card costs. On a land, the number shows the leather it grants."),
        ["mounts"] = ("Mounts", "Material used to pay card costs. On a land, the number shows the mounts it grants."),
        ["timber"] = ("Timber", "Material used to pay card costs. On a land, the number shows the timber it grants."),
        ["iron"] = ("Iron", "Material used to pay card costs. On a land, the number shows the iron it grants."),
        ["steel"] = ("Steel", "Material used to pay card costs. On a land, the number shows the steel it grants."),
        ["mithril"] = ("Mithril", "Material used to pay card costs. On a land, the number shows the mithril it grants."),
        ["gold"] = ("Gold", "Used to pay gold costs. On a land, the number shows the gold it grants."),
        ["joker"] = ("Any material", "A flexible material requirement that can be paid with any material."),
    };

    public static string StatusSprite(StatusEffects effect) => effect switch
    {
        StatusEffects.Poisoned => "poisoned",
        StatusEffects.Burning => "burning",
        StatusEffects.Cursed => "cursed",
        StatusEffects.Encouraged => "encouraged",
        StatusEffects.Discouraged => "discouraging",
        StatusEffects.Fear => "fear",
        StatusEffects.Frozen => "frozen",
        StatusEffects.Blessed => "light",
        StatusEffects.Bleeding => "bleeding",
        StatusEffects.Strenghtened => "strengthened",
        StatusEffects.Fortified => "fortified",
        _ => "error"
    };

    /// <summary>The glyph for a ground, from the terrain sheet the card sprite asset falls back to.</summary>
    public static string TerrainSprite(TerrainEnum ground) => ground switch
    {
        TerrainEnum.Plains => "plains", TerrainEnum.Forest => "forest", TerrainEnum.Hills => "hills",
        TerrainEnum.Mountains => "mountains", TerrainEnum.Marsh => "swamp", TerrainEnum.Desert => "desert",
        TerrainEnum.Coast => "shore", TerrainEnum.Wasteland => "wastelands", _ => "error"
    };

    /// <summary>What an army's "Ambush: [glyph]" line means, as its tooltip tells it.</summary>
    public const string AmbushBody = "This unit can ambush an opponent travelling across regions of this terrain type.";

    /// <summary>The ground's glyph as a sprite tag, for titles and bodies shown in a TMP label wired to the card icon sheet.</summary>
    public static string TerrainGlyph(TerrainEnum ground) => "<sprite name=\"" + TerrainSprite(ground) + "\">";

    /// <summary>
    /// Who fights on each ground: the kinds of army the catalog (Resources/Cards/Meta/ArmyCards.json)
    /// gives that terrain, so a hover says what a company crossing it should expect, and what an army
    /// of that ground rides with. Characters fight anywhere and are not listed.
    /// </summary>
    public static string TerrainHost(TerrainEnum ground) => ground switch
    {
        TerrainEnum.Plains => "the horse-lords and light cavalry of the open country, men-at-arms, spearmen, swordsmen and archers, hounds, chariots, steppe warbands and wainfolk riders, and every siege engine: catapults, trebuchets, mangonels and ladders",
        TerrainEnum.Forest => "elves (archers, scouts, warriors and elk riders), woodmen, hunters and rangers, wolves and war wolves, forest spiders, bears and beasts of the woods, forest orcs and wood trolls, and tree guardians",
        TerrainEnum.Hills => "hillmen and their riders, orcs (war orcs, crossbow orcs, half-orcs), hobgoblins, stone trolls, firstblood rangers, dwarven explorers, tower wardens and wild beasts",
        TerrainEnum.Mountains => "dwarves (axemen, archers, pikeshields, miners, goat riders and siege), cave goblins and goblin sappers, cave and snow trolls, orcs of the high passes, black crows and the Weaver's spawn",
        TerrainEnum.Marsh => "rivermen, bog swarms and the undead",
        TerrainEnum.Desert => "southerners (infantry, cavalry and assassins), camel riders, elephant guard spearmen and war elephants",
        TerrainEnum.Coast => "fleets and ships, corsairs and pirates, mariners, coastal knights and riders, rimefolk, and creatures of the deeps",
        TerrainEnum.Wasteland => "fallen men, war trolls, warbred and half trolls, orc garrisons, torch bearers and wolf-riders, ruin spiders, dragon hunters, and the engines of the ash: the Maw and head catapults",
        _ => "armies of that ground"
    };

    // The ground rule from the region's side: what crossing it exposes a company to (the title
    // carries the glyph).
    private static string TerrainBody(TerrainEnum ground)
        => "The ground of this region. A company travelling through it may be ambushed by characters, who fight anywhere, and by armies that fight on "
            + ground + " ground: " + TerrainHost(ground) + ". The traveller defends under the same rule.";

    // The ground rule from the army's side: where it may fall on the road, and in what company.
    private static string AmbushBodyFor(TerrainEnum ground)
        => AmbushBody + " On " + ground + " ground it fights alongside " + TerrainHost(ground) + ".";

    // The road rule, as the +1 label's tooltip tells it: only the company on the road draws, a random
    // card of the stop's kinds still in its deck. The fallbacks are MatchRules.Claim's.
    private static string TravelRewardBody(int stop)
    {
        var reward = MatchRules.RewardAt(stop);
        string kinds = reward switch
        {
            TravelReward.Land => "a Land",
            TravelReward.EventOrAction => "an Event or an Action",
            TravelReward.Encounter => "an Encounter",
            TravelReward.Army => "an Army",
            _ => "a Character (or another Army when none is left)"
        };
        return "Entering the " + Ordinal(stop) + " region of the road, the travelling company draws " + kinds
            + " at random from its own deck (the top card when it holds none). Only the company on the road draws; "
            + "the other company may then fall on it with characters, and with armies of this ground.";
    }

    private static string Ordinal(int n) => n switch { 1 => "first", 2 => "second", 3 => "third", 4 => "fourth", 5 => "fifth", _ => n + "th" };

    public static string DisplayName(string name)
    {
        if (name == "Strenghtened") return "Strengthened";
        if (name.StartsWith("BonusVersus", StringComparison.Ordinal))
            return "Bonus vs " + name.Substring("BonusVersus".Length).Replace("Halfings", "Halflings").Replace("Undeads", "Undead");
        return Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
    }

    public static bool TryGet(string id, out string title, out string body)
    {
        title = body = string.Empty;
        if (string.IsNullOrEmpty(id)) return false;
        int separator = id.IndexOf(':');
        if (separator < 0) return false;
        string prefix = id.Substring(0, separator), name = id.Substring(separator + 1);
        // "travel:<stop>": what a stop of the road pays, for the +1 labels on the map popups.
        if (prefix == "travel" && int.TryParse(name, out int stop) && stop >= 1 && stop <= RegionMap.MaxDistance)
        {
            title = "Stop " + stop + ": draw 1 " + MatchRules.RewardLabel(MatchRules.RewardAt(stop));
            body = TravelRewardBody(stop);
            return true;
        }
        // "terrain:<ground>" is a land's ground; "ambush:<ground>" is where an army may fall on the road.
        if ((prefix == "terrain" || prefix == "ambush") && Enum.TryParse(name, true, out TerrainEnum ground) && ground != TerrainEnum.None)
        {
            title = (prefix == "ambush" ? "Ambush: " + ground : ground + " ground") + " " + TerrainGlyph(ground);
            body = prefix == "ambush" ? AmbushBodyFor(ground) : TerrainBody(ground);
            return true;
        }
        if (prefix == "icon")
        {
            if (icons.TryGetValue(name, out var icon)) { title = icon.title; body = icon.body; return true; }
            foreach (StatusEffects status in Enum.GetValues(typeof(StatusEffects)))
                if (StatusSprite(status) == name) return TryGet("status:" + status, out title, out body);
            foreach (TerrainEnum terrain in Enum.GetValues(typeof(TerrainEnum)))
                if (terrain != TerrainEnum.None && TerrainSprite(terrain) == name) return TryGet("terrain:" + terrain, out title, out body);
            return false;
        }
        title = DisplayName(name);
        if (descriptions.TryGetValue(id, out body)) return true;
        if (prefix == "ability" && name.StartsWith("BonusVersus", StringComparison.Ordinal)
            && Enum.TryParse<ObjectCharacterArmySpecialAbilityEnum>(name, out var ability)
            && Enum.IsDefined(typeof(ObjectCharacterArmySpecialAbilityEnum), ability))
        {
            body = "Receives a combat bonus against " + title.Substring(9) + ".";
            return true;
        }
        return false;
    }
}

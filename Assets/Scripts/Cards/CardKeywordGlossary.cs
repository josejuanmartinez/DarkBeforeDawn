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
        if (prefix == "icon")
        {
            if (icons.TryGetValue(name, out var icon)) { title = icon.title; body = icon.body; return true; }
            foreach (StatusEffects status in Enum.GetValues(typeof(StatusEffects)))
                if (StatusSprite(status) == name) return TryGet("status:" + status, out title, out body);
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

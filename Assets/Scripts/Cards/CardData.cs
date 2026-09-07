using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

// The card face's data model, carried over from Runeboard's CardData (Assets/Scripts/UI/DeckManager.cs)
// with the gameplay half left behind. Field names and JSON shape are unchanged for every field that
// survives, so decks authored for Runeboard deserialize here as-is; the dropped fields (combat
// effects, encounter outcomes, object bonuses that fed Duel/Army) simply stay at their defaults and
// are ignored, because nothing on the card face reads them.
//
// What is kept is exactly what Card.cs renders: identity and art lookup names, the type/deck badge,
// the description builders, requirement icons, and the non-serialized presentation flags.
[Serializable]
public class CardData
{
    // --- Identity and art lookup -------------------------------------------------------------
    public int cardId;
    public string name;
    public string quote;
    public string actionEffect;
    public string type;
    public List<string> tags = new();
    public string deckId;
    public int alignment;
    public string actionClassName;
    public string action;
    public string spriteName;
    public string region;
    public string description;
    public string requirementsText;
    public string historyText;
    public string portraitName;
    public string characterGroup;
    public string deckSpriteName;

    // --- Character card face ------------------------------------------------------------------
    public int commander;
    public int agent;
    public int emmissary;
    public int mage;
    public RacesEnum race;
    public SexEnum sex = SexEnum.Male;
    public string startingPC = string.Empty;

    // --- Army and Character abilities -----------------------------------------------------------
    public TroopsTypeEnum troopType;
    // Open to both card types: an Army card may carry any number of these, and a Character card may
    // carry them alongside characterAbilities below.
    public List<CharacterAndArmySpecialAbilityEnum> specialAbilities = new();
    // Character-only half of the union. Ignored on every other card type, which is why it lives in
    // its own list rather than widening specialAbilities: the two enums overlap in ordinals, so one
    // list of ints could not say which enum a given value belongs to.
    public List<CharacterOnlySpecialAbilityEnum> characterAbilities = new();
    public int procChance;
    // MTG-style combat line for armies and characters.
    public int attack;
    public int defense;

    // --- Requirement icons (the cost row under the art) ----------------------------------------
    public int commanderSkillRequired;
    public int agentSkillRequired;
    public int emissarySkillRequired;
    public int mageSkillRequired;
    public int difficulty;
    public int leatherRequired;
    public int mountsRequired;
    public int timberRequired;
    public int ironRequired;
    public int steelRequired;
    public int mithrilRequired;
    public int goldRequired;
    public int jokerRequired;

    // --- Land / PC card face --------------------------------------------------------------------
    public int leatherGranted;
    public int mountsGranted;
    public int timberGranted;
    public int ironGranted;
    public int steelGranted;
    public int mithrilGranted;
    public int goldGranted;
    public bool isUnderground;

    // --- Object card face -----------------------------------------------------------------------
    public bool hidden;
    public int copies = 1;
    public int commanderBonus;
    public int agentBonus;
    public int emmissaryBonus;
    public int mageBonus;
    public bool transferable = true;
    public int healPerTurn;
    public int movementBonus;
    public bool ignoreTerrainMovementPenalty;
    public bool grantsHasteAtSea;
    public int autoScoutRadius;
    public int detectionEvasion;
    public int recruitBonusMenAtArms;
    public int scryAreaBonus;
    public int scryObjectBonus;
    public bool grantsEnvironmentalImmunity;

    // --- Situation labels (rendered as "When: Army at enemy PC") -----------------------------------
    public string situation = string.Empty;
    public string situation2 = string.Empty;

    // --- Presentation state, never serialized -------------------------------------------------------
    // encounterTargetHex is gone with the hex board; encounterRevealed survives on its own because
    // the reveal is a pure animation the card face owns (see Card.RevealEncounterCardAsync).
    [NonSerialized] public bool isPlayable = true;
    [NonSerialized] public bool encounterRevealed;
    [NonSerialized] public bool hasShownHandAnimation;
    // Filled in by whatever ICardPlayabilitySource the project installs; the face only reads it to
    // render the red requirement warnings under the description.
    [NonSerialized] public CardPlayabilityResult playability = new();

    public CardData Clone()
    {
        CardData copy = (CardData)MemberwiseClone();
        // MemberwiseClone is shallow: without this the copy shares the original's list instances,
        // so editing one card's tags would silently edit every clone's.
        copy.tags = tags != null ? new List<string>(tags) : new List<string>();
        copy.specialAbilities = specialAbilities != null
            ? new List<CharacterAndArmySpecialAbilityEnum>(specialAbilities)
            : new List<CharacterAndArmySpecialAbilityEnum>();
        copy.characterAbilities = characterAbilities != null
            ? new List<CharacterOnlySpecialAbilityEnum>(characterAbilities)
            : new List<CharacterOnlySpecialAbilityEnum>();
        copy.playability = new CardPlayabilityResult();
        return copy;
    }

    public CardTypeEnum GetCardType() => CardTypeParser.Parse(type);

    public CardSituationEnum GetSituation()
        => Enum.TryParse(situation, true, out CardSituationEnum s) ? s : CardSituationEnum.None;

    public CardSituationEnum GetSecondarySituation()
        => Enum.TryParse(situation2, true, out CardSituationEnum s) ? s : CardSituationEnum.None;

    public bool IsEventCard() => GetCardType() == CardTypeEnum.Event;

    public bool IsEncounterCard() => GetCardType() == CardTypeEnum.Ally;

    public bool HasTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag) || tags == null) return false;
        return tags.Any(t => string.Equals(t?.Trim(), tag.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public bool HasAnyTag(params string[] queryTags)
    {
        if (queryTags == null || queryTags.Length == 0) return false;
        return queryTags.Any(HasTag);
    }

    public string GetActionRef() => !string.IsNullOrWhiteSpace(action) ? action : actionClassName;

    public int GetCharacterPointTotal()
    {
        if (GetCardType() != CardTypeEnum.Character) return 0;
        return Mathf.Max(0, commander) + Mathf.Max(0, agent) + Mathf.Max(0, emmissary) + Mathf.Max(0, mage);
    }

    public int GetAdditionalGoldCost()
        => GetCardType() == CardTypeEnum.Character ? GetCharacterPointTotal() * 5 : 0;

    public int GetTotalGoldCost()
    {
        if (GetCardType() == CardTypeEnum.Character) return GetAdditionalGoldCost();
        return Mathf.Max(0, goldRequired) + GetAdditionalGoldCost();
    }

    // --- Description assembly ----------------------------------------------------------------------

    public string GetRenderedDescription(bool includeFoundingText = false)
    {
        string body = GetDescriptionBody(includeFoundingText);
        string quoteBlock = GetQuoteBlock();

        if (string.IsNullOrWhiteSpace(body)) return quoteBlock;
        if (string.IsNullOrWhiteSpace(quoteBlock)) return body;
        return $"{body}\n\n{quoteBlock}";
    }

    public string GetDescriptionBody(bool includeFoundingText = false)
    {
        CardTypeEnum cardType = GetCardType();
        string body = cardType switch
        {
            CardTypeEnum.Character => GetCharacterDescription(),
            CardTypeEnum.Army => GetArmyDescription(),
            CardTypeEnum.Land => GetLandDescription(),
            CardTypeEnum.PC => PcDescriptionBuilder.BuildBody(this, includeFoundingText),
            CardTypeEnum.Event or CardTypeEnum.Action or CardTypeEnum.Spell or CardTypeEnum.Environmental => GetActionEffectText(),
            CardTypeEnum.Ally => !string.IsNullOrWhiteSpace(description) ? description.Trim() : string.Empty,
            CardTypeEnum.Object => GetObjectDescription(),
            _ => string.Empty
        };

        if (cardType == CardTypeEnum.Character && !string.IsNullOrWhiteSpace(actionEffect))
        {
            string effect = actionEffect.Trim();
            body = string.IsNullOrWhiteSpace(body) ? effect : $"{body}\n\n{effect}";
        }

        return body;
    }

    public string GetQuoteBlock()
    {
        if (string.IsNullOrWhiteSpace(quote)) return string.Empty;

        string text = Regex.Replace(quote.Trim(), "<[^>]+>", string.Empty).Trim();
        if (text.StartsWith("\"", StringComparison.Ordinal) && text.EndsWith("\"", StringComparison.Ordinal) && text.Length >= 2)
        {
            text = text.Substring(1, text.Length - 2).Trim();
        }

        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return $"<align=\"center\"><color=#d3d3d388><i>\"{text}\"</i></color></align>";
    }

    public string GetActionEffectText()
        => string.IsNullOrWhiteSpace(actionEffect) ? string.Empty : actionEffect.Trim();

    public string GetCharacterDescription()
    {
        if (GetCardType() != CardTypeEnum.Character) return string.Empty;

        List<string> lines = new();
        if (!string.IsNullOrWhiteSpace(startingPC)) lines.Add($"Starts at {startingPC}.");

        List<string> classParts = new();
        AppendCharacterLevel(classParts, "commander", commander);
        AppendCharacterLevel(classParts, "agent", agent);
        AppendCharacterLevel(classParts, "emmissary", emmissary);
        AppendCharacterLevel(classParts, "mage", mage);
        if (classParts.Count > 0) lines.Add(string.Join(" ", classParts));

        // A character reads the union of both ability enums: the shared ones it has in common with
        // armies, then the character-only ones. Both go through the same proc-chance suffix armies
        // use, so a card that lists an ability reads the same way whichever face it is on.
        List<string> abilities = GetCharacterAbilityLabels();
        if (abilities.Count > 0) lines.Add($"{string.Join(". ", abilities)}.");

        return lines.Count > 0 ? string.Join(" ", lines) : string.Empty;
    }

    public string GetArmyDescription()
    {
        if (GetCardType() != CardTypeEnum.Army) return string.Empty;

        string raceLabel = FormatRaceLabel(race);
        string troopLabel = GetDefaultTroopName(troopType);
        if (string.IsNullOrWhiteSpace(troopLabel))
        {
            troopLabel = !string.IsNullOrWhiteSpace(name) ? name : string.Empty;
        }

        string spriteTag = $"<sprite name=\"{troopType.ToString().ToLowerInvariant()}\">";
        List<string> abilities = GetArmyAbilityLabels();

        if (string.IsNullOrWhiteSpace(troopLabel))
        {
            if (abilities.Count > 0)
            {
                return !string.IsNullOrWhiteSpace(raceLabel)
                    ? $"{raceLabel}. {string.Join(". ", abilities)}."
                    : string.Join(". ", abilities);
            }
            return raceLabel;
        }

        string baseText = string.IsNullOrWhiteSpace(raceLabel)
            ? $"{troopLabel} {spriteTag}."
            : $"{raceLabel}. {troopLabel} {spriteTag}.";
        return abilities.Count > 0 ? $"{baseText} {string.Join(". ", abilities)}." : baseText;
    }

    public string GetCombatStatsText()
    {
        CardTypeEnum cardType = GetCardType();
        if (cardType != CardTypeEnum.Army && cardType != CardTypeEnum.Character) return string.Empty;
        int a = attack;
        int d = defense;
        if (a <= 0 && d <= 0 && cardType == CardTypeEnum.Army)
        {
            (a, d) = troopType switch
            {
                TroopsTypeEnum.ma => (1, 1), TroopsTypeEnum.ar => (2, 1), TroopsTypeEnum.li => (2, 2),
                TroopsTypeEnum.hi => (3, 4), TroopsTypeEnum.lc => (3, 2), TroopsTypeEnum.hc => (6, 6),
                TroopsTypeEnum.ca => (4, 2), TroopsTypeEnum.ws => (5, 5), _ => (1, 1)
            };
        }
        if (a <= 0 && d <= 0 && cardType == CardTypeEnum.Character)
        {
            int level = Mathf.Clamp(GetCharacterPointTotal(), 1, 6);
            a = d = level;
        }
        return a > 0 || d > 0 ? $"{Mathf.Max(0, a)}/{Mathf.Max(0, d)}" : string.Empty;
    }

    public string GetLandDescription()
    {
        if (GetCardType() != CardTypeEnum.Land) return string.Empty;

        List<string> parts = new();
        if (!string.IsNullOrWhiteSpace(region))
        {
            parts.Add($"{PcDescriptionBuilder.FormatDisplayRegionName(region)}.");
        }

        List<string> grants = new();
        if (leatherGranted > 0) grants.Add($"{leatherGranted}<sprite name=\"leather\">");
        if (timberGranted > 0) grants.Add($"{timberGranted}<sprite name=\"timber\">");
        if (mountsGranted > 0) grants.Add($"{mountsGranted}<sprite name=\"mounts\">");
        if (ironGranted > 0) grants.Add($"{ironGranted}<sprite name=\"iron\">");
        if (steelGranted > 0) grants.Add($"{steelGranted}<sprite name=\"steel\">");
        if (mithrilGranted > 0) grants.Add($"{mithrilGranted}<sprite name=\"mithril\">");
        if (goldGranted > 0) grants.Add($"{goldGranted}<sprite name=\"gold\">");
        if (grants.Count > 0) parts.Add(string.Join(string.Empty, grants));

        if (!string.IsNullOrWhiteSpace(name))
        {
            parts.Add($"Reveals hexes and allows founding PCs originally from {PcDescriptionBuilder.FormatDisplayRegionName(name)}.");
        }

        return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    // Flavor text followed by the mechanical-effect summary, one bullet per line. The effect list is
    // the subset of object fields this project still carries; the combat effects (attack/defense/
    // vs-race bonuses) fed Duel.cs and Army.cs and went with the dropped gameplay half.
    public string GetObjectDescription()
    {
        if (GetCardType() != CardTypeEnum.Object) return string.Empty;

        string flavor = !string.IsNullOrWhiteSpace(description) ? description.Trim() : string.Empty;
        List<string> details = BuildObjectMechanicalDetails();
        string effectsBlock = details.Count > 0 ? string.Join("\n", details.Select(d => $"• {d}")) : string.Empty;

        if (string.IsNullOrWhiteSpace(flavor)) return effectsBlock;
        if (string.IsNullOrWhiteSpace(effectsBlock)) return flavor;
        return $"{flavor}\n\n{effectsBlock}";
    }

    public string GetSpriteString() => !string.IsNullOrEmpty(spriteName) ? spriteName : "artifact";

    public string GetHoverText()
    {
        var sb = new List<string> { $"<sprite name=\"{GetSpriteString()}\">{name}" };
        List<string> details = BuildObjectMechanicalDetails();
        if (details.Count > 0) sb.Add($"<br>{string.Join(", ", details)}");
        return string.Join("", sb);
    }

    private List<string> BuildObjectMechanicalDetails()
    {
        var details = new List<string>();
        if (commanderBonus > 0) details.Add($"+{commanderBonus}<sprite name=\"commander\">");
        if (agentBonus > 0) details.Add($"+{agentBonus}<sprite name=\"agent\">");
        if (emmissaryBonus > 0) details.Add($"+{emmissaryBonus}<sprite name=\"emmissary\">");
        if (mageBonus > 0) details.Add($"+{mageBonus}<sprite name=\"mage\">");

        if (healPerTurn > 0) details.Add($"heals {healPerTurn} each turn");
        if (movementBonus > 0) details.Add($"+{movementBonus} movement");
        if (ignoreTerrainMovementPenalty) details.Add("ignores terrain movement penalties");
        if (grantsHasteAtSea) details.Add("grants Haste at sea");
        if (autoScoutRadius > 0) details.Add($"auto-scouts radius {autoScoutRadius}");
        if (detectionEvasion > 0) details.Add($"+{detectionEvasion * 10}% harder to detect");
        if (recruitBonusMenAtArms > 0) details.Add($"+{recruitBonusMenAtArms} men-at-arms recruited");
        if (scryAreaBonus > 0) details.Add($"+{scryAreaBonus} Scry Area range");
        if (scryObjectBonus > 0) details.Add($"+{scryObjectBonus} Find Object");
        if (grantsEnvironmentalImmunity) details.Add("immune to negative environmental cards");
        if (!transferable) details.Add("non-transferable");
        return details;
    }

    private static void AppendCharacterLevel(List<string> parts, string spriteName, int required)
    {
        if (parts == null || string.IsNullOrWhiteSpace(spriteName) || required <= 0) return;
        parts.Add($"{required}<sprite name=\"{spriteName}\">");
    }

    private static string GetDefaultTroopName(TroopsTypeEnum troopType) => troopType switch
    {
        TroopsTypeEnum.ma => "Men-at-arms",
        TroopsTypeEnum.ar => "Archers",
        TroopsTypeEnum.li => "Light Infantry",
        TroopsTypeEnum.hi => "Heavy Infantry",
        TroopsTypeEnum.lc => "Light Cavalry",
        TroopsTypeEnum.hc => "Heavy Cavalry",
        TroopsTypeEnum.ca => "Catapults",
        TroopsTypeEnum.ws => "Warships",
        _ => string.Empty
    };

    private static string FormatRaceLabel(RacesEnum value)
    {
        string raw = value.ToString();
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(raw.Trim().ToLowerInvariant());
    }

    private List<string> GetArmyAbilityLabels()
    {
        if (specialAbilities == null || specialAbilities.Count == 0) return new List<string>();

        int chance = GetProcChance();
        var labels = new List<string>();
        foreach (CharacterAndArmySpecialAbilityEnum ability in specialAbilities)
        {
            string label = FormatArmyAbilityLabel(ability);
            if (!string.IsNullOrWhiteSpace(label)) labels.Add($"{label} {chance}%");
        }
        return labels;
    }

    // The character face's ability row: the shared enum first (same labels and icons an army would
    // show), then the character-only enum.
    private List<string> GetCharacterAbilityLabels()
    {
        var labels = new List<string>();
        int chance = GetProcChance();

        if (specialAbilities != null)
        {
            foreach (CharacterAndArmySpecialAbilityEnum ability in specialAbilities)
            {
                string label = FormatArmyAbilityLabel(ability);
                if (!string.IsNullOrWhiteSpace(label)) labels.Add($"{label} {chance}%");
            }
        }

        if (characterAbilities != null)
        {
            foreach (CharacterOnlySpecialAbilityEnum ability in characterAbilities)
            {
                string label = FormatCharacterAbilityLabel(ability);
                if (!string.IsNullOrWhiteSpace(label)) labels.Add($"{label} {chance}%");
            }
        }

        return labels;
    }

    private int GetProcChance() => Mathf.Clamp(procChance <= 0 ? 100 : procChance, 1, 100);

    // Same shape as FormatArmyAbilityLabel: a spaced-out name plus an icon. The sprite names are all
    // glyphs that exist in common_spritesheet, so nothing here renders as a missing-sprite box.
    public static string FormatCharacterAbilityLabel(CharacterOnlySpecialAbilityEnum ability)
    {
        string abilityName = ability switch
        {
            CharacterOnlySpecialAbilityEnum.EasternerLeader => "Easterner Leader",
            CharacterOnlySpecialAbilityEnum.SouthernerLeader => "Southerner Leader",
            CharacterOnlySpecialAbilityEnum.OrcWarChief => "Orc War Chief",
            CharacterOnlySpecialAbilityEnum.WarTroll => "War Troll",
            _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
                Regex.Replace(ability.ToString(), "([a-z])([A-Z])", "$1 $2").ToLowerInvariant())
        };

        string spriteName = ability switch
        {
            CharacterOnlySpecialAbilityEnum.Scholar => "book",
            // Lieutenant and Shieldbearer apply Strenghtened / Fortified, so they borrow those icons.
            CharacterOnlySpecialAbilityEnum.Lieutenant => "strengthened",
            CharacterOnlySpecialAbilityEnum.Shieldbearer => "fortified",
            CharacterOnlySpecialAbilityEnum.Ranger => "bow",
            CharacterOnlySpecialAbilityEnum.Wormtongue => "speech",
            CharacterOnlySpecialAbilityEnum.Friendly => "loyalty",
            CharacterOnlySpecialAbilityEnum.Cruel => "dagger",
            CharacterOnlySpecialAbilityEnum.Warlock => "spell",
            CharacterOnlySpecialAbilityEnum.Leader => "banner",
            CharacterOnlySpecialAbilityEnum.EasternerLeader => "scimitar",
            CharacterOnlySpecialAbilityEnum.SouthernerLeader => "sword",
            CharacterOnlySpecialAbilityEnum.OrcWarChief => "axe",
            CharacterOnlySpecialAbilityEnum.WarTroll => "mace",
            _ => "target"
        };

        return $"{abilityName} <sprite name=\"{spriteName}\">";
    }

    public static string FormatArmyAbilityLabel(CharacterAndArmySpecialAbilityEnum ability)
    {
        const string bonusVersusPrefix = "BonusVersus";
        string raw = ability.ToString();
        bool isBonusVersus = raw.StartsWith(bonusVersusPrefix, StringComparison.Ordinal);

        // Every BonusVersusX reads as "Bonus vs X"; the rest are single words or PascalCase that the
        // generic split handles ("Sightseeing", "Pyromancer", ...).
        string abilityName = isBonusVersus
            ? $"Bonus vs {raw.Substring(bonusVersusPrefix.Length)}"
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
                Regex.Replace(raw, "([a-z])([A-Z])", "$1 $2").ToLowerInvariant());

        // Sprite names come from common_spritesheet, which has no per-race icons, so every
        // BonusVersusX shares the generic target icon.
        string spriteName = ability switch
        {
            CharacterAndArmySpecialAbilityEnum.Ranged => "longrange",
            CharacterAndArmySpecialAbilityEnum.Mounted => "mounts",
            CharacterAndArmySpecialAbilityEnum.Poisoning => "poison",
            CharacterAndArmySpecialAbilityEnum.Pyromancer => "fire",
            CharacterAndArmySpecialAbilityEnum.Cursing => "cursed",
            CharacterAndArmySpecialAbilityEnum.Raiding => "raid",
            CharacterAndArmySpecialAbilityEnum.Pikemen => "pikemen",
            CharacterAndArmySpecialAbilityEnum.Shielded => "shielded",
            CharacterAndArmySpecialAbilityEnum.Encouraging => "encouraging",
            CharacterAndArmySpecialAbilityEnum.Discouraging => "discouraging",
            CharacterAndArmySpecialAbilityEnum.Berserker => "berserker",
            CharacterAndArmySpecialAbilityEnum.Charging => "charging",
            CharacterAndArmySpecialAbilityEnum.Fearsome => "fear",
            CharacterAndArmySpecialAbilityEnum.Freezing => "frozen",
            CharacterAndArmySpecialAbilityEnum.Blessing => "light",
            CharacterAndArmySpecialAbilityEnum.Hidden => "hidden",
            CharacterAndArmySpecialAbilityEnum.Slaughter => "bleeding",
            CharacterAndArmySpecialAbilityEnum.Flying => "wing",
            CharacterAndArmySpecialAbilityEnum.Sightseeing => "scout",
            _ => isBonusVersus ? "target" : raw.ToLowerInvariant()
        };

        return $"{abilityName} <sprite name=\"{spriteName}\">";
    }
}

// Why a card can't be played, as the face renders it. Populated by whatever ICardPlayabilitySource
// the project installs; with no source installed every card reads as playable and none of these
// warning lines appear.
[Serializable]
public class CardPlayabilityResult
{
    public bool isPlayable = true;
    public bool failsLevelRequirements;
    public bool failsResourceRequirements;
    public bool failsActionConditions;
    public bool failsAlreadyActioned;
    public bool failsCardHistoryRequirements;
    public bool failsStartingCityRequirement;
    public string cardHistoryReason;
    public string startingCityReason;
    // Free-form lines the source wants shown verbatim under the description (e.g. "Need 3 gold").
    // In Runeboard the card face built these itself by reading the selected Character's skills and
    // the Leader's resource piles; keeping them as strings is what lets the face stop knowing what
    // a skill or a resource is.
    public List<string> messages = new();

    public void Reset()
    {
        isPlayable = true;
        failsLevelRequirements = false;
        failsResourceRequirements = false;
        failsActionConditions = false;
        failsAlreadyActioned = false;
        failsCardHistoryRequirements = false;
        failsStartingCityRequirement = false;
        cardHistoryReason = null;
        startingCityReason = null;
        messages.Clear();
    }
}

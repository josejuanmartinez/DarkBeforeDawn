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
    // Encounter-only: the settlements where the encounter can be investigated. A character has one
    // home (startingPC); an encounter shared by several decks may need one birthplace per deck.
    public List<string> birthplaces = new();

    // --- Army and Character abilities -----------------------------------------------------------
    public TroopsTypeEnum troopType;
    // Open to both card types: an Army card may carry any number of these, and a Character card may
    // carry them alongside characterAbilities below.
    public List<ObjectCharacterArmySpecialAbilityEnum> specialAbilities = new();
    // Character-only half of the union. Ignored on every other card type, which is why it lives in
    // its own list rather than widening specialAbilities: the two enums overlap in ordinals, so one
    // list of ints could not say which enum a given value belongs to.
    public List<CharacterOnlySpecialAbilityEnum> characterAbilities = new();
    public List<StatusEffects> statusEffects = new();
    public int procChance;
    // MTG-style combat line for armies and characters.
    public int attack;
    public int defense;
    // Land and Army: the ground it is / fights on, as a TerrainEnum name (AgentScripts/assign_terrains.py).
    public string terrain = string.Empty;

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
    // PC-only: the kinds of object that can be equipped while this settlement is the destination.
    public List<ObjectTypeEnum> objectTypes = new();
    // PC-only: the side the settlement itself belongs to (0 Free People, 1 Dark Servants, 2 Neutral),
    // derived from which decks list it (AgentScripts/assign_settlements.py). Kept apart from
    // `alignment`, which a reference deck re-stamps with its own side when it deals the card.
    public int settlementAlignment = NeutralAlignment;
    // PC-only: the army card that holds the settlement. A company that is not welcome fights it
    // before it can act there.
    public string dwellers = string.Empty;
    public const int FreePeople = 0, DarkServants = 1, NeutralAlignment = 2;

    // --- Object card face -----------------------------------------------------------------------
    public bool hidden;
    public ObjectTypeEnum objectType;
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
    [NonSerialized] public bool isPlayable = true;
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
            ? new List<ObjectCharacterArmySpecialAbilityEnum>(specialAbilities)
            : new List<ObjectCharacterArmySpecialAbilityEnum>();
        copy.characterAbilities = characterAbilities != null
            ? new List<CharacterOnlySpecialAbilityEnum>(characterAbilities)
            : new List<CharacterOnlySpecialAbilityEnum>();
        copy.playability = new CardPlayabilityResult();
        copy.statusEffects = statusEffects != null ? new List<StatusEffects>(statusEffects) : new();
        copy.birthplaces = birthplaces != null ? new List<string>(birthplaces) : new();
        copy.objectTypes = objectTypes != null ? new List<ObjectTypeEnum>(objectTypes) : new();
        return copy;
    }

    public CardTypeEnum GetCardType() => CardTypeParser.Parse(type);

    public CardSituationEnum GetSituation()
        => Enum.TryParse(situation, true, out CardSituationEnum s) ? s : CardSituationEnum.None;

    public CardSituationEnum GetSecondarySituation()
        => Enum.TryParse(situation2, true, out CardSituationEnum s) ? s : CardSituationEnum.None;

    public bool IsEventCard() => GetCardType() == CardTypeEnum.Event;

    public bool IsEncounterCard() => GetCardType() == CardTypeEnum.Encounter;

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

    // --- Destinations -----------------------------------------------------------------------------
    // Where a card can be played from the hand: a character at its home, an encounter at any of its
    // birthplaces. Empty for every other type.
    public IEnumerable<string> GetBirthplaces()
    {
        CardTypeEnum cardType = GetCardType();
        if (cardType == CardTypeEnum.Character)
            return string.IsNullOrWhiteSpace(startingPC) ? Enumerable.Empty<string>() : new[] { startingPC };
        if (cardType == CardTypeEnum.Encounter)
            return (birthplaces ?? new List<string>()).Where(b => !string.IsNullOrWhiteSpace(b));
        return Enumerable.Empty<string>();
    }

    public TerrainEnum GetTerrain() => Enum.TryParse(terrain, true, out TerrainEnum value) ? value : TerrainEnum.None;
    /// <summary>Whether this unit can fight on the given ground: characters anywhere, armies only on their own terrain.</summary>
    public bool FightsOn(TerrainEnum ground)
    {
        var cardType = GetCardType();
        if (cardType == CardTypeEnum.Character) return true;
        if (cardType != CardTypeEnum.Army) return false;
        var own = GetTerrain();
        return own == TerrainEnum.None || ground == TerrainEnum.None || own == ground;
    }

    public bool IsSettlement() => GetCardType() == CardTypeEnum.PC;
    public bool IsNeutralSettlement() => IsSettlement() && settlementAlignment == NeutralAlignment;
    public static string AlignmentLabel(int alignment) => alignment switch
    {
        FreePeople => "Free People", DarkServants => "Dark Servants", _ => "Neutral"
    };

    public bool IsBornAt(string pcName)
        => GetBirthplaces().Any(b => string.Equals(b.Trim(), pcName?.Trim(), StringComparison.OrdinalIgnoreCase));

    // Whether this settlement trades in the given kind of object.
    public bool EquipsObject(ObjectTypeEnum kind)
        => GetCardType() == CardTypeEnum.PC && kind != ObjectTypeEnum.None && objectTypes != null && objectTypes.Contains(kind);

    // The card types that are played at the active destination rather than anywhere on the field.
    public bool RequiresDestination()
    {
        CardTypeEnum cardType = GetCardType();
        return cardType == CardTypeEnum.Character || cardType == CardTypeEnum.Encounter || cardType == CardTypeEnum.Object;
    }

    public bool CanBePlayedAt(CardData settlement)
    {
        if (settlement == null || settlement.GetCardType() != CardTypeEnum.PC) return false;
        return GetCardType() == CardTypeEnum.Object ? settlement.EquipsObject(objectType) : IsBornAt(settlement.name);
    }

    public static string FormatObjectTypeLabel(ObjectTypeEnum kind) => kind switch
    {
        ObjectTypeEnum.SeeingStone => "Seeing Stones",
        ObjectTypeEnum.Regalia => "Regalia",
        ObjectTypeEnum.Remedy => "Remedies",
        ObjectTypeEnum.None => string.Empty,
        _ => kind + "s"
    };

    // Glyphs from common_spritesheet standing in for each kind on the PC face.
    public static string ObjectTypeSprite(ObjectTypeEnum kind) => kind switch
    {
        ObjectTypeEnum.Weapon => "sword", ObjectTypeEnum.Bow => "bow", ObjectTypeEnum.Armor => "armor",
        ObjectTypeEnum.Ring => "ring", ObjectTypeEnum.Jewel => "jewel", ObjectTypeEnum.SeeingStone => "palantir",
        ObjectTypeEnum.Banner => "banner", ObjectTypeEnum.Regalia => "crown", ObjectTypeEnum.Remedy => "herb",
        ObjectTypeEnum.Tool => "key", ObjectTypeEnum.Mount => "hc", _ => "artifact"
    };

    public static string FormatObjectTypeTag(ObjectTypeEnum kind)
        => kind == ObjectTypeEnum.None ? string.Empty : $"{SpriteTag(ObjectTypeSprite(kind))}{FormatObjectTypeLabel(kind)}";

    public int GetCharacterPointTotal()
    {
        if (GetCardType() != CardTypeEnum.Character) return 0;
        return Mathf.Max(0, commander) + Mathf.Max(0, agent) + Mathf.Max(0, emmissary) + Mathf.Max(0, mage);
    }

    // A character is paid in coin rather than equipped out of stores, so their gold is derived
    // from their skill points instead of being authored on the card; the materials printed
    // alongside are what arming them costs. The curve is a flat base plus one per point rather
    // than a multiplier: Lands are the only source of gold and mana empties every turn, so the
    // deck avatars (the 4-5 point characters) must stay only a little dearer than the 1-2 point
    // rank and file. At five per point an avatar cost 25 and was never affordable.
    public const int CharacterGoldBase = 1;
    public const int GoldPerCharacterPoint = 1;

    public int GetAdditionalGoldCost()
    {
        if (GetCardType() != CardTypeEnum.Character) return 0;
        int points = GetCharacterPointTotal();
        return points > 0 ? CharacterGoldBase + points * GoldPerCharacterPoint : 0;
    }

    // Everything a player pays for out of the mana pool, gold included. Skill requirements are
    // gates, not payments, so they are not counted here.
    public int GetTotalMaterialCost()
        => GetTotalGoldCost() + Mathf.Max(0, jokerRequired) + Mathf.Max(0, leatherRequired) + Mathf.Max(0, mountsRequired)
         + Mathf.Max(0, timberRequired) + Mathf.Max(0, ironRequired) + Mathf.Max(0, steelRequired) + Mathf.Max(0, mithrilRequired);

    // Card types that are spent from hand rather than founded or mustered: every one of them must
    // print a cost, otherwise a free effect card is strictly better than anything on the field.
    public bool IsPaidEffectCard()
    {
        CardTypeEnum cardType = GetCardType();
        return cardType == CardTypeEnum.Event || cardType == CardTypeEnum.Action
            || cardType == CardTypeEnum.Spell || cardType == CardTypeEnum.Object;
    }

    public bool IsMissingCost() => IsPaidEffectCard() && GetTotalMaterialCost() <= 0;

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
            CardTypeEnum.Encounter => GetEncounterDescription(),
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
        // Where it fights: on the road an army only strikes or stands in regions of its own ground.
        if (GetTerrain() != TerrainEnum.None) baseText += $" {GetTerrain()} ground.";
        return abilities.Count > 0 ? $"{baseText} {string.Join(". ", abilities)}." : baseText;
    }

    public string GetCombatStatsText()
    {
        CardTypeEnum cardType = GetCardType();
        if (cardType != CardTypeEnum.Army && cardType != CardTypeEnum.Character) return string.Empty;
        var (a, d) = GetCombatStats();
        return a > 0 || d > 0
            ? $"{Mathf.Max(0, a)}{SpriteTag("attack")} {Mathf.Max(0, d)}{SpriteTag("defense")}"
            : string.Empty;
    }

    public (int attack, int defense) GetCombatStats()
    {
        CardTypeEnum cardType = GetCardType();
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
        return (Mathf.Max(0, a), Mathf.Max(0, d));
    }

    public string GetClassStatsText(bool compact = false)
    {
        if (GetCardType() != CardTypeEnum.Character) return string.Empty;
        var parts = new List<string>();
        AppendCharacterLevel(parts, "commander", commander);
        AppendCharacterLevel(parts, "agent", agent);
        AppendCharacterLevel(parts, "emmissary", emmissary);
        AppendCharacterLevel(parts, "mage", mage);
        if (compact && parts.Count > 2)
            return string.Join(" ", parts.Take(2)) + "\n" + string.Join(" ", parts.Skip(2));
        return string.Join(" ", parts);
    }

    public string GetStatusEffectsText(bool compact = false)
    {
        if (GetCardType() != CardTypeEnum.Character && GetCardType() != CardTypeEnum.Army)
            return string.Empty;
        if (statusEffects == null) return string.Empty;
        var icons = statusEffects.Distinct()
            .Where(effect => Enum.IsDefined(typeof(StatusEffects), effect))
            .Select(effect => $"<link=\"status:{effect}\">{(effect == StatusEffects.Halted ? "Halted" : SpriteTag(CardKeywordGlossary.StatusSprite(effect)))}</link>").ToList();
        if (!compact || icons.Count <= 4) return string.Join(" ", icons);
        var rows = new List<string>();
        for (int i = 0; i < icons.Count; i += 4) rows.Add(string.Join(" ", icons.Skip(i).Take(4)));
        // Sprite-only TMP lines otherwise have no reliable baseline separation.
        return "<line-height=100%>" + string.Join("\n", rows) + "</line-height>";
    }

    // Glyphs are never scaled inline: an inline <size> only enlarges the sprite, so auto-sizing
    // shrinks the base font to compensate and the numbers beside it collapse. Overlays that want
    // bigger resource or combat art raise their own font size instead (see Card.ConfigureOverlay).
    public static string SpriteTag(string spriteName) => $"<sprite name=\"{spriteName}\">";

    public string GetLandDescription()
    {
        if (GetCardType() != CardTypeEnum.Land) return string.Empty;

        List<string> parts = new();
        if (!string.IsNullOrWhiteSpace(region))
        {
            parts.Add($"{PcDescriptionBuilder.FormatDisplayRegionName(region)}.");
        }
        // The ground a company crosses here, and so which armies can fall on it.
        if (GetTerrain() != TerrainEnum.None) parts.Add($"{GetTerrain()} ground.");

        List<string> grants = new();
        if (leatherGranted > 0) grants.Add(leatherGranted + SpriteTag("leather"));
        if (timberGranted > 0) grants.Add(timberGranted + SpriteTag("timber"));
        if (mountsGranted > 0) grants.Add(mountsGranted + SpriteTag("mounts"));
        if (ironGranted > 0) grants.Add(ironGranted + SpriteTag("iron"));
        if (steelGranted > 0) grants.Add(steelGranted + SpriteTag("steel"));
        if (mithrilGranted > 0) grants.Add(mithrilGranted + SpriteTag("mithril"));
        if (goldGranted > 0) grants.Add(goldGranted + SpriteTag("gold"));
        if (grants.Count > 0) parts.Add($"Provides {string.Join(string.Empty, grants)} each turn.");

        // A land is named after its region (the region field itself is blank on land cards).
        if (!string.IsNullOrWhiteSpace(name))
        {
            parts.Add($"Allows travelling to Population Centers from {PcDescriptionBuilder.FormatDisplayRegionName(name)}.");
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
        // The kind leads: it decides which destinations the object can be played at.
        if (objectType != ObjectTypeEnum.None)
            details.Insert(0, $"{FormatObjectTypeTag(objectType)} - equip at a destination that trades in them");
        string effectsBlock = details.Count > 0 ? string.Join("\n", details.Select(d => $"• {d}")) : string.Empty;

        if (string.IsNullOrWhiteSpace(flavor)) return effectsBlock;
        if (string.IsNullOrWhiteSpace(effectsBlock)) return flavor;
        return $"{flavor}\n\n{effectsBlock}";
    }

    // The face says where an encounter can be investigated and nothing of what happens there: the
    // outcome belongs to the play, not to the card text.
    public string GetEncounterDescription()
    {
        if (GetCardType() != CardTypeEnum.Encounter) return string.Empty;
        var homes = GetBirthplaces().Select(PcDescriptionBuilder.FormatDisplayRegionName).ToList();
        if (homes.Count == 0) return "Investigate this encounter at its birthplace.";
        return $"Investigate this encounter at {JoinNames(homes)}.";
    }

    public static string JoinNames(IReadOnlyList<string> names)
    {
        if (names == null || names.Count == 0) return string.Empty;
        if (names.Count == 1) return names[0];
        return string.Join(", ", names.Take(names.Count - 1)) + " or " + names[names.Count - 1];
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
        details.AddRange(GetArmyAbilityLabels());
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
        foreach (ObjectCharacterArmySpecialAbilityEnum ability in specialAbilities)
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
            foreach (ObjectCharacterArmySpecialAbilityEnum ability in specialAbilities)
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

        return $"<link=\"character:{ability}\"><u>{abilityName}</u> <sprite name=\"{spriteName}\"></link>";
    }

    public static string FormatArmyAbilityLabel(ObjectCharacterArmySpecialAbilityEnum ability)
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
            ObjectCharacterArmySpecialAbilityEnum.Ranged => "longrange",
            ObjectCharacterArmySpecialAbilityEnum.Mounted => "mounts",
            ObjectCharacterArmySpecialAbilityEnum.Poisoning => "poison",
            ObjectCharacterArmySpecialAbilityEnum.Pyromancer => "fire",
            ObjectCharacterArmySpecialAbilityEnum.Cursing => "cursed",
            ObjectCharacterArmySpecialAbilityEnum.Raiding => "raid",
            ObjectCharacterArmySpecialAbilityEnum.Pikemen => "pikemen",
            ObjectCharacterArmySpecialAbilityEnum.Shielded => "shielded",
            ObjectCharacterArmySpecialAbilityEnum.Encouraging => "encouraging",
            ObjectCharacterArmySpecialAbilityEnum.Discouraging => "discouraging",
            ObjectCharacterArmySpecialAbilityEnum.Berserker => "berserker",
            ObjectCharacterArmySpecialAbilityEnum.Charging => "charging",
            ObjectCharacterArmySpecialAbilityEnum.Fearsome => "fear",
            ObjectCharacterArmySpecialAbilityEnum.Freezing => "frozen",
            ObjectCharacterArmySpecialAbilityEnum.Blessing => "light",
            ObjectCharacterArmySpecialAbilityEnum.Hidden => "hidden",
            ObjectCharacterArmySpecialAbilityEnum.Slaughter => "bleeding",
            ObjectCharacterArmySpecialAbilityEnum.Flying => "wing",
            ObjectCharacterArmySpecialAbilityEnum.Sightseeing => "scout",
            _ => isBonusVersus ? "target" : raw.ToLowerInvariant()
        };

        return $"<link=\"ability:{ability}\"><u>{abilityName}</u> <sprite name=\"{spriteName}\"></link>";
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

// Run with: unity command eval_file Tests/CardAbilityChecks.cs
var sprites = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_SpriteAsset>("Assets/Art/Fonts/Spritesheets/common_spritesheet.asset");
sprites.UpdateLookupTables();
int cards = 0, abilities = 0;
foreach (var type in new[] { "Object", "Character", "Army" })
{
    var json = System.IO.File.ReadAllText("Assets/Resources/Cards/Meta/" + type + "Cards.json");
    var deck = UnityEngine.JsonUtility.FromJson<DeckData>(json);
    foreach (var card in deck.cards)
    {
        var text = card.GetRenderedDescription();
        foreach (var ability in card.specialAbilities)
        {
            if (!System.Enum.IsDefined(typeof(ObjectCharacterArmySpecialAbilityEnum), ability))
                throw new System.Exception("Invalid shared ability: " + card.name);
            if (!text.Contains(CardData.FormatArmyAbilityLabel(ability)))
                throw new System.Exception("Missing shared ability in full description: " + card.name);
            abilities++;
        }
        if (type != "Character" && card.characterAbilities.Count != 0)
            throw new System.Exception("Character-only ability on " + card.name);
        foreach (var ability in card.characterAbilities)
        {
            if (!System.Enum.IsDefined(typeof(CharacterOnlySpecialAbilityEnum), ability))
                throw new System.Exception("Invalid character ability: " + card.name);
            if (!text.Contains(CardData.FormatCharacterAbilityLabel(ability)))
                throw new System.Exception("Missing character ability in full description: " + card.name);
            abilities++;
        }
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text, "<sprite name=\"([^\"]+)\">"))
            if (sprites.GetSpriteIndexFromName(match.Groups[1].Value) < 0)
                throw new System.Exception("Missing sprite " + match.Groups[1].Value + " on " + card.name);
        if (!string.IsNullOrWhiteSpace(card.quote) && !text.Contains(card.GetQuoteBlock()))
            throw new System.Exception("Lost quote: " + card.name);
        cards++;
    }
}
var emptyObject = new CardData { type = "Object", description = "Flavor", specialAbilities = null };
if (emptyObject.GetObjectDescription() != "Flavor") throw new System.Exception("Empty abilities changed object flavor");
return cards + " cards checked; " + abilities + " abilities appear in full descriptions; all referenced sprites resolve.";

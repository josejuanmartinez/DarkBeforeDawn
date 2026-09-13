// Run in the connected Editor: unity command eval_file Tests/CardKeywordChecks.cs
var sprites = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_SpriteAsset>("Assets/Art/Fonts/Spritesheets/common_spritesheet.asset");
sprites.UpdateLookupTables();
int count = 0;
foreach (ObjectCharacterArmySpecialAbilityEnum ability in System.Enum.GetValues(typeof(ObjectCharacterArmySpecialAbilityEnum)))
{
    if (!CardKeywordGlossary.TryGet("ability:" + ability, out var title, out var body) || string.IsNullOrWhiteSpace(body))
        throw new System.Exception("Missing ability explanation: " + ability);
    if (!CardData.FormatArmyAbilityLabel(ability).Contains("<u>")) throw new System.Exception("Missing underline");
    count++;
}
foreach (CharacterOnlySpecialAbilityEnum ability in System.Enum.GetValues(typeof(CharacterOnlySpecialAbilityEnum)))
{
    if (!CardKeywordGlossary.TryGet("character:" + ability, out _, out _)) throw new System.Exception("Missing character explanation");
    count++;
}
var data = new CardData { name = "Keyword check", type = "Character", commander = 3, agent = 2, emmissary = 1, mage = 4 };
foreach (StatusEffects effect in System.Enum.GetValues(typeof(StatusEffects)))
{
    data.statusEffects.Add(effect);
    if (!CardKeywordGlossary.TryGet("status:" + effect, out _, out _)) throw new System.Exception("Missing status explanation");
    if (sprites.GetSpriteIndexFromName(CardKeywordGlossary.StatusSprite(effect)) < 0) throw new System.Exception("Missing status sprite");
    count++;
}
var clone = data.Clone(); clone.statusEffects.Clear();
if (data.statusEffects.Count != System.Enum.GetValues(typeof(StatusEffects)).Length) throw new System.Exception("Status clone shares its list");
if (CardKeywordGlossary.DisplayName("BonusVersusDragons") != "Bonus vs Dragons") throw new System.Exception("Bad bonus label");
var root = new UnityEngine.GameObject("Keyword checks", typeof(UnityEngine.RectTransform), typeof(UnityEngine.Canvas));
try
{
    root.GetComponent<UnityEngine.Canvas>().renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
    var go = new UnityEngine.GameObject("Text", typeof(UnityEngine.RectTransform), typeof(TMPro.TextMeshProUGUI));
    go.transform.SetParent(root.transform, false);
    var text = go.GetComponent<TMPro.TextMeshProUGUI>();
    text.font = TMPro.TMP_Settings.defaultFontAsset; text.spriteAsset = sprites;
    text.rectTransform.sizeDelta = new UnityEngine.Vector2(800, 200); text.fontSize = 22;
    var samples = new System.Collections.Generic.List<string>();
    foreach (ObjectCharacterArmySpecialAbilityEnum ability in System.Enum.GetValues(typeof(ObjectCharacterArmySpecialAbilityEnum))) samples.Add(CardData.FormatArmyAbilityLabel(ability));
    foreach (CharacterOnlySpecialAbilityEnum ability in System.Enum.GetValues(typeof(CharacterOnlySpecialAbilityEnum))) samples.Add(CardData.FormatCharacterAbilityLabel(ability));
    samples.Add(data.GetClassStatsText()); samples.Add(data.GetCombatStatsText());
    samples.Add(data.GetStatusEffectsText());
    samples.Add("<sprite name=\"leather\"> <sprite name=\"mounts\"> <sprite name=\"timber\"> <sprite name=\"iron\"> <sprite name=\"steel\"> <sprite name=\"mithril\"> <sprite name=\"gold\"> <sprite name=\"joker\">");
    foreach (string sample in samples)
    {
        text.text = sample;
        UnityEngine.Canvas.ForceUpdateCanvases(); text.ForceMeshUpdate();
        for (int i = 0; i < text.textInfo.characterCount; i++)
        {
            var ch = text.textInfo.characterInfo[i];
            if (!ch.isVisible) continue;
            var point = text.transform.TransformPoint((ch.bottomLeft + ch.topRight) * .5f);
            if (!CardKeywordHover.TryResolve(text, point, null, out var id))
            {
                if (ch.elementType == TMPro.TMP_TextElementType.Sprite || sample.StartsWith("<link="))
                    throw new System.Exception("Failed hit detection: " + sample + " at " + i);
            }
            else if (sample.StartsWith("<link="))
            {
                string expected = text.textInfo.linkInfo[0].GetLinkID();
                if (samples.IndexOf(sample) < 48 && id != expected) throw new System.Exception("Incorrect link precedence");
            }
        }
    }
    foreach (string prefab in new[] { "Card", "TokenCard", "TokenCardMasked" })
    {
        var template = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Prefabs/" + prefab + ".prefab");
        var instance = UnityEngine.Object.Instantiate(template, root.transform);
        instance.SetActive(true);
        var card = instance.GetComponent<Card>(); card.TypewriterEffect = false;
        card.Initialize(data.Clone(), prefab != "Card");
        if (prefab == "Card") BoardPresentation.StyleFullCard(card);
        if (card.ClassStatsLabel.text != data.GetClassStatsText(card.IsTokenOnlyPresentation)) throw new System.Exception("Class row missing on " + prefab);
        if (card.StatusEffectsLabel.text != data.GetStatusEffectsText(card.IsTokenOnlyPresentation)) throw new System.Exception("Status row missing on " + prefab);
        if (prefab != "Card") card.CompactTokenInPlace();
        UnityEngine.Canvas.ForceUpdateCanvases();
        foreach (var label in new[] { card.ClassStatsLabel, card.StatusEffectsLabel })
        {
            label.ForceMeshUpdate();
            if (!label.gameObject.activeInHierarchy || label.isTextTruncated) throw new System.Exception("Hidden or truncated row on " + prefab);
            int actual = 0;
            for (int i = 0; i < label.textInfo.characterCount; i++)
                if (label.textInfo.characterInfo[i].elementType == TMPro.TMP_TextElementType.Sprite && label.textInfo.characterInfo[i].isVisible) actual++;
            if (actual != (label == card.ClassStatsLabel ? 4 : 11)) throw new System.Exception("Missing rendered icons on " + prefab);
        }
        card.SetStatusEffects(null);
        if (card.StatusEffectsLabel.gameObject.activeSelf) throw new System.Exception("Cleared statuses still visible");
        card.SetStatusEffects(new[] { StatusEffects.Frozen });
        if (!card.StatusEffectsLabel.gameObject.activeSelf) throw new System.Exception("Applied status hidden");
        foreach (var type in new[] { "Army", "Object" })
        {
            var other = data.Clone(); other.type = type;
            card.Initialize(other, prefab != "Card");
            if (card.ClassStatsLabel.gameObject.activeSelf) throw new System.Exception("Class row on " + type);
            if (card.StatusEffectsLabel.gameObject.activeSelf != (type == "Army")) throw new System.Exception("Wrong status type restriction");
        }
        UnityEngine.Object.DestroyImmediate(instance);
    }
    // The board keeps the token subtree after discarding its Card component.
    var holder = new UnityEngine.GameObject("Board token", typeof(UnityEngine.RectTransform), typeof(BoardCardView));
    holder.transform.SetParent(root.transform, false);
    var view = holder.GetComponent<BoardCardView>();
    typeof(BoardCardView).GetProperty("Data").SetValue(view, data.Clone());
    var tokenTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Prefabs/TokenCard.prefab");
    var sourceToken = UnityEngine.Object.Instantiate(tokenTemplate, root.transform);
    var tokenCard = sourceToken.GetComponent<Card>(); tokenCard.InitializeTokenVisualOnly(data.Clone());
    var visual = tokenCard.CreateTokenVisualClone(holder.transform, out _); visual.SetActive(true);
    visual.AddComponent<CardKeywordHover>().RefreshTargets();
    UnityEngine.Object.DestroyImmediate(sourceToken);
    view.SetStatusEffects(new[] { StatusEffects.Burning });
    var statusLabel = System.Array.Find(visual.GetComponentsInChildren<TMPro.TMP_Text>(true), t => t.name == "StatusEffects");
    if (statusLabel == null || !statusLabel.gameObject.activeInHierarchy || !statusLabel.text.Contains("status:Burning"))
        throw new System.Exception("Cloned token did not update its status");
    view.SetStatusEffects(null);
    if (statusLabel.gameObject.activeSelf) throw new System.Exception("Cloned token did not clear its status");
}
finally { UnityEngine.Object.DestroyImmediate(root); }
return count + " keyword rules checked; TMP word/icon hit detection and class/status rows passed on all three card prefabs.";

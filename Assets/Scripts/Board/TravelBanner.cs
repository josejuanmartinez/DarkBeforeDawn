using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Travel stage on screen: the road as a chain of stops under the stage bar, a company marker
/// that walks from stop to stop, the region and its terrain lit up as it is entered, and who may
/// fall on the company there. Owned by TowerMatchController; shown only while the stage is Travel.
/// </summary>
public sealed class TravelBanner : MonoBehaviour
{
    Board board;
    TowerMatchController match;
    RectTransform panel, marker, strip;
    CanvasGroup fade;
    Image current;
    Text region, terrain, phase, title;
    readonly List<Vector2> nodePositions = new();
    string signature;
    float shownAt;
    Vector2 movementPosition;
    readonly List<(CanvasGroup group, float alpha, bool blocks, bool created)> covered = new();
    bool closing;
    const float NodeSize = 10, MarkerSize = 30, StripHeight = 58;
    BoardSkin Skin => BoardPresentation.SkinFor(transform);
    public bool IsTravelling => panel != null && !closing && match.Rules?.Travel != null && nodePositions.Count > 0 &&
        (Time.unscaledTime - shownAt < 1.1f || Vector2.Distance(movementPosition,
            nodePositions[Mathf.Clamp(match.Rules.Travel.Stop + 1,0,nodePositions.Count-1)]) > 1);

    public void Initialize(Board board, TowerMatchController match) { this.board = board; this.match = match; }

    public static Color TerrainColor(TerrainEnum ground) => ground switch
    {
        TerrainEnum.Plains => new Color(.78f, .70f, .36f), TerrainEnum.Forest => new Color(.36f, .66f, .38f),
        TerrainEnum.Hills => new Color(.68f, .52f, .34f), TerrainEnum.Mountains => new Color(.66f, .70f, .76f),
        TerrainEnum.Marsh => new Color(.36f, .62f, .58f), TerrainEnum.Desert => new Color(.90f, .76f, .46f),
        TerrainEnum.Coast => new Color(.40f, .62f, .86f), TerrainEnum.Wasteland => new Color(.76f, .34f, .28f),
        _ => new Color(.6f, .6f, .6f)
    };

    /// <summary>Called from the controller's HUD refresh: opens, updates or closes with the stage.</summary>
    public void Sync()
    {
        var rules = match.Rules;
        if (rules == null || rules.Stage != MatchStage.Travel || rules.Travel == null) { if (panel != null) closing = true; return; }
        closing = false;
        string now = rules.Active + "|" + rules.Travel.Destination.name + "|" + rules.Travel.Stop + "|" + rules.Phase + "|" + rules.Attacks.Count + "|" + rules.Travel.Stops.Count + "|" + rules.Fights.Count;
        if (panel != null && now == signature) return;
        bool fresh = panel == null || !signature.StartsWith(rules.Active + "|" + rules.Travel.Destination.name + "|", System.StringComparison.Ordinal);
        signature = now;
        Build(fresh);
    }

    void Build(bool fresh)
    {
        var rules = match.Rules; var skin = Skin; var journey = rules.Travel;
        Vector2 markerFrom = marker != null ? marker.anchoredPosition : Vector2.zero;
        float alpha = fade != null && !fresh ? fade.alpha : 0;
        if (panel != null) { panel.gameObject.SetActive(false); Destroy(panel.gameObject); }
        bool ours = rules.Active == 0;
        if (covered.Count == 0)
            foreach (var zone in new CardZoneVisualizer[] { board.opponentLands, board.opponentPopulationCenters })
            {
                if (zone == null) continue;
                var group=zone.transform.parent.GetComponent<CanvasGroup>(); bool created=group==null;
                if(created) group=zone.transform.parent.gameObject.AddComponent<CanvasGroup>();
                covered.Add((group,group.alpha,group.blocksRaycasts,created)); group.alpha=0; group.blocksRaycasts=false;
            }
        var ground = rules.Ground;
        var accent = ours ? skin.colors.teal : skin.colors.gold;
        var background = BoardPresentation.Panel(transform, "Travel banner", skin.colors.ink);
        panel = background.rectTransform;
        panel.anchorMin = panel.anchorMax = new Vector2(.508f, .93f); panel.pivot = new Vector2(.5f, 1);
        int nodes = journey.Stops.Count + 1;
        float width = ((RectTransform)transform).rect.width * .71f;
        float StripInset = width * .14f;
        float height = ((RectTransform)transform).rect.height * .112f;
        panel.sizeDelta = new Vector2(width, height);
        panel.anchoredPosition = Vector2.zero;
        BoardSurface.Dress(background, accent);
        var landscape = CardCatalog.AllCards().FirstOrDefault(c => c.GetCardType() == CardTypeEnum.Land &&
            (c.region == journey.Region || (rules.Map?.RegionOfLand(c.name) ?? c.name) == journey.Region));
        LocationPortrait(panel, landscape ?? journey.Destination, true, width);
        LocationPortrait(panel, journey.Destination, false, width);
        var shadow = background.gameObject.AddComponent<Shadow>(); shadow.effectColor = skin.colors.previewShadow; shadow.effectDistance = skin.preview.shadowOffset;
        fade = panel.gameObject.AddComponent<CanvasGroup>(); fade.alpha = alpha; fade.blocksRaycasts = false;
        if (fresh) shownAt = Time.unscaledTime;

        string who = ours ? "YOUR COMPANY" : "THE ENEMY COMPANY";
        string heading = (journey.Moving ? who + " ON THE ROAD TO " + journey.Destination.name.ToUpperInvariant() : who + " HOLDS AT " + journey.Destination.name.ToUpperInvariant())
            + "   ·   " + (journey.Stop + 1) + " / " + journey.Stops.Count;
        title = BoardPresentation.TextLabel(panel, heading, board.interfaceFont, skin.typography.previewLabelSize + 1, skin.colors.gold, new Vector2(0, 1), Vector2.one, TextAnchor.MiddleCenter);
        title.rectTransform.pivot = new Vector2(.5f, 1); title.rectTransform.sizeDelta = new Vector2(-StripInset * 2, 24);
        title.resizeTextForBestFit = true; title.resizeTextMinSize = 9; title.resizeTextMaxSize = 13;

        // The road: the region the company set out from, then one diamond per region entered.
        strip = new GameObject("Road", typeof(RectTransform)).GetComponent<RectTransform>();
        strip.SetParent(panel, false);
        strip.anchorMin = new Vector2(0, 1); strip.anchorMax = new Vector2(1, 1); strip.pivot = new Vector2(.5f, 1);
        strip.sizeDelta = new Vector2(-StripInset * 2, StripHeight); strip.anchoredPosition = new Vector2(0, -26);
        var labels = new List<string>();
        string origin = journey.Moving ? rules.Players[rules.Active].Destination?.Card.region : null;
        labels.Add(origin != null ? PcDescriptionBuilder.FormatDisplayRegionName(origin) : "SET OUT");
        labels.AddRange(journey.Stops.Select(PcDescriptionBuilder.FormatDisplayRegionName));
        float usable = width - StripInset * 2, step = usable / labels.Count, nodeY = -StripHeight * .45f;
        nodePositions.Clear();
        for (int i = 0; i < labels.Count; i++)
        {
            float x = -usable * .5f + step * (i + .5f);
            nodePositions.Add(new Vector2(x, nodeY));
            int stopIndex = i - 1; // -1 is where the company set out
            bool passed = stopIndex < journey.Stop, here = stopIndex == journey.Stop;
            var stopGround = stopIndex >= 0 ? rules.TerrainOf(journey.Stops[stopIndex]) : TerrainEnum.None;
            var tint = stopIndex >= 0 ? TerrainColor(stopGround) : skin.colors.muted;
            if (i > 0)
            {
                var link = BoardPresentation.Panel(strip, "Road", passed || here ? accent : skin.colors.stackBorder);
                link.rectTransform.anchorMin = link.rectTransform.anchorMax = new Vector2(.5f, 1);
                link.rectTransform.sizeDelta = new Vector2(step - NodeSize - 8, here || passed ? 3 : 2);
                link.rectTransform.anchoredPosition = new Vector2(x - step * .5f, nodeY);
            }
            var node = BoardPresentation.Panel(strip, "Stop", passed || here || i == 0 ? tint : skin.colors.ink);
            node.rectTransform.anchorMin = node.rectTransform.anchorMax = new Vector2(.5f, 1);
            node.rectTransform.sizeDelta = Vector2.one * NodeSize;
            node.rectTransform.anchoredPosition = nodePositions[i];
            node.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            if (!(passed || here || i == 0)) BoardPresentation.Border(node.rectTransform, tint, 2);
            if (here) current = node;
            var label = BoardPresentation.TextLabel(strip, labels[i], board.interfaceFont, skin.typography.previewLabelSize - 2,
                here ? skin.colors.ivory : skin.colors.muted, new Vector2(.5f, 1), new Vector2(.5f, 1), TextAnchor.UpperCenter);
            label.rectTransform.pivot = new Vector2(.5f, 1); label.rectTransform.sizeDelta = new Vector2(step - 4, 18);
            label.rectTransform.anchoredPosition = new Vector2(x, nodeY - NodeSize);
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 7; label.resizeTextMaxSize = skin.typography.previewLabelSize - 2;
            if (stopIndex >= 0 && stopIndex < RegionMap.MaxDistance && !here)
            {
                var reward = BoardPresentation.TextLabel(strip, MatchRules.RewardLabel(MatchRules.RewardAt(stopIndex + 1)).ToUpperInvariant(), board.interfaceFont, skin.typography.previewLabelSize - 3,
                    passed || here ? skin.colors.gold : skin.colors.muted, new Vector2(.5f, 1), new Vector2(.5f, 1), TextAnchor.LowerCenter);
                reward.rectTransform.pivot = new Vector2(.5f, 0); reward.rectTransform.sizeDelta = new Vector2(step - 4, 14);
                reward.rectTransform.anchoredPosition = new Vector2(x, nodeY + NodeSize * .5f + 2);
                reward.resizeTextForBestFit = true; reward.resizeTextMinSize = 6; reward.resizeTextMaxSize = skin.typography.previewLabelSize - 3;
            }
        }
        // The company itself: a bright marker that walks the road as stops are passed.
        var mark = BoardPresentation.Panel(strip, "Company", skin.colors.ink);
        marker = mark.rectTransform;
        marker.anchorMin = marker.anchorMax = new Vector2(.5f, 1);
        marker.sizeDelta = Vector2.one * MarkerSize;
        BoardSurface.Dress(mark, accent, false, true);
        var champion = CardCatalog.FindCardByName(ours ? board.humanAvatarCardName : board.opponentAvatarCardName);
        var portrait = BoardPresentation.Panel(marker, "Travelling champion", Color.white);
        BoardPresentation.Stretch(portrait.rectTransform, Vector2.zero, Vector2.one);
        portrait.rectTransform.offsetMin = Vector2.one * 3; portrait.rectTransform.offsetMax = Vector2.one * -3;
        portrait.sprite = Artwork(champion); portrait.preserveAspect = false;
        int at = journey.Stop + 1;
        marker.anchoredPosition = fresh ? nodePositions[Mathf.Max(0, at - 1)] : markerFrom;
        movementPosition = marker.anchoredPosition;

        // Where the company stands now, and what it faces there.
        region = BoardPresentation.TextLabel(panel, PcDescriptionBuilder.FormatDisplayRegionName(journey.Region).ToUpperInvariant(), board.interfaceFont, skin.typography.previewLabelSize + 3, skin.colors.ivory,
            new Vector2(0, 0), new Vector2(.5f, 0), TextAnchor.MiddleRight);
        BoardPresentation.Stretch(region.rectTransform, Vector2.zero, Vector2.zero);
        region.rectTransform.pivot = Vector2.zero; region.rectTransform.sizeDelta = new Vector2(StripInset-20, 24); region.rectTransform.anchoredPosition = new Vector2(10, 26);
        region.alignment = TextAnchor.MiddleCenter; region.resizeTextForBestFit = true; region.resizeTextMinSize = 8; region.resizeTextMaxSize = 13;
        var badge = BoardPresentation.Panel(panel, "Terrain", accent);
        badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = Vector2.zero; badge.rectTransform.pivot = Vector2.zero;
        badge.rectTransform.sizeDelta = new Vector2(StripInset-40, 18); badge.rectTransform.anchoredPosition = new Vector2(20, 8);
        terrain = BoardPresentation.TextLabel(badge.rectTransform, (ground == TerrainEnum.None ? "UNKNOWN" : ground.ToString().ToUpperInvariant()) + " GROUND", board.interfaceFont, skin.typography.previewLabelSize - 1,
            skin.colors.ink, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
        terrain.resizeTextForBestFit = true; terrain.resizeTextMinSize = 7; terrain.resizeTextMaxSize = skin.typography.previewLabelSize - 1;
        phase = BoardPresentation.TextLabel(panel, PhaseLine(), board.interfaceFont, skin.typography.previewLabelSize, skin.colors.muted, Vector2.zero, Vector2.right, TextAnchor.MiddleCenter);
        phase.rectTransform.pivot = new Vector2(.5f, 0); phase.rectTransform.sizeDelta = new Vector2(-StripInset * 2, 24); phase.rectTransform.anchoredPosition = new Vector2(0, 5);
        phase.resizeTextForBestFit = true; phase.resizeTextMinSize = 8; phase.resizeTextMaxSize = skin.typography.previewLabelSize;
        panel.SetAsLastSibling();
    }

    string PhaseLine()
    {
        var rules = match.Rules;
        bool ours = rules.Active == 0;
        if (rules.Phase == TravelPhase.Attack)
        {
            var raiders = rules.Raiders().Select(u => u.Card.name).Concat(rules.HandRaiders().Select(c => c.name + " (hand)")).Distinct().ToList();
            string who = ours ? "The enemy" : "You";
            if (rules.Attacks.Count > 0) return rules.Attacks.Count + (rules.Attacks.Count == 1 ? " attack declared: " : " attacks declared: ") + string.Join(", ", rules.Attacks.Select(a => a.Attacker.Card.name).Distinct());
            if (raiders.Count == 0) return (ours ? "Nothing of the enemy's" : "Nothing of yours") + " can fight on this ground: the company passes.";
            return who + " may strike here with " + string.Join(", ", raiders.Take(4)) + (raiders.Count > 4 ? " +" + (raiders.Count - 4) : "") + ".";
        }
        return rules.Attacks.Count + (rules.Attacks.Count == 1 ? " attack" : " attacks") + " on the company: " + (ours ? "assign your defenders." : "the enemy assigns defenders.");
    }

    void Update()
    {
        if (panel == null) return;
        if (closing)
        {
            fade.alpha = Mathf.MoveTowards(fade.alpha, 0, Time.unscaledDeltaTime * Skin.preview.fadeSpeed);
            if (fade.alpha <= 0) { Destroy(panel.gameObject); panel = null; marker = null; signature = null; closing = false; RestoreCovered(); }
            return;
        }
        fade.alpha = Mathf.MoveTowards(fade.alpha, 1, Time.unscaledDeltaTime * Skin.preview.fadeSpeed);
        var rules = match.Rules;
        if (rules?.Travel == null || marker == null) return;
        // Walk the marker to the current stop, with a bob, and let the stop it stands on breathe.
        int at = Mathf.Clamp(rules.Travel.Stop + 1, 0, nodePositions.Count - 1);
        var target = nodePositions[at];
        var position = movementPosition = Vector2.MoveTowards(movementPosition, target, Time.unscaledDeltaTime * 140);
        float travelling = Vector2.Distance(position, target) > .5f ? 1 : 0;
        marker.anchoredPosition = position + Vector2.up * (Mathf.Abs(Mathf.Sin(Time.unscaledTime * 9)) * 6 * travelling);
        marker.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.unscaledTime * 3) * 4 * travelling);
        if (current != null) current.rectTransform.localScale = Vector3.one * (1.15f + .25f * Mathf.Sin((Time.unscaledTime - shownAt) * 4));
    }

    void RestoreCovered()
    {
        foreach(var item in covered) if(item.group!=null)
        { item.group.alpha=item.alpha;item.group.blocksRaycasts=item.blocks; }
        covered.Clear();
    }
    void OnDisable() { RestoreCovered(); if (panel != null) { Destroy(panel.gameObject); panel = null; marker = null; signature = null; } }

    public static Sprite Artwork(CardData data)
    {
        if (data == null || CardServices.Art == null) return null;
        foreach (var candidate in new[] { data.spriteName, data.portraitName, data.name })
            if (!string.IsNullOrWhiteSpace(candidate) && CardServices.Art.TryGetSprite(candidate, true, out var sprite)) return sprite;
        return null;
    }
    void LocationPortrait(RectTransform parent, CardData data, bool left, float width)
    {
        var art = BoardPresentation.Panel(parent, left ? "Current landscape" : "Destination landscape", Color.white);
        BoardPresentation.Stretch(art.rectTransform, new Vector2(left ? 0 : 1,0), new Vector2(left ? 0 : 1,1));
        art.rectTransform.pivot = new Vector2(left ? 0 : 1,.5f);
        art.rectTransform.sizeDelta = new Vector2(width*.14f-16,-16);
        art.rectTransform.anchoredPosition = new Vector2(left ? 8 : -8,0);
        art.sprite = Artwork(data);
        var shade = BoardPresentation.Panel(art.transform,"Landscape shade",new Color(.015f,.025f,.03f,.56f));
        BoardPresentation.Stretch(shade.rectTransform,Vector2.zero,Vector2.one);
        if (!left)
        {
            var label = BoardPresentation.TextLabel(shade.transform,"DESTINATION\n" + data.name,board.interfaceFont,12,Skin.colors.ivory,Vector2.zero,Vector2.one,TextAnchor.MiddleCenter);
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 8; label.resizeTextMaxSize = 12;
        }
    }
}

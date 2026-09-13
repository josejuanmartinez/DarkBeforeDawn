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
    bool closing;
    const float Height = 150, NodeSize = 14, MarkerSize = 22, StripHeight = 56, StripInset = 40;
    BoardSkin Skin => BoardPresentation.SkinFor(transform);

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
        var ground = rules.Ground;
        var accent = TerrainColor(ground);
        var background = BoardPresentation.Panel(transform, "Travel banner", skin.colors.ink);
        panel = background.rectTransform;
        panel.anchorMin = new Vector2(.5f, 1); panel.anchorMax = new Vector2(.5f, 1); panel.pivot = new Vector2(.5f, 1);
        int nodes = journey.Stops.Count + 1;
        float width = Mathf.Max(520, nodes * 96 + StripInset * 2);
        panel.sizeDelta = new Vector2(width, Height);
        panel.anchoredPosition = new Vector2(0, -((RectTransform)transform).rect.height * .095f - 6);
        BoardPresentation.Border(panel, accent);
        var shadow = background.gameObject.AddComponent<Shadow>(); shadow.effectColor = skin.colors.previewShadow; shadow.effectDistance = skin.preview.shadowOffset;
        fade = panel.gameObject.AddComponent<CanvasGroup>(); fade.alpha = alpha; fade.blocksRaycasts = false;
        if (fresh) shownAt = Time.unscaledTime;

        string who = ours ? "YOUR COMPANY" : "THE ENEMY COMPANY";
        string heading = (journey.Moving ? who + " ON THE ROAD TO " + journey.Destination.name.ToUpperInvariant() : who + " HOLDS AT " + journey.Destination.name.ToUpperInvariant())
            + "   ·   STOP " + (journey.Stop + 1) + " OF " + journey.Stops.Count;
        title = BoardPresentation.TextLabel(panel, heading, board.interfaceFont, skin.typography.previewLabelSize + 1, skin.colors.gold, new Vector2(0, 1), Vector2.one, TextAnchor.MiddleCenter);
        title.rectTransform.pivot = new Vector2(.5f, 1); title.rectTransform.sizeDelta = new Vector2(-20, 24);

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
                var link = BoardPresentation.Panel(strip, "Road", passed || here ? tint : skin.colors.stackBorder);
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
            if (stopIndex >= 0 && stopIndex < RegionMap.MaxDistance)
            {
                var reward = BoardPresentation.TextLabel(strip, MatchRules.RewardLabel(MatchRules.RewardAt(stopIndex + 1)).ToUpperInvariant(), board.interfaceFont, skin.typography.previewLabelSize - 3,
                    passed || here ? skin.colors.gold : skin.colors.muted, new Vector2(.5f, 1), new Vector2(.5f, 1), TextAnchor.LowerCenter);
                reward.rectTransform.pivot = new Vector2(.5f, 0); reward.rectTransform.sizeDelta = new Vector2(step - 4, 14);
                reward.rectTransform.anchoredPosition = new Vector2(x, nodeY + NodeSize * .5f + 2);
                reward.resizeTextForBestFit = true; reward.resizeTextMinSize = 6; reward.resizeTextMaxSize = skin.typography.previewLabelSize - 3;
            }
        }
        // The company itself: a bright marker that walks the road as stops are passed.
        var mark = BoardPresentation.Panel(strip, "Company", ours ? skin.colors.gold : DestinationPicker.Hostile);
        marker = mark.rectTransform;
        marker.anchorMin = marker.anchorMax = new Vector2(.5f, 1);
        marker.sizeDelta = Vector2.one * MarkerSize;
        marker.localRotation = Quaternion.Euler(0, 0, 45);
        BoardPresentation.Border(marker, skin.colors.ivory, 2);
        int at = journey.Stop + 1;
        marker.anchoredPosition = fresh ? nodePositions[Mathf.Max(0, at - 1)] : markerFrom;

        // Where the company stands now, and what it faces there.
        region = BoardPresentation.TextLabel(panel, PcDescriptionBuilder.FormatDisplayRegionName(journey.Region).ToUpperInvariant(), board.interfaceFont, skin.typography.previewLabelSize + 3, skin.colors.ivory,
            new Vector2(0, 0), new Vector2(.5f, 0), TextAnchor.MiddleRight);
        region.rectTransform.pivot = new Vector2(.5f, 0); region.rectTransform.sizeDelta = new Vector2(-12, 24); region.rectTransform.anchoredPosition = new Vector2(-6, 44);
        var badge = BoardPresentation.Panel(panel, "Terrain", accent);
        badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(.5f, 0); badge.rectTransform.pivot = new Vector2(0, 0);
        badge.rectTransform.sizeDelta = new Vector2(110, 22); badge.rectTransform.anchoredPosition = new Vector2(6, 45);
        terrain = BoardPresentation.TextLabel(badge.rectTransform, (ground == TerrainEnum.None ? "UNKNOWN" : ground.ToString().ToUpperInvariant()) + " GROUND", board.interfaceFont, skin.typography.previewLabelSize - 1,
            skin.colors.ink, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
        terrain.resizeTextForBestFit = true; terrain.resizeTextMinSize = 7; terrain.resizeTextMaxSize = skin.typography.previewLabelSize - 1;
        phase = BoardPresentation.TextLabel(panel, PhaseLine(), board.interfaceFont, skin.typography.previewLabelSize, skin.colors.muted, Vector2.zero, Vector2.right, TextAnchor.MiddleCenter);
        phase.rectTransform.pivot = new Vector2(.5f, 0); phase.rectTransform.sizeDelta = new Vector2(-20, 22); phase.rectTransform.anchoredPosition = new Vector2(0, 21);
        phase.resizeTextForBestFit = true; phase.resizeTextMinSize = 8; phase.resizeTextMaxSize = skin.typography.previewLabelSize;
        // The dice of the last clash, so the numbers behind a wound or a loss are on screen.
        if (rules.Fights.Count > 0)
        {
            var dice = BoardPresentation.TextLabel(panel, string.Join("   ", rules.Fights.Select(f => f.ToString())), board.interfaceFont, skin.typography.previewLabelSize - 1, skin.colors.gold,
                Vector2.zero, Vector2.right, TextAnchor.MiddleCenter);
            dice.rectTransform.pivot = new Vector2(.5f, 0); dice.rectTransform.sizeDelta = new Vector2(-20, 18); dice.rectTransform.anchoredPosition = new Vector2(0, 2);
            dice.resizeTextForBestFit = true; dice.resizeTextMinSize = 7; dice.resizeTextMaxSize = skin.typography.previewLabelSize - 1;
        }
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
            if (fade.alpha <= 0) { Destroy(panel.gameObject); panel = null; marker = null; signature = null; closing = false; }
            return;
        }
        fade.alpha = Mathf.MoveTowards(fade.alpha, 1, Time.unscaledDeltaTime * Skin.preview.fadeSpeed);
        var rules = match.Rules;
        if (rules?.Travel == null || marker == null) return;
        // Walk the marker to the current stop, with a bob, and let the stop it stands on breathe.
        int at = Mathf.Clamp(rules.Travel.Stop + 1, 0, nodePositions.Count - 1);
        var target = nodePositions[at];
        var position = Vector2.MoveTowards(marker.anchoredPosition, target, Time.unscaledDeltaTime * 260);
        float travelling = Vector2.Distance(position, target) > .5f ? 1 : 0;
        marker.anchoredPosition = position + Vector2.up * (Mathf.Abs(Mathf.Sin(Time.unscaledTime * 9)) * 6 * travelling);
        marker.localRotation = Quaternion.Euler(0, 0, 45 + Mathf.Sin(Time.unscaledTime * 3) * 6 * travelling);
        if (current != null) current.rectTransform.localScale = Vector3.one * (1.15f + .25f * Mathf.Sin((Time.unscaledTime - shownAt) * 4));
    }

    void OnDisable() { if (panel != null) { Destroy(panel.gameObject); panel = null; marker = null; signature = null; } }
}

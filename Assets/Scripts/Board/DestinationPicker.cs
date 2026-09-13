using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The Select Destination popup: the settlements the human can travel to, as a pile showing one face
/// at a time with arrows to browse, and a button to travel there or to stay. The pile is ordered by
/// MatchRules.RankedDestinations (most playable hand cards, then own side, then the shortest road)
/// and opens on the best one. The frame
/// says how the town would receive the company (teal: welcome, orange: neutral, red: hostile) -- the
/// face itself already names the side and the dwellers, so the footer only lists what could be
/// played there, warns when the dwellers must be fought first, and draws the route from where the
/// company stands, one stop per region entered with the card each stop draws. Owned and opened by
/// TowerMatchController while its stage is on.
/// </summary>
public sealed class DestinationPicker : MonoBehaviour
{
    public static readonly Color Hostile = new(.86f, .27f, .22f), Neutral = new(.95f, .62f, .2f);
    Board board;
    TowerMatchController match;
    RectTransform panel;
    CanvasGroup fade;
    readonly List<CardData> choices = new();
    int index;
    BoardSkin Skin => BoardPresentation.SkinFor(transform);
    public bool IsOpen => panel != null;
    public CardData Shown => choices.Count > 0 ? choices[Mathf.Clamp(index, 0, choices.Count - 1)] : null;
    // The footer grows to hold the playable line, the fight warning if any, and the route strip above the buttons.
    const float RouteHeight = 58, LineHeight = 22, StripInset = 26, NodeSize = 13, MinStopWidth = 74;

    public void Initialize(Board board, TowerMatchController match) { this.board = board; this.match = match; }

    public static Color StandingColor(Standing standing, BoardSkin skin) => standing switch
    {
        Standing.Hostile => Hostile, Standing.Neutral => Neutral, _ => skin.colors.teal
    };

    /// <summary>Opens on the human's choices, or closes when there is nothing to choose. Rebuilds only when the pile changed.</summary>
    public void Sync(IEnumerable<CardData> candidates)
    {
        // The controller hands over the rules' ranked pile, best town first, or null to close.
        var list = candidates?.ToList() ?? new List<CardData>();
        if (list.Count == 0) { Close(); return; }
        if (IsOpen && list.SequenceEqual(choices)) return;
        choices.Clear(); choices.AddRange(list);
        index = 0;
        Build();
    }

    public void Close()
    {
        choices.Clear();
        if (panel != null) { panel.gameObject.SetActive(false); Destroy(panel.gameObject); }
        panel = null; fade = null;
    }

    public void Browse(int direction)
    {
        if (choices.Count == 0) return;
        index = Mathf.Clamp(index + direction, 0, choices.Count - 1);
        Build();
    }

    void Build()
    {
        if (choices.Count == 0) return;
        bool fresh = panel == null;
        float alpha = fade != null ? fade.alpha : 0;
        if (panel != null) { panel.gameObject.SetActive(false); Destroy(panel.gameObject); }
        var skin = Skin;
        var data = Shown;
        var rules = match.Rules;
        bool current = rules.IsDestination(0, data);
        var standing = rules.StandingAt(0, data);
        var accent = StandingColor(standing, skin);
        var stops = rules.Stops(0, data);
        int journey = rules.Rewards(0, data);
        string origin = rules.Players[0].Destination?.Card.region;

        var background = BoardPresentation.Panel(transform, "Destination picker", skin.colors.ink);
        panel = background.rectTransform;
        panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one * .5f;
        background.raycastTarget = true;
        var shadow = background.gameObject.AddComponent<Shadow>();
        shadow.effectColor = skin.colors.previewShadow; shadow.effectDistance = skin.preview.shadowOffset;
        BoardPresentation.Border(panel, accent);
        fade = panel.gameObject.AddComponent<CanvasGroup>(); fade.alpha = fresh ? 0 : alpha;

        string warning = FightLine(data, standing);
        int lines = warning == null ? 1 : 2;
        float header = skin.preview.header + 6, footer = skin.preview.footer + 40 + RouteHeight + LineHeight * lines, padding = skin.preview.padding + 40;
        var holder = new GameObject("Settlement", typeof(RectTransform));
        holder.transform.SetParent(panel, false);
        var visual = (RectTransform)holder.transform;
        var natural = BoardCardView.BuildVisual(board, data, false, visual, board.humanPopulationCenters);
        BoardCardView.EnablePreviewArtworkMotion(visual);
        visual.sizeDelta = natural;
        var area = ((RectTransform)transform).rect;
        float scale = Mathf.Min(skin.preview.preferredHeight / natural.y, Mathf.Max(1, area.height * .8f - header - footer) / natural.y);
        visual.localScale = Vector3.one * scale;
        visual.anchoredPosition = new Vector2(0, (footer - header) * .5f);
        // The pile: the settlements behind the shown one peek out as stacked edges, the same way the
        // discard and victory piles read on the board.
        for (int i = 1; i <= Mathf.Min(skin.piles.visibleStackEdges, choices.Count - 1); i++)
        {
            var edge = BoardPresentation.Panel(panel, "Stack edge", skin.colors.ink);
            edge.rectTransform.sizeDelta = natural * scale;
            edge.rectTransform.anchoredPosition = visual.anchoredPosition + skin.piles.stackOffset * i * 3;
            BoardPresentation.Border(edge.rectTransform, skin.colors.stackBorder);
            edge.transform.SetSiblingIndex(holder.transform.GetSiblingIndex());
        }
        // Wide enough for the card, and for every stop of the route to carry its own label.
        int nodes = stops.Count + (origin != null ? 1 : 0);
        float width = Mathf.Max(natural.x * scale + padding * 2, nodes * MinStopWidth + StripInset * 2 + 60);
        panel.sizeDelta = new Vector2(width, natural.y * scale + header + footer);

        var title = BoardPresentation.TextLabel(panel, "SELECT DESTINATION  /  " + (index + 1) + " OF " + choices.Count,
            board.interfaceFont, skin.typography.previewLabelSize + 2, accent, Vector2.up, Vector2.one, TextAnchor.MiddleCenter);
        title.rectTransform.pivot = new Vector2(.5f, 1); title.rectTransform.sizeDelta = new Vector2(-skin.preview.labelInset * 2, header);

        float y = skin.preview.footer + 4;
        BuildRoute(stops, origin, journey, accent, y, width);
        y += RouteHeight;
        Line(PlayableLine(data), skin.colors.muted, y); y += LineHeight;
        if (warning != null) Line(warning, accent, y);

        var previous = Arrow("Previous", "<", -1);
        var next = Arrow("Next", ">", 1);
        previous.interactable = index > 0;
        next.interactable = index < choices.Count - 1;

        float buttonY = skin.preview.footer * .5f;
        // The journey's draw as a count and the card icon, so the button reads "TRAVEL HERE · 5 [card]".
        string travelLabel = current ? "CURRENT" : "TRAVEL HERE  ·  " + journey + " <sprite name=\"card\">";
        var travel = MakeButton("Travel", travelLabel, new Vector2(.5f, 0), new Vector2(-100, buttonY), new Vector2(190, skin.preview.footer - 8), rich: true);
        travel.interactable = !current;
        var chosen = data;
        travel.onClick.AddListener(() => match.Travel(chosen));
        var destination = rules.Players[0].Destination;
        var stay = MakeButton("Stay", destination == null ? "SKIP" : "STAY AT " + destination.Card.name.ToUpperInvariant(),
            new Vector2(.5f, 0), new Vector2(100, buttonY), new Vector2(190, skin.preview.footer - 8));
        stay.onClick.AddListener(match.Advance);
        panel.SetAsLastSibling();
    }

    // The face already says whose town it is and who dwells there; the footer only adds what the
    // face cannot: that the dwellers stand between the company and acting here.
    string FightLine(CardData data, Standing standing)
    {
        var dwellers = match.Rules.Dwellers(data);
        string guard = dwellers != null ? dwellers.name + " (" + dwellers.GetCombatStats().attack + "/" + dwellers.GetCombatStats().defense + ")" : "the dwellers";
        return standing switch
        {
            Standing.Hostile => "Hostile: beat " + guard + " in a normal attack to act here",
            Standing.Neutral => "Neutral: retention attack on " + guard + " to act here",
            _ => null
        };
    }

    // What the trip is for: the hand cards this town lets the player play, two by name.
    public static string PlayableSummary(IEnumerable<CardData> playable)
    {
        var names = playable.Select(c => c.name).Distinct().ToList();
        if (names.Count == 0) return "Nothing in hand can be played here.";
        return "Allows you playing " + string.Join(", ", names.Take(2)) + (names.Count > 2 ? ", among others" : "") + ".";
    }

    string PlayableLine(CardData data) => PlayableSummary(match.Rules.PlayableAt(0, data));

    // The journey as a chain of stops: a diamond per region entered, joined by a line, the region
    // beneath and the card the stop draws above. Stops past the fifth draw nothing and say so.
    void BuildRoute(List<string> stops, string origin, int journey, Color accent, float bottom, float width)
    {
        var skin = Skin;
        var strip = new GameObject("Route", typeof(RectTransform)).GetComponent<RectTransform>();
        strip.SetParent(panel, false);
        strip.anchorMin = Vector2.zero; strip.anchorMax = Vector2.right; strip.pivot = new Vector2(.5f, 0);
        strip.sizeDelta = new Vector2(-StripInset * 2, RouteHeight); strip.anchoredPosition = new Vector2(0, bottom);
        // Each stop is tinted by its ground: that is what decides which armies can fall on the company there.
        var labels = new List<(string above, string below, bool filled, Color tint)>();
        if (origin != null) labels.Add(("HERE", PcDescriptionBuilder.FormatDisplayRegionName(origin), true, skin.colors.muted));
        for (int i = 0; i < stops.Count; i++)
        {
            int stop = i + 1;
            string reward = stop <= journey ? MatchRules.RewardLabel(MatchRules.RewardAt(stop)).ToUpperInvariant() : "—";
            var ground = match.Rules.TerrainOf(stops[i]);
            string below = PcDescriptionBuilder.FormatDisplayRegionName(stops[i]) + (ground != TerrainEnum.None ? "\n" + ground : "");
            labels.Add((reward, below, i == stops.Count - 1, TravelBanner.TerrainColor(ground)));
        }
        float usable = width - StripInset * 2;
        float step = labels.Count > 1 ? usable / labels.Count : 0;
        float nodeY = RouteHeight * .5f;
        float FirstX() => labels.Count > 1 ? -usable * .5f + step * .5f : 0;
        for (int i = 0; i < labels.Count; i++)
        {
            float x = FirstX() + step * i;
            if (i > 0)
            {
                var link = BoardPresentation.Panel(strip, "Link", accent);
                link.rectTransform.anchorMin = link.rectTransform.anchorMax = new Vector2(.5f, 0);
                link.rectTransform.sizeDelta = new Vector2(step - NodeSize - 6, 2);
                link.rectTransform.anchoredPosition = new Vector2(x - step * .5f, nodeY);
            }
            var node = BoardPresentation.Panel(strip, "Stop", labels[i].filled ? labels[i].tint : skin.colors.ink);
            node.rectTransform.anchorMin = node.rectTransform.anchorMax = new Vector2(.5f, 0);
            node.rectTransform.sizeDelta = Vector2.one * NodeSize;
            node.rectTransform.anchoredPosition = new Vector2(x, nodeY);
            node.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            if (!labels[i].filled) BoardPresentation.Border(node.rectTransform, labels[i].tint, 2);
            var above = BoardPresentation.TextLabel(strip, labels[i].above, board.interfaceFont, skin.typography.previewLabelSize - 2,
                labels[i].above == "—" ? skin.colors.muted : skin.colors.ivory, new Vector2(.5f, 0), new Vector2(.5f, 0), TextAnchor.LowerCenter);
            above.rectTransform.pivot = new Vector2(.5f, 0); above.rectTransform.sizeDelta = new Vector2(step > 0 ? step : usable, 18);
            above.rectTransform.anchoredPosition = new Vector2(x, nodeY + NodeSize);
            above.resizeTextForBestFit = true; above.resizeTextMinSize = 7; above.resizeTextMaxSize = skin.typography.previewLabelSize - 2;
            var below = BoardPresentation.TextLabel(strip, labels[i].below, board.interfaceFont, skin.typography.previewLabelSize - 2, skin.colors.muted,
                new Vector2(.5f, 0), new Vector2(.5f, 0), TextAnchor.UpperCenter);
            below.rectTransform.pivot = new Vector2(.5f, 1); below.rectTransform.sizeDelta = new Vector2(step > 0 ? step : usable, 28);
            below.rectTransform.anchoredPosition = new Vector2(x, nodeY - NodeSize);
            below.resizeTextForBestFit = true; below.resizeTextMinSize = 7; below.resizeTextMaxSize = skin.typography.previewLabelSize - 2;
        }
    }

    Text Line(string text, Color color, float bottom)
    {
        var skin = Skin;
        var label = BoardPresentation.TextLabel(panel, text, board.interfaceFont, skin.typography.previewLabelSize, color,
            Vector2.zero, Vector2.right, TextAnchor.MiddleCenter);
        label.rectTransform.pivot = new Vector2(.5f, 0); label.rectTransform.sizeDelta = new Vector2(-skin.preview.statusInset, LineHeight);
        label.rectTransform.anchoredPosition = new Vector2(0, bottom);
        label.resizeTextForBestFit = true; label.resizeTextMinSize = 8; label.resizeTextMaxSize = skin.typography.previewLabelSize;
        return label;
    }

    Button Arrow(string name, string label, int direction)
    {
        var button = MakeButton(name, label, new Vector2(direction < 0 ? 0 : 1, .5f),
            new Vector2(direction < 0 ? Skin.preview.arrowInset.x : -Skin.preview.arrowInset.x, 0), Skin.preview.arrowSize);
        button.onClick.AddListener(() => Browse(direction));
        return button;
    }

    /// <summary>A footer button. A rich label is TextMeshPro with the card icon sheet, so it can carry sprite tags.</summary>
    Button MakeButton(string name, string label, Vector2 anchor, Vector2 position, Vector2 size, bool rich = false)
    {
        var skin = Skin;
        var image = BoardPresentation.Panel(panel, name, skin.colors.button);
        image.raycastTarget = true;
        var rect = image.rectTransform; rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = Vector2.one * .5f; rect.sizeDelta = size; rect.anchoredPosition = position;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = skin.colors.gold;
        colors.pressedColor = skin.colors.teal; colors.disabledColor = skin.colors.disabledButton; button.colors = colors;
        if (rich) { RichLabel(rect, label, skin.typography.buttonSize - 4, skin.colors.ivory); return button; }
        var text = BoardPresentation.TextLabel(rect, label, board.interfaceFont, skin.typography.buttonSize - 4, skin.colors.ivory,
            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
        text.resizeTextForBestFit = true; text.resizeTextMinSize = 9; text.resizeTextMaxSize = skin.typography.buttonSize - 4;
        return button;
    }

    TextMeshProUGUI RichLabel(RectTransform parent, string label, int size, Color color)
    {
        var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false);
        text.font = BoardPresentation.ReadingFontFor(Skin);
        text.spriteAsset = BoardPresentation.SpriteAssetFor(board);
        text.text = label; text.color = color; text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.NoWrap;
        text.enableAutoSizing = true; text.fontSizeMin = 9; text.fontSizeMax = size;
        var rect = text.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        return text;
    }

    void Update()
    {
        if (!IsOpen) return;
        if (fade != null) fade.alpha = Mathf.MoveTowards(fade.alpha, 1, Time.unscaledDeltaTime * Skin.preview.fadeSpeed);
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.leftArrowKey.wasPressedThisFrame) Browse(-1);
        else if (keyboard.rightArrowKey.wasPressedThisFrame) Browse(1);
    }

    void OnDisable() { Close(); }
}

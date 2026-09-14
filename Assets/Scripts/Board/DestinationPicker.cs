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
/// played there and warns when the dwellers must be fought first. Beside the face, the map of
/// Caldrath shows the road from where the company stands, one stop per region entered with the card
/// each stop draws, and rings every other town on offer: clicking one browses to it. Owned and opened
/// by TowerMatchController while its stage is on.
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
    // The footer grows to hold the playable line and the fight warning if any above the buttons.
    const float LineHeight = 22, NodeSize = 14, MapGap = 26;

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
        if (panel != null) { BoardCardPreview.UnregisterModal(panel); panel.gameObject.SetActive(false); Destroy(panel.gameObject); }
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
        string origin = rules.Players[0].Destination?.Card.region;

        var background = BoardPresentation.Panel(transform, "Destination picker", skin.colors.ink);
        panel = background.rectTransform;
        panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one * .5f;
        background.raycastTarget = true;
        var shadow = background.gameObject.AddComponent<Shadow>();
        shadow.effectColor = skin.colors.previewShadow; shadow.effectDistance = skin.preview.shadowOffset;
        BoardPresentation.Border(panel, accent);
        fade = panel.gameObject.AddComponent<CanvasGroup>(); fade.alpha = fresh ? 0 : alpha;
        BoardCardPreview.RegisterModal(panel);
        board.preview?.Hide();

        string warning = FightLine(data, standing);
        int lines = warning == null ? 1 : 2;
        float header = skin.preview.header + 6, footer = skin.preview.footer + 24 + LineHeight * lines, padding = skin.preview.padding + 40;
        var holder = new GameObject("Settlement", typeof(RectTransform));
        holder.transform.SetParent(panel, false);
        var visual = (RectTransform)holder.transform;
        var natural = BoardCardView.BuildVisual(board, data, false, visual, board.humanPopulationCenters);
        BoardCardView.EnablePreviewArtworkMotion(visual);
        visual.sizeDelta = natural;
        var area = ((RectTransform)transform).rect;
        float scale = Mathf.Min(skin.preview.preferredHeight / natural.y, Mathf.Max(1, area.height * .8f - header - footer) / natural.y);
        visual.localScale = Vector3.one * scale;
        // The map takes the face's height as its side and stands to its left, the pair centred in the popup.
        float side = natural.y * scale, cardWidth = natural.x * scale, left = -(side + MapGap + cardWidth) * .5f;
        float mapX = left + side * .5f, cardX = left + side + MapGap + cardWidth * .5f;
        visual.anchoredPosition = new Vector2(cardX, (footer - header) * .5f);
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
        float width = cardWidth + side + MapGap + padding * 2;
        panel.sizeDelta = new Vector2(width, side + header + footer);
        BuildMap(data, origin, accent, side, mapX, visual.anchoredPosition.y);

        var title = BoardPresentation.TextLabel(panel, "SELECT DESTINATION  /  " + (index + 1) + " OF " + choices.Count,
            board.interfaceFont, skin.typography.previewLabelSize + 2, accent, Vector2.up, Vector2.one, TextAnchor.MiddleCenter);
        title.rectTransform.pivot = new Vector2(.5f, 1); title.rectTransform.sizeDelta = new Vector2(-skin.preview.labelInset * 2, header);

        float y = skin.preview.footer + 4;
        Line(PlayableLine(data), skin.colors.muted, y); y += LineHeight;
        if (warning != null) Line(warning, accent, y);

        var previous = Arrow("Previous", "<", -1);
        var next = Arrow("Next", ">", 1);
        previous.interactable = index > 0;
        next.interactable = index < choices.Count - 1;

        float buttonY = skin.preview.footer * .5f;
        int journey = rules.Rewards(0, data);
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

    // The journey on the map of Caldrath: the road from where the company stands, a diamond per region
    // entered tinted by its ground (that is what decides which armies can fall on the company there),
    // the card each stop draws beside it, and a ring on every other town on offer, clickable to browse.
    // The map is framed on the company and everything it could travel to, so it holds still while browsing.
    void BuildMap(CardData data, string origin, Color accent, float side, float x, float y)
    {
        var skin = Skin;
        var rules = match.Rules;
        var map = new CaldrathMapView(panel, rules.Map, Vector2.one * side, accent);
        map.Viewport.anchorMin = map.Viewport.anchorMax = map.Viewport.pivot = Vector2.one * .5f;
        map.Viewport.anchoredPosition = new Vector2(x, y);
        var interest = choices.Select(c => c.region).ToList();
        if (origin != null) interest.Add(origin);
        map.Frame(interest);
        var stops = rules.Stops(0, data);
        int journey = rules.Rewards(0, data);
        // Every other town on offer: a ring on its region, and a click browses to it.
        foreach (var region in choices.Select(c => c.region).Distinct())
        {
            if (region == data.region || !map.TryLocate(region, out var at)) continue;
            map.Halo(at, new Color(skin.colors.gold.r, skin.colors.gold.g, skin.colors.gold.b, .8f), NodeSize + 6);
            var target = region;
            map.Hotspot("Choose " + region, at, NodeSize * 2.4f, () => BrowseTo(target));
        }
        // The road itself, a leg per border crossed.
        var road = new List<(string region, Vector2 at)>();
        if (origin != null && origin != data.region && map.TryLocate(origin, out var from)) road.Add((origin, from));
        foreach (var stop in stops) if (map.TryLocate(stop, out var at)) road.Add((stop, at));
        for (int i = 1; i < road.Count; i++) map.Link(road[i - 1].at, road[i].at, accent, 3, NodeSize * .5f);
        if (origin != null && map.TryLocate(origin, out var here))
        {
            map.Node("Here", here, skin.colors.muted, true, NodeSize);
            // Staying put: the town's own name below the node says it all.
            if (origin != data.region) Caption(map.Overlay, "HERE", here + Vector2.down * (NodeSize + 2), skin.colors.ivory, skin.typography.previewLabelSize - 2, true);
        }
        for (int i = 0; i < stops.Count; i++)
        {
            if (!map.TryLocate(stops[i], out var at)) continue;
            bool last = i == stops.Count - 1;
            var tint = TravelBanner.TerrainColor(rules.TerrainOf(stops[i]));
            if (last) map.Halo(at, accent, NodeSize * 2.2f).gameObject.AddComponent<Pulse>();
            map.Node("Stop", at, tint, true, NodeSize);
            // The stop's pay reads "+1 [glyph]" beside the node; stops past the fifth draw nothing.
            int stop = i + 1;
            string pay = stop <= journey ? "+1 <sprite name=\"" + TravelBanner.RewardSprite(MatchRules.RewardAt(stop)) + "\">" : "—";
            var label = TravelBanner.RichLabel(map.Overlay, pay, board, skin.typography.previewLabelSize, stop <= journey ? skin.colors.ivory : skin.colors.muted);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = Vector2.one * .5f;
            label.rectTransform.pivot = new Vector2(.5f, 0); label.rectTransform.sizeDelta = new Vector2(Mathf.Max(30, label.preferredWidth + 8), 18);
            label.rectTransform.anchoredPosition = at + Vector2.up * (NodeSize * .5f + 2);
            CaldrathMapView.Slip(label.rectTransform);
        }
        // The destination named on the map too, so the route reads without looking back at the face.
        if (map.TryLocate(data.region, out var end))
            Caption(map.Overlay, data.name.ToUpperInvariant(), end + Vector2.down * (NodeSize + 2), accent, skin.typography.previewLabelSize - 1, true);
        var legend = BoardPresentation.TextLabel(map.Viewport, journey + (journey == 1 ? " STOP" : " STOPS") + "  ·  " + journey + (journey == 1 ? " CARD" : " CARDS"),
            board.interfaceFont, skin.typography.previewLabelSize - 1, skin.colors.ivory, Vector2.zero, Vector2.right, TextAnchor.MiddleCenter);
        legend.rectTransform.anchorMin = legend.rectTransform.anchorMax = new Vector2(.5f, 0);
        legend.rectTransform.pivot = new Vector2(.5f, 0); legend.rectTransform.sizeDelta = new Vector2(legend.preferredWidth + 16, 20); legend.rectTransform.anchoredPosition = new Vector2(0, 4);
        CaldrathMapView.Slip(legend.rectTransform);
    }

    // A name on the map: small caps on a dark slip so it reads over the parchment.
    Text Caption(Transform parent, string text, Vector2 at, Color color, int size, bool below)
    {
        var label = BoardPresentation.TextLabel(parent, text, board.interfaceFont, size, color, Vector2.one * .5f, Vector2.one * .5f, TextAnchor.MiddleCenter);
        label.fontStyle = FontStyle.Bold; label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.rectTransform.pivot = new Vector2(.5f, below ? 1 : 0); label.rectTransform.sizeDelta = new Vector2(label.preferredWidth + 10, 18);
        label.rectTransform.anchoredPosition = at;
        CaldrathMapView.Slip(label.rectTransform);
        return label;
    }

    /// <summary>Breathes a ring: the town the road leads to.</summary>
    sealed class Pulse : MonoBehaviour
    {
        void Update() { transform.localScale = Vector3.one * (1 + .12f * Mathf.Sin(Time.unscaledTime * 4)); transform.localRotation = Quaternion.Euler(0, 0, 45); }
    }

    /// <summary>Browses to the first town of a region other than the one shown, for a click on the map.</summary>
    void BrowseTo(string region)
    {
        int found = choices.FindIndex(c => c.region == region && c != Shown);
        if (found < 0) return;
        index = found;
        Build();
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

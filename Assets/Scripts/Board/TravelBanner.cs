using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Travel stage on screen, as a popup over the middle of the board: the map of Caldrath framed on
/// the road, a company marker that walks it from stop to stop, the region and its terrain lit up as it
/// is entered, and — since the popup covers the army lanes — the units themselves as chips: who may
/// fall on the company, who may stand, what has been committed. Chips are clickable for whatever the
/// human may do at this stop. Owned by TowerMatchController; shown only while the stage is Travel.
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
    bool closing;
    const float NodeSize = 14, MarkerSize = 34, TitleHeight = 28, GroundHeight = 64;
    BoardSkin Skin => BoardPresentation.SkinFor(transform);
    public bool IsTravelling => panel != null && !closing && match.Rules?.Travel != null && nodePositions.Count > 0 &&
        (Time.unscaledTime - shownAt < 1.1f || Vector2.Distance(movementPosition,
            nodePositions[Mathf.Clamp(match.Rules.Travel.Stop + 1,0,nodePositions.Count-1)]) > 1);
    /// <summary>The popup is up and covering the board.</summary>
    public bool IsOpen => panel != null && !closing;

    public void Initialize(Board board, TowerMatchController match) { this.board = board; this.match = match; }

    /// <summary>The icon-sheet glyph for what a stop draws.</summary>
    public static string RewardSprite(TravelReward reward) => reward switch
    {
        TravelReward.Land => "map", TravelReward.EventOrAction => "action", TravelReward.Encounter => "encounter",
        TravelReward.Army => "army", _ => "chars"
    };
    /// <summary>"+1 [glyph] ARMY": what a stop of the road pays, as rich text for a TMP label wired to the card icon sheet.</summary>
    public static string RewardRichLabel(TravelReward reward)
        => "+1 <sprite name=\"" + RewardSprite(reward) + "\"> " + MatchRules.RewardLabel(reward).ToUpperInvariant();

    /// <summary>A TMP label in the board's reading font with the card icon sheet, for chrome that shows sprite tags.</summary>
    public static TextMeshProUGUI RichLabel(Transform parent, string text, Board board, float size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var go = new GameObject("Rich label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        var skin = BoardPresentation.SkinFor(parent);
        var font = BoardPresentation.ReadingFontFor(skin);
        if (font != null) { label.font = font; label.fontSharedMaterial = font.material; }
        label.spriteAsset = BoardPresentation.SpriteAssetFor(board);
        label.text = text; label.color = color; label.alignment = alignment; label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Overflow;
        label.enableAutoSizing = true; label.fontSizeMin = Mathf.Max(6, size * .55f); label.fontSizeMax = size; label.fontSize = size;
        return label;
    }

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
        string now = rules.Active + "|" + rules.Travel.Destination.name + "|" + rules.Travel.Stop + "|" + rules.Phase + "|" + rules.Attacks.Count + "|" + rules.Travel.Stops.Count + "|" + rules.Fights.Count
            + "|" + (match.PendingCombatCard?.name ?? "") + "|" + string.Join(",", rules.Attacks.SelectMany(a => a.Blockers).Select(b => b.Card.name))
            + "|" + string.Join(",", rules.Players[rules.Attacker].Hand.Select(c => c.name));
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
        if (panel != null) { BoardCardPreview.UnregisterModal(panel); panel.gameObject.SetActive(false); Destroy(panel.gameObject); }
        bool ours = rules.Active == 0;
        var ground = rules.Ground;
        var accent = ours ? skin.colors.teal : skin.colors.gold;
        // The popup: centred over the army lanes, which it replaces with chips while the road is walked.
        var background = BoardPresentation.Panel(transform, "Travel popup", skin.colors.ink);
        background.raycastTarget = true;
        panel = background.rectTransform;
        panel.anchorMin = panel.anchorMax = new Vector2(.508f, .60f); panel.pivot = new Vector2(.5f, .5f);
        var boardRect = ((RectTransform)transform).rect;
        float width = boardRect.width * .64f;
        float height = boardRect.height * .52f;
        panel.sizeDelta = new Vector2(width, height);
        panel.anchoredPosition = Vector2.zero;
        BoardSurface.Dress(background, accent, true);
        var shadow = background.gameObject.AddComponent<Shadow>(); shadow.effectColor = skin.colors.previewShadow; shadow.effectDistance = skin.preview.shadowOffset * 2;
        fade = panel.gameObject.AddComponent<CanvasGroup>(); fade.alpha = alpha; fade.blocksRaycasts = true;
        BoardCardPreview.RegisterModal(panel);
        board.preview?.Hide();
        if (fresh) shownAt = Time.unscaledTime;
        float inset = 18;

        string who = ours ? "YOUR COMPANY" : "THE ENEMY COMPANY";
        string heading = (journey.Moving ? who + " ON THE ROAD TO " + journey.Destination.name.ToUpperInvariant() : who + " HOLDS AT " + journey.Destination.name.ToUpperInvariant())
            + "   ·   STOP " + (journey.Stop + 1) + " OF " + journey.Stops.Count;
        title = BoardPresentation.TextLabel(panel, heading, board.interfaceFont, skin.typography.previewLabelSize + 4, skin.colors.gold, new Vector2(0, 1), Vector2.one, TextAnchor.MiddleCenter);
        if (skin.typography.mastheadFont != null) title.font = skin.typography.mastheadFont;
        title.rectTransform.pivot = new Vector2(.5f, 1); title.rectTransform.sizeDelta = new Vector2(-inset * 2, TitleHeight); title.rectTransform.anchoredPosition = new Vector2(0, -8);
        title.resizeTextForBestFit = true; title.resizeTextMinSize = 10; title.resizeTextMaxSize = skin.typography.previewLabelSize + 4;

        // The road on the map: the region the company set out from, then one diamond per region entered,
        // framed so the whole journey shows. The map takes the left of the popup, square, as tall as it can.
        float top = TitleHeight + 14;
        float mapSide = Mathf.Min(height - top - inset, width * .42f);
        var map = new CaldrathMapView(panel, rules.Map, Vector2.one * mapSide, accent, "Road");
        strip = map.Viewport;
        strip.anchorMin = strip.anchorMax = new Vector2(0, 1); strip.pivot = new Vector2(0, 1);
        strip.anchoredPosition = new Vector2(inset, -top);
        string origin = journey.Moving ? rules.Players[rules.Active].Destination?.Card.region : null;
        var road = new List<string> { origin ?? journey.Stops[0] };
        road.AddRange(journey.Stops);
        map.Frame(road, .1f, .3f);
        nodePositions.Clear();
        for (int i = 0; i < road.Count; i++)
        {
            // Without a map the road is still walked, as a straight line across the window.
            if (!map.TryLocate(road[i], out var at)) at = new Vector2(-mapSide * .5f + mapSide / road.Count * (i + .5f), 0);
            nodePositions.Add(at);
        }
        for (int i = 0; i < road.Count; i++)
        {
            var at = nodePositions[i];
            int stopIndex = i - 1; // -1 is where the company set out
            bool passed = stopIndex < journey.Stop, here = stopIndex == journey.Stop;
            var stopGround = stopIndex >= 0 ? rules.TerrainOf(journey.Stops[stopIndex]) : TerrainEnum.None;
            var tint = stopIndex >= 0 ? TerrainColor(stopGround) : skin.colors.muted;
            if (i > 0) map.Link(nodePositions[i - 1], at, passed || here ? accent : skin.colors.stackBorder, here || passed ? 3 : 2, NodeSize * .5f);
            if (i == 0 && origin == null) continue; // holding: the set-out node is the stop itself
            if (i == road.Count - 1 && journey.Moving) map.Halo(at, accent, NodeSize * 2.2f);
            var node = map.Node(i == 0 ? "Here" : "Stop", at, tint, passed || here || i == 0, NodeSize);
            if (here) current = node;
            if (stopIndex >= 0 && stopIndex < RegionMap.MaxDistance)
            {
                // What the stop pays: "+1 [glyph]". Passed stops have paid, the current one is paying, the rest are owed.
                var reward = RichLabel(map.Overlay, "+1 <sprite name=\"" + RewardSprite(MatchRules.RewardAt(stopIndex + 1)) + "\">", board, skin.typography.previewLabelSize,
                    here ? skin.colors.ivory : passed ? skin.colors.gold : skin.colors.muted);
                reward.rectTransform.anchorMin = reward.rectTransform.anchorMax = Vector2.one * .5f;
                reward.rectTransform.pivot = new Vector2(.5f, 0); reward.rectTransform.sizeDelta = new Vector2(Mathf.Max(30, reward.preferredWidth + 8), 16);
                reward.rectTransform.anchoredPosition = at + Vector2.up * (NodeSize * .5f + (here ? 8 : 3));
                CaldrathMapView.Slip(reward.rectTransform);
            }
        }
        // The destination named on the map, and the current stop, so the road reads at a glance.
        if (journey.Moving) MapCaption(map.Overlay, journey.Destination.name.ToUpperInvariant(), nodePositions[nodePositions.Count - 1] + Vector2.down * (NodeSize + 2), accent, skin);
        // The company itself: a bright marker that walks the road as stops are passed. It sits over the
        // clipped picture, a direct child of the window, so it is never cut off at the frame.
        var mark = BoardPresentation.Panel(strip, "Company", skin.colors.ink);
        marker = mark.rectTransform;
        marker.anchorMin = marker.anchorMax = Vector2.one * .5f;
        marker.sizeDelta = Vector2.one * MarkerSize;
        BoardSurface.Dress(mark, accent, false, true);
        var champion = CardCatalog.FindCardByName(ours ? board.humanAvatarCardName : board.opponentAvatarCardName);
        var portrait = BoardPresentation.Panel(marker, "Travelling champion", Color.white);
        BoardPresentation.Stretch(portrait.rectTransform, Vector2.zero, Vector2.one);
        portrait.rectTransform.offsetMin = Vector2.one * 3; portrait.rectTransform.offsetMax = Vector2.one * -3;
        portrait.sprite = Artwork(champion); portrait.preserveAspect = false;
        int at_ = journey.Stop + 1;
        marker.anchoredPosition = fresh ? nodePositions[Mathf.Max(0, at_ - 1)] : markerFrom;
        movementPosition = marker.anchoredPosition;
        float columnX = inset + mapSide + 12, columnWidth = width - columnX - inset;

        // Where the company stands now, what ground it is, and what is happening there.
        float groundTop = -top;
        var here_ = new GameObject("Ground", typeof(RectTransform)).GetComponent<RectTransform>();
        here_.SetParent(panel, false);
        here_.anchorMin = here_.anchorMax = new Vector2(0, 1); here_.pivot = new Vector2(0, 1);
        here_.sizeDelta = new Vector2(columnWidth, GroundHeight); here_.anchoredPosition = new Vector2(columnX, groundTop);
        var landscape = CardCatalog.AllCards().FirstOrDefault(c => c.GetCardType() == CardTypeEnum.Land &&
            (c.region == journey.Region || (rules.Map?.RegionOfLand(c.name) ?? c.name) == journey.Region));
        LocationPortrait(here_, landscape ?? journey.Destination, true, GroundHeight, null);
        LocationPortrait(here_, journey.Destination, false, GroundHeight, "DESTINATION\n" + journey.Destination.name);
        region = BoardPresentation.TextLabel(here_, PcDescriptionBuilder.FormatDisplayRegionName(journey.Region).ToUpperInvariant(), board.interfaceFont, skin.typography.previewLabelSize + 6, skin.colors.ivory,
            new Vector2(0, .5f), new Vector2(1, 1), TextAnchor.MiddleCenter);
        region.rectTransform.offsetMin = new Vector2(GroundHeight * 1.5f + 12, 0); region.rectTransform.offsetMax = new Vector2(-(GroundHeight * 1.5f + 12), 0);
        region.resizeTextForBestFit = true; region.resizeTextMinSize = 9; region.resizeTextMaxSize = skin.typography.previewLabelSize + 6;
        var badge = BoardPresentation.Panel(here_, "Terrain", TerrainColor(ground));
        badge.rectTransform.anchorMin = new Vector2(.5f, 0); badge.rectTransform.anchorMax = new Vector2(.5f, 0); badge.rectTransform.pivot = new Vector2(.5f, 0);
        badge.rectTransform.sizeDelta = new Vector2(150, 20); badge.rectTransform.anchoredPosition = new Vector2(0, 4);
        terrain = BoardPresentation.TextLabel(badge.rectTransform, (ground == TerrainEnum.None ? "UNKNOWN" : ground.ToString().ToUpperInvariant()) + " GROUND", board.interfaceFont, skin.typography.previewLabelSize,
            skin.colors.ink, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
        terrain.fontStyle = FontStyle.Bold;
        terrain.resizeTextForBestFit = true; terrain.resizeTextMinSize = 7; terrain.resizeTextMaxSize = skin.typography.previewLabelSize;

        // The phase line, then the two lanes of chips: who may strike, who may stand.
        float phaseTop = groundTop - GroundHeight - 6;
        phase = BoardPresentation.TextLabel(panel, PhaseLine(), board.interfaceFont, skin.typography.previewLabelSize + 1, skin.colors.ivory, new Vector2(0, 1), new Vector2(0, 1), TextAnchor.MiddleCenter);
        phase.rectTransform.pivot = new Vector2(0, 1); phase.rectTransform.sizeDelta = new Vector2(columnWidth, 24); phase.rectTransform.anchoredPosition = new Vector2(columnX, phaseTop);
        phase.resizeTextForBestFit = true; phase.resizeTextMinSize = 8; phase.resizeTextMaxSize = skin.typography.previewLabelSize + 1;
        float lanesTop = phaseTop - 28;
        float lanesHeight = height + lanesTop - 14;
        BuildLanes(rules, skin, columnX, lanesTop, lanesHeight, columnWidth);
        panel.SetAsLastSibling();
    }

    // A name on the map: bold on a dark slip so it reads over the parchment.
    void MapCaption(Transform parent, string text, Vector2 at, Color color, BoardSkin skin)
    {
        var label = BoardPresentation.TextLabel(parent, text, board.interfaceFont, skin.typography.previewLabelSize - 1, color, Vector2.one * .5f, Vector2.one * .5f, TextAnchor.MiddleCenter);
        label.fontStyle = FontStyle.Bold; label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.rectTransform.pivot = new Vector2(.5f, 1); label.rectTransform.sizeDelta = new Vector2(label.preferredWidth + 10, 18);
        label.rectTransform.anchoredPosition = at;
        CaldrathMapView.Slip(label.rectTransform);
    }

    // The units at this stop. The raiders' lane lists the other company's units (and hand cards) that
    // could fall on the traveller, committed ones lit; the defenders' lane lists the traveller's units
    // that can stand here, and, while defenders are being assigned, what they have taken on.
    void BuildLanes(MatchRules rules, BoardSkin skin, float left, float top, float height, float width)
    {
        var raiderColor = rules.Attacker == 0 ? skin.colors.teal : skin.colors.gold;
        var defenderColor = rules.Active == 0 ? skin.colors.teal : skin.colors.gold;
        float laneWidth = (width - 12) * .5f;
        RectTransform Lane(string name, float x, Color accent)
        {
            var lane = BoardPresentation.Panel(panel, name, new Color(0, 0, 0, .22f));
            lane.rectTransform.anchorMin = lane.rectTransform.anchorMax = new Vector2(0, 1); lane.rectTransform.pivot = new Vector2(0, 1);
            lane.rectTransform.sizeDelta = new Vector2(laneWidth, height); lane.rectTransform.anchoredPosition = new Vector2(x, top);
            BoardPresentation.Border(lane.rectTransform, new Color(accent.r, accent.g, accent.b, .35f), 1);
            var heading = BoardPresentation.TextLabel(lane.rectTransform, name, board.interfaceFont, skin.typography.previewLabelSize - 1, accent, new Vector2(0, 1), Vector2.one, TextAnchor.MiddleCenter);
            heading.rectTransform.pivot = new Vector2(.5f, 1); heading.rectTransform.sizeDelta = new Vector2(-8, 18); heading.rectTransform.anchoredPosition = new Vector2(0, -2);
            heading.fontStyle = FontStyle.Bold;
            return lane.rectTransform;
        }
        // Raiders: everything already committed, then the field units and hand cards that could still fall on the company.
        var raiders = new List<(CardData card, MatchRules.Unit unit, string tag, bool committed)>();
        foreach (var strike in rules.Attacks) raiders.Add((strike.Attacker.Card, strike.Attacker, strike.Attacker.Roadside ? "FROM HAND" : "ATTACKING", true));
        if (rules.Phase == TravelPhase.Attack)
        {
            foreach (var unit in rules.Raiders()) raiders.Add((unit.Card, unit, null, false));
            foreach (var card in rules.HandRaiders()) raiders.Add((card, null, "IN HAND", false));
        }
        string raiderTitle = (rules.Attacker == 0 ? "YOUR RAIDERS" : "ENEMY RAIDERS") + "  ·  " + rules.Attacks.Count + (rules.Phase == TravelPhase.Attack ? " COMMITTED" : " ATTACKING");
        var raiderLane = Lane(raiderTitle, left, raiderColor);
        FillLane(raiderLane, raiders, raiderColor, skin, rules.Attacker == 0 && rules.Phase == TravelPhase.Attack, rules);
        // Defenders: the traveller's units that can stand on this ground, blocking ones tagged with what they took on.
        var defenders = new List<(CardData card, MatchRules.Unit unit, string tag, bool committed)>();
        foreach (var unit in rules.Players[rules.Active].Field.Where(u => u.IsCombatant))
        {
            var strike = rules.Attacks.FirstOrDefault(a => a.Blockers.Contains(unit));
            bool can = unit.Card.FightsOn(rules.Ground) && !unit.Wounded;
            if (strike == null && !can) continue;
            string tag = strike != null ? (strike.StoodFast.Contains(unit) ? "STANDS FAST VS " : "MEETS ") + strike.Attacker.Card.name.ToUpperInvariant() : unit.Tapped ? "TAPPED" : null;
            defenders.Add((unit.Card, unit, tag, strike != null));
        }
        string defenderTitle = (rules.Active == 0 ? "YOUR DEFENDERS" : "ENEMY DEFENDERS") + (rules.Phase == TravelPhase.Defend ? "  ·  " + rules.Attacks.Sum(a => a.Blockers.Count) + " STANDING" : "");
        var defenderLane = Lane(defenderTitle, left + laneWidth + 12, defenderColor);
        FillLane(defenderLane, defenders, defenderColor, skin, rules.Active == 0 && rules.Phase == TravelPhase.Defend, rules);
    }

    void FillLane(RectTransform lane, List<(CardData card, MatchRules.Unit unit, string tag, bool committed)> entries, Color accent, BoardSkin skin, bool interactive, MatchRules rules)
    {
        if (entries.Count == 0)
        {
            var none = BoardPresentation.TextLabel(lane, "Nothing can fight on this ground.", board.interfaceFont, skin.typography.previewLabelSize - 1, skin.colors.muted, Vector2.zero, new Vector2(1, .85f), TextAnchor.MiddleCenter);
            none.resizeTextForBestFit = true; none.resizeTextMinSize = 8; none.resizeTextMaxSize = skin.typography.previewLabelSize - 1;
            return;
        }
        float areaWidth = lane.sizeDelta.x - 8, areaHeight = lane.sizeDelta.y - 24;
        const float chipWidth = 78, chipHeight = 96, gap = 6;
        int columns = Mathf.Max(1, Mathf.FloorToInt((areaWidth + gap) / (chipWidth + gap)));
        int rows = Mathf.CeilToInt((float)entries.Count / columns);
        float scale = Mathf.Min(1, areaHeight / (rows * chipHeight + (rows - 1) * gap));
        float startX = -(Mathf.Min(columns, entries.Count) * (chipWidth + gap) - gap) * .5f * scale;
        for (int i = 0; i < entries.Count; i++)
        {
            var (card, unit, tag, committed) = entries[i];
            int row = i / columns, column = i % columns;
            var view = match.ViewOf(card);
            bool actionable = interactive && view != null && match.IsActionable(view);
            bool selected = match.PendingCombatCard == card;
            var chip = BoardPresentation.Panel(lane, "Chip " + card.name, skin.colors.ink);
            chip.raycastTarget = true;
            var rect = chip.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(chipWidth, chipHeight); rect.localScale = Vector3.one * scale;
            rect.anchoredPosition = new Vector2(startX + column * (chipWidth + gap) * scale, -22 - row * (chipHeight + gap) * scale);
            var frameColor = selected ? Color.white : committed ? accent : actionable ? new Color(.3f, 1f, .78f) : new Color(accent.r, accent.g, accent.b, .35f);
            var frame = BoardPresentation.Border(rect, frameColor, selected || committed || actionable ? 3 : 1);
            var art = BoardPresentation.Panel(rect, "Portrait", Color.white);
            BoardPresentation.Stretch(art.rectTransform, new Vector2(0, .38f), Vector2.one);
            art.rectTransform.offsetMin = new Vector2(4, 2); art.rectTransform.offsetMax = new Vector2(-4, -4);
            art.sprite = Artwork(card); art.preserveAspect = true;
            if (art.sprite == null) art.color = new Color(.2f, .22f, .25f);
            if (unit != null && unit.Tapped && !committed) art.color = new Color(.55f, .55f, .55f);
            var name = BoardPresentation.TextLabel(rect, card.name, board.interfaceFont, 11, skin.colors.ivory, new Vector2(0, .2f), new Vector2(1, .38f), TextAnchor.MiddleCenter);
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 7; name.resizeTextMaxSize = 11;
            var stats = unit != null ? rules.Stats(unit) : card.GetCombatStats();
            string line = stats.attack + "/" + stats.defense + (tag != null ? "  " + tag : "");
            var footer = BoardPresentation.TextLabel(rect, line, board.interfaceFont, 10, tag != null ? new Color(1f, .8f, .45f) : skin.colors.muted, new Vector2(0, .02f), new Vector2(1, .2f), TextAnchor.MiddleCenter);
            footer.resizeTextForBestFit = true; footer.resizeTextMinSize = 6; footer.resizeTextMaxSize = 10;
            if (!actionable) continue;
            // The chip stands in for the board card the popup covers: it does what a click on it would.
            var button = chip.gameObject.AddComponent<Button>();
            button.targetGraphic = chip;
            var colors = button.colors; colors.highlightedColor = new Color(1.4f, 1.4f, 1.3f); colors.pressedColor = new Color(.7f, .8f, .8f); button.colors = colors;
            var target = view;
            button.onClick.AddListener(() => match.PerformAction(target));
            var glow = chip.gameObject.AddComponent<ChipGlow>(); glow.frame = frame.gameObject.AddComponent<CanvasGroup>();
        }
    }

    /// <summary>Breathes the legal-action frame of a chip, the way board cards pulse when they may act.</summary>
    sealed class ChipGlow : MonoBehaviour
    {
        public CanvasGroup frame;
        void Update() { if (frame != null) frame.alpha = .7f + .3f * Mathf.Sin(Time.unscaledTime * 3); }
    }

    string PhaseLine()
    {
        var rules = match.Rules;
        bool ours = rules.Active == 0;
        if (rules.Phase == TravelPhase.Attack)
        {
            int raiders = rules.Raiders().Count() + rules.HandRaiders().Count();
            if (rules.Attacks.Count > 0) return rules.Attacks.Count + (rules.Attacks.Count == 1 ? " attack declared" : " attacks declared") + (rules.Attacker == 0 ? ": commit more, or ATTACK!" : ".");
            if (raiders == 0) return (ours ? "Nothing of the enemy's" : "Nothing of yours") + " can fight on this ground: the company passes.";
            return ours ? "The enemy weighs an ambush..." : "Click a raider to fall on the company, or let them pass.";
        }
        if (match.PendingCombatCard != null && ours) return match.PendingCombatCard.name + " stands: click the attacker it meets.";
        return rules.Attacks.Count + (rules.Attacks.Count == 1 ? " attack" : " attacks") + " on the company: " + (ours ? "click a defender, then the attacker it meets, or resolve combat." : "the enemy assigns defenders.");
    }

    void Update()
    {
        if (panel == null) return;
        if (closing)
        {
            fade.alpha = Mathf.MoveTowards(fade.alpha, 0, Time.unscaledDeltaTime * Skin.preview.fadeSpeed);
            if (fade.alpha <= 0) { BoardCardPreview.UnregisterModal(panel); Destroy(panel.gameObject); panel = null; marker = null; signature = null; closing = false; }
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

    void OnDisable() { if (panel != null) { BoardCardPreview.UnregisterModal(panel); Destroy(panel.gameObject); panel = null; marker = null; signature = null; } }

    public static Sprite Artwork(CardData data)
    {
        if (data == null || CardServices.Art == null) return null;
        foreach (var candidate in new[] { data.spriteName, data.portraitName, data.name })
            if (!string.IsNullOrWhiteSpace(candidate) && CardServices.Art.TryGetSprite(candidate, true, out var sprite)) return sprite;
        return null;
    }
    void LocationPortrait(RectTransform parent, CardData data, bool left, float size, string caption)
    {
        var art = BoardPresentation.Panel(parent, left ? "Current landscape" : "Destination landscape", Color.white);
        art.rectTransform.anchorMin = art.rectTransform.anchorMax = new Vector2(left ? 0 : 1, .5f);
        art.rectTransform.pivot = new Vector2(left ? 0 : 1, .5f);
        art.rectTransform.sizeDelta = new Vector2(size * 1.5f, size);
        art.rectTransform.anchoredPosition = Vector2.zero;
        art.sprite = Artwork(data);
        BoardPresentation.Border(art.rectTransform, Skin.colors.gold, 1);
        if (caption == null) return;
        var shade = BoardPresentation.Panel(art.transform, "Landscape shade", new Color(.015f, .025f, .03f, .56f));
        BoardPresentation.Stretch(shade.rectTransform, Vector2.zero, Vector2.one);
        var label = BoardPresentation.TextLabel(shade.transform, caption, board.interfaceFont, 12, Skin.colors.ivory, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
        label.resizeTextForBestFit = true; label.resizeTextMinSize = 8; label.resizeTextMaxSize = 12;
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Runtime board chrome. Authored cards and match collections remain the source of truth.</summary>
[ExecuteAlways, DefaultExecutionOrder(-40)]
[DisallowMultipleComponent]
public sealed class BoardPresentation : MonoBehaviour
{
    public SkinManager skinManager;
    public BoardSkin Skin => skinManager != null ? skinManager.ActiveSkin : BoardSkin.Default;
    private readonly List<(CardZoneVisualizer zone, Text label)> counts = new();
    private readonly List<GameObject> generated = new();
    private Board board;
    private Font font;
    private bool started;
    private readonly List<(bool opponent, int index, TMP_Text label)> materialLabels = new();
    private Text endTurnLabel;
    private Text actionStatus;

    private void Awake()
    {
        board = GetComponent<Board>();
        if (skinManager == null) skinManager = GetComponent<SkinManager>();
    }
    private void OnEnable()
    {
        if (skinManager != null) skinManager.SkinChanged += ApplySkin;
        ApplySkin(Skin);
    }
    private void Start() { started = true; }
    private void OnDisable() { if (skinManager != null) skinManager.SkinChanged -= ApplySkin; }

    public static BoardSkin SkinFor(Transform target)
    {
        var skin = SkinOrNull(target);
        if (skin == null) throw new System.InvalidOperationException("Assign a BoardSkin to SkinManager or create Resources/Skins/Default.");
        return skin;
    }

    /// <summary>
    /// The skin governing this transform, or null when there is neither a board above it nor a
    /// Default asset. For style decisions that have a sensible unskinned answer and so should not
    /// take down a card that is being previewed outside a board.
    /// </summary>
    public static BoardSkin SkinOrNull(Transform target)
    {
        var presentation = target.GetComponentInParent<BoardPresentation>();
        return presentation != null ? presentation.Skin : BoardSkin.Default;
    }

    private T Track<T>(T component) where T : Component
    {
        // Board chrome is rebuilt from the active skin whenever the scene loads.  Do not serialize
        // these transient objects into the scene: that avoids stale generated UI after a script
        // reload and keeps the authored hierarchy free of helper components.
        if (!Application.isPlaying) component.gameObject.hideFlags |= HideFlags.DontSaveInEditor;
        generated.Add(component.gameObject);
        return component;
    }

    private void ApplySkin(BoardSkin skin)
    {
        if (board == null || skin == null) return;
        InstallCardFaceServices(skin);
        board.preview?.Hide();
        ClearStaleGeneratedBackdrops();
        // Clear cached labels before immediate edit-mode destruction, so LateUpdate never tries
        // to write to a label that belonged to the previous generated chrome.
        counts.Clear();
        materialLabels.Clear();
        foreach (var go in generated)
        {
            if (go == null) continue;
            DestroyGenerated(go);
        }
        generated.Clear();
        font = skin.typography.interfaceFont != null ? skin.typography.interfaceFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        board.interfaceFont = font;
        var shade = Track(Panel(transform, "Atmosphere veil", skin.colors.atmosphere));
        Stretch(shade.rectTransform, Vector2.zero, Vector2.one);
        shade.transform.SetAsFirstSibling();
        if (skin.backdrop != null)
        {
            var landscape = Track(new GameObject("Illustrated landscape", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>());
            landscape.transform.SetParent(transform, false);
            Stretch(landscape.rectTransform, Vector2.zero, Vector2.one);
            landscape.texture = skin.backdrop;
            landscape.raycastTarget = false;
            var fit = landscape.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = (float)skin.backdrop.width / skin.backdrop.height;
            landscape.transform.SetAsFirstSibling();
        }
        if (skin.openTable)
        {
            var table = Track(new GameObject("Battlefield inlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(TableInlay)).GetComponent<TableInlay>());
            table.transform.SetParent(transform, false);
            Stretch(table.rectTransform, new Vector2(.175f,.245f), new Vector2(.805f,.90f));
            table.raycastTarget = false;
            table.color = skin.colors.gold;
            table.transform.SetSiblingIndex(shade.transform.GetSiblingIndex()+1);
        }
        var header = Track(Panel(transform, "Board masthead", skin.openTable ? Color.clear : skin.colors.ink));
        Stretch(header.rectTransform, skin.chrome.header.min, skin.chrome.header.max);
        if (!skin.openTable) BoardSurface.Dress(header, skin.colors.gold, true);
        for (int i = 0; i < skin.chrome.headerLabels.Length; i++)
            if (i == 0 || board.Match == null) StyledLabel(header.transform, skin.chrome.headerLabels[i]);
        foreach (var style in skin.zones) if (style != null) Zone(ResolveZone(style.zone), style);
        PlayerPanel(false);
        PlayerPanel(true);
        if (board.hand != null) board.hand.gap = skin.chrome.handGap;
        foreach (var label in skin.chrome.footerLabels) StyledLabel(transform, label);
        if (board.preview != null) board.preview.transform.SetAsLastSibling();
        // Runtime zones own generated BoardCardViews. In Edit mode the authored prefab instances
        // are laid out by CardZoneVisualizerEditor instead, so rebuilding here would leave them
        // out of sync with the scene authoring surface.
        if (Application.isPlaying && started)
            foreach (var zone in board.GetComponentsInChildren<CardZoneVisualizer>()) zone.RefreshSkin();
    }

    // Hands the card face the parts of the skin it draws itself. Card never sees BoardSkin -- it
    // asks CardServices, the same seam it uses for art and playability -- but with these installed
    // the skin is still the single authority for every style decision on a board card.
    //
    // The palette is only installed when the skin actually carries one: leaving it empty means
    // "whatever CardServicesInstaller set up", and overwriting that with the built-in defaults would
    // silently undo a project's own palette. This runs at -40, after that installer's -100 Awake,
    // so an assigned skin palette deliberately wins.
    private static void InstallCardFaceServices(BoardSkin skin)
    {
        if (skin.CardTypePalette != null) CardServices.Palette = skin.CardTypePalette;
        if (skin.face != null) CardServices.FaceStyle = skin.face;
    }

    private static void DestroyGenerated(GameObject go)
    {
        go.SetActive(false);
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    // Earlier edit-mode rebuilds did not retain their generated-object tracking after a domain
    // reload. Remove those old full-screen/panel roots by their unique generated names before
    // creating the current skin, otherwise their alpha values stack into an opaque black board.
    private void ClearStaleGeneratedBackdrops()
    {
        var landscape = transform.Find("Illustrated landscape");
        if (landscape != null) DestroyGenerated(landscape.gameObject);
        var table = transform.Find("Battlefield inlay");
        if (table != null) DestroyGenerated(table.gameObject);
        foreach (var text in GetComponentsInChildren<Text>(true))
        {
            if (text.transform.parent != transform) continue;
            if (text.name == "YOUR REALM" || text.name == "CARD INSPECTION"
                || text.name == "Inspect a card\nto read its story." || text.name == "Click to pin\nEsc to close"
                || text.name == "Material action status" || text.name.StartsWith("Pin a land to tap it."))
                DestroyGenerated(text.gameObject);
        }
        foreach (var image in GetComponentsInChildren<Image>(true))
        {
            if (image == null) continue;
            if (image.name != "Atmosphere veil" && image.name != "Board masthead" && image.name != "Zone surface"
                && image.name != "Your materials" && image.name != "Opponent materials"
                && image.name != "Your avatar" && image.name != "Opponent avatar") continue;
            DestroyGenerated(image.gameObject);
        }
    }

    private void StyledLabel(Transform parent, BoardSkin.LabelStyle style)
    {
        if (style == null) return;
        var text = Label(parent, style.text, style.size, Skin.ColorFor(style.color), style.bounds.min, style.bounds.max, style.alignment);
        if (style.mastheadFont && Skin.typography.mastheadFont != null) text.font = Skin.typography.mastheadFont;
    }

    public CardZoneVisualizer ResolveZone(BoardZoneId id) => id switch {
        BoardZoneId.OpponentLands => board.opponentLands, BoardZoneId.OpponentSettlements => board.opponentPopulationCenters,
        BoardZoneId.OpponentArmies => board.opponentArmies, BoardZoneId.OpponentVictory => board.opponentVictoryPoints,
        BoardZoneId.OpponentDiscard => board.opponentDiscard, BoardZoneId.Environment => board.environmental,
        BoardZoneId.HumanArmies => board.humanArmies, BoardZoneId.HumanLands => board.humanLands,
        BoardZoneId.HumanSettlements => board.humanPopulationCenters, BoardZoneId.HumanVictory => board.humanVictoryPoints,
        BoardZoneId.HumanDiscard => board.humanDiscard, _ => board.hand
    };

    private void Zone(CardZoneVisualizer zone, BoardSkin.ZoneStyle style)
    {
        if (zone == null) return;
        var skin = Skin; var chrome = skin.chrome;
        Color accent = skin.ColorFor(style.accent);
        var panel = (RectTransform)zone.transform.parent;
        Stretch(panel, style.bounds.min, style.bounds.max);
        foreach (var old in panel.GetComponentsInChildren<Graphic>())
            if (!old.transform.IsChildOf(zone.transform)) old.enabled = false;
        if (skin.openTable)
        {
            OpenZone(zone, style, panel);
            return;
        }
        var surface = Track(Panel(panel, "Zone surface", skin.colors.zoneSurface));
        Stretch(surface.rectTransform, Vector2.zero, Vector2.one);
        surface.transform.SetAsFirstSibling();
        BoardSurface.Dress(surface, accent, style.zone == BoardZoneId.Hand || style.zone == BoardZoneId.Environment, false, BoardSurface.EmblemFor(style.zone));
        var ribbon = Panel(surface.transform, "Recessed heading", new Color(accent.r, accent.g, accent.b, .025f));
        Stretch(ribbon.rectTransform, Vector2.up, Vector2.one);
        ribbon.rectTransform.pivot = new Vector2(.5f, 1);
        ribbon.rectTransform.sizeDelta = new Vector2(-20, chrome.headingHeight + 2);
        ribbon.rectTransform.anchoredPosition = new Vector2(0, -4);
        var heading = Label(panel, style.title, skin.typography.zoneHeadingSize, accent, Vector2.up, Vector2.one);
        heading.rectTransform.pivot = new Vector2(.5f, 1);
        heading.rectTransform.sizeDelta = new Vector2(-chrome.headingInset.x * 2, chrome.headingHeight);
        heading.rectTransform.anchoredPosition = new Vector2(0, -chrome.headingInset.y);
        if (skin.typography.mastheadFont != null) heading.font = skin.typography.mastheadFont;
        heading.fontStyle = FontStyle.Normal;
        heading.fontSize = Mathf.Max(12, skin.typography.zoneHeadingSize);
        var count = Label(panel, "", skin.typography.countSize, skin.colors.muted, Vector2.one, Vector2.one, TextAnchor.MiddleRight);
        count.rectTransform.pivot = Vector2.one;
        count.rectTransform.sizeDelta = new Vector2(chrome.countWidth, chrome.headingHeight);
        count.rectTransform.anchoredPosition = -chrome.headingInset;
        counts.Add((zone, count));
        var area = (RectTransform)zone.transform;
        Stretch(area, Vector2.zero, Vector2.one);
        area.offsetMin = chrome.contentInsetMin;
        area.offsetMax = -chrome.contentInsetMax;
        zone.gap = chrome.zoneGap;
    }

    private void OpenZone(CardZoneVisualizer zone, BoardSkin.ZoneStyle style, RectTransform panel)
    {
        // Labels are attached to small physical groups, never full-width boxed lanes.
        var root = Track(Panel(panel, "Zone surface", Color.clear));
        Stretch(root.rectTransform, Vector2.zero, Vector2.one);
        root.transform.SetAsFirstSibling();
        bool army = style.zone == BoardZoneId.HumanArmies || style.zone == BoardZoneId.OpponentArmies;
        bool hand = style.zone == BoardZoneId.Hand;
        var title = Label(root.transform, style.title, army ? 15 : 13, Skin.colors.muted,
            new Vector2(0,.88f), Vector2.one, TextAnchor.UpperCenter);
        title.font = Skin.typography.mastheadFont != null ? Skin.typography.mastheadFont : font;
        title.color = new Color(title.color.r,title.color.g,title.color.b,army ? .55f : .9f);
        if (hand) title.text = "";
        var count = Label(root.transform, "", 12, Skin.colors.muted,
            new Vector2(.85f,.88f), Vector2.one, TextAnchor.UpperRight);
        counts.Add((zone, count));
        var area = (RectTransform)zone.transform;
        Stretch(area, Vector2.zero, Vector2.one);
        area.offsetMin = new Vector2(8, hand ? 4 : 6);
        area.offsetMax = new Vector2(-8, hand ? -4 : -24);
        zone.gap = army ? 20 : 12;
    }

    private void LateUpdate()
    {
        foreach (var entry in materialLabels)
            if (entry.label != null) entry.label.text = MaterialAmount(entry.opponent ? board.OpponentMaterials : board.HumanMaterials, entry.index);
        if (endTurnLabel != null) endTurnLabel.text = board.IsOpponentTurn ? "END OPPONENT TURN" : "END TURN";
        if (actionStatus != null) actionStatus.text = board.ActionStatus;
        foreach (var entry in counts)
        {
            if (entry.zone == null || entry.label == null) continue;
            int handLimit = board.Match != null ? board.Match.Rules?.Players[0].HandLimit ?? board.defaultHandSize : board.maximumHandSize;
            var value = entry.zone.isHand ? $"{entry.zone.Count} / {handLimit}" : Skin.openTable ? (entry.zone.Count > 0 ? entry.zone.Count.ToString() : "") : entry.zone.Count.ToString("00");
            if (entry.label.text != value) entry.label.text = value;
        }
    }

    private void PlayerPanel(bool opponent)
    {
        var style = Skin.players;
        var accent = opponent ? Skin.colors.gold : Skin.colors.teal;
        var bounds = opponent ? style.opponentMaterials : style.humanMaterials;
        var panel = Track(Panel(transform, opponent ? "Opponent materials" : "Your materials", Skin.openTable ? Color.clear : Skin.colors.zoneSurface));
        Stretch(panel.rectTransform, bounds.min, bounds.max);
        if (!Skin.openTable) BoardSurface.Dress(panel, accent, false, false, BoardEmblem.Materials);
        Label(panel.transform, Skin.openTable ? "" : opponent ? "OPPONENT MATERIALS" : "YOUR MATERIALS", style.headingSize, accent,
            new Vector2(.02f,.85f), new Vector2(.98f,1), TextAnchor.MiddleCenter);
        var pool = opponent ? board.OpponentMaterials : board.HumanMaterials;
        for (int i = 0; i < PlayerMaterials.Names.Length; i++)
        {
            int row = i / 4, column = i % 4;
            float left = .02f + column * .24f;
            float bottom = row == 0 ? .5f : .17f;
            var amountObject = new GameObject(PlayerMaterials.Names[i] + " amount", typeof(RectTransform), typeof(TextMeshProUGUI));
            amountObject.transform.SetParent(panel.transform, false);
            var amount = amountObject.GetComponent<TextMeshProUGUI>();
            amount.font = ReadingFontFor(Skin);
            amount.spriteAsset = SpriteAssetFor(board);
            amount.fontSize = style.amountSize;
            amount.color = Color.white;
            amount.alignment = TextAlignmentOptions.Center;
            amount.textWrappingMode = TextWrappingModes.NoWrap;
            amount.raycastTarget = false;
            amount.text = MaterialAmount(pool, i);
            Stretch(amount.rectTransform, new Vector2(left,bottom+.11f), new Vector2(left+.24f,bottom+.34f));
            // Match creation/retries replace the pools after the chrome has already been built.
            materialLabels.Add((opponent,i,amount));
            Label(panel.transform, PlayerMaterials.Names[i].ToUpperInvariant(), style.materialSize, Skin.colors.muted,
                new Vector2(left,bottom), new Vector2(left+.24f,bottom+.12f), TextAnchor.MiddleCenter);
        }
        if (!opponent && (!Skin.openTable || board.Match == null))
        {
            var next = Panel(panel.transform, "End turn", Skin.colors.button);
            Stretch(next.rectTransform, new Vector2(.04f,.015f), new Vector2(.96f,.145f));
            next.raycastTarget = true;
            BoardSurface.Dress(next, accent, false, true);
            var button = next.gameObject.AddComponent<Button>(); button.targetGraphic = next;
            // Tint the engraved mesh itself so hover and press remain visible over its opaque face.
            button.targetGraphic = next.GetComponentInChildren<BoardSurface>();
            var feedback = button.colors;
            feedback.normalColor = Color.white;
            feedback.highlightedColor = new Color(1.3f, 1.3f, 1.15f);
            feedback.pressedColor = new Color(.65f, .8f, .75f);
            feedback.fadeDuration = .12f;
            button.colors = feedback;
            button.onClick.AddListener(board.EndTurn);
            endTurnLabel = Label(next.transform, board.IsOpponentTurn ? "END OPPONENT TURN" : "END TURN",
                style.materialSize, accent, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
        }
        var avatarBounds = opponent ? style.opponentAvatar : style.humanAvatar;
        var avatar = Track(Panel(transform, opponent ? "Opponent avatar" : "Your avatar", Skin.openTable ? Color.clear : Skin.colors.zoneSurface));
        Stretch(avatar.rectTransform, avatarBounds.min, avatarBounds.max);
        if (!Skin.openTable) BoardSurface.Dress(avatar, accent, true, false, BoardEmblem.Champion);
        var avatarHeading = Label(avatar.transform, opponent ? "OPPONENT AVATAR" : "YOUR AVATAR", style.headingSize, accent,
            Vector2.up, Vector2.one, TextAnchor.MiddleCenter);
        avatarHeading.rectTransform.pivot = new Vector2(.5f, 1);
        avatarHeading.rectTransform.sizeDelta = new Vector2(-12, 24);
        avatarHeading.rectTransform.anchoredPosition = new Vector2(0, -2);
        // Life is drawn on the champion's card itself (AvatarCardPresentation), not as a separate widget.
        var cardName = opponent ? board.opponentAvatarCardName : board.humanAvatarCardName;
        var data = string.IsNullOrWhiteSpace(cardName) ? null : CardCatalog.FindCardByName(cardName);
        if (data == null)
            Label(avatar.transform, "Choose avatar\non Board", style.headingSize, Skin.colors.muted,
                Vector2.zero, new Vector2(1,.82f), TextAnchor.MiddleCenter);
        else
        {
            var go = new GameObject("Avatar card", typeof(RectTransform));
            go.transform.SetParent(avatar.transform, false);
            var area = (RectTransform)go.transform;
            Stretch(area, Vector2.zero, Vector2.one);
            area.offsetMin = new Vector2(6, 10);
            area.offsetMax = new Vector2(-6, -28);
            var zone = go.AddComponent<AvatarZoneVisualizer>(); zone.board = board;
            zone.Owner = opponent ? 1 : 0;
            zone.HealthLabel = avatarHeading;
            zone.SetCards(new[] { data.Clone() });
        }
        if (!opponent)
        {
            actionStatus = Label(transform, board.ActionStatus, 11, Skin.colors.muted,
                new Vector2(.153f,.564f), new Vector2(.863f,.572f), TextAnchor.MiddleCenter);
            actionStatus.name = "Material action status";
            if (Skin.openTable) actionStatus.gameObject.SetActive(false);
        }
    }

    private static string MaterialAmount(PlayerMaterials pool, int index)
        => $"{pool[index]} <sprite name=\"{PlayerMaterials.Names[index].ToLowerInvariant()}\">";

    private Text Label(Transform parent, string value, int size, Color color, Vector2 min, Vector2 max, TextAnchor alignment = TextAnchor.MiddleLeft)
        => Track(TextLabel(parent, value, font, size, color, min, max, alignment));

    public static Text TextLabel(Transform parent, string value, Font font, int size, Color color, Vector2 min, Vector2 max, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        var go = new GameObject(!string.IsNullOrEmpty(value) ? value : "Count", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.font = font; text.fontSize = size; text.color = color; text.text = value;
        text.alignment = alignment; text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        Stretch(text.rectTransform, min, max);
        return text;
    }

    public static Image Panel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
        return image;
    }

    public static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.pivot = Vector2.one * .5f;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    public static void Rule(Transform parent, Color color, Vector2 min, Vector2 max, float width = 0)
    {
        var line = Panel(parent, "Inlay", color);
        Stretch(line.rectTransform, min, max);
        line.rectTransform.sizeDelta = new Vector2(0, width > 0 ? width : SkinFor(parent).chrome.lineWidth);
    }

    /// <summary>A width of 0 takes the skin's chrome.lineWidth, which is what board chrome uses.</summary>
    public static Image Border(RectTransform parent, Color color, float width = 0)
    {
        if (width <= 0) width = SkinFor(parent).chrome.lineWidth;
        var frame = Panel(parent, "Frame", Color.clear);
        Stretch(frame.rectTransform, Vector2.zero, Vector2.one);
        // Four thin strips avoid an opaque centre and remain crisp at different scales.
        Rule(frame.transform, color, Vector2.zero, Vector2.right, width);
        Rule(frame.transform, color, Vector2.up, Vector2.one, width);
        foreach (float x in new[] { 0f, 1f })
        {
            var edge = Panel(frame.transform, "Edge", color);
            Stretch(edge.rectTransform, new Vector2(x, 0), new Vector2(x, 1));
            edge.rectTransform.sizeDelta = new Vector2(width, 0);
        }
        return frame;
    }

    /// <summary>
    /// The uniform scale a token visual needs to fill the skin's footprint, and the footprint a
    /// layout slot should reserve for it. A skin that leaves tokens.size at zero gets the prefab's
    /// own measurement and a scale of 1, so the authored token is untouched.
    /// </summary>
    public static float TokenScaleFor(BoardSkin skin, Card card, out Vector2 footprint)
    {
        Vector2 natural = card.TokenFootprint;
        var style = skin.tokens;
        footprint = style.size.x > 1f && style.size.y > 1f ? style.size : natural;
        if (natural.x <= 1f || natural.y <= 1f) return 1f;
        Vector2 box = footprint - Vector2.one * (style.artInset * 2f);
        if (box.x <= 1f || box.y <= 1f) return 1f;
        return Mathf.Min(box.x / natural.x, box.y / natural.y);
    }

    /// <summary>The icon sheet the card faces use, so chrome text can show the same sprite tags. Null when none is wired.</summary>
    public static TMP_SpriteAsset SpriteAssetFor(Board board)
    {
        if (board == null || board.fullCardPrefab == null) return null;
        foreach (var source in board.fullCardPrefab.GetComponentsInChildren<TMP_Text>(true))
            if (source.spriteAsset != null) return source.spriteAsset;
        return null;
    }

    /// <summary>The reading font for card text, falling back to the skin's named resource.</summary>
    public static TMP_FontAsset ReadingFontFor(BoardSkin skin)
        => skin.typography.readingFont != null ? skin.typography.readingFont
            : string.IsNullOrWhiteSpace(skin.typography.readingFontFallback) ? null
            : Resources.Load<TMP_FontAsset>(skin.typography.readingFontFallback);

    /// <summary>Which side's numerals a token shows. Full cards keep the prefab's own material.</summary>
    public static Material StatMaterialFor(BoardSkin skin, CardZoneVisualizer zone)
        => zone != null && zone.board != null && zone.board.IsOpponentZone(zone)
            ? skin.colors.opponentTokenStats : skin.colors.ownTokenStats;

    public static void StyleFullCard(Card card, bool handPortrait = false)
    {
        var skin = SkinFor(card.transform);
        var style = skin.cards;
        var root = (RectTransform)card.transform;
        var real = root.Find("RealCard") as RectTransform;
        if (real == null) return;
        ClearGeneratedCardChrome(root, real);
        real.Find("DescriptionBackground")?.gameObject.SetActive(true);
        real.anchorMin = real.anchorMax = Vector2.one * .5f;
        real.sizeDelta = style.size;
        real.anchoredPosition = Vector2.zero;
        real.localScale = Vector3.one;
        var backing = Panel(root, "Obsidian card stock", skin.colors.ink);
        Stretch(backing.rectTransform, Vector2.zero, Vector2.one);
        backing.transform.SetAsFirstSibling();
        BoardSurface.Dress(backing, skin.colors.gold, false, true);
        var shadow = backing.gameObject.AddComponent<Shadow>();
        shadow.effectColor = skin.colors.cardShadow; shadow.effectDistance = style.shadowOffset;
        SetPiece(real, "Image", style.art.size, style.art.position, skin.colors.ink);
        SetPiece(real, "TitleBackground", style.title.size, style.title.position, skin.colors.ink);
        SetPiece(real, "DescriptionBackground", style.description.size, style.description.position, skin.colors.ink);
        SetPiece(real, "TypeBackground", style.badge.size, style.badge.position, skin.colors.ink);
        var border = real.Find("Border");
        if (border != null) border.gameObject.SetActive(style.keepAuthoredBorder);
        foreach (var hover in root.GetComponentsInChildren<Hover>(true)) hover.gameObject.SetActive(style.keepHoverEffects);
        var title = real.Find("TitleBackground/Title")?.GetComponent<TMP_Text>();
        var readingFont = ReadingFontFor(skin);
        foreach (var text in real.GetComponentsInChildren<TMP_Text>(true))
        {
            if (readingFont != null) { text.font = readingFont; text.fontSharedMaterial = readingFont.material; }
            text.fontStyle = FontStyles.Normal;
            text.outlineWidth = 0;
        }
        if (title != null)
        {
            Stretch(title.rectTransform, Vector2.zero, Vector2.one);
            title.rectTransform.offsetMin = style.titleInsetMin; title.rectTransform.offsetMax = -style.titleInsetMax;
            title.text = title.text.Replace("<u>", "").Replace("</u>", "");
            title.color = skin.colors.ivory; title.enableAutoSizing = true; title.fontSizeMin = skin.typography.titleFontRange.x; title.fontSizeMax = skin.typography.titleFontRange.y;
            title.fontStyle = skin.typography.titleStyle;
            title.outlineWidth = 0;
        }
        var description = real.Find("DescriptionBackground/Description")?.GetComponent<TMP_Text>();
        if (description != null)
        {
            Stretch(description.rectTransform, Vector2.zero, Vector2.one);
            description.margin = style.descriptionMargin;
            description.color = skin.colors.ivory; description.enableAutoSizing = true;
            description.fontSizeMin = skin.typography.descriptionFontRange.x; description.fontSizeMax = skin.typography.descriptionFontRange.y;
            description.outlineWidth = 0;
        }
        // One palette authority. The card face tints its own background and token ring from
        // CardServices.Palette; reading anything else here is how the generated border and the
        // card's own frame ended up able to disagree about what an Army is coloured. ApplySkin
        // installs the skin's palette into CardServices, so this picks it up either way.
        var accent = CardServices.Palette
            .GetCardTypeColor(card.cardData != null ? card.cardData.GetCardType() : CardTypeEnum.Unknown);
        var requirements = real.Find("Image/Requirements") as RectTransform;
        if (requirements != null)
        {
            Stretch(requirements, new Vector2(0, 1), new Vector2(1, 1));
            var requirementText = requirements.GetComponent<TMP_Text>();
            float height = style.requirementHeightRange.x;
            bool hasCost = requirementText != null && !string.IsNullOrWhiteSpace(requirementText.text);
            if (hasCost)
            {
                requirementText.fontSize = skin.typography.requirementsSize;
                height = Mathf.Clamp(requirementText.GetPreferredValues(requirementText.text, Mathf.Max(1, style.art.size.x - style.requirementMargin.x - style.requirementMargin.z), Mathf.Infinity).y + style.requirementMargin.y + style.requirementMargin.w, style.requirementHeightRange.x, style.requirementHeightRange.y);
                requirementText.margin = style.requirementMargin;
                // Plain white by default, not the ivory the rest of the card reads in: this row is
                // numerals beside coloured resource sprites, and a warm tint on it fights them.
                // White is also what the font renders untinted, so the row matches the glyph art.
                requirementText.color = skin.colors.requirements;
            }
            requirements.pivot = new Vector2(.5f, 1); requirements.sizeDelta = new Vector2(0, height);
            // The costs get the same ink-and-border plaque as the combat and class rows below. It
            // has to be a sibling drawn just before the text rather than a child of it: a child
            // renders after its parent, so it would paint over the numerals. The measured height
            // drives both rects, so a wrapped cost keeps its own space.
            var plaque = Panel(requirements.parent, "Cost plaque", skin.colors.ink);
            var plaqueRect = plaque.rectTransform;
            Stretch(plaqueRect, new Vector2(0, 1), new Vector2(1, 1));
            plaqueRect.pivot = new Vector2(.5f, 1); plaqueRect.sizeDelta = new Vector2(0, height);
            Border(plaqueRect, Color.Lerp(skin.colors.gold, accent, style.typeBorderBlend));
            plaque.transform.SetSiblingIndex(requirements.GetSiblingIndex());
            plaque.gameObject.SetActive(hasCost && requirements.gameObject.activeSelf);
        }
        StyleCombatStats(card, root, skin, readingFont, accent);
        Border(root, Color.Lerp(skin.colors.gold, accent, style.typeBorderBlend));
        Rule(root, accent, Vector2.zero, Vector2.right);
        if (handPortrait)
        {
            // Rules stay on the full inspection face. At table scale the illustration, name,
            // price and combat values are what a player can actually read.
            float top = style.title.position.y - style.title.size.y * .5f - 5;
            float bottom = -style.size.y * .5f + 38;
            SetPiece(real, "Image", new Vector2(style.art.size.x, top - bottom), new Vector2(0, (top + bottom) * .5f), Color.white);
            real.Find("DescriptionBackground")?.gameObject.SetActive(false);
            var ribbon = Panel(root, "Hand type ribbon", new Color(.79f,.73f,.57f));
            ribbon.rectTransform.sizeDelta = new Vector2(style.art.size.x, 27);
            ribbon.rectTransform.anchoredPosition = new Vector2(0, -style.size.y * .5f + 20);
            var type = TextLabel(ribbon.transform, card.cardData.GetCardType().ToString().ToUpperInvariant(),
                skin.typography.mastheadFont, 16, new Color(.12f,.14f,.10f), Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            type.raycastTarget = false;
            foreach (string name in new[] { "Stat plaque", "Class plaque", "Status plaque" })
            {
                var piece = root.Find(name) as RectTransform;
                if (piece != null) piece.anchoredPosition += new Vector2(0, bottom + 22 - style.stats.position.y);
            }
            foreach (var text in new[] { card.CombatStatsLabel, card.ClassStatsLabel, card.StatusEffectsLabel })
                if (text != null) text.rectTransform.anchoredPosition += new Vector2(0, bottom + 22 - style.stats.position.y);
        }
    }

    /// <summary>
    /// Gives the combat numbers their own plaque on the artwork's lower-right corner. Card sizes the
    /// overlay for a token, where the stats are the only thing along the bottom and may take the
    /// whole width; a full card has a description and flavour line down there, so left at that size
    /// the numerals sit on top of the story text at several times its size.
    /// </summary>
    public static void StyleFieldCard(Card card, Board board)
    {
        var real = card.transform.Find("RealCard");
        real.Find("Image/Requirements")?.gameObject.SetActive(false);
        real.Find("Image/Cost plaque")?.gameObject.SetActive(false);
        var ribbon = card.transform.Find("Hand type ribbon");
        if (ribbon != null && card.cardData.GetCardType() == CardTypeEnum.Land)
        {
            foreach (var label in ribbon.GetComponentsInChildren<Text>()) label.enabled = false;
            var text = new GameObject("Land yield", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(ribbon,false);
            Stretch(text.rectTransform,Vector2.zero,Vector2.one);
            text.font = ReadingFontFor(SkinFor(card.transform)); text.spriteAsset=SpriteAssetFor(board);
            text.text=card.LandResourceSummary; text.fontSize=23; text.enableAutoSizing=true;
            text.fontSizeMin=12; text.fontSizeMax=23; text.color=new Color(.12f,.14f,.10f);
            text.alignment=TextAlignmentOptions.Center; text.raycastTarget=false;
        }
    }

    private static void StyleCombatStats(Card card, RectTransform root, BoardSkin skin, TMP_FontAsset readingFont, Color accent)
    {
        var style = skin.cards;
        StyleStatBadge(card.CombatStatsLabel, "Stat plaque", style.stats, root, skin, readingFont, accent);
        float left = style.art.position.x - style.art.size.x * .5f + 4;
        float right = style.stats.position.x - style.stats.size.x * .5f - 6;
        var classes = new BoardSkin.Piece(new Vector2(Mathf.Max(40, right - left), style.stats.size.y),
            new Vector2((left + right) * .5f, style.stats.position.y));
        StyleStatBadge(card.ClassStatsLabel, "Class plaque", classes, root, skin, readingFont, accent);
        var effects = new BoardSkin.Piece(new Vector2(style.art.size.x - 8, style.stats.size.y),
            new Vector2(style.art.position.x, style.stats.position.y + style.stats.size.y + 4));
        StyleStatBadge(card.StatusEffectsLabel, "Status plaque", effects, root, skin, readingFont, accent);
    }

    private static void StyleStatBadge(TMP_Text stats, string plaqueName, BoardSkin.Piece bounds,
        RectTransform root, BoardSkin skin, TMP_FontAsset readingFont, Color accent)
    {
        // Card hides the overlay outright on a type that has no combat numbers -- no plaque either.
        if (stats == null) return;
        var style = skin.cards;
        var plaque = Panel(root, plaqueName, skin.colors.ink);
        plaque.raycastTarget = false;
        Place(plaque.rectTransform, bounds);
        Border(plaque.rectTransform, Color.Lerp(skin.colors.gold, accent, style.typeBorderBlend));
        Place(stats.rectTransform, bounds);
        if (readingFont != null) { stats.font = readingFont; stats.fontSharedMaterial = readingFont.material; }
        stats.color = skin.colors.ivory;
        stats.fontStyle = FontStyles.Bold;
        stats.outlineWidth = 0;
        stats.alignment = TextAlignmentOptions.Center;
        stats.margin = style.statsMargin;
        // Attack and defence read as one unit, so they never wrap; auto-sizing between the two ends
        // of the range is what keeps a two-digit army inside the same plaque as a one-digit one.
        stats.textWrappingMode = TextWrappingModes.NoWrap;
        stats.enableAutoSizing = true;
        stats.fontSizeMin = style.statsFontRange.x;
        stats.fontSizeMax = style.statsFontRange.y;
        // Both live on the card root beside RealCard, so the plaque only has to land immediately
        // behind the numerals to sit between them and the artwork.
        plaque.transform.SetSiblingIndex(stats.transform.GetSiblingIndex());
        plaque.gameObject.SetActive(stats.gameObject.activeSelf);
    }

    private static void Place(RectTransform rect, BoardSkin.Piece piece)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        rect.sizeDelta = piece.size;
        rect.anchoredPosition = piece.position;
        rect.localScale = Vector3.one;
    }

    private static void ClearGeneratedCardChrome(RectTransform root, RectTransform real)
    {
        ClearChildrenNamed(root, "Obsidian card stock", "Frame", "Inlay", "Stat plaque", "Class plaque", "Status plaque", "Hand type ribbon");
        for (int i = real.childCount - 1; i >= 0; i--)
        {
            var child = real.GetChild(i);
            // The cost plaque is regenerated on every pass. Nothing draws a ribbon any more; that
            // strips one left behind in a scene by an earlier styling pass, which would otherwise
            // survive as authored hierarchy.
            foreach (var generated in child.GetComponentsInChildren<Transform>(true))
                if (generated.name == "Cost plaque" || generated.name == "Requirement ribbon")
                {
                    if (Application.isPlaying) Destroy(generated.gameObject);
                    else DestroyImmediate(generated.gameObject);
                }
        }
    }

    private static void ClearChildrenNamed(Transform parent, params string[] names)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (!System.Array.Exists(names, name => child.name == name)) continue;
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
    }

    /// <summary>Styles an authored token card as the same framed, captioned token used at runtime.</summary>
    public static Vector2 StyleTokenCard(Card card, Font interfaceFont, CardZoneVisualizer zone = null)
    {
        var skin = SkinFor(card.transform);
        var root = (RectTransform)card.transform;
        ClearGeneratedTokenChrome(root);
        card.ShowToken();
        card.CompactTokenInPlace();
        card.ApplyCompactInfoMaterial(StatMaterialFor(skin, zone));
        card.ScaleTokenVisual(TokenScaleFor(skin, card, out Vector2 tokenSize));
        root.sizeDelta = tokenSize + Vector2.up * skin.tokens.captionHeight;
        card.SetTokenPreviewOffset(Vector2.up * skin.tokens.captionHeight * .5f);
        var backing = Panel(root, "Token stock", skin.colors.ink);
        Stretch(backing.rectTransform, Vector2.zero, Vector2.one);
        backing.transform.SetAsFirstSibling();
        BoardSurface.Dress(backing, skin.colors.gold, false, true);
        Border(backing.rectTransform, skin.colors.tokenBorder, skin.tokens.borderWidth);
        string caption = System.Text.RegularExpressions.Regex.Replace(card.cardData?.name ?? string.Empty, "(?<=[a-z])(?=[A-Z])", " ");
        var label = TextLabel(root, caption, interfaceFont, skin.tokens.captionMaxSize, skin.colors.ivory,
            Vector2.zero, Vector2.right, TextAnchor.MiddleCenter);
        label.name = "Token caption";
        label.rectTransform.pivot = new Vector2(.5f, 0);
        label.rectTransform.sizeDelta = new Vector2(-skin.tokens.captionInset * 2, skin.tokens.captionHeight);
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = skin.tokens.captionMinSize;
        label.resizeTextMaxSize = skin.tokens.captionMaxSize;
        return root.sizeDelta;
    }

    private static void ClearGeneratedTokenChrome(RectTransform root)
    {
        ClearChildrenNamed(root, "Token stock", "Token caption");
    }

    private static void SetPiece(RectTransform real, string name, Vector2 size, Vector2 position, Color background)
    {
        var piece = real.Find(name) as RectTransform;
        if (piece == null) return;
        foreach (var layout in piece.GetComponents<Behaviour>())
            if (layout is LayoutGroup || layout is ContentSizeFitter) layout.enabled = false;
        piece.anchorMin = piece.anchorMax = piece.pivot = Vector2.one * .5f;
        piece.sizeDelta = size; piece.anchoredPosition = position; piece.localScale = Vector3.one;
        var image = piece.GetComponent<Image>();
        if (image != null && name.EndsWith("Background")) { image.sprite = null; image.color = background; }
    }
}

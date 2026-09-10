using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

// The card face, ported from Runeboard's Assets/Scripts/UI/Card.cs.
//
// Everything about how a card *looks and behaves as a piece of UI* is kept: the token <-> card flip,
// art resolution, the type-colored border, the hand-draw typewriter, the encounter "?" cover and its
// fade reveal, requirement icons, token tinting for the bloom wheel, and the display-only clones the
// play-flight animations fly around.
//
// Everything that made it a Runeboard component is gone. Where the original called Game.Instance,
// Board.Instance, DeckManager, ActionsManager, Illustrations, Colors, CursorManager, Sounds or
// HexPathRenderer, this asks CardServices instead — see CardServices.cs. Nothing is required: with no
// services installed the prefabs still render, every card reads as playable, and clicks are inert.
//
// The serialized field block below is unchanged in name, type and order from the original, so
// Card.prefab / TokenCard.prefab / TokenCardMasked.prefab keep their wiring with no reauthoring.
[RequireComponent(typeof(CanvasGroup))]
public class Card : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    // readonly, so there is nothing for the auto cleanup to reset to; OnEnable/OnDisable keep it
    // accurate on their own, which empties it as play mode tears the cards down.
    [NoAutoStaticsCleanup]
    private static readonly List<Card> activeCards = new();

    // Call after anything that could change what is playable (a resource spent, a turn ended).
    public static void RequestInteractionRefreshAll()
    {
        for (int i = 0; i < activeCards.Count; i++)
        {
            if (activeCards[i] != null)
            {
                activeCards[i].UpdateInteractableState();
            }
        }
    }

    [Header("UI References")]
    [FormerlySerializedAs("title")]
    [SerializeField] private TextMeshProUGUI titleText;
    [FormerlySerializedAs("description")]
    [SerializeField] private TextMeshProUGUI descriptionText;
    [FormerlySerializedAs("type")]
    [SerializeField] private Hover hover;
    [FormerlySerializedAs("requirements")]
    [SerializeField] private TextMeshProUGUI requirementsText;
    [FormerlySerializedAs("image")]
    [SerializeField] private Image cardArtImage;
    [FormerlySerializedAs("borderImage")]
    [SerializeField] private Image cardBackgroundImage;
    [SerializeField] private GameObject discardButton;
    [SerializeField] private Image deckTypeImage;

    [Header("Token / Card Flip")]
    [SerializeField] private Image tokenImage;
    [SerializeField] private Image tokenBorder;
    [SerializeField] private CanvasGroup tokenCanvasGroup;
    [SerializeField] private CanvasGroup realCardCanvasGroup;
    [SerializeField] private TextMeshProUGUI environmentalSprite;
    [SerializeField] private TMP_SpriteAsset compactSpriteAsset;
    [Tooltip("Material for the combat/resource numerals. Left empty the overlays keep the prefab's " +
             "own material, which is what full cards use; the board assigns a per-side material to " +
             "tokens. Re-applied on every refresh, so it never reverts.")]
    [SerializeField] private Material compactInfoMaterial;
    [SerializeField] private TextMeshProUGUI combatStatsText;
    [SerializeField] private TextMeshProUGUI landResourcesText;
    [SerializeField] private TextMeshProUGUI classStatsText;
    [SerializeField] private TextMeshProUGUI statusEffectsText;

    [Header("Tuning")]
    // Requirement-message colour used to live here as a per-prefab field, which meant two cards in
    // the same scene could disagree and no skin could reach either. It comes from
    // CardServices.FaceStyle now -- see ICardFaceStyle.
    [SerializeField] private bool showRequirementWarnings = true;
    [SerializeField] private bool showCloseIcon = true;
    [Tooltip("If false, the card's description text appears instantly instead of being typed out.")]
    [SerializeField] private bool typewriterEffect = true;
    [Tooltip("True on TokenCard instances that only carry the compact token visual: hovering unfolds the card into CardCenterPreview instead of flipping the (absent) RealCard subtree in place.")]
    [SerializeField] private bool isTokenOnlyPresentation;

    /// <summary>True on TokenCard.prefab, false on Card.prefab: which of the two this instance is.</summary>
    public bool IsTokenOnlyPresentation => isTokenOnlyPresentation;

    public CardData cardData { get; private set; }

    // Refreshed by UpdateInteractableState (RequestInteractionRefreshAll runs it on every relevant
    // state change); read each frame by anything that tints tokens by playability.
    public bool LastKnownPlayable { get; private set; } = true;
    public bool IsPlayInProgress { get; private set; }

    private CanvasGroup canvasGroup;
    private LayoutElement layoutElement;
    private RectTransform rectTransform;
    private Graphic rootHitGraphic;

    public bool SuppressHoverEffects { get; set; }
    public bool UseCardArtFolderOnly { get; set; }

    public bool TypewriterEffect
    {
        get => typewriterEffect;
        set => typewriterEffect = value;
    }

    public bool ShowRequirementWarnings
    {
        get => showRequirementWarnings;
        set
        {
            showRequirementWarnings = value;
            if (cardData != null) UpdateInteractableState();
        }
    }

    public bool ShowCloseIcon
    {
        get => showCloseIcon;
        set
        {
            showCloseIcon = value;
            UpdateDiscardButtonState();
        }
    }

    private bool lockedToRealCard;
    public void SetAvatarUnlocked(bool unlocked)
    {
        if (descriptionText != null)
            descriptionText.text = unlocked ? baseDescription : "Play from hand to unlock abilities";
        foreach (var image in GetComponentsInChildren<UnityEngine.UI.Image>(true))
        {
            if (image.name != "Image") continue;
            var color = image.color;
            color.a = unlocked ? 1f : .4f;
            image.color = color;
        }
    }

    private string baseDescription = string.Empty;
    private Image encounterArtOverlay;
    private TextMeshProUGUI encounterQuestionMark;
    private Image encounterTokenOverlay;
    private TextMeshProUGUI encounterTokenQuestionMark;
    private Coroutine descriptionTypewriterCoroutine;
    private bool isEnvironmentalPresentation;
    private bool environmentalPreviewHovered;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        canvasGroup.alpha = 1f;

        layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = gameObject.AddComponent<LayoutElement>();
        }
        layoutElement.ignoreLayout = false;

        rectTransform = GetComponent<RectTransform>();
        rootHitGraphic = GetComponent<Graphic>();
        EnsureTokenHoverHitArea();

        BindLegacyPrefabReferences();
        RestrictRaycastsToRootCard();
        UpdateDiscardButtonState();

        activeCards.Add(this);
    }

    private void OnEnable()
    {
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        if (layoutElement == null)
        {
            layoutElement = GetComponent<LayoutElement>();
        }
        if (layoutElement != null)
        {
            layoutElement.ignoreLayout = false;
        }

        BindLegacyPrefabReferences();
        RestrictRaycastsToRootCard();
        if (cardData != null)
        {
            UpdateInteractableState();
        }
    }

    private void OnDestroy()
    {
        activeCards.Remove(this);
    }

    private void Update()
    {
        if (!isEnvironmentalPresentation || cardData == null) return;

        // Must resolve to the root canvas, not whichever nested sort-order Canvas happens to sit
        // closest in the hierarchy — a nested Canvas added purely for sortingOrder overrides
        // defaults to ScreenSpaceOverlay/no camera, which silently breaks screen-point containment
        // math if the actual root canvas renders via a camera.
        Canvas canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        Vector2 pointer = Input.mousePosition;
        bool hovered = IsPointerInside(rectTransform, pointer, eventCamera)
            || IsPointerInside(tokenImage != null ? tokenImage.rectTransform : null, pointer, eventCamera)
            || IsPointerInside(tokenBorder != null ? tokenBorder.rectTransform : null, pointer, eventCamera)
            || IsPointerInside(environmentalSprite != null ? environmentalSprite.rectTransform : null, pointer, eventCamera);

        if (hovered == environmentalPreviewHovered) return;
        environmentalPreviewHovered = hovered;

        CardCenterPreview preview = CardCenterPreview.Instance != null
            ? CardCenterPreview.Instance
            : FindAnyObjectByType<CardCenterPreview>();
        if (hovered) preview?.ShowPreview(cardData, hoverDriven: true);
        else preview?.HidePreview();
    }

    private static bool IsPointerInside(RectTransform target, Vector2 pointer, Camera eventCamera)
    {
        return target != null
            && target.gameObject.activeInHierarchy
            && RectTransformUtility.RectangleContainsScreenPoint(target, pointer, eventCamera);
    }

    private void OnDisable()
    {
        if (!environmentalPreviewHovered) return;
        environmentalPreviewHovered = false;
        CardCenterPreview.Instance?.HidePreview();
    }

    // The pulse effect (which image, where it sits) is authored directly in the prefab as a
    // CardEnvironmentalPulseEffect component on a child Image; this just toggles it.
    public void SetEnvironmentalPulse(bool active)
    {
        GameObject target = tokenCanvasGroup != null ? tokenCanvasGroup.gameObject : gameObject;
        CardEnvironmentalPulseEffect pulse = target.GetComponentInChildren<CardEnvironmentalPulseEffect>(true);
        if (pulse != null) pulse.enabled = active;
    }

    public void Initialize(CardData data, bool startAsToken = true)
    {
        if (data == null) return;
        cardData = data;
        EnsureCompactInfoVisuals();
        BindLegacyPrefabReferences();
        RestrictRaycastsToRootCard();

        if (titleText != null) titleText.text = FormatCardTitle(data.name);
        if (hover != null) hover.Initialize(FormatCardTypeLabel(data.GetCardType()));
        ApplyCardTypeColor(data.GetCardType());

        // Only the active environmental card shows this icon; hidden by default so it never leaks
        // onto hand cards. Some legacy card prefabs resolve this TMP reference on the card root
        // itself — never deactivate the whole card while trying to hide only the icon.
        if (environmentalSprite != null && environmentalSprite.gameObject != gameObject)
            environmentalSprite.gameObject.SetActive(false);

        if (descriptionText != null)
        {
            baseDescription = GetActionDescription(data);
            descriptionText.text = baseDescription;
        }
        RefreshCompactInfoVisuals();

        if (requirementsText != null)
        {
            requirementsText.text = BuildRequirementsText(data);
        }

        {
            Sprite sprite = ResolveCardArtwork(data);

            if (cardArtImage != null)
            {
                cardArtImage.sprite = sprite;
                cardArtImage.enabled = sprite != null;

                if (cardArtImage.GetComponent<CardShineEffect>() == null)
                    cardArtImage.gameObject.AddComponent<CardShineEffect>();
            }

            // Token-only cards never wire cardArtImage, so tokenImage must be resolved
            // unconditionally or it keeps whatever placeholder sprite was last authored on the
            // prefab regardless of which card is shown.
            if (tokenImage != null)
            {
                tokenImage.sprite = sprite;
                tokenImage.enabled = sprite != null;
            }
        }

        lockedToRealCard = !startAsToken;
        if (startAsToken) ShowToken();
        else ShowRealCard();

        if (deckTypeImage != null)
        {
            Sprite deckSprite = null;
            if (!string.IsNullOrWhiteSpace(data.deckSpriteName))
            {
                CardServices.Art?.TryGetSprite(data.deckSpriteName, false, out deckSprite);
            }

            // Explicitly clear/hide when there is nothing to show, instead of leaving whatever
            // placeholder sprite is authored on the prefab enabled (most cards have no deck badge).
            deckTypeImage.sprite = deckSprite;
            deckTypeImage.enabled = deckSprite != null;
        }

        if (data.IsEncounterCard() && !data.encounterRevealed)
        {
            SetupEncounterHiddenVisuals(data);
        }
        else
        {
            // This Card instance may have previously shown an unrevealed encounter card (a recycled
            // slot, say) — its "?" overlay is only ever removed by the animated reveal flow, so a
            // non-encounter card reused into the same instance must clear it here.
            ClearEncounterHiddenVisualsInstant();
        }

        UpdateInteractableState();

        if (!data.hasShownHandAnimation && descriptionText != null && !string.IsNullOrEmpty(baseDescription))
        {
            if (!TypewriterEffect)
            {
                descriptionText.text = baseDescription;
                data.hasShownHandAnimation = true;
            }
            else
            {
                // Show the mechanical text immediately and type only the flavor quote, so a player
                // can read what the card does without waiting out the animation.
                string quoteBlock = data.GetQuoteBlock();
                if (!string.IsNullOrWhiteSpace(quoteBlock) && baseDescription.Contains(quoteBlock))
                {
                    int quoteStart = baseDescription.LastIndexOf(quoteBlock, StringComparison.Ordinal);
                    string immediateText = baseDescription.Substring(0, quoteStart).TrimEnd();
                    descriptionText.text = immediateText;
                    descriptionTypewriterCoroutine = StartCoroutine(HandDrawTypewriterCoroutine("\n\n" + quoteBlock, data, append: true));
                }
                else
                {
                    descriptionText.text = string.Empty;
                    descriptionTypewriterCoroutine = StartCoroutine(HandDrawTypewriterCoroutine(baseDescription, data));
                }
            }
        }
    }

    // Centered hover previews must be fully initialized without starting the one-time hand-draw
    // typewriter coroutine or mutating that animation flag on the real CardData.
    public void InitializePreview(CardData data)
    {
        if (data == null) return;
        bool hadShownHandAnimation = data.hasShownHandAnimation;
        data.hasShownHandAnimation = true;
        Initialize(data, startAsToken: false);
        data.hasShownHandAnimation = hadShownHandAnimation;
    }

    // Minimal initialize: applies only what the round token visual needs (art + type-colored
    // border). Full Initialize evaluates playability and builds description text; menus and
    // selection screens that only show tokens don't need either.
    public void InitializeTokenVisualOnly(CardData data)
    {
        if (data == null) return;
        cardData = data;
        EnsureCompactInfoVisuals();
        BindLegacyPrefabReferences();
        ApplyCardTypeColor(data.GetCardType());
        if (environmentalSprite != null && environmentalSprite.gameObject != gameObject)
            environmentalSprite.gameObject.SetActive(false);
        Sprite sprite = ResolveCardArtwork(data);
        if (tokenImage != null)
        {
            tokenImage.sprite = sprite;
            // Never draw a sprite-less Image — it renders as a solid white square.
            tokenImage.enabled = sprite != null;
        }
        RefreshCompactInfoVisuals();
    }

    private void EnsureCompactInfoVisuals()
    {
        if (GetComponent<CardKeywordHover>() == null) gameObject.AddComponent<CardKeywordHover>();
        TMP_SpriteAsset spriteAsset = compactSpriteAsset != null ? compactSpriteAsset : (descriptionText != null ? descriptionText.spriteAsset : null);
        if (spriteAsset == null)
        {
            TextMeshProUGUI existingText = GetComponentInChildren<TextMeshProUGUI>(true);
            spriteAsset = existingText != null ? existingText.spriteAsset : null;
        }
        Transform compactParent = transform;
        if (isTokenOnlyPresentation && tokenImage != null)
        {
            // The artwork Image is the only rect guaranteed to match the visible token.  Keep all
            // compact information inside it and mask it so no resource glyph can escape onto the
            // board when a token is particularly small.
            compactParent = tokenImage.transform;
            if (tokenImage.GetComponent<RectMask2D>() == null)
                tokenImage.gameObject.AddComponent<RectMask2D>();
        }
        if (combatStatsText == null)
            combatStatsText = CreateOverlay("CombatStats", compactParent);
        if (landResourcesText == null)
            landResourcesText = CreateOverlay("LandResources", compactParent);
        if (classStatsText == null)
            classStatsText = CreateOverlay("ClassStats", compactParent);
        if (statusEffectsText == null)
            statusEffectsText = CreateOverlay("StatusEffects", compactParent);

        ConfigureOverlay(classStatsText,
            isTokenOnlyPresentation ? new Vector2(.08f, .60f) : new Vector2(.03f, .30f),
            isTokenOnlyPresentation ? new Vector2(.92f, .96f) : new Vector2(.60f, .40f),
            isTokenOnlyPresentation ? 36f : 20f, TextAlignmentOptions.Center);
        classStatsText.textWrappingMode = TextWrappingModes.NoWrap;
        classStatsText.fontSizeMin = 6f;
        ConfigureOverlay(statusEffectsText,
            isTokenOnlyPresentation ? new Vector2(.12f, .34f) : new Vector2(.04f, .42f),
            isTokenOnlyPresentation ? new Vector2(.88f, .58f) : new Vector2(.96f, .52f),
            isTokenOnlyPresentation ? 28f : 20f, TextAlignmentOptions.Center);
        statusEffectsText.fontSizeMin = 6f;
        statusEffectsText.textWrappingMode = TextWrappingModes.NoWrap;

        // Configured on every refresh, not just on creation: the card prefabs ship their own
        // CombatStats and LandResources nodes, so anything left inside the creation branch is dead
        // code for them and the prefab's own sizing silently wins.
        //
        // A token has no other bottom furniture, so the stats take its full width there and may be
        // as large as they like. A full card is the opposite case: the bottom third is the
        // description and the flavour line, so the numerals get a small badge tucked into the
        // artwork's lower-right corner instead -- a token-sized band there buries the story text.
        ConfigureOverlay(combatStatsText,
            isTokenOnlyPresentation ? new Vector2(.02f, .00f) : new Vector2(.62f, .30f),
            isTokenOnlyPresentation ? new Vector2(.98f, .40f) : new Vector2(.97f, .40f),
            isTokenOnlyPresentation ? 54f : 20f,
            isTokenOnlyPresentation ? TextAlignmentOptions.Bottom : TextAlignmentOptions.Center);
        // Both stats read as a single unit, so they must never wrap. With wrapping off and a low
        // floor, auto-sizing settles on the largest size that fits them on one line.
        combatStatsText.textWrappingMode = TextWrappingModes.NoWrap;
        combatStatsText.fontSizeMin = 6f;

        // The resource grid is the whole point of a land token, and a land can grant up to seven of
        // them, so it claims the full art area -- auto-sizing is bounded by the box, and the old
        // 64%-tall band was what kept the glyphs small however high the font ceiling went.
        ConfigureOverlay(landResourcesText, new Vector2(.02f, .02f), new Vector2(.98f, .98f), isTokenOnlyPresentation ? 60f : 84f, TextAlignmentOptions.Center);
        // The floor stays low so auto-sizing can settle on a fit instead of truncating a land that
        // grants several resources.
        landResourcesText.fontSizeMin = 6f;

        if (spriteAsset != null)
        {
            combatStatsText.spriteAsset = spriteAsset;
            landResourcesText.spriteAsset = spriteAsset;
            classStatsText.spriteAsset = spriteAsset;
            statusEffectsText.spriteAsset = spriteAsset;
        }
        ApplyCompactInfoMaterial(compactInfoMaterial);
        GetComponent<CardKeywordHover>().RefreshTargets();
    }

    /// <summary>The combat numerals overlay, so presentation code can restyle it for a full card.</summary>
    public TMP_Text CombatStatsLabel => combatStatsText;
    public TMP_Text ClassStatsLabel => classStatsText;
    public TMP_Text StatusEffectsLabel => statusEffectsText;

    private static TextMeshProUGUI CreateOverlay(string overlayName, Transform parent)
    {
        GameObject go = new(overlayName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        return go.GetComponent<TextMeshProUGUI>();
    }

    private static void ConfigureOverlay(TextMeshProUGUI text, Vector2 min, Vector2 max, float size, TextAlignmentOptions alignment)
    {
        RectTransform rt = text.rectTransform; rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero;
        // Colour is deliberately not set here: this runs on every refresh, and it used to reset the
        // per-side tint (and any hand edit in the inspector) back to white. EnsureCompactInfoVisuals
        // re-applies compactInfoColor instead.
        text.fontSize = size; text.fontStyle = FontStyles.Bold; text.alignment = alignment; text.raycastTarget = false; text.richText = true;
        text.enableAutoSizing = true; text.fontSizeMin = Mathf.Max(6f, size * .45f); text.fontSizeMax = size; text.overflowMode = TextOverflowModes.Truncate;
    }

    private void RefreshCompactInfoVisuals()
    {
        if (cardData == null) return;
        CardTypeEnum type = cardData.GetCardType();
        ShowOverlay(combatStatsText, cardData.GetCombatStatsText());
        ShowOverlay(classStatsText, cardData.GetClassStatsText(isTokenOnlyPresentation));
        ShowOverlay(statusEffectsText, cardData.GetStatusEffectsText(isTokenOnlyPresentation));
        ShowOverlay(landResourcesText, type == CardTypeEnum.Land && isTokenOnlyPresentation ? BuildLandResourceVisual() : string.Empty);
    }

    private static void ShowOverlay(TextMeshProUGUI text, string content)
    {
        text.text = content;
        bool visible = !string.IsNullOrEmpty(content);
        text.gameObject.SetActive(visible);
    }

    /// <summary>
    /// Puts the stat overlays on a shared material. One asset per side rather than a per-card tint,
    /// so face colour, outline and dilate are all editable in the inspector and every card of that
    /// side follows. A null material leaves whatever the prefab authored.
    /// </summary>
    public void ApplyCompactInfoMaterial(Material material)
    {
        compactInfoMaterial = material;
        if (material == null) return;
        if (combatStatsText != null) combatStatsText.fontSharedMaterial = material;
        if (landResourcesText != null) landResourcesText.fontSharedMaterial = material;
        if (classStatsText != null) classStatsText.fontSharedMaterial = material;
        if (statusEffectsText != null) statusEffectsText.fontSharedMaterial = material;
    }

    public void SetStatusEffects(IEnumerable<StatusEffects> effects)
    {
        if (cardData == null) return;
        cardData.statusEffects = effects != null ? new List<StatusEffects>(effects) : new();
        RefreshCompactInfoVisuals();
        var plaque = transform.Find("Status plaque");
        if (plaque != null) plaque.gameObject.SetActive(statusEffectsText.gameObject.activeSelf);
    }

    private string BuildLandResourceVisual()
    {
        List<string> parts = new();
        if (cardData.leatherGranted > 0) parts.Add(cardData.leatherGranted + CardData.SpriteTag("leather"));
        if (cardData.timberGranted > 0) parts.Add(cardData.timberGranted + CardData.SpriteTag("timber"));
        if (cardData.mountsGranted > 0) parts.Add(cardData.mountsGranted + CardData.SpriteTag("mounts"));
        if (cardData.ironGranted > 0) parts.Add(cardData.ironGranted + CardData.SpriteTag("iron"));
        if (cardData.steelGranted > 0) parts.Add(cardData.steelGranted + CardData.SpriteTag("steel"));
        if (cardData.mithrilGranted > 0) parts.Add(cardData.mithrilGranted + CardData.SpriteTag("mithril"));
        if (cardData.goldGranted > 0) parts.Add(cardData.goldGranted + CardData.SpriteTag("gold"));
        return string.Join(" ", parts);
    }

    // Reveals the environmental glyph, rendered via the normalized card name (the same scheme as the
    // sprite-asset m_Name fields, e.g. "wind", "sun", "redsun").
    public void ShowEnvironmentalSprite()
    {
        if (environmentalSprite == null) return;
        isEnvironmentalPresentation = true;
        environmentalSprite.gameObject.SetActive(true);
        // RestrictRaycastsToRootCard normally disables child text raycasts. The environmental glyph
        // is offset from the token's border, however, so it must be a hit target itself for pointer
        // events to bubble up to this Card component.
        environmentalSprite.raycastTarget = true;
        // isEnvironmentalPresentation just became true, so retry the hit-area setup Awake() skipped
        // (it ran before this flag was set).
        EnsureTokenHoverHitArea();
        environmentalSprite.text = cardData != null
            ? $"<sprite name=\"{CardNameUtility.Normalize(cardData.name)}\">"
            : string.Empty;
    }

    private IEnumerator HandDrawTypewriterCoroutine(string text, CardData data, bool append = false)
    {
        if (append)
            yield return StartCoroutine(AppendTypewriterEffectCoroutine(descriptionText, text));
        else
            yield return StartCoroutine(TypewriterEffectCoroutine(descriptionText, text));
        if (data != null) data.hasShownHandAnimation = true;
        descriptionTypewriterCoroutine = null;
        UpdateInteractableState();
    }

    private IEnumerator AppendTypewriterEffectCoroutine(TextMeshProUGUI textComponent, string appendText)
    {
        if (textComponent == null || string.IsNullOrEmpty(appendText)) yield break;
        string prefix = textComponent.text;
        float delay = Mathf.Min(0.05f, 2f / appendText.Length);
        for (int i = 0; i < appendText.Length; i++)
        {
            if (textComponent == null) yield break;
            textComponent.text = prefix + appendText.Substring(0, i + 1);
            yield return new WaitForSecondsRealtime(delay);
        }
    }

    private IEnumerator TypewriterEffectCoroutine(TextMeshProUGUI textComponent, string fullText)
    {
        if (textComponent == null || string.IsNullOrEmpty(fullText)) yield break;
        textComponent.text = string.Empty;
        float delay = Mathf.Min(0.05f, 2f / fullText.Length);
        foreach (char c in fullText)
        {
            if (textComponent == null) yield break;
            textComponent.text += c;
            yield return new WaitForSecondsRealtime(delay);
        }
    }

    // Tries each name the card might file its art under, in the original's order of preference.
    private Sprite ResolveCardArtwork(CardData data)
    {
        ICardArtSource art = CardServices.Art;
        if (data == null || art == null) return null;

        string[] candidates =
        {
            data.spriteName,
            data.portraitName,
            data.name,
            data.actionClassName,
            data.action
        };

        foreach (string candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            if (art.TryGetSprite(candidate, UseCardArtFolderOnly, out Sprite sprite)) return sprite;
        }

        return null;
    }

    private void ClearEncounterHiddenVisualsInstant()
    {
        if (encounterArtOverlay != null)
        {
            Destroy(encounterArtOverlay.gameObject);
            encounterArtOverlay = null;
            encounterQuestionMark = null;
        }
        if (encounterTokenOverlay != null)
        {
            Destroy(encounterTokenOverlay.gameObject);
            encounterTokenOverlay = null;
            encounterTokenQuestionMark = null;
        }
    }

    // Covers an unrevealed encounter card's art with a veiled "?" panel, on both the card face and
    // the token. RevealEncounterCard fades these back off. Colours and glyph size come from
    // CardServices.FaceStyle so a skin owns them; the built-in default is the black/white/64 this
    // shipped with.
    private void SetupEncounterHiddenVisuals(CardData data)
    {
        if (titleText != null) titleText.text = "Encounter";
        var faceStyle = CardServices.FaceStyle;

        if (encounterArtOverlay == null && cardArtImage != null)
        {
            var overlayGo = new GameObject("EncounterOverlay", typeof(RectTransform), typeof(Image));
            overlayGo.transform.SetParent(cardArtImage.transform, false);
            var overlayRect = overlayGo.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            encounterArtOverlay = overlayGo.GetComponent<Image>();
            encounterArtOverlay.color = faceStyle.EncounterOverlayColor;

            var qGo = new GameObject("QuestionMark", typeof(RectTransform), typeof(TextMeshProUGUI));
            qGo.transform.SetParent(overlayGo.transform, false);
            var qRect = qGo.GetComponent<RectTransform>();
            qRect.anchorMin = Vector2.zero;
            qRect.anchorMax = Vector2.one;
            qRect.offsetMin = Vector2.zero;
            qRect.offsetMax = Vector2.zero;
            encounterQuestionMark = qGo.GetComponent<TextMeshProUGUI>();
            encounterQuestionMark.text = "?";
            encounterQuestionMark.fontSize = faceStyle.EncounterGlyphSize;
            encounterQuestionMark.alignment = TextAlignmentOptions.Center;
            encounterQuestionMark.color = faceStyle.EncounterGlyphColor;
            encounterQuestionMark.fontStyle = FontStyles.Bold;
        }

        if (encounterTokenOverlay == null && tokenImage != null)
        {
            var tokenOverlayGo = new GameObject("EncounterTokenOverlay", typeof(RectTransform), typeof(Image));
            tokenOverlayGo.transform.SetParent(tokenImage.transform, false);
            var tokenOverlayRect = tokenOverlayGo.GetComponent<RectTransform>();
            tokenOverlayRect.anchorMin = Vector2.zero;
            tokenOverlayRect.anchorMax = Vector2.one;
            tokenOverlayRect.offsetMin = Vector2.zero;
            tokenOverlayRect.offsetMax = Vector2.zero;
            encounterTokenOverlay = tokenOverlayGo.GetComponent<Image>();
            encounterTokenOverlay.color = faceStyle.EncounterOverlayColor;
            encounterTokenOverlay.raycastTarget = false;

            var tqGo = new GameObject("QuestionMark", typeof(RectTransform), typeof(TextMeshProUGUI));
            tqGo.transform.SetParent(tokenOverlayGo.transform, false);
            var tqRect = tqGo.GetComponent<RectTransform>();
            tqRect.anchorMin = Vector2.zero;
            tqRect.anchorMax = Vector2.one;
            tqRect.offsetMin = Vector2.zero;
            tqRect.offsetMax = Vector2.zero;
            encounterTokenQuestionMark = tqGo.GetComponent<TextMeshProUGUI>();
            encounterTokenQuestionMark.text = "?";
            encounterTokenQuestionMark.fontSize = faceStyle.EncounterGlyphSize;
            encounterTokenQuestionMark.alignment = TextAlignmentOptions.Center;
            encounterTokenQuestionMark.color = faceStyle.EncounterGlyphColor;
            encounterTokenQuestionMark.fontStyle = FontStyles.Bold;
            encounterTokenQuestionMark.raycastTarget = false;
        }

        // Runeboard named the hex the encounter waited at. With no board here, the card just says
        // an encounter is hidden; supply your own text by setting data.description instead.
        baseDescription = !string.IsNullOrWhiteSpace(data.description)
            ? data.description.Trim()
            : "An encounter waits to be investigated.";
        if (descriptionText != null) descriptionText.text = baseDescription;
    }

    // Fades the "?" cover off and types in the card's real title and description. Call it when the
    // encounter is actually entered; it sets encounterRevealed itself.
    public void RevealEncounterCard()
    {
        if (cardData == null || cardData.encounterRevealed) return;
        StartCoroutine(RevealEncounterCoroutine());
    }

    private IEnumerator RevealEncounterCoroutine()
    {
        const float FadeDuration = 0.8f;
        float elapsed = 0f;

        while (elapsed < FadeDuration)
        {
            if (this == null) yield break;
            float alpha = 1f - elapsed / FadeDuration;
            ApplyEncounterOverlayAlpha(alpha);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        ClearEncounterHiddenVisualsInstant();

        cardData.encounterRevealed = true;
        if (titleText != null) titleText.text = FormatCardTitle(cardData.name);

        string realDescription = GetActionDescription(cardData);
        if (TypewriterEffect)
        {
            yield return StartCoroutine(TypewriterEffectCoroutine(descriptionText, realDescription));
        }
        else if (descriptionText != null)
        {
            descriptionText.text = realDescription;
        }
        baseDescription = realDescription;
        UpdateInteractableState();
    }

    private void ApplyEncounterOverlayAlpha(float alpha)
    {
        SetGraphicAlpha(encounterArtOverlay, alpha);
        SetGraphicAlpha(encounterQuestionMark, alpha);
        SetGraphicAlpha(encounterTokenOverlay, alpha);
        SetGraphicAlpha(encounterTokenQuestionMark, alpha);
    }

    private static void SetGraphicAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null) return;
        Color c = graphic.color;
        c.a = alpha;
        graphic.color = c;
    }

    // Prefabs authored before the fields were named resolve their references by child name instead.
    private void BindLegacyPrefabReferences()
    {
        if (titleText == null) titleText = FindTextByName("Title");
        if (descriptionText == null) descriptionText = FindTextByName("Description");
        if (requirementsText == null) requirementsText = FindTextByName("Requirements");

        if (cardArtImage == null) cardArtImage = FindImageByName("Image");
        if (cardBackgroundImage == null) cardBackgroundImage = FindImageByName("Border");
        if (discardButton == null) discardButton = FindChildByName("Discard");
    }

    // The whole card is one hit target: children must not intercept pointer events, or hover would
    // flicker as the cursor crossed from art to text.
    private void RestrictRaycastsToRootCard()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null) continue;
            if (graphic.gameObject == gameObject) continue;
            if (hover != null && graphic.gameObject == hover.gameObject) continue;
            if (graphic.GetComponent<Selectable>() != null) continue;
            graphic.raycastTarget = false;
        }

        if (cardBackgroundImage != null)
        {
            cardBackgroundImage.raycastTarget = true;
        }
    }

    // Token-only cards have no expanded card background on their root. Give them a transparent UI
    // Graphic so pointer enter/exit covers the complete token rect rather than depending on one of
    // the small, offset child visuals receiving the raycast.
    private void EnsureTokenHoverHitArea()
    {
        if (!(isTokenOnlyPresentation || isEnvironmentalPresentation) || rootHitGraphic != null) return;

        Image hitArea = gameObject.AddComponent<Image>();
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;
        rootHitGraphic = hitArea;
    }

    private TextMeshProUGUI FindTextByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && string.Equals(texts[i].gameObject.name, name, StringComparison.OrdinalIgnoreCase))
            {
                return texts[i];
            }
        }
        return null;
    }

    private Image FindImageByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && string.Equals(images[i].gameObject.name, name, StringComparison.OrdinalIgnoreCase))
            {
                return images[i];
            }
        }
        return null;
    }

    private GameObject FindChildByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && string.Equals(children[i].name, name, StringComparison.OrdinalIgnoreCase))
            {
                return children[i].gameObject;
            }
        }
        return null;
    }

    // Runeboard fell back to ActionsManager.ResolveActionByRef(...).GetDescriptionForCard() when a
    // card carried no authored body. With the action pipeline gone, the card's own text is all there
    // is; a card with an empty body simply renders without one.
    private string GetActionDescription(CardData data)
    {
        if (data == null) return string.Empty;

        CardTypeEnum cardType = data.GetCardType();
        string typePrefix = FormatCardTypeLabel(cardType);
        // includeFoundingText was gated on the PC not already existing on the board. With no board,
        // a PC card always shows what founding it would allow.
        string body = data.GetRenderedDescription(includeFoundingText: true);
        return string.IsNullOrWhiteSpace(body) ? typePrefix : PrefixWithCardType(typePrefix, body);
    }

    private string PrefixWithCardType(string typePrefix, string text)
    {
        if (string.IsNullOrWhiteSpace(typePrefix)) return text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return typePrefix;
        return $"{typePrefix}. {text}";
    }

    // "GapOfRohan" -> "Gap Of Rohan". Cards are authored with run-together names matching their
    // art filenames.
    private string FormatCardTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        List<char> chars = new(value.Length + 4);
        for (int i = 0; i < value.Length; i++)
        {
            if (PcDescriptionBuilder.ShouldInsertWordSpace(value, i)) chars.Add(' ');
            chars.Add(value[i]);
        }

        string formatted = new string(chars.ToArray()).Trim().ToLowerInvariant();
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(formatted);
    }

    private Color GetCardTypeColor(CardTypeEnum cardType)
        => CardServices.Palette?.GetCardTypeColor(cardType) ?? Color.clear;

    private void ApplyCardTypeColor(CardTypeEnum cardType)
    {
        Color c = GetCardTypeColor(cardType);
        // Alpha 0 means "this type has no color" — leave the authored border alone rather than
        // painting it transparent.
        if (c.a < 0.01f) return;

        if (cardBackgroundImage != null)
            cardBackgroundImage.color = new Color(c.r, c.g, c.b, cardBackgroundImage.color.a);
        if (tokenBorder != null)
            tokenBorder.color = new Color(c.r, c.g, c.b, tokenBorder.color.a);
    }

    private string FormatCardTypeLabel(CardTypeEnum cardType)
    {
        string label = cardType switch
        {
            CardTypeEnum.PC => "PC",
            CardTypeEnum.Land => "Land",
            CardTypeEnum.Character => "Character",
            CardTypeEnum.Army => "Army",
            CardTypeEnum.Event => "Event",
            CardTypeEnum.Action => "Action",
            CardTypeEnum.Spell => "Spell",
            CardTypeEnum.Object => "Object",
            CardTypeEnum.Encounter => "Encounter",
            CardTypeEnum.Environmental => "Environmental",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(label)) return string.Empty;

        Color c = GetCardTypeColor(cardType);
        if (c.a < 0.01f) return label;

        return $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{label}</color>";
    }

    private string BuildRequirementsText(CardData data)
    {
        if (data == null) return string.Empty;
        List<string> reqs = new();

        if (data.GetCardType() != CardTypeEnum.Army)
        {
        AppendRequirement(reqs, "commander", data.commanderSkillRequired);
        AppendRequirement(reqs, "agent", data.agentSkillRequired);
        AppendRequirement(reqs, "emmissary", data.emissarySkillRequired);
        AppendRequirement(reqs, "mage", data.mageSkillRequired);
        }

        AppendRequirement(reqs, "gold", data.GetTotalGoldCost());

        AppendRequirement(reqs, "leather", data.leatherRequired);
        AppendRequirement(reqs, "timber", data.timberRequired);
        AppendRequirement(reqs, "mounts", data.mountsRequired);
        AppendRequirement(reqs, "iron", data.ironRequired);
        AppendRequirement(reqs, "steel", data.steelRequired);
        AppendRequirement(reqs, "mithril", data.mithrilRequired);

        string situationLabel = FormatSituationLabel(data);
        if (!string.IsNullOrWhiteSpace(situationLabel))
        {
            string costPart = reqs.Count > 0 ? $"\n{string.Join(" ", reqs)}" : string.Empty;
            return $"{situationLabel}{costPart}";
        }

        if (reqs.Count == 0) return string.Empty;
        return string.Join(" ", reqs);
    }

    private string FormatSituationLabel(CardData data)
    {
        if (data == null || data.GetCardType() != CardTypeEnum.Action) return string.Empty;
        CardSituationEnum situation = data.GetSituation();
        if (situation == CardSituationEnum.None) return string.Empty;

        // TODO: We will to format the name once situations are in
        string label = situation.ToString();

        return string.IsNullOrWhiteSpace(label) ? string.Empty : $"When: {label}";
    }

    private void AppendRequirement(List<string> requirements, string spriteName, int count)
    {
        if (requirements == null || string.IsNullOrWhiteSpace(spriteName) || count <= 0) return;
        requirements.Add($"{count}<sprite name=\"{spriteName}\">");
    }

    // Asks the installed playability source, if any. With none installed every card is playable —
    // the right answer for a project that has art and layout but no rules yet.
    public bool EvaluateIsPlayable()
    {
        if (cardData == null) return false;

        ICardPlayabilitySource source = CardServices.Playability;
        if (source == null)
        {
            cardData.isPlayable = true;
            return true;
        }

        CardPlayabilityResult result = source.Evaluate(cardData) ?? cardData.playability;
        cardData.playability = result;
        cardData.isPlayable = result.isPlayable;
        return result.isPlayable;
    }

    public void UpdateInteractableState()
    {
        if (cardData == null) return;

        bool isTypewriting = descriptionTypewriterCoroutine != null;
        bool isPlayable = EvaluateIsPlayable();
        // Cached so token tinting can fade unplayable tokens without re-running the evaluation
        // every frame.
        LastKnownPlayable = isPlayable;

        if (!SuppressHoverEffects && canvasGroup != null)
        {
            canvasGroup.alpha = isPlayable ? 1f : 0.5f;
            canvasGroup.interactable = isPlayable;
            canvasGroup.blocksRaycasts = true;
        }

        if (!isTypewriting && descriptionText != null)
        {
            if (isPlayable || !showRequirementWarnings)
            {
                descriptionText.text = baseDescription;
            }
            else
            {
                string errorText = BuildRequirementsMessageText();
                if (!string.IsNullOrWhiteSpace(errorText))
                {
                    string colorHex = ColorUtility.ToHtmlStringRGB(CardServices.FaceStyle.RequirementsMessageColor);
                    string separator = string.IsNullOrWhiteSpace(baseDescription) ? string.Empty : "\n";
                    descriptionText.text = $"{baseDescription}{separator}<color=#{colorHex}>{errorText}</color>";
                }
                else
                {
                    descriptionText.text = baseDescription;
                }
            }
        }

        UpdateDiscardButtonState();
    }

    // Runeboard assembled these lines here by reading the selected Character's skills and the
    // Leader's resource piles. The playability source now owns that knowledge and hands back
    // finished lines; the standard flags still get a default line if the source left messages empty.
    private string BuildRequirementsMessageText()
    {
        CardPlayabilityResult p = cardData?.playability;
        if (p == null) return string.Empty;

        if (p.messages != null && p.messages.Count > 0) return string.Join("\n", p.messages);

        List<string> messages = new();
        if (p.failsLevelRequirements) messages.Add("<sprite name=\"error\">Skill requirements not met.");
        if (p.failsResourceRequirements) messages.Add("<sprite name=\"error\">Not enough resources.");
        if (p.failsStartingCityRequirement && !string.IsNullOrWhiteSpace(p.startingCityReason))
            messages.Add($"<sprite name=\"error\">{p.startingCityReason}");
        if (p.failsAlreadyActioned) messages.Add("<sprite name=\"error\">Already acted this turn.");
        else if (p.failsActionConditions) messages.Add("<sprite name=\"error\">Action conditions not met.");
        if (p.failsCardHistoryRequirements)
        {
            messages.Add(string.IsNullOrWhiteSpace(p.cardHistoryReason)
                ? "<sprite name=\"error\">Card history requirements not met."
                : $"<sprite name=\"error\">{p.cardHistoryReason}");
        }

        return string.Join("\n", messages);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!SuppressHoverEffects)
        {
            if (isTokenOnlyPresentation || isEnvironmentalPresentation)
                CardCenterPreview.Instance?.ShowPreview(cardData, hoverDriven: true);
            else ShowRealCard();
        }

        CardServices.Feedback?.OnCardHoverEnter(cardData, LastKnownPlayable);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!SuppressHoverEffects)
        {
            if (isTokenOnlyPresentation || isEnvironmentalPresentation)
                CardCenterPreview.Instance?.HidePreview();
            else ShowToken();
        }

        CardServices.Feedback?.OnCardHoverExit(cardData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        Play();
    }

    // Plays the card through the installed interaction handler. Safe to call from a custom layout
    // (a bloom wheel, a hand) that does its own hit testing: it re-checks LastKnownPlayable rather
    // than trusting the root CanvasGroup's flag, which such layouts deliberately bypass.
    public void Play()
    {
        if (IsPlayInProgress || cardData == null) return;
        if (!LastKnownPlayable) return;

        ICardInteractionHandler handler = CardServices.Interaction;
        if (handler == null) return;

        IsPlayInProgress = true;
        bool consumed;
        try
        {
            consumed = handler.TryPlay(this, cardData);
        }
        finally
        {
            IsPlayInProgress = false;
        }

        if (consumed && this != null && gameObject != null)
        {
            StartCoroutine(AnimateDiscardAndDestroy());
        }
        else
        {
            UpdateInteractableState();
        }
    }

    private void UpdateDiscardButtonState()
    {
        if (discardButton == null) return;
        discardButton.SetActive(showCloseIcon);
        if (!showCloseIcon) return;
        Button btn = discardButton.GetComponent<Button>();
        if (btn == null) return;
        // An unrevealed encounter can't be thrown away — it has to be entered.
        btn.interactable = cardData != null && !cardData.IsEncounterCard();
    }

    // Wired to the card's own discard button in the prefab.
    public void Discard()
    {
        if (cardData == null) return;

        ICardInteractionHandler handler = CardServices.Interaction;
        if (handler == null) return;
        if (!handler.TryDiscard(this, cardData)) return;

        if (this != null && gameObject != null)
        {
            StartCoroutine(AnimateDiscardAndDestroy());
        }
    }

    // Flutters the card up and away, then destroys it. Public so a handler that resolves a play
    // asynchronously can trigger the exit itself once its own work finishes.
    public IEnumerator AnimateDiscardAndDestroy()
    {
        // Block further interaction immediately.
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        // Escape the layout group so siblings reflow while we fly away.
        Canvas.ForceUpdateCanvases();
        Transform gridParent = rectTransform.parent;
        Transform floatTarget = (gridParent != null && gridParent.parent != null) ? gridParent.parent : gridParent;
        if (floatTarget != null && floatTarget != rectTransform.parent)
        {
            Vector3 worldPos = rectTransform.position;
            rectTransform.SetParent(floatTarget, false);
            rectTransform.position = worldPos;
            rectTransform.SetAsLastSibling();
        }
        else if (layoutElement != null)
        {
            layoutElement.ignoreLayout = true;
        }

        Vector2 startPos = rectTransform.anchoredPosition;
        float drift = UnityEngine.Random.Range(-70f, 70f);
        Vector2 endPos = startPos + new Vector2(drift, 200f);
        float startRot = rectTransform.localEulerAngles.z;
        float endRot = startRot + UnityEngine.Random.Range(-18f, 18f);

        float duration = 0.32f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (this == null) yield break;
            float p = elapsed / duration;
            float eased = 1f - (1f - p) * (1f - p);

            rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);
            rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(startRot, endRot, p));
            rectTransform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.55f, p);
            if (canvasGroup != null) canvasGroup.alpha = 1f - p;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }

    // Snapshots the enlarged center-preview card's on-screen footprint (world center + width) at
    // click time, for a fly-out animation to launch from. Must be read at click time: playing a card
    // typically rebuilds the hand and destroys the live preview clone before an awaited handler
    // returns.
    public static (Vector3? center, float? width) SnapshotCenterPreviewSource()
    {
        RectTransform previewRect = CardCenterPreview.Instance != null ? CardCenterPreview.Instance.CurrentPreviewRect : null;
        if (previewRect == null) return (null, null);
        return (previewRect.TransformPoint(previewRect.rect.center), previewRect.rect.width * Mathf.Abs(previewRect.lossyScale.x));
    }

    // --- Token tinting ---------------------------------------------------------------------------
    // 'dim' darkens non-hovered tokens multiplicatively (0 = untouched, 1 = black); 'redness' shifts
    // unplayable tokens toward red (green/blue suppressed) so unavailability reads as a color, not
    // transparency. Alpha is never touched — it belongs to the containing CanvasGroup's fades. Base
    // colors are captured on first use, after Initialize has applied the card-type border color.
    private Color tokenImageBaseColor = Color.white;
    private Color tokenBorderBaseColor = Color.white;
    private bool tokenBaseColorsCaptured;

    public void SetTokenTint(float dim01, float redness01)
    {
        if (tokenImage == null && tokenBorder == null) return;

        if (!tokenBaseColorsCaptured)
        {
            if (tokenImage != null) tokenImageBaseColor = tokenImage.color;
            if (tokenBorder != null) tokenBorderBaseColor = tokenBorder.color;
            tokenBaseColorsCaptured = true;
        }

        float k = 1f - Mathf.Clamp01(dim01);
        float redness = Mathf.Clamp01(redness01);
        if (tokenImage != null) tokenImage.color = TintTokenColor(tokenImageBaseColor, k, redness);
        if (tokenBorder != null) tokenBorder.color = TintTokenColor(tokenBorderBaseColor, k, redness);
    }

    private Color cardBackgroundBaseColor = Color.white;
    private bool cardBackgroundBaseColorCaptured;

    // Reddens the full-card border/background where cards render as real cards (not tokens), so
    // SetTokenTint doesn't apply.
    public void SetUnplayableRealCardTint(bool unplayable)
    {
        if (cardBackgroundImage == null) return;

        if (!cardBackgroundBaseColorCaptured)
        {
            cardBackgroundBaseColor = cardBackgroundImage.color;
            cardBackgroundBaseColorCaptured = true;
        }

        cardBackgroundImage.color = TintTokenColor(cardBackgroundBaseColor, 1f, unplayable ? 1f : 0f);
    }

    private static Color TintTokenColor(Color baseColor, float k, float redness)
    {
        Color darkened = new(baseColor.r * k, baseColor.g * k, baseColor.b * k, baseColor.a);
        if (redness <= 0f) return darkened;
        // Push toward red: hold the red channel up (so dark art still reads red) and pull green/blue
        // down, respecting the darkening already applied.
        Color reddened = new(Mathf.Max(darkened.r, 0.75f * k), darkened.g * 0.25f, darkened.b * 0.25f, baseColor.a);
        return Color.Lerp(darkened, reddened, redness);
    }

    // --- Display-only clones ----------------------------------------------------------------------

    // Clones the compact token visual (round art + border ring) for play-flight animations and
    // selection screens. The clone is display-only: interaction and raycasts are stripped. tokenSize
    // reports the visual footprint.
    // NOTE: the prefab's token root is a plain Transform, not a RectTransform, and its children carry
    // authored offsets that position them over the card layout — the clone re-centers them so it
    // works standalone.
    // The full-card counterpart of TokenFootprint. The prefab composes the card out of pieces that
    // deliberately overflow RealCard's 200x200 box, so the root's authored 200x250 says nothing about
    // how much room a card needs. The border's *unscaled* rect does: it is exactly the box the art,
    // title bar, description and type badge fill. Its 1.3 localScale is decorative bleed drawn at 4%
    // alpha, so reserving that too would pad every card with ~23% invisible margin on both axes and
    // no card could ever reach the edge of its slot.
    // Resolved by BindLegacyPrefabReferences during Initialize, so read this after initializing.
    public Vector2 CardFootprint =>
        cardBackgroundImage != null && cardBackgroundImage.transform is RectTransform frame && frame.rect.size.sqrMagnitude > 1f
            ? frame.rect.size
            : new Vector2(300f, 350f);

    // The border ring is the widest token piece; its scaled rect is the token's visual footprint.
    // Board zones size their layout slots from this, so a card previewed as a token while authoring
    // occupies the same space as the clone CreateTokenVisualClone hands back at runtime.
    public Vector2 TokenFootprint =>
        tokenBorder != null && tokenBorder.transform is RectTransform ring && ring.rect.size.sqrMagnitude > 1f
            ? Vector2.Scale(ring.rect.size, ring.localScale)
            : new Vector2(132f, 132f);

    // In-place counterpart to CreateTokenVisualClone, for a card previewed as a token where it
    // stands rather than having its token subtree cloned out. Same reason the clone re-centers its
    // children: the token pieces carry authored offsets that position them over the card layout, so
    // left alone they sit off-centre in a board slot.
    public void CompactTokenInPlace()
    {
        if (tokenCanvasGroup == null) return;
        tokenCanvasGroup.transform.localPosition = Vector3.zero;
        foreach (Transform child in tokenCanvasGroup.transform)
        {
            if (child is RectTransform childRect) childRect.anchoredPosition = Vector2.zero;
        }
        Transform environmentalChild = tokenCanvasGroup.transform.Find("Environmental");
        if (environmentalChild != null) environmentalChild.gameObject.SetActive(false);
    }

    /// <summary>
    /// Scales the compact token artwork uniformly. The token is composed of a border ring, art and
    /// numeral overlays carrying authored offsets relative to one another, so a caller that wants a
    /// different token size scales the whole subtree rather than resizing the pieces: that keeps the
    /// composition the prefab authored and lets a skin own the footprint. 1 restores the prefab's
    /// own size.
    /// </summary>
    public void ScaleTokenVisual(float scale)
    {
        if (scale <= 0f) return;
        BindLegacyPrefabReferences();
        Transform visual = tokenCanvasGroup != null ? tokenCanvasGroup.transform : tokenImage != null ? tokenImage.transform : null;
        if (visual != null) visual.localScale = Vector3.one * scale;
    }

    /// <summary>Moves the compact token artwork within an authored board-preview card.</summary>
    public void SetTokenPreviewOffset(Vector2 offset)
    {
        BindLegacyPrefabReferences();
        Transform visual = tokenCanvasGroup != null ? tokenCanvasGroup.transform : tokenImage != null ? tokenImage.transform : null;
        if (visual != null) visual.localPosition = new Vector3(offset.x, offset.y, visual.localPosition.z);
    }

    public GameObject CreateTokenVisualClone(Transform parent, out Vector2 tokenSize)
    {
        tokenSize = TokenFootprint;
        if (tokenCanvasGroup == null) return null;

        GameObject clone = Instantiate(tokenCanvasGroup.gameObject, parent, false);
        clone.name = "TokenVisual";
        clone.transform.localPosition = Vector3.zero;
        foreach (Transform child in clone.transform)
        {
            if (child is RectTransform childRect) childRect.anchoredPosition = Vector2.zero;
        }
        Transform environmentalChild = clone.transform.Find("Environmental");
        if (environmentalChild != null) environmentalChild.gameObject.SetActive(false);

        return StripForDisplayOnly(clone);
    }

    // Clones the full expanded card visual (art, title, description) for a fail/dissolve animation —
    // unlike the token clone, this one doesn't compact first: a fizzled play dissolves the card as
    // the player was already looking at it. Display-only, like above.
    public GameObject CreateRealCardVisualClone(Transform parent, out Vector2 cardSize)
    {
        cardSize = Vector2.zero;
        if (realCardCanvasGroup == null) return null;

        if (realCardCanvasGroup.transform is RectTransform sourceRect)
        {
            cardSize = Vector2.Scale(sourceRect.rect.size, sourceRect.localScale);
        }

        GameObject clone = Instantiate(realCardCanvasGroup.gameObject, parent, false);
        clone.name = "RealCardVisual";
        clone.transform.localPosition = Vector3.zero;
        foreach (Transform child in clone.transform)
        {
            if (child is RectTransform childRect) childRect.anchoredPosition = Vector2.zero;
        }

        return StripForDisplayOnly(clone);
    }

    private static GameObject StripForDisplayOnly(GameObject clone)
    {
        CanvasGroup cg = clone.GetComponent<CanvasGroup>();
        if (cg == null) cg = clone.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        CardEnvironmentalPulseEffect pulse = clone.GetComponentInChildren<CardEnvironmentalPulseEffect>(true);
        if (pulse != null) Destroy(pulse);

        foreach (Graphic graphic in clone.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }
        return clone;
    }

    // --- Token <-> card flip -----------------------------------------------------------------------

    public void ShowToken()
    {
        if (lockedToRealCard) return;
        if (tokenCanvasGroup != null)
        {
            tokenCanvasGroup.alpha = 1f;
            tokenCanvasGroup.blocksRaycasts = true;
            tokenCanvasGroup.interactable = true;
        }
        if (realCardCanvasGroup != null)
        {
            realCardCanvasGroup.alpha = 0f;
            realCardCanvasGroup.blocksRaycasts = false;
            realCardCanvasGroup.interactable = false;
        }
        if (tokenImage != null) tokenImage.raycastTarget = true;
        if (rootHitGraphic != null) rootHitGraphic.raycastTarget = false;
    }

    public void ShowRealCard()
    {
        if (tokenCanvasGroup != null)
        {
            tokenCanvasGroup.alpha = 0f;
            tokenCanvasGroup.blocksRaycasts = false;
            tokenCanvasGroup.interactable = false;
        }
        if (realCardCanvasGroup != null)
        {
            realCardCanvasGroup.alpha = 1f;
            realCardCanvasGroup.blocksRaycasts = true;
            realCardCanvasGroup.interactable = true;
        }
        if (tokenImage != null) tokenImage.raycastTarget = false;
        if (rootHitGraphic != null) rootHitGraphic.raycastTarget = true;
        if (cardBackgroundImage != null) cardBackgroundImage.raycastTarget = true;
    }
}

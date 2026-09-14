using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The champion's life, drawn on the champion's own card: a blood-red bar across the top of the
/// artwork, where a hand card would print its cost. It bleeds when hit (a bright trail that drains
/// after the fill), flashes and shakes on damage, keeps a slow heartbeat on its glyph that quickens
/// as life runs low, and a sheen sweeps it now and then so it reads as lacquered, not flat.
/// </summary>
public sealed class AvatarHealthBar : MonoBehaviour
{
    Board board;
    int owner, last = -1;
    RectTransform plate, well, fillRect, trailRect, sheen, glyph;
    Image fill, flash, lowGlow;
    TMP_Text value;
    float displayed = 1, damage = 1, changedAt = -10, sheenAt;
    Vector2 restingPosition;
    static readonly Color Blood = new(.70f, .11f, .09f), BloodDark = new(.38f, .04f, .05f), Ember = new(1f, .58f, .22f), Bone = new(.96f, .92f, .84f);

    /// <summary>
    /// Builds the bar on a full card as its own band stacked between the title and the artwork, with
    /// clear ground on both sides so it never sits on the picture, even mid-shake. The card makes
    /// room by giving up height below the bar: the artwork loses half of it off its top and slides
    /// down, the description loses the other half, and the plaques that hang on the artwork's lower
    /// corner follow its bottom edge.
    /// </summary>
    public static AvatarHealthBar CreateOnCard(Card card, Board board, int owner)
    {
        var skin = BoardPresentation.SkinFor(card.transform);
        var art = skin.cards.art; var title = skin.cards.title;
        const float height = 26, gap = 8;
        // The bar hangs a gap under the title; the artwork's top must clear it by another gap.
        float barTop = title.position.y - title.size.y * .5f - gap;
        float artTop = art.position.y + art.size.y * .5f;
        float drop = Mathf.Max(0, artTop - (barTop - height - gap));
        float artShrink = Mathf.Round(drop * .5f), descriptionShrink = drop - artShrink;
        var real = card.transform.Find("RealCard");
        if (real != null)
        {
            // Top edge down by the whole drop, bottom edge down by what the description gives up.
            Shift(real.Find("Image") as RectTransform, drop - artShrink * .5f, artShrink);
            Shift(real.Find("DescriptionBackground") as RectTransform, descriptionShrink * .5f, descriptionShrink);
        }
        foreach (string plaque in new[] { "Stat plaque", "Class plaque", "Status plaque" })
            Shift(card.transform.Find(plaque) as RectTransform, descriptionShrink, 0);
        if (card.CombatStatsLabel != null) Shift(card.CombatStatsLabel.rectTransform, descriptionShrink, 0);
        if (card.ClassStatsLabel != null) Shift(card.ClassStatsLabel.rectTransform, descriptionShrink, 0);
        if (card.StatusEffectsLabel != null) Shift(card.StatusEffectsLabel.rectTransform, descriptionShrink, 0);
        var plate = BoardPresentation.Panel(card.transform, "Health bar", BloodDark);
        var rect = plate.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        rect.sizeDelta = new Vector2(art.size.x, height);
        rect.anchoredPosition = new Vector2(art.position.x, barTop - height * .5f);
        rect.localScale = Vector3.one;
        BoardPresentation.Border(rect, new Color(.16f, .02f, .02f), 2);
        var health = plate.gameObject.AddComponent<AvatarHealthBar>();
        health.board = board; health.owner = owner; health.plate = rect; health.restingPosition = rect.anchoredPosition;
        // The well the fill sits in, masked so the sheen never slides out past the bar's ends.
        var wellImage = BoardPresentation.Panel(plate.transform, "Well", new Color(.08f, .01f, .015f));
        health.well = wellImage.rectTransform;
        BoardPresentation.Stretch(health.well, Vector2.zero, Vector2.one);
        health.well.offsetMin = new Vector2(3, 3); health.well.offsetMax = new Vector2(-3, -3);
        health.well.gameObject.AddComponent<RectMask2D>();
        var trail = BoardPresentation.Panel(health.well, "Recent damage", Ember);
        health.trailRect = trail.rectTransform;
        health.fill = BoardPresentation.Panel(health.well, "Remaining health", Blood);
        health.fillRect = health.fill.rectTransform;
        foreach (var bar in new[] { health.trailRect, health.fillRect })
        { bar.anchorMin = Vector2.zero; bar.anchorMax = Vector2.one; bar.offsetMin = bar.offsetMax = Vector2.zero; bar.pivot = new Vector2(0, .5f); }
        // Lacquer: a bright lip along the top and a dark one along the bottom of the fill.
        var lip = BoardPresentation.Panel(health.fillRect, "Lip", new Color(1, .75f, .7f, .28f));
        BoardPresentation.Stretch(lip.rectTransform, new Vector2(0, .72f), Vector2.one);
        var shade = BoardPresentation.Panel(health.fillRect, "Shade", new Color(0, 0, 0, .28f));
        BoardPresentation.Stretch(shade.rectTransform, Vector2.zero, new Vector2(1, .22f));
        var sheenImage = BoardPresentation.Panel(health.fillRect, "Sheen", new Color(1, .95f, .9f, .35f));
        health.sheen = sheenImage.rectTransform;
        health.sheen.anchorMin = new Vector2(0, 0); health.sheen.anchorMax = new Vector2(0, 1); health.sheen.pivot = new Vector2(.5f, .5f);
        health.sheen.sizeDelta = new Vector2(18, 0); health.sheen.localRotation = Quaternion.Euler(0, 0, 18);
        health.lowGlow = BoardPresentation.Panel(health.well, "Low health glow", new Color(1, .2f, .1f, 0));
        BoardPresentation.Stretch(health.lowGlow.rectTransform, Vector2.zero, Vector2.one);
        health.flash = BoardPresentation.Panel(plate.transform, "Hit flash", new Color(1, 1, 1, 0));
        BoardPresentation.Stretch(health.flash.rectTransform, Vector2.zero, Vector2.one);
        // The numerals, with the heart glyph the card faces use, over everything.
        var textObject = new GameObject("Health value", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(plate.transform, false);
        health.value = textObject.GetComponent<TextMeshProUGUI>();
        var text = (TextMeshProUGUI)health.value;
        var font = BoardPresentation.ReadingFontFor(skin);
        if (font != null) { text.font = font; text.fontSharedMaterial = font.material; }
        text.spriteAsset = BoardPresentation.SpriteAssetFor(board);
        text.fontSize = 15; text.fontStyle = FontStyles.Bold; text.color = Bone;
        text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.NoWrap; text.raycastTarget = false;
        text.enableAutoSizing = true; text.fontSizeMin = 9; text.fontSizeMax = 15;
        BoardPresentation.Stretch(text.rectTransform, Vector2.zero, Vector2.one);
        health.glyph = text.rectTransform;
        health.sheenAt = Time.unscaledTime + Random.Range(.5f, 2.5f);
        health.Update();
        return health;
    }

    // Moves a piece down and, if asked, takes height off it. Pieces are centred, so shrinking one
    // by h while moving it down by h/2 keeps its bottom edge where it was.
    static void Shift(RectTransform piece, float down, float shrink)
    {
        if (piece == null) return;
        piece.anchoredPosition += Vector2.down * down;
        if (shrink > 0) piece.sizeDelta = new Vector2(piece.sizeDelta.x, Mathf.Max(20, piece.sizeDelta.y - shrink));
    }

    void Update()
    {
        if (board == null || plate == null) return;
        int maximum = Mathf.Max(1, board.Match != null ? board.Match.startingLife : 20);
        int life = board.Match?.Rules?.Players[owner].Life ?? maximum;
        float target = Mathf.Clamp01((float)life / maximum);
        float now = Time.unscaledTime;
        if (life != last)
        {
            bool hit = last >= 0 && life < last;
            if (last < 0) displayed = damage = target;
            if (hit) { changedAt = now; flash.color = new Color(1, .9f, .85f, .85f); }
            value.text = "<sprite name=\"health\"> " + Mathf.Max(0, life) + " / " + maximum;
            last = life;
        }
        displayed = Mathf.MoveTowards(displayed, target, Time.unscaledDeltaTime * 1.6f);
        // The wound shows first as a bright band, which drains away after the fill has settled.
        if (now - changedAt > .6f) damage = Mathf.MoveTowards(damage, target, Time.unscaledDeltaTime * .45f);
        damage = Mathf.Max(damage, displayed);
        fillRect.anchorMax = new Vector2(displayed, 1); fillRect.offsetMax = Vector2.zero;
        trailRect.anchorMax = new Vector2(damage, 1); trailRect.offsetMax = Vector2.zero;
        fillRect.gameObject.SetActive(displayed > .01f); trailRect.gameObject.SetActive(damage > .01f);
        // Low: the blood glows and the heartbeat quickens.
        float low = Mathf.InverseLerp(.35f, .1f, target);
        float beat = 1f + (.04f + .07f * low) * Mathf.Max(0, Mathf.Sin(now * (2.2f + 3.5f * low) * Mathf.PI));
        glyph.localScale = Vector3.one * beat;
        fill.color = Color.Lerp(Blood, new Color(.92f, .16f, .1f), low * (.5f + .5f * Mathf.Sin(now * 6)));
        lowGlow.color = new Color(1, .25f, .12f, low * (.12f + .1f * Mathf.Sin(now * 6)));
        // A sheen sweeps the fill now and then; it lives inside the fill, so it is clipped to what is left.
        float sweep = (now - sheenAt) / 1.1f;
        if (sweep > 1) { sheenAt = now + Random.Range(2.5f, 5f); sweep = 1; }
        float fillWidth = well.rect.width * displayed;
        sheen.anchoredPosition = new Vector2(Mathf.Lerp(-20, fillWidth + 20, Mathf.Clamp01(sweep)), 0);
        sheen.gameObject.SetActive(sweep >= 0 && sweep < 1);
        // The hit: a flash that fades and a short shake.
        float since = now - changedAt;
        var flashColor = flash.color; flashColor.a = Mathf.MoveTowards(flashColor.a, 0, Time.unscaledDeltaTime * 2.2f); flash.color = flashColor;
        float shake = since < .45f ? (1 - since / .45f) * 5f : 0;
        plate.anchoredPosition = restingPosition + new Vector2(Mathf.Sin(since * 70) * shake, Mathf.Cos(since * 53) * shake * .5f);
    }
}

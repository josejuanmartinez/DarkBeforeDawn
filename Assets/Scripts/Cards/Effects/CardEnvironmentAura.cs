using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The weather on a board card. A unit whose side an environmental card in play names gets a
/// breathing ring in that environment's colour, motes drifting up through its frame, and a badge in
/// its corner carrying the environment's glyph (or its art), one per environment. The environment's
/// own token gets the ring alone, so the source and what it touches read as one system. Attached by
/// TowerMatchController.Sync to every field view; it looks the rules up itself each frame.
/// </summary>
public sealed class CardEnvironmentAura : MonoBehaviour
{
    Board board;
    BoardCardView view;
    RectTransform ring, badges;
    readonly List<Image> ringStrips = new();
    readonly List<(RectTransform rect, Image image, float speed, float phase, float x)> motes = new();
    readonly List<(CardData environment, RectTransform badge, float phase)> shown = new();
    CanvasGroup group;
    Color tint = Color.white;
    string signature;
    float alpha;
    static readonly System.Random seed = new();

    public static void Attach(Board board, BoardCardView view)
    {
        if (view == null || view.GetComponent<CardEnvironmentAura>() != null) return;
        var aura = view.gameObject.AddComponent<CardEnvironmentAura>();
        aura.board = board; aura.view = view;
    }

    /// <summary>A colour for an environment, read off its name: sun and dawn warm, night and fog cold, fire red, water blue, wind green.</summary>
    public static Color TintFor(CardData environment)
    {
        string n = (environment?.name ?? "").ToLowerInvariant();
        bool Any(params string[] words) => words.Any(n.Contains);
        if (Any("fire", "burn", "ash", "wildfire", "smoke", "lightning", "ember", "cinder", "drought", "sand")) return new Color(1f, .48f, .2f);
        if (Any("snow", "frozen", "winter", "chill", "frost", "barrow", "redhorn")) return new Color(.62f, .86f, 1f);
        if (Any("moon", "night", "stars", "twilight", "shadow", "gloom", "dark", "fog", "wraith", "unquiet", "dead", "veil")) return new Color(.66f, .5f, 1f);
        if (Any("rain", "drown", "sea", "water", "flood", "storm", "shore", "mud", "pollution", "lilies")) return new Color(.36f, .68f, 1f);
        if (Any("wind", "butterflies", "ravens", "flowers", "forest", "willow", "elves", "spring")) return new Color(.5f, .9f, .55f);
        return new Color(1f, .84f, .42f); // sun, dawn, light, day, morning and everything bright
    }

    void Update()
    {
        var rules = board != null ? board.Match?.Rules : null;
        var unit = rules != null && view != null ? board.Match.Unit(view) : null;
        List<CardData> environments = null;
        bool source = false;
        if (unit != null)
        {
            if (unit.Card.GetCardType() == CardTypeEnum.Environmental) { source = true; environments = new List<CardData> { unit.Card }; }
            else environments = rules.EnvironmentsAffecting(unit).Select(e => e.Card).ToList();
        }
        string now = environments == null || environments.Count == 0 ? null : string.Join("|", environments.Select(e => e.name)) + (source ? "#" : "");
        if (now != signature) { signature = now; Rebuild(environments, source); }
        float target = signature == null ? 0 : 1;
        alpha = Mathf.MoveTowards(alpha, target, Time.unscaledDeltaTime * 3);
        if (group != null) group.alpha = alpha;
        if (alpha <= 0 && ring != null && signature == null) { Clear(); return; }
        if (ring == null) return;
        float t = Time.unscaledTime;
        // The ring breathes: alpha and a touch of scale, slower than the legal-action pulse so the two never beat together.
        float breath = .5f + .5f * Mathf.Sin(t * 1.7f);
        ring.localScale = Vector3.one * (1f + .03f * breath);
        foreach (var strip in ringStrips) if (strip != null) strip.color = new Color(tint.r, tint.g, tint.b, Mathf.Lerp(.35f, .9f, breath));
        // Motes rise through the frame and are born again at the bottom.
        var size = view.Rect.rect.size;
        for (int i = 0; i < motes.Count; i++)
        {
            var mote = motes[i];
            if (mote.rect == null) continue;
            float life = Mathf.Repeat(t * mote.speed + mote.phase, 1f);
            float sway = Mathf.Sin((t + mote.phase * 10) * 2.3f) * size.x * .06f;
            mote.rect.anchoredPosition = new Vector2(mote.x * size.x + sway, Mathf.Lerp(-size.y * .45f, size.y * .45f, life));
            float fade = Mathf.Sin(life * Mathf.PI);
            mote.image.color = new Color(tint.r, tint.g, tint.b, .75f * fade);
            mote.rect.localScale = Vector3.one * (.6f + .6f * fade);
        }
        // The badges bob, each on its own beat.
        for (int i = 0; i < shown.Count; i++)
        {
            var badge = shown[i];
            if (badge.badge == null) continue;
            badge.badge.anchoredPosition = new Vector2(-size.x * .5f + 14 + i * 26, size.y * .5f - 14 + Mathf.Sin(t * 2.1f + badge.phase) * 3);
            badge.badge.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.3f + badge.phase) * 6);
        }
    }

    void Rebuild(List<CardData> environments, bool source)
    {
        Clear();
        if (environments == null || environments.Count == 0) return;
        tint = TintFor(environments[0]);
        var skin = BoardPresentation.SkinFor(transform);
        var root = new GameObject("Weather", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(transform, false);
        BoardPresentation.Stretch(root, Vector2.zero, Vector2.one);
        group = root.gameObject.AddComponent<CanvasGroup>(); group.alpha = alpha; group.blocksRaycasts = false;
        ring = root;
        // Four strips, wider than the board's own frames, so the colour reads at token size.
        float width = 4;
        foreach (var (min, max, size) in new[] {
            (Vector2.zero, Vector2.right, new Vector2(0, width)), (Vector2.up, Vector2.one, new Vector2(0, width)),
            (Vector2.zero, Vector2.up, new Vector2(width, 0)), (Vector2.right, Vector2.one, new Vector2(width, 0)) })
        {
            var strip = BoardPresentation.Panel(root, "Weather ring", tint);
            BoardPresentation.Stretch(strip.rectTransform, min, max);
            strip.rectTransform.sizeDelta = size;
            ringStrips.Add(strip);
        }
        if (source) return;
        // Motes: a handful of soft squares, turned to diamonds, at random columns and beats.
        for (int i = 0; i < 6; i++)
        {
            var mote = BoardPresentation.Panel(root, "Mote", tint);
            mote.rectTransform.anchorMin = mote.rectTransform.anchorMax = Vector2.one * .5f;
            mote.rectTransform.sizeDelta = Vector2.one * 5;
            mote.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            motes.Add((mote.rectTransform, mote, .12f + (float)seed.NextDouble() * .1f, (float)seed.NextDouble(), -.4f + (float)seed.NextDouble() * .8f));
        }
        // One badge per environment, in the top-left corner: the environment's glyph where the icon sheet has one, else its art.
        var spriteAsset = BoardPresentation.SpriteAssetFor(board);
        for (int i = 0; i < environments.Count; i++)
        {
            var environment = environments[i];
            var badgeTint = TintFor(environment);
            var badge = BoardPresentation.Panel(root, "Weather badge " + environment.name, skin.colors.ink);
            var rect = badge.rectTransform;
            rect.anchorMin = rect.anchorMax = Vector2.one * .5f; rect.sizeDelta = Vector2.one * 24;
            BoardPresentation.Border(rect, badgeTint, 2);
            string glyph = CardNameUtility.Normalize(environment.name);
            if (spriteAsset != null && HasSprite(spriteAsset, glyph))
            {
                var textObject = new GameObject("Glyph", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObject.transform.SetParent(rect, false);
                var text = textObject.GetComponent<TextMeshProUGUI>();
                text.spriteAsset = spriteAsset; text.text = "<sprite name=\"" + glyph + "\">";
                text.fontSize = 18; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; text.enableAutoSizing = true; text.fontSizeMin = 8; text.fontSizeMax = 18;
                BoardPresentation.Stretch(text.rectTransform, Vector2.zero, Vector2.one);
            }
            else
            {
                var art = BoardPresentation.Panel(rect, "Art", Color.white);
                BoardPresentation.Stretch(art.rectTransform, Vector2.zero, Vector2.one);
                art.rectTransform.offsetMin = Vector2.one * 3; art.rectTransform.offsetMax = -Vector2.one * 3;
                art.sprite = TravelBanner.Artwork(environment); art.preserveAspect = true;
                if (art.sprite == null) art.color = badgeTint;
            }
            shown.Add((environment, rect, (float)seed.NextDouble() * 6));
        }
    }

    static bool HasSprite(TMP_SpriteAsset asset, string name)
    {
        if (asset == null) return false;
        if (asset.GetSpriteIndexFromName(name) >= 0) return true;
        if (asset.fallbackSpriteAssets != null)
            foreach (var fallback in asset.fallbackSpriteAssets)
                if (fallback != null && fallback != asset && HasSprite(fallback, name)) return true;
        return false;
    }

    void Clear()
    {
        if (ring != null) { ring.gameObject.SetActive(false); Destroy(ring.gameObject); }
        ring = null; group = null; ringStrips.Clear(); motes.Clear(); shown.Clear();
    }

    void OnDestroy() => Clear();
}

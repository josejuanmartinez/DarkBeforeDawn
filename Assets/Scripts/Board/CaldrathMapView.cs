using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The drawn map of Caldrath (Resources/Maps/Caldrath.png) as a framed viewport for the travel popups.
/// The picture is masked to a rectangle and zoomed on the regions that matter; routes, stops and the
/// company marker are laid over it in viewport space from RegionMap's marker positions, so they stay
/// crisp whatever the zoom. Built fresh each time a popup rebuilds, like the rest of the board chrome.
/// </summary>
public sealed class CaldrathMapView
{
    const string Resource = "Maps/Caldrath";
    static Sprite sheet;
    static bool looked;
    /// <summary>The map picture, or null when the resource is missing.</summary>
    public static Sprite Sheet
    {
        get { if (!looked) { sheet = Resources.Load<Sprite>(Resource); looked = true; } return sheet; }
    }

    readonly RegionMap map;
    readonly RectTransform image;
    readonly Vector2 size;
    float zoom = 1;
    /// <summary>The masked viewport; anchored by the caller.</summary>
    public RectTransform Viewport { get; }
    /// <summary>Where stops, links and markers go: an unscaled layer over the picture, clipped with it.</summary>
    public RectTransform Overlay { get; }
    /// <summary>The side of the square picture when it exactly fits the viewport.</summary>
    float Fit => Mathf.Min(size.x, size.y);

    public CaldrathMapView(Transform parent, RegionMap map, Vector2 size, Color frame, string name = "Map")
    {
        this.map = map; this.size = size;
        var skin = BoardPresentation.SkinFor(parent);
        var window = BoardPresentation.Panel(parent, name, skin.colors.ink);
        Viewport = window.rectTransform;
        Viewport.sizeDelta = size;
        // The picture and its overlay clip to the window; the frame sits beside them, never clipped.
        var clip = new GameObject("Clip", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        clip.SetParent(Viewport, false);
        BoardPresentation.Stretch(clip, Vector2.zero, Vector2.one);
        var picture = BoardPresentation.Panel(clip, "Caldrath", Color.white);
        picture.sprite = Sheet;
        if (picture.sprite == null) picture.color = new Color(.16f, .19f, .2f);
        image = picture.rectTransform;
        image.anchorMin = image.anchorMax = image.pivot = Vector2.one * .5f;
        image.sizeDelta = Vector2.one * Fit;
        Overlay = new GameObject("Overlay", typeof(RectTransform)).GetComponent<RectTransform>();
        Overlay.SetParent(clip, false);
        BoardPresentation.Stretch(Overlay, Vector2.zero, Vector2.one);
        BoardPresentation.Border(Viewport, frame, 2);
    }

    /// <summary>
    /// Zooms and pans the picture so the named regions fill the viewport with a margin around them, never
    /// closer than the picture's own pixels allow nor further than the whole map. Unknown regions are ignored;
    /// with none known the whole map shows.
    /// </summary>
    public void Frame(IEnumerable<string> regions, float margin = .08f, float minSpan = .28f)
    {
        var points = regions.Select(r => (ok: TryUv(r, out var uv), uv)).Where(p => p.ok).Select(p => p.uv).ToList();
        if (points.Count == 0) { zoom = 1; image.sizeDelta = Vector2.one * Fit; image.anchoredPosition = Vector2.zero; return; }
        float minU = points.Min(p => p.x) - margin, maxU = points.Max(p => p.x) + margin;
        float minV = points.Min(p => p.y) - margin, maxV = points.Max(p => p.y) + margin;
        float spanU = Mathf.Max(maxU - minU, minSpan), spanV = Mathf.Max(maxV - minV, minSpan);
        // Native pixels are the ceiling: past that the picture only blurs.
        float native = Sheet != null ? Sheet.rect.width : 1254;
        float maxZoom = Mathf.Max(1, native / Fit);
        zoom = Mathf.Clamp(Mathf.Min(size.x / (spanU * Fit), size.y / (spanV * Fit)), 1, maxZoom);
        float side = Fit * zoom;
        image.sizeDelta = Vector2.one * side;
        var centre = new Vector2((minU + maxU) * .5f, (minV + maxV) * .5f);
        var offset = new Vector2(-(centre.x - .5f) * side, (centre.y - .5f) * side);
        // Keep the picture's edges outside the viewport, so no bare ink shows past the coast.
        var slack = new Vector2(Mathf.Max(0, (side - size.x) * .5f), Mathf.Max(0, (side - size.y) * .5f));
        image.anchoredPosition = new Vector2(Mathf.Clamp(offset.x, -slack.x, slack.x), Mathf.Clamp(offset.y, -slack.y, slack.y));
    }

    bool TryUv(string region, out Vector2 uv)
    {
        uv = Vector2.zero;
        return map != null && map.TryMapPosition(region, out uv);
    }

    /// <summary>Where a region's marker lies in the viewport (centre origin), after framing. False for an unknown region.</summary>
    public bool TryLocate(string region, out Vector2 local)
    {
        local = Vector2.zero;
        if (!TryUv(region, out var uv)) return false;
        float side = Fit * zoom;
        local = image.anchoredPosition + new Vector2((uv.x - .5f) * side, (.5f - uv.y) * side);
        return true;
    }

    /// <summary>Ink at four fifths: the under-stroke that lifts roads, stops and captions off the parchment.</summary>
    static Color Ink(Transform at) { var ink = BoardPresentation.SkinFor(at).colors.ink; return new Color(ink.r, ink.g, ink.b, .8f); }

    Image Diamond(string name, Vector2 at, Color color, float size)
    {
        var node = BoardPresentation.Panel(Overlay, name, color);
        node.rectTransform.anchorMin = node.rectTransform.anchorMax = Vector2.one * .5f;
        node.rectTransform.sizeDelta = Vector2.one * size;
        node.rectTransform.anchoredPosition = at;
        node.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        return node;
    }

    /// <summary>A stop: a diamond the way the road strips drew them, filled or ringed in its tint, on an ink under-stroke.</summary>
    public Image Node(string name, Vector2 at, Color tint, bool filled, float size)
    {
        Diamond("Under " + name, at, Ink(Overlay), size + 4);
        var node = Diamond(name, at, filled ? tint : BoardPresentation.SkinFor(Overlay).colors.ink, size);
        if (!filled) BoardPresentation.Border(node.rectTransform, tint, 2);
        return node;
    }

    /// <summary>A leg of the road: a straight line between two points, trimmed clear of the nodes at either end, on an ink under-stroke.</summary>
    public Image Link(Vector2 from, Vector2 to, Color color, float width, float trim = 0)
    {
        var delta = to - from;
        float length = Mathf.Max(0, delta.magnitude - trim * 2);
        Image Stroke(string name, Color tint, float thickness)
        {
            var link = BoardPresentation.Panel(Overlay, name, tint);
            link.transform.SetAsFirstSibling();
            link.rectTransform.anchorMin = link.rectTransform.anchorMax = Vector2.one * .5f;
            link.rectTransform.sizeDelta = new Vector2(length, thickness);
            link.rectTransform.anchoredPosition = (from + to) * .5f;
            link.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            return link;
        }
        var road = Stroke("Road", color, width);
        Stroke("Under road", Ink(Overlay), width + 4);
        return road;
    }

    /// <summary>
    /// Sets a label on a dark slip so it reads over the parchment: the slip takes the label's place and
    /// size in the hierarchy and the label fills it, so it draws above the slip rather than under it.
    /// </summary>
    public static Image Slip(RectTransform label)
    {
        var slip = BoardPresentation.Panel(label.parent, "Slip", Ink(label));
        var rect = slip.rectTransform;
        rect.anchorMin = label.anchorMin; rect.anchorMax = label.anchorMax; rect.pivot = label.pivot;
        rect.sizeDelta = label.sizeDelta; rect.anchoredPosition = label.anchoredPosition;
        rect.SetSiblingIndex(label.GetSiblingIndex());
        label.SetParent(rect, false);
        BoardPresentation.Stretch(label, Vector2.zero, Vector2.one);
        return slip;
    }

    /// <summary>
    /// A name on the map: bold small capitals on a dark slip, hanging below the point or standing above
    /// it. TextMeshPro rather than the legacy Text the rest of the chrome uses: at caption size under
    /// the canvas scale the bitmap glyphs smear, and the signed-distance font stays crisp.
    /// </summary>
    public static TMPro.TextMeshProUGUI Caption(Transform parent, string text, Board board, Vector2 at, Color color, float size, bool below, float height = 18, Vector2? anchor = null)
    {
        var label = TravelBanner.RichLabel(parent, text, board, size, color);
        label.enableAutoSizing = false; label.fontSize = size; label.fontStyle = TMPro.FontStyles.Bold; label.characterSpacing = 2;
        var rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = anchor ?? Vector2.one * .5f;
        rect.pivot = new Vector2(.5f, below ? 1 : 0);
        rect.sizeDelta = new Vector2(label.preferredWidth + 12, height);
        rect.anchoredPosition = at;
        Slip(rect);
        return label;
    }

    /// <summary>A soft ring around a point: the halo under the company or the town it is bound for.</summary>
    public Image Halo(Vector2 at, Color color, float size)
    {
        var halo = BoardPresentation.Panel(Overlay, "Halo", Color.clear);
        halo.rectTransform.anchorMin = halo.rectTransform.anchorMax = Vector2.one * .5f;
        halo.rectTransform.sizeDelta = Vector2.one * size;
        halo.rectTransform.anchoredPosition = at;
        halo.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        BoardPresentation.Border(halo.rectTransform, color, 2);
        return halo;
    }

    /// <summary>
    /// The painted terrain symbol of every region in view explained on hover: an invisible square over each
    /// marker, registered with the popup's hover, so pointing at a symbol names the region, its ground and
    /// what that ground means for a company crossing it.
    /// </summary>
    public void ExplainTerrains(CardKeywordHover hover, Func<string, TerrainEnum> terrainOf, float size)
    {
        if (map == null || hover == null || terrainOf == null) return;
        foreach (var region in map.Regions)
        {
            var ground = terrainOf(region.name);
            if (ground == TerrainEnum.None || !TryLocate(region.name, out var at)) continue;
            // Marks framed out of the window are clipped from sight; they must not answer the pointer either.
            if (Mathf.Abs(at.x) > this.size.x * .5f || Mathf.Abs(at.y) > this.size.y * .5f) continue;
            var mark = BoardPresentation.Panel(Overlay, "Terrain " + region.name, Color.clear);
            mark.rectTransform.anchorMin = mark.rectTransform.anchorMax = Vector2.one * .5f;
            mark.rectTransform.sizeDelta = Vector2.one * size;
            mark.rectTransform.anchoredPosition = at;
            mark.transform.SetAsFirstSibling();
            CardKeywordGlossary.TryGet("terrain:" + ground, out var title, out var body);
            hover.AddBadge(mark.rectTransform, region.name + "  ·  " + title, body);
        }
    }

    /// <summary>An invisible clickable square over a point, for choosing a region straight off the map.</summary>
    public Button Hotspot(string name, Vector2 at, float size, Action onClick)
    {
        var spot = BoardPresentation.Panel(Overlay, name, Color.clear);
        spot.raycastTarget = true;
        spot.rectTransform.anchorMin = spot.rectTransform.anchorMax = Vector2.one * .5f;
        spot.rectTransform.sizeDelta = Vector2.one * size;
        spot.rectTransform.anchoredPosition = at;
        var button = spot.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => onClick());
        return button;
    }
}

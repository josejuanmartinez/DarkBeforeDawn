using UnityEngine;
using UnityEngine.UI;

/// <summary>The blind-stamped device on a panel: each kind of widget wears the shape of what it holds.</summary>
public enum BoardEmblem { Sprig, Lands, Settlements, Armies, Deck, Discard, Environment, Hand, Materials, Champion, None }

/// <summary>Leather panels with brass bindings. One mesh, no textures or per-frame allocations.</summary>
[DisallowMultipleComponent]
public sealed class BoardSurface : MaskableGraphic
{
    public Color accent;
    public Color surface;
    public bool ceremonial;
    public bool compact;
    /// <summary>Keeps the skin surface's own opacity even on large panels: for popups that must hide what they cover.</summary>
    public bool opaque;
    public BoardEmblem emblem = BoardEmblem.Sprig;

    /// <summary>The device for a board zone: mountains for lands, a keep for settlements, crossed swords for armies, and so on.</summary>
    public static BoardEmblem EmblemFor(BoardZoneId zone) => zone switch
    {
        BoardZoneId.HumanLands or BoardZoneId.OpponentLands => BoardEmblem.Lands,
        BoardZoneId.HumanSettlements or BoardZoneId.OpponentSettlements => BoardEmblem.Settlements,
        BoardZoneId.HumanArmies or BoardZoneId.OpponentArmies => BoardEmblem.Armies,
        BoardZoneId.HumanVictory or BoardZoneId.OpponentVictory => BoardEmblem.Deck,
        BoardZoneId.HumanDiscard or BoardZoneId.OpponentDiscard => BoardEmblem.Discard,
        BoardZoneId.Environment => BoardEmblem.Environment,
        _ => BoardEmblem.Hand
    };

    public static void Dress(Image panel, Color accent, bool ceremonial = false, bool compact = false, BoardEmblem emblem = BoardEmblem.Sprig, bool opaque = false)
    {
        var go = new GameObject("Engraved surface", typeof(RectTransform), typeof(CanvasRenderer), typeof(BoardSurface));
        go.transform.SetParent(panel.transform, false);
        BoardPresentation.Stretch((RectTransform)go.transform, Vector2.zero, Vector2.one);
        go.transform.SetAsFirstSibling();
        var art = go.GetComponent<BoardSurface>();
        art.surface = panel.color;
        art.accent = accent;
        art.ceremonial = ceremonial;
        art.compact = compact;
        art.emblem = emblem;
        art.opaque = opaque;
        art.raycastTarget = false;
        panel.color = Color.clear;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width < 4 || r.height < 4) return;
        float cut = Mathf.Min(2, r.height * .1f);
        // Thick book-cover edges and warm leather keep the board in the fantasy world.
        Plate(vh, new Rect(r.x - 2, r.y - 4, r.width + 4, r.height + 4), cut, new Color(0, 0, 0, .12f), new Color(0, 0, 0, .12f));
        Color low = Color.Lerp(surface, new Color(.045f, .027f, .017f, .94f), .72f);
        Color high = Color.Lerp(surface, new Color(.19f, .125f, .067f, .97f), .6f);
        // The landscape is part of the board: large furniture stays transparent, while small
        // card stock and controls retain the opacity of their own skin surface.
        low.a = opaque ? 1 : compact ? surface.a : .66f;
        high.a = opaque ? 1 : compact ? surface.a : .73f;
        Plate(vh, r, cut, low, high);
        // Fine horizontal grain. Deterministic geometry avoids texture imports and stretching.
        // No full-width scan lines: these fight the landscape and shimmer at smaller view sizes.
        Outline(vh, r, cut, new Color(.20f,.13f,.065f,.95f), 4);
        Outline(vh, r, cut, Tone(.65f), 1);
        Rect inner = new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6);
        Outline(vh, inner, Mathf.Max(1, cut - 2), new Color(0, 0, 0, .5f), 1);
        // Short, irregular pores instead of a repeated screen-like grid.
        int pores = Mathf.Min(400, Mathf.FloorToInt(r.width*r.height/550));
        for (int i=0;i<pores;i++)
        {
            float x=r.xMin+7+Mathf.Repeat(i*73.137f,Mathf.Max(1,r.width-14));
            float y=r.yMin+7+Mathf.Repeat(i*31.719f,Mathf.Max(1,r.height-14));
            Line(vh,new Vector2(x,y),new Vector2(x+1.5f,y+.5f),.7f,new Color(.7f,.49f,.24f,.055f));
        }
        if (!compact)
        {
            // Brass book corners, round studs, and short leather stitches.
            foreach (float x in new[] { r.xMin, r.xMax })
            foreach (float y in new[] { r.yMin, r.yMax })
            {
                float sx = x == r.xMin ? 1 : -1, sy = y == r.yMin ? 1 : -1;
                var a = new Vector2(x + sx * 5, y + sy * 23);
                var b = new Vector2(x + sx * 5, y + sy * 5);
                var c = new Vector2(x + sx * 23, y + sy * 5);
                Line(vh, a, b, 4, Tone(.7f));
                Line(vh, b, c, 4, Tone(.7f));
                Ring(vh, new Vector2(x+sx*7,y+sy*7),2,Tone(.95f));
                for(int stitch=0;stitch<3;stitch++)
                    Line(vh,new Vector2(x+sx*(29+stitch*7),y+sy*6),new Vector2(x+sx*(32+stitch*7),y+sy*6),1,Tone(.25f));
            }
            // Blind-stamped device, like a worn leather-bound chronicle: wide lanes wear it on their
            // free right side, squarer panels in the middle, and each kind of panel wears its own.
            if (emblem != BoardEmblem.None && r.width > 150 && r.height > 70)
            {
                Vector2 center = r.width > 430 ? new Vector2(r.xMax - 68, r.center.y - 7) : new Vector2(r.center.x, r.center.y - 4);
                float radius = Mathf.Min(r.height * .29f, r.width * .2f, 38);
                Stamp(vh, center, radius);
            }
        }
        if (ceremonial)
        {
            float mid = r.center.x, y = r.yMin + 6;
            Line(vh, new Vector2(mid - r.width * .28f, y), new Vector2(mid - 16, y), 1, Tone(.45f));
            Line(vh, new Vector2(mid + 16, y), new Vector2(mid + r.width * .28f, y), 1, Tone(.45f));
            Diamond(vh, new Vector2(mid, y), 4, Tone(.85f));
            Diamond(vh, new Vector2(mid - 10, y), 1.5f, Tone(.6f));
            Diamond(vh, new Vector2(mid + 10, y), 1.5f, Tone(.6f));
        }
    }

    // The devices. All of them are line work at the same faint tone, so they read as stamped into
    // the leather rather than printed on it.
    private void Stamp(VertexHelper vh, Vector2 c, float radius)
    {
        Color ink = Tone(.1f), bright = Tone(.14f);
        switch (emblem)
        {
            case BoardEmblem.Lands:
                // Two peaks over a horizon, a sun rising behind the far one.
                Line(vh, c + new Vector2(-radius * 1.1f, -radius * .45f), c + new Vector2(radius * 1.1f, -radius * .45f), 1.2f, ink);
                Poly(vh, ink, 2, c + new Vector2(-radius * .95f, -radius * .45f), c + new Vector2(-radius * .3f, radius * .55f), c + new Vector2(radius * .2f, -radius * .45f));
                Poly(vh, ink, 2, c + new Vector2(-radius * .15f, -radius * .45f), c + new Vector2(radius * .4f, radius * .2f), c + new Vector2(radius * .95f, -radius * .45f));
                Ring(vh, c + new Vector2(radius * .55f, radius * .5f), radius * .22f, bright);
                break;
            case BoardEmblem.Settlements:
                // A keep: walls, three merlons, an arched door.
                Poly(vh, ink, 2, c + new Vector2(-radius * .6f, -radius * .6f), c + new Vector2(-radius * .6f, radius * .35f), c + new Vector2(radius * .6f, radius * .35f), c + new Vector2(radius * .6f, -radius * .6f), c + new Vector2(-radius * .6f, -radius * .6f));
                for (int i = -1; i <= 1; i++)
                    Poly(vh, ink, 2, c + new Vector2(i * radius * .4f - radius * .14f, radius * .35f), c + new Vector2(i * radius * .4f - radius * .14f, radius * .62f), c + new Vector2(i * radius * .4f + radius * .14f, radius * .62f), c + new Vector2(i * radius * .4f + radius * .14f, radius * .35f));
                Poly(vh, bright, 2, c + new Vector2(-radius * .16f, -radius * .6f), c + new Vector2(-radius * .16f, -radius * .1f), c + new Vector2(0, radius * .05f), c + new Vector2(radius * .16f, -radius * .1f), c + new Vector2(radius * .16f, -radius * .6f));
                break;
            case BoardEmblem.Armies:
                // Crossed swords: blades, crossguards, pommels.
                foreach (int side in new[] { -1, 1 })
                {
                    Vector2 hilt = c + new Vector2(-side * radius * .75f, -radius * .75f), tip = c + new Vector2(side * radius * .75f, radius * .75f);
                    Line(vh, hilt, tip, 2.2f, ink);
                    Vector2 d = (tip - hilt).normalized, n = new Vector2(-d.y, d.x);
                    Vector2 guard = hilt + d * radius * .35f;
                    Line(vh, guard - n * radius * .28f, guard + n * radius * .28f, 2, bright);
                    Diamond(vh, hilt, 3.5f, bright);
                }
                break;
            case BoardEmblem.Deck:
                // A stack of three cards, fanned a little.
                for (int i = 0; i < 3; i++)
                {
                    float a = (i - 1) * 12 * Mathf.Deg2Rad;
                    Vector2 o = c + new Vector2((i - 1) * radius * .22f, 0);
                    Vector2 ex = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * .34f, ey = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * radius * .5f;
                    Poly(vh, i == 1 ? bright : ink, 1.6f, o - ex - ey, o - ex + ey, o + ex + ey, o + ex - ey, o - ex - ey);
                }
                break;
            case BoardEmblem.Discard:
                // Crossbones under a small skull ring.
                Ring(vh, c + Vector2.up * radius * .35f, radius * .3f, bright);
                foreach (int side in new[] { -1, 1 })
                {
                    Vector2 a = c + new Vector2(-side * radius * .8f, -radius * .7f), b = c + new Vector2(side * radius * .8f, radius * .1f);
                    Line(vh, a, b, 2, ink);
                    Diamond(vh, a, 3, ink); Diamond(vh, b, 3, ink);
                }
                break;
            case BoardEmblem.Environment:
                // Sun and rays: the weather over the whole board.
                Ring(vh, c, radius * .38f, bright);
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4;
                    Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Line(vh, c + dir * radius * .55f, c + dir * radius * (i % 2 == 0 ? 1f : .8f), 1.6f, ink);
                }
                break;
            case BoardEmblem.Hand:
                // A hand of five cards fanned from one point.
                for (int i = 0; i < 5; i++)
                {
                    float a = (i - 2) * 16 * Mathf.Deg2Rad;
                    Vector2 pivot = c + Vector2.down * radius * .8f;
                    Vector2 up = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)), right = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Vector2 bottom = pivot + up * radius * .25f, top = pivot + up * radius * 1.5f;
                    Poly(vh, i == 2 ? bright : ink, 1.4f, bottom - right * radius * .2f, top - right * radius * .2f, top + right * radius * .2f, bottom + right * radius * .2f);
                }
                break;
            case BoardEmblem.Materials:
                // An ingot: a hexagonal seal with a gem at its heart.
                var hex = new Vector2[7];
                for (int i = 0; i <= 6; i++) { float angle = i * Mathf.PI / 3 + Mathf.PI / 6; hex[i] = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * .8f; }
                Poly(vh, ink, 2, hex);
                Diamond(vh, c, radius * .3f, bright);
                break;
            case BoardEmblem.Champion:
                // A five-pointed star in a ring.
                Ring(vh, c, radius * .9f, ink);
                for (int i = 0; i < 5; i++)
                {
                    float a = Mathf.PI / 2 + i * 2 * Mathf.PI / 5, b = Mathf.PI / 2 + ((i + 2) % 5) * 2 * Mathf.PI / 5;
                    Line(vh, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * .7f, c + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius * .7f, 1.8f, bright);
                }
                break;
            default:
                // The heraldic sprig.
                Line(vh, c + Vector2.down * radius, c + Vector2.up * radius, .8f, Tone(.08f));
                for (int i = 0; i < 4; i++)
                {
                    var stem = c + Vector2.up * (i - 1.5f) * radius * .42f;
                    foreach (int side in new[] { -1, 1 })
                    {
                        var tip = stem + new Vector2(side * radius * .42f, radius * .32f);
                        Line(vh, stem, tip, 2, Tone(.08f));
                        Diamond(vh, tip, 3, Tone(.08f));
                    }
                }
                break;
        }
    }
    private static void Poly(VertexHelper vh, Color color, float width, params Vector2[] points)
    {
        for (int i = 0; i + 1 < points.Length; i++) Line(vh, points[i], points[i + 1], width, color);
    }

    private Color Tone(float alpha) => new Color(accent.r, accent.g, accent.b, alpha * accent.a);
    private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb)
    {
        int i = vh.currentVertCount;
        vh.AddVert(a, ca, Vector2.zero); vh.AddVert(b, ca, Vector2.zero);
        vh.AddVert(c, cb, Vector2.zero); vh.AddVert(d, cb, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
    }
    private static Vector2[] Corners(Rect r, float c) => new[] {
        new Vector2(r.xMin+c,r.yMin), new Vector2(r.xMax-c,r.yMin),
        new Vector2(r.xMax,r.yMin+c), new Vector2(r.xMax,r.yMax-c),
        new Vector2(r.xMax-c,r.yMax), new Vector2(r.xMin+c,r.yMax),
        new Vector2(r.xMin,r.yMax-c), new Vector2(r.xMin,r.yMin+c) };
    private static void Plate(VertexHelper vh, Rect r, float cut, Color low, Color high)
    {
        var corners = Corners(r, cut); int start = vh.currentVertCount;
        vh.AddVert(r.center, Color.Lerp(low, high, .5f), Vector2.zero);
        foreach (var p in corners) vh.AddVert(p, Color.Lerp(low, high, (p.y-r.yMin)/r.height), Vector2.zero);
        for (int i=0;i<8;i++) vh.AddTriangle(start,start+1+i,start+1+(i+1)%8);
    }
    private static void Outline(VertexHelper vh, Rect r, float cut, Color color, float width)
    {
        var points = Corners(r, cut);
        for (int i=0;i<8;i++) Line(vh, points[i], points[(i+1)%8], width, color);
    }
    private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 d = (b-a).normalized; Vector2 n = new Vector2(-d.y,d.x)*width*.5f;
        Quad(vh,a-n,b-n,b+n,a+n,color,color);
    }
    private static void Diamond(VertexHelper vh, Vector2 p, float radius, Color color)
        => Quad(vh,p+Vector2.down*radius,p+Vector2.right*radius,p+Vector2.up*radius,p+Vector2.left*radius,color,color);
    private static void Ring(VertexHelper vh, Vector2 p, float radius, Color color)
    {
        for (int i=0;i<48;i++)
        {
            float a=i*Mathf.PI/24, b=(i+1)*Mathf.PI/24;
            Line(vh,p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,p+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,.7f,color);
        }
    }
}

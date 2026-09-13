using UnityEngine;
using UnityEngine.UI;

/// <summary>Resolution independent, engraved board furniture. One mesh, no textures or per-frame allocations.</summary>
[DisallowMultipleComponent]
public sealed class BoardSurface : MaskableGraphic
{
    public Color accent;
    public Color surface;
    public bool ceremonial;
    public bool compact;

    public static void Dress(Image panel, Color accent, bool ceremonial = false, bool compact = false)
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
        art.raycastTarget = false;
        panel.color = Color.clear;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width < 4 || r.height < 4) return;
        float cut = Mathf.Min(compact ? 4 : 9, r.height * .15f);
        // Recessed shadow, cool slate center, softly lit metal at the upper edge.
        Plate(vh, new Rect(r.x - 2, r.y - 4, r.width + 4, r.height + 4), cut, new Color(0, 0, 0, .12f), new Color(0, 0, 0, .12f));
        Color low = Color.Lerp(surface, new Color(.015f, .025f, .03f, .94f), .72f);
        Color high = Color.Lerp(surface, new Color(.11f, .14f, .15f, .97f), .6f);
        // The landscape is part of the board: large furniture stays transparent, while small
        // card stock and controls retain the opacity of their own skin surface.
        low.a = compact ? surface.a : .36f;
        high.a = compact ? surface.a : .53f;
        Plate(vh, r, cut, low, high);
        // Fine horizontal grain. Deterministic geometry avoids texture imports and stretching.
        // No full-width scan lines: these fight the landscape and shimmer at smaller view sizes.
        Outline(vh, r, cut, Tone(.4f), 1);
        Rect inner = new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6);
        Outline(vh, inner, Mathf.Max(1, cut - 2), new Color(0, 0, 0, .5f), 1);
        if (!compact)
        {
            // Inlaid corner brackets and rivets replace the uniform prototype box outline.
            foreach (float x in new[] { r.xMin, r.xMax })
            foreach (float y in new[] { r.yMin, r.yMax })
            {
                float sx = x == r.xMin ? 1 : -1, sy = y == r.yMin ? 1 : -1;
                var a = new Vector2(x + sx * 5, y + sy * 20);
                var b = new Vector2(x + sx * 5, y + sy * 10);
                var c = new Vector2(x + sx * 10, y + sy * 5);
                var d = new Vector2(x + sx * 30, y + sy * 5);
                Line(vh, a, b, 1.4f, Tone(.85f));
                Line(vh, b, c, 1.4f, Tone(.85f));
                Line(vh, c, d, 1.4f, Tone(.85f));
                Diamond(vh, new Vector2(x + sx * 10, y + sy * 10), 1.6f, Tone(.95f));
            }
            // A quiet ornament in the unused right side of a lane; never competes with card art.
            if (r.width > 430)
            {
                Vector2 center = new Vector2(r.xMax - 68, r.center.y - 7);
                float radius = Mathf.Min(r.height * .29f, 38);
                Ring(vh, center, radius, Tone(.085f));
                Ring(vh, center, radius * .78f, Tone(.06f));
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4;
                    Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Line(vh, center + dir * radius * .35f, center + dir * radius * 1.13f, .7f, Tone(.1f));
                }
                Diamond(vh, center, 4, Tone(.13f));
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

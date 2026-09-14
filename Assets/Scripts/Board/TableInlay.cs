using UnityEngine;
using UnityEngine.UI;

/// <summary>A soft oval playing surface with a single engraved horizon and eclipse device.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TableInlay : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = rectTransform.rect;
        var center = r.center;
        // Feathered perimeter: the battlefield has no rectangular container or hard outer edge.
        const int segments = 96;
        vh.AddVert(center, new Color(.015f,.028f,.025f,.86f), Vector2.zero);
        for (int i=0;i<=segments;i++)
        {
            float a = i * Mathf.PI * 2 / segments;
            vh.AddVert(center + new Vector2(Mathf.Cos(a)*r.width*.6f,Mathf.Sin(a)*r.height*.65f),
                new Color(.015f,.028f,.025f,0), Vector2.zero);
            if (i>0) vh.AddTriangle(0,i,i+1);
        }
        var gold = new Color(color.r,color.g,color.b,.42f);
        float y = Mathf.Lerp(r.yMin,r.yMax,.48f);
        Segment(vh,new Vector2(r.xMin+25,y),new Vector2(center.x-38,y),.8f,gold);
        Segment(vh,new Vector2(center.x+38,y),new Vector2(r.xMax-25,y),.8f,gold);
        var sun = new Vector2(center.x,y);
        for (int i=0;i<64;i++)
        {
            float a=i*Mathf.PI/32, b=(i+1)*Mathf.PI/32;
            Segment(vh,sun+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*18,
                sun+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*18,1,gold);
        }
        for (int i=0;i<16;i++)
        {
            float a=i*Mathf.PI/8;
            var d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
            Segment(vh,sun+d*23,sun+d*(i%2==0?32:28),.8f,gold);
        }
    }
    static void Segment(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
    {
        var d=(b-a).normalized;
        var n=new Vector2(-d.y,d.x)*width*.5f;
        int start=vh.currentVertCount;
        vh.AddVert(a-n,tint,Vector2.zero); vh.AddVert(a+n,tint,Vector2.zero);
        vh.AddVert(b+n,tint,Vector2.zero); vh.AddVert(b-n,tint,Vector2.zero);
        vh.AddTriangle(start,start+1,start+2); vh.AddTriangle(start,start+2,start+3);
    }
}

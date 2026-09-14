using UnityEngine;
using UnityEngine.UI;

/// <summary>Small texture-free light primitives shared by the board's fantasy effects.</summary>
internal static class FantasyVfxMesh
{
    public static Color Alpha(Color c,float alpha) { c.a=alpha; return c; }
    public static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
    {
        int n=vh.currentVertCount;
        vh.AddVert(a,color,Vector2.zero);vh.AddVert(b,color,Vector2.zero);vh.AddVert(c,color,Vector2.zero);vh.AddVert(d,color,Vector2.zero);
        vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
    }
    public static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color color)
    {
        var delta=b-a;
        if(delta.sqrMagnitude<.001f) return;
        var normal=new Vector2(-delta.y,delta.x).normalized*width*.5f;
        Quad(vh,a-normal,b-normal,b+normal,a+normal,color);
    }
    public static void GlowLine(VertexHelper vh,Vector2 a,Vector2 b,float width,Color color,float alpha)
    {
        Line(vh,a,b,width*5,Alpha(color,alpha*.055f));
        Line(vh,a,b,width*2.5f,Alpha(color,alpha*.15f));
        Line(vh,a,b,width,Alpha(color,alpha));
    }
    public static void Spark(VertexHelper vh,Vector2 p,float radius,Color color,float alpha)
    {
        Disc(vh,p,radius*3,Alpha(color,alpha*.22f));
        Quad(vh,p+Vector2.down*radius*1.6f,p+Vector2.right*radius*.65f,p+Vector2.up*radius*1.6f,p+Vector2.left*radius*.65f,Alpha(Color.Lerp(color,Color.white,.45f),alpha));
    }
    public static void Disc(VertexHelper vh,Vector2 p,float radius,Color color)
    {
        int n=vh.currentVertCount;vh.AddVert(p,color,Vector2.zero);
        for(int i=0;i<=16;i++)
        {
            float a=i*Mathf.PI/8;
            vh.AddVert(p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,Alpha(color,0),Vector2.zero);
        }
        for(int i=0;i<16;i++) vh.AddTriangle(n,n+i+1,n+i+2);
    }
    public static void Ring(VertexHelper vh,Vector2 p,float radius,float width,Color color,float alpha,float phase=0,float fraction=1)
    {
        const int steps=48;
        for(int i=0;i<steps;i++)
        {
            float a=phase+i*Mathf.PI*2*fraction/steps,b=phase+(i+1)*Mathf.PI*2*fraction/steps;
            GlowLine(vh,p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,p+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,width,color,alpha);
        }
    }
    public static Vector2 Curve(Vector2 a,Vector2 bend,Vector2 b,float t)
        => (1-t)*(1-t)*a+2*(1-t)*t*bend+t*t*b;
}

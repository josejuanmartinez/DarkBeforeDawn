using UnityEngine;
using UnityEngine.UI;

/// <summary>Softly veils the upper stair, so the visible slice never gives away its eventual height.</summary>
public sealed class AscentMist : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();Rect r=rectTransform.rect;
        Color mist=new(.055f,.043f,.032f,0);
        const int bands=20;
        for(int i=0;i<bands;i++)
        {
            float t=i/(float)bands,u=(i+1)/(float)bands;
            float a=Mathf.SmoothStep(0,1,t),b=Mathf.SmoothStep(0,1,u);
            int n=vh.currentVertCount;
            float y0=Mathf.Lerp(r.yMin+r.height*.67f,r.yMax,t),y1=Mathf.Lerp(r.yMin+r.height*.67f,r.yMax,u);
            vh.AddVert(new Vector2(r.xMin,y0),FantasyVfxMesh.Alpha(mist,a),Vector2.zero);
            vh.AddVert(new Vector2(r.xMax,y0),FantasyVfxMesh.Alpha(mist,a),Vector2.zero);
            vh.AddVert(new Vector2(r.xMax,y1),FantasyVfxMesh.Alpha(mist,b),Vector2.zero);
            vh.AddVert(new Vector2(r.xMin,y1),FantasyVfxMesh.Alpha(mist,b),Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
    }
}

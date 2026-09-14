using UnityEngine;
using UnityEngine.UI;

/// <summary>Breathing gold, candle motes and moving edge light. Never alters the card's layout or hit area.</summary>
public sealed class FantasyCardAura : MaskableGraphic
{
    float strength, target;
    Color tint;
    bool charged;
    float phase;
    public static FantasyCardAura Create(RectTransform parent)
    {
        var go=new GameObject("Enchanted card aura",typeof(RectTransform),typeof(CanvasRenderer),typeof(FantasyCardAura));
        go.transform.SetParent(parent,false);
        BoardPresentation.Stretch((RectTransform)go.transform,Vector2.zero,Vector2.one);
        var effect=go.GetComponent<FantasyCardAura>();effect.raycastTarget=false;
        effect.phase=parent.GetSiblingIndex()*.73f+parent.localPosition.x*.013f;
        return effect;
    }
    public void SetPresentation(float intensity,Color color,bool active)
    { target=intensity;tint=color;charged=active; }
    void Update()
    {
        float old=strength;
        strength=Mathf.MoveTowards(strength,target,Time.unscaledDeltaTime*4);
        if(strength>.001f || old>.001f) SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();if(strength<.001f)return;
        Rect r=rectTransform.rect;float t=Time.unscaledTime+phase;
        float pulse=strength*(.72f+.18f*Mathf.Sin(t*2.5f));
        Vector2 a=new(r.xMin-2,r.yMin-2), b=new(r.xMax+2,r.yMin-2), c=new(r.xMax+2,r.yMax+2), d=new(r.xMin-2,r.yMax+2);
        FantasyVfxMesh.GlowLine(vh,a,b,2,tint,pulse*.65f);FantasyVfxMesh.GlowLine(vh,b,c,2,tint,pulse*.65f);
        FantasyVfxMesh.GlowLine(vh,c,d,2,tint,pulse*.65f);FantasyVfxMesh.GlowLine(vh,d,a,2,tint,pulse*.65f);
        // Fully lit (a card that may act, or is chosen): a wide halo of light stands off the edge too.
        if(strength>.7f)
        {
            float halo=(strength-.7f)/.3f*(.55f+.25f*Mathf.Sin(t*3.1f));
            float o=7;Vector2 ha=new(r.xMin-o,r.yMin-o), hb=new(r.xMax+o,r.yMin-o), hc=new(r.xMax+o,r.yMax+o), hd=new(r.xMin-o,r.yMax+o);
            FantasyVfxMesh.GlowLine(vh,ha,hb,5,tint,halo);FantasyVfxMesh.GlowLine(vh,hb,hc,5,tint,halo);
            FantasyVfxMesh.GlowLine(vh,hc,hd,5,tint,halo);FantasyVfxMesh.GlowLine(vh,hd,ha,5,tint,halo);
        }
        float perimeter=(r.width+r.height)*2;
        for(int i=0;i<(charged?12:5);i++)
        {
            float distance=Mathf.Repeat(t*(charged?45:26)+i*perimeter/12,perimeter);
            Vector2 p=distance<r.width ? new Vector2(r.xMin+distance,r.yMin) : distance<r.width+r.height ? new Vector2(r.xMax,r.yMin+distance-r.width)
                : distance<r.width*2+r.height ? new Vector2(r.xMax-(distance-r.width-r.height),r.yMax) : new Vector2(r.xMin,r.yMax-(distance-r.width*2-r.height));
            FantasyVfxMesh.Spark(vh,p,charged?2.2f:1.5f,tint,pulse);
        }
        for(int i=0;i<(charged?9:4);i++)
        {
            float life=Mathf.Repeat(t*.3f+i*.137f,1);
            float x=r.xMin+Mathf.Repeat(i*.3819f,1)*r.width+Mathf.Sin(t+i)*4;
            var p=new Vector2(x,r.yMin+life*Mathf.Min(r.height,70));
            FantasyVfxMesh.Spark(vh,p,1.3f,tint,Mathf.Sin(life*Mathf.PI)*pulse*.8f);
        }
        if(charged)
        {
            var p=new Vector2(r.center.x,r.yMax+8);
            FantasyVfxMesh.Spark(vh,p,4+Mathf.Sin(t*2),tint,pulse);
            FantasyVfxMesh.GlowLine(vh,p+Vector2.left*18,p+Vector2.left*7,1,tint,pulse);
            FantasyVfxMesh.GlowLine(vh,p+Vector2.right*7,p+Vector2.right*18,1,tint,pulse);
        }
    }
}

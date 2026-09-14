using System.Collections.Generic;
using UnityEngine;

public sealed partial class MatchCinematic
{
    readonly List<Mesh> towerMeshes = new();
    Material stairArtwork;
    const float StairRise = 3.6f;

    static string Roman(int value)
    {
        if(value>3999)return value.ToString();
        string result="";
        int[] amounts={1000,900,500,400,100,90,50,40,10,9,5,4,1};
        string[] glyphs={"M","CM","D","CD","C","XC","L","XL","X","IX","V","IV","I"};
        for(int i=0;i<amounts.Length;i++)while(value>=amounts[i]){result+=glyphs[i];value-=amounts[i];}
        return result;
    }

    // The authored pixel artwork supplies the architecture and lighting. Texture strips extend the
    // column beyond the camera; no low-poly placeholder geometry or cap gives away its height.
    void BuildEndlessStair(int first,int count,int current,int final)
    {
        foreach(var mesh in towerMeshes)if(mesh!=null)Destroy(mesh);
        towerMeshes.Clear();
        if(stairArtwork==null)
        {
            var authored=Resources.Load<Material>("Art/EndlessStairMaterial");
            if(authored==null)throw new System.InvalidOperationException("Import the ascent art with AgentScripts/ImportStairArtwork.cs.");
            stairArtwork=new Material(authored);
            stairArtwork.name="Retro carved stair artwork";
            stairArtwork.SetColor("_BaseColor",Color.white);
            materials.Add(stairArtwork);
        }
        // Five complete storeys between two cornices in the source art. Keeping them in one strip
        // preserves the hand-authored irregular steps, reliefs and perspective between landings.
        const float imageHeight=1774, topPixel=240, bottomPixel=1704;
        const float width=887/(bottomPixel-topPixel)*(StairRise*5);
        int last=first+count;
        if(final>0)last=Mathf.Min(last,final);
        for(int tile=first/5;tile*5<last;tile++)
        {
            int start=Mathf.Max(first,tile*5),end=Mathf.Min(last,tile*5+5);
            float y0=(start-first)*StairRise,y1=(end-first)*StairRise;
            float uv0=1-Mathf.Lerp(bottomPixel,topPixel,(start-tile*5)/5f)/imageHeight;
            float uv1=1-Mathf.Lerp(bottomPixel,topPixel,(end-tile*5)/5f)/imageHeight;
            var mesh=new Mesh {name="Painted stair levels "+(start+1)+" to "+end};
            mesh.vertices=new[]{new Vector3(-width/2,y0,0),new Vector3(width/2,y0,0),new Vector3(width/2,y1,0),new Vector3(-width/2,y1,0)};
            mesh.uv=new[]{new Vector2(0,uv0),new Vector2(1,uv0),new Vector2(1,uv1),new Vector2(0,uv1)};
            mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateNormals();mesh.RecalculateBounds();towerMeshes.Add(mesh);
            var strip=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));
            strip.layer=Layer;strip.transform.SetParent(tower.transform,false);
            strip.GetComponent<MeshFilter>().sharedMesh=mesh;
            strip.GetComponent<MeshRenderer>().sharedMaterial=stairArtwork;
        }
    }
}

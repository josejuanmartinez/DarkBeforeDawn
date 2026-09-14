using System.Collections.Generic;
using UnityEngine;

public sealed partial class MatchCinematic
{
    readonly List<Mesh> towerMeshes = new();
    Material[] masonry;
    Material mortar, sandstone, roofSlate, bannerRed, bannerGold, fire;

    const float StairRise = 3.6f;
    static float LandingX(int floor) => floor % 2 == 0 ? -3.65f : 3.65f;
    static string Roman(int value)
    {
        if(value>3999)return value.ToString();
        string result="";
        int[] amounts={1000,900,500,400,100,90,50,40,10,9,5,4,1};
        string[] glyphs={"M","CM","D","CD","C","XC","L","XL","X","IX","V","IV","I"};
        for(int i=0;i<amounts.Length;i++)while(value>=amounts[i]){result+=glyphs[i];value-=amounts[i];}
        return result;
    }

    // Open switchback stairs: the visible window advances with the player, not a fixed five-floor tower.
    void BuildEndlessStair(int first,int count,int current,int final)
    {
        foreach(var mesh in towerMeshes)if(mesh!=null)Destroy(mesh);
        towerMeshes.Clear();
        if(masonry==null)
        {
            masonry=new[]{Material(new Color(.29f,.30f,.26f),0,.08f),Material(new Color(.38f,.36f,.29f),0,.08f),Material(new Color(.24f,.27f,.24f),0,.06f),Material(new Color(.43f,.40f,.32f),0,.1f)};
            mortar=Material(new Color(.105f,.12f,.10f),0,.03f);
            sandstone=Material(new Color(.40f,.39f,.32f),0,.12f);
            roofSlate=Material(new Color(.12f,.16f,.14f),0,.05f);
            bannerRed=Material(new Color(.35f,.05f,.03f),0,.08f);
            bannerGold=Material(new Color(.7f,.48f,.20f),.2f,.2f);
            fire=Material(new Color(1,.52f,.09f),0,0);
            fire.EnableKeyword("_EMISSION");fire.SetColor("_EmissionColor",new Color(1,.32f,.025f)*2);
        }
        for(int level=0;level<count;level++)
        {
            int absolute=first+level;
            float y=level*StairRise,x=LandingX(absolute);
            bool ending=final>0 && absolute==final-1;
            // Broad landings on ancient cantilevered piers; an open void remains between them.
            Block("Landing "+Roman(absolute+1),new Vector3(x,y,0),new Vector3(3.5f,.32f,3.1f),sandstone);
            for(int row=0;row<4;row++)
            {
                float breadth=2.7f-row*.42f;
                Block("Weathered landing corbel",new Vector3(x,y-.32f-row*.3f,.15f),new Vector3(breadth,.32f,2.55f-row*.3f),masonry[row]);
            }
            for(int tile=0;tile<5;tile++)
                Block("Worn landing flagstone",new Vector3(x-1.4f+tile*.7f,y+.19f,0),new Vector3(.67f,.08f,2.9f),masonry[(tile+level)%4]);
            // The floor pairing is set into an upright roadside shrine, not a tower facade.
            Block("Shrine stone back",new Vector3(x,y+1.36f,.62f),new Vector3(2.95f,2.1f,.4f),mortar);
            for(int side=-1;side<=1;side+=2)
            {
                Block("Shrine carved jamb",new Vector3(x+side*1.57f,y+1.32f,.58f),new Vector3(.22f,2.3f,.58f),sandstone);
                Torch(new Vector3(x+side*1.44f,y+2.35f,.18f));
                // Stout outside parapets leave the stair mouths open.
                Block("Landing outer parapet",new Vector3(x+side*1.66f,y+.55f,.96f),new Vector3(.18f,.9f,1.2f),masonry[(level+1)%4]);
            }
            Block("Shrine lintel",new Vector3(x,y+2.48f,.6f),new Vector3(3.45f,.2f,.65f),sandstone);
            var crest=Block("Landing crest",new Vector3(x,y+2.69f,.55f),new Vector3(.32f,.32f,.25f),absolute==current?bannerGold:sandstone);
            crest.transform.localRotation=Quaternion.Euler(0,0,45);
            if(level<6)
            {
                float outside=x+Mathf.Sign(x)*1.95f;
                Block("Hanging banner staff",new Vector3(outside,y+.7f,.15f),new Vector3(.07f,2.3f,.07f),bannerGold);
                Cloth(new Vector3(outside-.32f,y+1.7f,.1f),.64f,1.65f,true);
            }
            if(ending)
            {
                // The ending is only visible when the player's window finally reaches it.
                foreach(int side in new[]{-1,1})
                {
                    Block("Final threshold pillar",new Vector3(x+side*1.25f,y+3.25f,1),new Vector3(.38f,2.5f,.5f),sandstone);
                    var arch=Block("Final threshold arch",new Vector3(x+side*.59f,y+4.52f,1),new Vector3(1.6f,.32f,.55f),bannerGold);
                    arch.transform.localRotation=Quaternion.Euler(0,0,-side*38);
                }
                continue;
            }
            float nextX=LandingX(absolute+1),sign=Mathf.Sign(nextX-x);
            float startX=x+sign*1.48f,endX=nextX-sign*1.48f;
            const int steps=22;
            for(int step=0;step<steps;step++)
            {
                float t=(step+.5f)/steps;
                float sx=Mathf.Lerp(startX,endX,t),sy=y+.2f+(step+1)*StairRise/steps;
                Block("Ascending stone tread",new Vector3(sx,sy,-.25f),new Vector3(Mathf.Abs(endX-startX)/steps+.025f,.18f,1.72f),masonry[(step+level)%4]);
                Block("Deep stair riser",new Vector3(sx,sy-.2f,-.25f),new Vector3(Mathf.Abs(endX-startX)/steps+.02f,.3f,1.62f),mortar);
                if(step%4==0)
                {
                    Block("Carved baluster",new Vector3(sx,sy+.4f,-1.17f),new Vector3(.095f,.85f,.095f),sandstone);
                    Block("Rear stair baluster",new Vector3(sx,sy+.4f,.65f),new Vector3(.095f,.85f,.095f),sandstone);
                }
                if(absolute==current && step%3==0)
                    Block("Candlelit stair nosing",new Vector3(sx,sy+.095f,-1.10f),new Vector3(.13f,.025f,.045f),bannerGold);
            }
            Vector3 a=new(startX,y+1.0f,-1.17f),b=new(endX,y+StairRise+1.0f,-1.17f);
            var handrail=Block("Sloping stone handrail",(a+b)*.5f,new Vector3(Vector3.Distance(a,b),.11f,.14f),sandstone);
            handrail.transform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);
            a.z=b.z=.65f;
            handrail=Block("Rear stone handrail",(a+b)*.5f,new Vector3(Vector3.Distance(a,b),.11f,.14f),sandstone);
            handrail.transform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);
        }
        if(first==0)
        {
            float x=LandingX(0);
            for(int i=0;i<6;i++)
                Block("First worn steps",new Vector3(x,-.65f+i*.14f,-2.5f+i*.3f),new Vector3(2.8f,.2f,1.2f),masonry[i%4]);
        }
        CombineTowerGeometry();
    }
    GameObject Block(string name, Vector3 p, Vector3 size, Material mat)
        => Shape(PrimitiveType.Cube,tower.transform,p,size,mat,name);

    void Torch(Vector3 p)
    {
        Block("Iron sconce",p,new Vector3(.12f,.38f,.24f),mortar);
        Shape(PrimitiveType.Sphere,tower.transform,p+Vector3.up*.25f,new Vector3(.14f,.3f,.14f),fire,"Amber torch flame");
    }

    void Cloth(Vector3 p,float width,float length,bool hanging)
    {
        var vertices=new Vector3[12]; var triangles=new List<int>();
        for(int i=0;i<6;i++)
        {
            float x=i*width/5; float ripple=Mathf.Sin(i*1.7f)*.07f;
            vertices[i*2]=p+new Vector3(x,0,ripple);
            vertices[i*2+1]=p+new Vector3(x,-length+(hanging ? Mathf.Abs(i-2.5f)*.12f : i*.05f),ripple);
            if(i==5) continue;
            int n=i*2;
            triangles.AddRange(new[]{n,n+1,n+2,n+2,n+1,n+3,n+2,n+1,n,n+3,n+1,n+2});
        }
        TowerMesh("Crimson swallowtail pennant",vertices,triangles.ToArray(),bannerRed);
        if(hanging)
        {
            Block("Heraldic gold pale",p+new Vector3(width/2,-length*.43f,-.1f),new Vector3(.09f,length*.7f,.025f),bannerGold);
            var seal=Block("Heraldic lozenge",p+new Vector3(width/2,-length*.35f,-.13f),new Vector3(.3f,.3f,.025f),bannerGold);
            seal.transform.localRotation=Quaternion.Euler(0,0,45);
        }
    }

    void TowerMesh(string name,Vector3[] vertices,int[] triangles,Material mat)
    {
        var mesh=new Mesh { name=name,vertices=vertices,triangles=triangles }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); towerMeshes.Add(mesh);
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)); go.layer=Layer; go.transform.SetParent(tower.transform,false);
        go.GetComponent<MeshFilter>().sharedMesh=mesh; go.GetComponent<MeshRenderer>().sharedMaterial=mat;
    }

    void CombineTowerGeometry()
    {
        var groups=new Dictionary<Material,List<CombineInstance>>();
        var parts=tower.GetComponentsInChildren<MeshFilter>();
        foreach(var part in parts)
        {
            var mat=part.GetComponent<MeshRenderer>().sharedMaterial;
            if(!groups.TryGetValue(mat,out var list)) groups.Add(mat,list=new List<CombineInstance>());
            list.Add(new CombineInstance { mesh=part.sharedMesh,transform=tower.transform.worldToLocalMatrix*part.transform.localToWorldMatrix });
        }
        foreach(var group in groups)
        {
            var mesh=new Mesh { name="Batched keep masonry",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(group.Value.ToArray()); towerMeshes.Add(mesh);
            var go=new GameObject("Keep architecture",typeof(MeshFilter),typeof(MeshRenderer)); go.layer=Layer; go.transform.SetParent(tower.transform,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh; go.GetComponent<MeshRenderer>().sharedMaterial=group.Key;
        }
        foreach(var part in parts) { part.gameObject.SetActive(false); Destroy(part.gameObject); }
    }
}

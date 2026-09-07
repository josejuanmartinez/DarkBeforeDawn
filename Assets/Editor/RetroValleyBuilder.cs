using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Deterministic, editable geometry for the card-art-inspired board backdrop.</summary>
public static class RetroValleyBuilder
{
    const string Folder = "Assets/Art/RetroValley";
    const string RootName = "The Golden Vale";
    // These are short-lived editor build caches. Build() recreates/clears them explicitly,
    // so lifecycle management must not reset them in the middle of a menu operation.
    [NoAutoStaticsCleanup] static System.Random random;
    [NoAutoStaticsCleanup] static Transform root;
    [NoAutoStaticsCleanup] static Dictionary<Material, Geometry> batches;
    [NoAutoStaticsCleanup] static Material earth, rock, bark, leaves, goldLeaves, pine, stone, trim, roof, ink, water, falls, flowers;
    [NoAutoStaticsCleanup] static Mesh sphere;
    [NoAutoStaticsCleanup] static readonly List<Vector3> stairTops = new List<Vector3>();
    [NoAutoStaticsCleanup] static readonly List<Vector3> stairBases = new List<Vector3>();
    public static int StairCount => stairTops.Count;
    [field: NoAutoStaticsCleanup] public static float StairLandingGroundGap { get; private set; }
    [field: NoAutoStaticsCleanup] public static float MaximumStairSupportGap { get; private set; }

    sealed class Geometry
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Color> colors = new List<Color>();
        public readonly List<Vector2> uvs = new List<Vector2>();
        public readonly List<int> triangles = new List<int>();
        public void Add(Vector3 a, Vector3 b, Vector3 c, Color color, Vector2 ua = default, Vector2 ub = default, Vector2 uc = default)
        {
            int start = vertices.Count;
            var normal = Vector3.Cross(b - a, c - a).normalized;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            for (int i = 0; i < 3; i++) { normals.Add(normal); colors.Add(color); triangles.Add(start + i); }
            uvs.Add(ua); uvs.Add(ub); uvs.Add(uc);
        }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            Add(a,b,c,color,new Vector2(0,0),new Vector2(0,1),new Vector2(1,1));
            Add(a,c,d,color,new Vector2(0,0),new Vector2(1,1),new Vector2(1,0));
        }
        public void Instance(Mesh mesh, Matrix4x4 matrix, Color color)
        {
            int start = vertices.Count;
            var vs = mesh.vertices; var ns = mesh.normals; var inv = matrix.inverse.transpose;
            for (int i=0;i<vs.Length;i++) { vertices.Add(matrix.MultiplyPoint3x4(vs[i])); normals.Add(inv.MultiplyVector(ns[i]).normalized); colors.Add(color); uvs.Add(Vector2.zero); }
            foreach(int t in mesh.triangles) triangles.Add(start+t);
        }
        public Mesh Mesh(string name)
        {
            var mesh = new Mesh {name=name, indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors); mesh.SetUVs(0,uvs); mesh.SetTriangles(triangles,0); mesh.RecalculateBounds();
            return mesh;
        }
    }

    [MenuItem("Tools/Background/Rebuild Golden Vale")]
    public static void Build()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit Play mode to build the valley.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/TCGBoard.unity") throw new InvalidOperationException("Open TCGBoard first.");
        var camera = Camera.main;
        if (camera == null) throw new InvalidOperationException("Main Camera is required.");
        foreach(var shader in new[]{"PrintedFantasy","GoldenDawn","IllustratedRiver"})
            if(Shader.Find("DarkBeforeDawn/"+shader)==null) throw new InvalidOperationException("Missing shader: "+shader);
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art", "RetroValley");
        random = new System.Random(714); batches = new Dictionary<Material, Geometry>();
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Build Golden Vale backdrop");
        var old = GameObject.Find(RootName); if(old != null) Undo.DestroyObjectImmediate(old);
        var previous = GameObject.Find("The Greenwood Marches");
        if(previous!=null) { Undo.RecordObject(previous,"Preserve previous backdrop"); previous.SetActive(false); }
        root = new GameObject(RootName).transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject,"Create Golden Vale");
        earth=Mat("Moss and fern carpet",new Color(.36f,.42f,.15f));
        rock=Mat("Blue slate cliffs",new Color(.32f,.40f,.35f),5);
        bark=Mat("Ancient dark oak",new Color(.28f,.23f,.12f),3);
        leaves=Mat("Forest green pigment",new Color(.30f,.43f,.16f),2);
        goldLeaves=Mat("Sunlit golden leaves",new Color(.62f,.58f,.20f),2);
        pine=Mat("Deep teal firs",new Color(.105f,.235f,.20f),2);
        stone=Mat("Citadel honey limestone",new Color(.66f,.48f,.25f),1);
        trim=Mat("Citadel sunlit edges",new Color(.88f,.70f,.37f),1);
        roof=Mat("Verdigris spires",new Color(.10f,.27f,.27f),4);
        ink=Mat("Window and arch ink",new Color(.035f,.095f,.10f));
        flowers=Mat("Meadow gold",new Color(.81f,.63f,.19f),2);
        earth.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/External/PolyHaven/aerial_grass_rock/aerial_grass_rock_Diffuse.jpg"));earth.SetFloat("_TextureStrength",.55f);
        rock.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/External/PolyHaven/aerial_grass_rock/aerial_grass_rock_Diffuse.jpg"));rock.SetFloat("_TextureStrength",.5f);
        bark.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/External/PolyHaven/pine_bark/pine_bark_Diffuse.jpg"));bark.SetFloat("_TextureStrength",.65f);
        var leafTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/IllustratedOak.png");
        if(leafTexture==null)throw new InvalidOperationException("Illustrated oak texture is missing.");
        foreach(var foliage in new[]{leaves,goldLeaves,pine})
        {
            foliage.shader=Shader.Find("DarkBeforeDawn/IllustratedFoliage");foliage.SetTexture("_BaseMap",leafTexture);
            foliage.SetColor("_BaseColor",foliage==goldLeaves?new Color(1.05f,.97f,.76f):foliage==pine?new Color(.48f,.67f,.60f):new Color(.76f,.91f,.73f));
            foliage.SetFloat("_Cutoff",.55f);foliage.EnableKeyword("_ALPHATEST_ON");
        }
        water=Special("Silver jade river","IllustratedRiver");
        water.SetColor("_BaseColor",new Color(.095f,.36f,.33f)); water.SetColor("_Foam",new Color(.67f,.78f,.52f));
        falls=Special("Waterfall silk","IllustratedRiver");falls.SetFloat("_Fall",1);falls.SetColor("_BaseColor",new Color(.15f,.40f,.40f));falls.SetColor("_Foam",new Color(.79f,.84f,.62f));
        sphere = CrownMesh();
        Landscape(); Mountains(); River(); Castle(); Bridge(); Waterfall(); Woodland(); Foreground();
        foreach(var entry in batches)
        {
            var mesh=entry.Value.Mesh(entry.Key.name);
            string path=Folder+"/"+entry.Key.name+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing!=null)
            {
                // Update through Mesh's API so a live renderer also refreshes its GPU buffers.
                existing.Clear();existing.indexFormat=IndexFormat.UInt32;existing.vertices=mesh.vertices;existing.normals=mesh.normals;
                existing.colors=mesh.colors;existing.uv=mesh.uv;existing.triangles=mesh.triangles;existing.RecalculateBounds();
                UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(mesh);
            }
            else AssetDatabase.CreateAsset(mesh,path);
            var go=new GameObject(entry.Key.name,typeof(MeshFilter),typeof(MeshRenderer));go.layer=8;go.transform.SetParent(root,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=entry.Key;
            renderer.shadowCastingMode=entry.Key==earth||entry.Key==water||entry.Key.name.StartsWith("Mountain")?ShadowCastingMode.Off:ShadowCastingMode.TwoSided;
        }
        UnityEngine.Object.DestroyImmediate(sphere);
        Atmosphere(camera);
        EditorUtility.SetDirty(water);EditorUtility.SetDirty(falls);
        Undo.CollapseUndoOperations(undo);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("Golden Vale saved: layered mountains, citadel, river, waterfall, bridge and ancient woodland.");
    }

    static Geometry G(Material m) { if(!batches.TryGetValue(m,out var g)) batches[m]=g=new Geometry(); return g; }
    static float F(float min,float max) => Mathf.Lerp(min,max,(float)random.NextDouble());
    static Color Tint(float min=.8f,float max=1.14f) => Color.white*F(min,max);
    static Material Special(string name,string shader)
    {
        string path=Folder+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("DarkBeforeDawn/"+shader));AssetDatabase.CreateAsset(m,path);}
        m.shader=Shader.Find("DarkBeforeDawn/"+shader);EditorUtility.SetDirty(m);return m;
    }
    static Material Mat(string name,Color color,float surface=0)
    {
        var m=Special(name,"PrintedFantasy");m.SetColor("_BaseColor",color);m.SetColor("_ShadowColor",new Color(.018f,.060f,.070f));m.SetFloat("_Surface",surface);m.SetFloat("_Wind",surface==2?1:0);return m;
    }
    static float RX(float z) => -4+Mathf.Sin(z*.036f)*10;
    static float Width(float z) => Mathf.Lerp(6.7f,2.8f,Mathf.InverseLerp(-20,130,z));
    static float H(float x,float z)
    {
        float distance=Mathf.Abs(x-RX(z));
        float hill=4+Mathf.PerlinNoise(x*.024f+20,z*.024f+30)*11;
        hill+=15*Mathf.Exp(-((x-24)*(x-24)/340+(z-46)*(z-46)/530));
        float bank=Mathf.SmoothStep(0,1,Mathf.Clamp01((distance-Width(z)+1)/14));
        float natural=Mathf.Lerp(-1.4f,hill,bank);
        float siteRadius=Mathf.Sqrt((x-24)*(x-24)/(16*16)+(z-46)*(z-46)/(12*12));
        float plateau=22*Mathf.SmoothStep(0,1,Mathf.Clamp01((1.5f-siteRadius)/.65f));
        float riverClear=Mathf.SmoothStep(0,1,Mathf.Clamp01((distance-Width(z)+.5f)/3));
        // Preserve the submerged channel; a zero-height plateau would z-fight the water.
        return Mathf.Max(natural,(plateau+1.4f)*riverClear-1.4f);
    }
    static void Landscape()
    {
        var g=G(earth);int nx=180,nz=190;float step=1.6f;
        for(int z=0;z<nz;z++)for(int x=0;x<nx;x++)
        {
            float wx=(x-nx/2)*step,wz=-52+z*step;
            var a=new Vector3(wx,H(wx,wz),wz);var b=new Vector3(wx,H(wx,wz+step),wz+step);
            var c=new Vector3(wx+step,H(wx+step,wz+step),wz+step);var d=new Vector3(wx+step,H(wx+step,wz),wz);
            g.Quad(a,b,c,d,Tint(.88f,1.08f));
        }
        // Clustered, stratified rock faces form the castle's river bluff.
        for(int i=0;i<90;i++)
        {
            float x=F(12,35),z=F(29,64);float y=H(x,z);
            if(x>20&&x<28&&z<43)continue;
            Blob(new Vector3(x,y-1.5f,z),new Vector3(F(1.7f,4.5f),F(2.5f,6),F(2,4)),rock,Tint(.8f,1.15f));
        }
    }
    static void Mountains()
    {
        for(int layer=0;layer<3;layer++)
        {
            var m=Mat("Mountain range "+(layer+1),Color.Lerp(new Color(.18f,.32f,.34f),new Color(.49f,.57f,.51f),layer*.34f));
            m.SetColor("_ShadowColor",Color.Lerp(new Color(.09f,.21f,.25f),new Color(.35f,.46f,.46f),layer*.32f));
            var g=G(m);float z=135+layer*35;
            for(int i=0;i<80;i++)
            {
                float x=-240+i*6;
                Func<float,float> height=wx=>24+layer*8+Mathf.PerlinNoise(wx*.023f+13+layer*7,layer*3+7)*33+Mathf.PerlinNoise(wx*.085f+41,layer+6)*8;
                var a=new Vector3(x,0,z-22);var b=new Vector3(x,height(x),z);var c=new Vector3(x+6,height(x+6),z);var d=new Vector3(x+6,0,z-22);
                var ridge=new Vector3(x+2,height(x)*.52f,z-15);
                g.Add(a,b,ridge,Tint(.92f,1.02f));g.Add(ridge,b,c,Tint(.98f,1.12f));g.Add(ridge,c,d,Tint(.92f,1.02f));g.Add(a,ridge,d,Color.white);
            }
        }
    }
    static void River()
    {
        var g=G(water);
        for(int i=0;i<210;i++)
        {
            float z=-52+i*1.1f,n=z+1.1f;
            g.Quad(new Vector3(RX(z)-Width(z),0,z),new Vector3(RX(n)-Width(n),0,n),new Vector3(RX(n)+Width(n),0,n),new Vector3(RX(z)+Width(z),0,z),Color.white);
        }
        for(int i=0;i<190;i++)
        {
            float z=F(-25,125),side=i%2==0?-1:1;float x=RX(z)+side*(Width(z)+F(.1f,3));
            float s=F(.35f,1.4f);Blob(new Vector3(x,Mathf.Max(.02f,H(x,z)),z),new Vector3(s,s*.62f,s*.8f),i%3==0?earth:rock,Tint());
        }
    }
    static void Box(Vector3 p,Vector3 scale,Material m, float angle=0)
    {
        var mat=Matrix4x4.TRS(p,Quaternion.Euler(0,angle,0),scale);var v=new Vector3[8];
        for(int i=0;i<8;i++)v[i]=mat.MultiplyPoint3x4(new Vector3((i&1)==0?-.5f:.5f,(i&2)==0?-.5f:.5f,(i&4)==0?-.5f:.5f));
        var g=G(m);var c=Tint(.92f,1.06f);
        g.Quad(v[0],v[2],v[3],v[1],c);g.Quad(v[5],v[7],v[6],v[4],c);g.Quad(v[4],v[6],v[2],v[0],c);
        g.Quad(v[1],v[3],v[7],v[5],c);g.Quad(v[2],v[6],v[7],v[3],c);g.Quad(v[4],v[0],v[1],v[5],c);
    }
    static void Tube(Vector3 a,Vector3 b,float r0,float r1,Material material, int sides=9)
    {
        var g=G(material);var q=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);var color=Tint();
        for(int i=0;i<sides;i++)
        {
            float u=i*Mathf.PI*2/sides,v=(i+1)*Mathf.PI*2/sides;
            var d0=q*new Vector3(Mathf.Cos(u),0,Mathf.Sin(u));var d1=q*new Vector3(Mathf.Cos(v),0,Mathf.Sin(v));
            g.Quad(a+d0*r0,b+d0*r1,b+d1*r1,a+d1*r0,color);
            if(r1>0)g.Add(b,b+d1*r1,b+d0*r1,color);
        }
    }
    static void Blob(Vector3 p,Vector3 scale,Material material,Color color)
    {
        if(material==leaves||material==goldLeaves||material==pine)
        {
            for(int i=0;i<3;i++)
            {
                var q=Quaternion.Euler(F(-25,25),F(-55,55),F(-35,35));
                var right=q*Vector3.right*scale.x*1.5f;var up=q*Vector3.up*Mathf.Max(scale.y,scale.x*.68f);
                var center=p+new Vector3(F(-.3f,.3f),F(-.2f,.2f),F(-.3f,.3f))*scale.x;
                G(material).Quad(center-right-up,center-right+up,center+right+up,center+right-up,color);
            }
        }
        else if(material==rock)
        {
            // Irregular fractured strata rather than smooth, inflated boulders.
            const int sides=7;var v=new Vector3[sides*3];
            for(int ring=0;ring<3;ring++)for(int s=0;s<sides;s++)
            {
                float a=s*Mathf.PI*2/sides;float radius=F(.75f,1.1f)*(ring==1?1:.8f);
                v[ring*sides+s]=p+Vector3.Scale(new Vector3(Mathf.Cos(a)*radius+(ring-1)*.1f,(ring-1)*.85f+F(-.12f,.12f),Mathf.Sin(a)*radius),scale);
            }
            for(int ring=0;ring<2;ring++)for(int s=0;s<sides;s++)
            {
                int a=ring*sides+s,b=ring*sides+(s+1)%sides;
                G(material).Quad(v[a],v[a+sides],v[b+sides],v[b],color);
            }
            for(int s=0;s<sides;s++)G(material).Add(p+Vector3.up*scale.y*.86f,v[2*sides+(s+1)%sides],v[2*sides+s],color);
        }
        else G(material).Instance(sphere,Matrix4x4.TRS(p,Quaternion.Euler(F(-12,12),F(0,360),F(-12,12)),scale),color);
    }
    static Mesh CrownMesh()
    {
        int rings=5,sides=9;var vertices=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();
        Func<int,int,Vector3> point=(r,s)=>{float a=s*Mathf.PI*2/sides,b=r*Mathf.PI/rings;float w=1+Mathf.Sin(s*13+r*7)*.1f;return new Vector3(Mathf.Cos(a)*Mathf.Sin(b)*w,Mathf.Cos(b),Mathf.Sin(a)*Mathf.Sin(b)*w);};
        for(int r=0;r<=rings;r++)for(int s=0;s<sides;s++){var v=point(r,s);vertices.Add(v);normals.Add(v.normalized);}
        for(int r=0;r<rings;r++)for(int s=0;s<sides;s++){int a=r*sides+s,b=r*sides+(s+1)%sides,c=b+sides,d=a+sides;triangles.AddRange(new[]{a,b,c,a,c,d});}
        var mesh=new Mesh {name="Temporary foliage crown"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);return mesh;
    }
    static void Castle()
    {
        var origin=new Vector3(24,H(24,46)+1,46);
        // Retaining masonry extends from the courtyard down INTO the actual hillside.
        var foundation=Mat("Citadel retaining masonry",new Color(.43f,.40f,.29f),1);
        var corners=new[]{new Vector2(-11,-7),new Vector2(-11,6),new Vector2(-7,8),new Vector2(8,8),new Vector2(11,4),new Vector2(11,-7)};
        for(int i=0;i<corners.Length;i++)
        {
            var a=origin+new Vector3(corners[i].x,-.05f,corners[i].y);var b=origin+new Vector3(corners[(i+1)%corners.Length].x,-.05f,corners[(i+1)%corners.Length].y);
            var bottomA=new Vector3(a.x,H(a.x,a.z)-1,a.z);var bottomB=new Vector3(b.x,H(b.x,b.z)-1,b.z);
            G(foundation).Quad(bottomA,bottomB,b,a,Color.white);G(foundation).Add(origin+Vector3.down*.05f,a,b,Color.white);
        }
        Box(origin+new Vector3(0,3,0),new Vector3(15,6,10),stone);
        Box(origin+new Vector3(0,6,0),new Vector3(15.6f,.5f,10.5f),trim);
        // Slender grouped towers create the silhouette of the Rivendell land card.
        Tower(origin+new Vector3(-6,0,-4),1.25f,12);
        Tower(origin+new Vector3(6,0,-4),1.45f,15);
        Tower(origin+new Vector3(-5,0,4),1.2f,18);
        Tower(origin+new Vector3(4,0,4),1.15f,22);
        Tower(origin+new Vector3(0,5,0),2.1f,20);
        Tower(origin+new Vector3(9,-2,1),.85f,11);
        Tower(origin+new Vector3(-9,-2,2),.9f,10);
        // Roof of the great hall, with deep arcaded windows and stone buttresses.
        var g=G(roof);var p=origin+new Vector3(0,8,-2);
        g.Quad(p+new Vector3(-6,0,-3),p+new Vector3(-6,4,0),p+new Vector3(6,4,0),p+new Vector3(6,0,-3),Color.white);
        g.Quad(p+new Vector3(6,0,3),p+new Vector3(6,4,0),p+new Vector3(-6,4,0),p+new Vector3(-6,0,3),Color.white);
        for(int i=-5;i<=5;i+=2)
        {
            Gothic(origin+new Vector3(i,2.6f,-5.02f),.48f,2.3f);
            Box(origin+new Vector3(i+1,3.5f,-5.3f),new Vector3(.22f,6.3f,.6f),trim);
        }
        Gothic(origin+new Vector3(0,0,-5.05f),1,3.4f);
        SupportedApproach(origin,foundation);
    }
    static void SupportedApproach(Vector3 origin,Material foundation)
    {
        stairTops.Clear();stairBases.Clear();MaximumStairSupportGap=0;
        const int count=64;const float tread=.43f;const float halfWidth=1.65f;
        float startZ=origin.z-5.2f,endZ=startZ-count*tread;
        float endHeight=H(origin.x,endZ)+.12f;
        for(int i=0;i<count;i++)
        {
            float z=startZ-(i+.5f)*tread;
            float top=Mathf.Lerp(origin.y+.06f,endHeight,(i+1f)/count);
            float terrain=H(origin.x,z);
            top=Mathf.Max(top,terrain+.12f);
            float low=Mathf.Min(Mathf.Min(H(origin.x-halfWidth,z-tread),H(origin.x+halfWidth,z-tread)),Mathf.Min(H(origin.x-halfWidth,z+tread),H(origin.x+halfWidth,z+tread)))-.6f;
            var p=new Vector3(origin.x,top,z);
            Box(new Vector3(p.x,(top+low)*.5f,p.z),new Vector3(halfWidth*2,top-low,tread+.02f),foundation);
            Box(p+Vector3.up*.065f,new Vector3(halfWidth*2+.1f,.13f,tread+.025f),trim);
            for(int side=-1;side<=1;side+=2)Box(p+new Vector3(side*(halfWidth+.05f),.4f,0),new Vector3(.22f,.8f,tread+.025f),foundation);
            stairTops.Add(p);stairBases.Add(new Vector3(p.x,low,p.z));
            MaximumStairSupportGap=Mathf.Max(MaximumStairSupportGap,low-terrain);
        }
        StairLandingGroundGap=stairTops[count-1].y-H(origin.x,stairTops[count-1].z);
        // The approach continues as a paved route along the ground to the bridge.
        var start=new Vector3(origin.x,0,endZ);var finish=new Vector3(RX(11)+Width(11)+2.5f,0,11);
        for(int i=0;i<64;i++)
        {
            var a=Vector3.Lerp(start,finish,i/64f);var b=Vector3.Lerp(start,finish,(i+1)/64f);
            var side=Vector3.Cross((b-a).normalized,Vector3.up)*1.35f;
            var v=new[]{a-side,b-side,b+side,a+side};
            for(int j=0;j<4;j++)v[j].y=H(v[j].x,v[j].z)+.16f;
            G(foundation).Quad(v[0],v[3],v[2],v[1],Color.white);
        }
    }
    static void Tower(Vector3 p,float r,float h)
    {
        Tube(p,p+Vector3.up*h,r,r*.84f,stone,12);
        foreach(float y in new[]{.4f,h*.45f,h-.3f,h})Tube(p+Vector3.up*y,p+Vector3.up*(y+.22f),r*1.05f,r*1.05f,trim,12);
        Tube(p+Vector3.up*(h+.2f),p+Vector3.up*(h+r*3.8f),r*1.3f,0,roof,12);
        Tube(p+Vector3.up*(h+r*3.1f),p+Vector3.up*(h+r*4.5f),.07f,.015f,trim,6);
        for(int floor=0;floor<3;floor++)Gothic(p+new Vector3(0,h*(.27f+floor*.24f),-r-.02f),r*.24f,r*1.6f);
        for(int i=0;i<4;i++)
        {
            float angle=i*Mathf.PI*.5f;var offset=new Vector3(Mathf.Cos(angle)*r*.85f,0,Mathf.Sin(angle)*r*.85f);
            Tube(p+offset,p+offset+Vector3.up*(h+.7f),.11f,.07f,trim,5);
        }
    }
    static void Gothic(Vector3 p,float half,float height)
    {
        var g=G(ink);var a=p+Vector3.left*half;var b=p+Vector3.right*half;
        g.Quad(a,a+Vector3.up*(height-half),b+Vector3.up*(height-half),b,Color.white);
        g.Add(a+Vector3.up*(height-half),p+Vector3.up*height,b+Vector3.up*(height-half),Color.white);
        Box(p+new Vector3(0,height*.43f,-.025f),new Vector3(.055f,height*.82f,.035f),trim);
    }
    static void Bridge()
    {
        float z=11,x=RX(z),span=Width(z)+2.5f;
        var g=G(stone);
        for(int i=0;i<32;i++)
        {
            float a=i*Mathf.PI/32,b=(i+1)*Mathf.PI/32;
            float x0=x-Mathf.Cos(a)*span,x1=x-Mathf.Cos(b)*span;
            float y0=1+Mathf.Sin(a)*4.4f,y1=1+Mathf.Sin(b)*4.4f;
            g.Quad(new Vector3(x0,y0,z-1.5f),new Vector3(x0,y0+.8f,z-1.5f),new Vector3(x1,y1+.8f,z-1.5f),new Vector3(x1,y1,z-1.5f),Color.white);
            g.Quad(new Vector3(x0,y0+.8f,z-1.5f),new Vector3(x0,y0+.8f,z+1.5f),new Vector3(x1,y1+.8f,z+1.5f),new Vector3(x1,y1+.8f,z-1.5f),Color.white);
            g.Quad(new Vector3(x0,y0,z+1.5f),new Vector3(x1,y1,z+1.5f),new Vector3(x1,y1+.8f,z+1.5f),new Vector3(x0,y0+.8f,z+1.5f),Color.white);
            for(int side=-1;side<=1;side+=2)
                Tube(new Vector3(x0,y0+1.1f,z+side*1.5f),new Vector3(x1,y1+1.1f,z+side*1.5f),.13f,.13f,trim,5);
            if(i%3==0)Box(new Vector3(x0,y0+.8f,z-1.5f),new Vector3(.24f,.8f,.24f),trim);
        }
        for(int side=-1;side<=1;side+=2)Box(new Vector3(x+side*span,1.1f,z),new Vector3(1.7f,2.5f,3.5f),rock);
    }
    static void Waterfall()
    {
        var g=G(falls);
        for(int lane=0;lane<3;lane++)
        {
            float z=38+lane*.9f;float x=19+lane*.6f;
            for(int i=0;i<24;i++)
            {
                float a=i/24f,b=(i+1)/24f;
                var p=new Vector3(x-a*11,0,z-a*13);
                var q=new Vector3(x-b*11,0,z-b*13);
                p.y=Mathf.Max(.14f,H(p.x,p.z)+.32f);q.y=Mathf.Max(.14f,H(q.x,q.z)+.32f);
                var width=new Vector3(.42f,0,-.35f)*(1+a*.3f);
                g.Quad(p-width,p+width,q+width,q-width,Color.white);
            }
        }
    }
    static void Tree(Vector3 p,float h,bool golden=false)
    {
        Tube(p,p+new Vector3(h*.07f,h*.73f,0),h*.07f,h*.025f,bark,7);
        for(int branch=0;branch<5;branch++)
        {
            float a=branch*2.4f;var end=p+new Vector3(Mathf.Cos(a)*h*.29f,h*(.62f+branch*.065f),Mathf.Sin(a)*h*.26f);
            Tube(p+Vector3.up*h*.42f,end,h*.028f,.035f,bark,6);
            for(int c=0;c<5;c++)
            {
                var offset=new Vector3(F(-.14f,.14f),F(-.09f,.1f),F(-.15f,.15f))*h;
                float s=F(.12f,.21f)*h;
                Blob(end+offset,new Vector3(s,s*.63f,s*.85f),golden||branch==4?goldLeaves:leaves,Tint(.65f,1.18f));
            }
        }
    }
    static void Fir(Vector3 p,float h)
    {
        Tube(p,p+Vector3.up*h,h*.03f,.018f,bark,6);
        for(int j=0;j<10;j++)
        {
            float y=h*(.18f+j*.077f),r=h*(.25f-j*.021f);
            for(int side=0;side<4;side++)
            {
                float a=side*1.57f+j*1.3f;var end=p+new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);
                Tube(p+Vector3.up*(y+.2f),end,.045f,.012f,bark,5);
                Blob(end,new Vector3(r*.65f,r*.3f,r*.6f),pine,Tint(.72f,1));
            }
        }
    }
    static void Woodland()
    {
        for(int i=0;i<370;i++)
        {
            float z=F(-8,142),x=F(-96,96);
            if(Mathf.Abs(x-RX(z))<Width(z)+4)continue;
            if(Mathf.Abs(x-24)<14&&Mathf.Abs(z-46)<16)continue;
            if(x>8&&x<23&&z>20&&z<40)continue;
            if(Mathf.Abs(x-24)<13&&z>-12&&z<43)continue;
            float h=F(4.4f,10);
            if(i%3==0)Fir(new Vector3(x,H(x,z),z),h*1.25f);
            else Tree(new Vector3(x,H(x,z)-.15f,z),h,i%8==0);
        }
        for(int i=0;i<120;i++)
        {
            float z=F(-24,50),x=RX(z)+(i%2==0?-1:1)*F(Width(z)+2,Width(z)+8);
            Blob(new Vector3(x,H(x,z),z),new Vector3(F(.5f,1.5f),F(.35f,.8f),F(.5f,1.2f)),leaves,Tint(.65f,1.1f));
        }
    }
    static void Foreground()
    {
        // Branches and foliage wrap the vista in an irregular engraved silhouette.
        for(int side=-1;side<=1;side+=2)
        {
            var p=new Vector3(side*29,H(side*29,-13)-2,-13);
            var bend=p+new Vector3(-side*1.5f,9,1);
            var fork=p+new Vector3(-side*4,23,2);
            Tube(p,bend,1.5f,1.05f,bark,11);Tube(bend,fork,1.05f,.65f,bark,9);
            for(int b=0;b<7;b++)
            {
                var end=fork+new Vector3(-side*(2+b*1.2f),F(-1,5),F(-1,4));
                Tube(bend+Vector3.up*b*.8f,end,.5f-b*.04f,.09f,bark,7);
                for(int j=0;j<12;j++)Blob(end+new Vector3(F(-2.5f,2.5f),F(-.8f,1.5f),F(-2,2)),new Vector3(F(.8f,1.6f),F(.4f,.85f),F(.8f,1.5f)),b%3==0?goldLeaves:leaves,Tint(.5f,.88f));
            }
            for(int i=0;i<8;i++)
            {
                var end=p+new Vector3(F(-5,5),-1,F(-5,4));
                end.y=H(end.x,end.z)+.1f;Tube(p+Vector3.up*.4f,end,.48f,.04f,bark,7);
            }
        }
        // Fern fans and small gold flowers give the banks a close foreground scale.
        for(int i=0;i<230;i++)
        {
            float z=F(-26,14),x=RX(z)+(i%2==0?-1:1)*F(Width(z)+2,Width(z)+16);
            var p=new Vector3(x,H(x,z)+.08f,z);float size=F(.3f,.8f);
            if(i%4==0){Tube(p,p+Vector3.up*size,.025f,.012f,bark,4);Blob(p+Vector3.up*size,new Vector3(.12f,.055f,.12f),flowers,Tint());continue;}
            for(int leaf=0;leaf<7;leaf++)
            {
                float a=leaf*Mathf.PI*2/7;var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var tangent=Vector3.Cross(d,Vector3.up)*size*.13f;
                var mid=p+d*size*.5f+Vector3.up*size*.65f;var tip=p+d*size;
                G(leaves).Add(p,mid+tangent,tip,Tint());G(leaves).Add(p,tip,mid-tangent,Tint());
            }
        }
    }
    static void Atmosphere(Camera camera)
    {
        var sky=Special("Golden dawn sky","GoldenDawn");sky.SetColor("_Top",new Color(.055f,.21f,.30f));sky.SetColor("_Horizon",new Color(.80f,.65f,.35f));RenderSettings.skybox=sky;
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.0045f;RenderSettings.fogColor=new Color(.43f,.56f,.53f);
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.56f,.64f,.61f);RenderSettings.ambientEquatorColor=new Color(.27f,.37f,.32f);RenderSettings.ambientGroundColor=new Color(.12f,.18f,.16f);
        var sunObject=new GameObject("Golden hour",typeof(Light));sunObject.transform.SetParent(root);sunObject.transform.rotation=Quaternion.Euler(32,-32,0);
        var sun=sunObject.GetComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.79f,.46f);sun.intensity=1.1f;sun.cullingMask=1<<8;sun.shadows=LightShadows.Soft;sun.shadowBias=.03f;sun.shadowNormalBias=.2f;RenderSettings.sun=sun;
        Undo.RecordObject(camera,"Compose valley");Undo.RecordObject(camera.transform,"Compose valley");
        var orbit=camera.GetComponent<FantasyCameraOrbit>();if(orbit!=null){Undo.RecordObject(orbit,"Use bounded camera drift");orbit.enabled=false;}
        var drift=camera.GetComponent<RetroValleyCamera>();if(drift==null)drift=Undo.AddComponent<RetroValleyCamera>(camera.gameObject);
        Undo.RecordObject(drift,"Compose valley drift");drift.enabled=true;drift.position=new Vector3(0,13,-42);drift.focus=new Vector3(0,21,45);drift.drift=.65f;drift.Apply();
        camera.fieldOfView=53;camera.farClipPlane=550;camera.clearFlags=CameraClearFlags.Skybox;
        var post=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Greenwood/Retro landscape.mat");
        if(post!=null){post.SetFloat("_VerticalResolution",720);post.SetFloat("_Dither",.48f);EditorUtility.SetDirty(post);}
        var data=camera.GetComponent<UniversalAdditionalCameraData>();if(data!=null)data.renderPostProcessing=true;
        var cycle=UnityEngine.Object.FindAnyObjectByType<ValleyDayNightCycle>();if(cycle!=null)cycle.RebindSun(sun);
    }
}

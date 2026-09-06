using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Builds a small articulated dragon; it remains separate from the landscape generator.</summary>
public static class ValleyDragonBuilder
{
    const string Folder="Assets/Art/ValleyDragon";
    const string RootName="Sky Dragon - East to West";
    static Material hide,membrane,gold,eye;
    static readonly Dictionary<Material,Geometry> geometry=new Dictionary<Material,Geometry>();
    sealed class Geometry
    {
        public readonly List<Vector3> vertices=new List<Vector3>();
        public readonly List<Color> colors=new List<Color>();
        public readonly List<int> triangles=new List<int>();
        public void Tri(Vector3 a,Vector3 b,Vector3 c,Color tint,bool doubleSided=false)
        {
            int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
            colors.Add(tint);colors.Add(tint);colors.Add(tint);triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);
            if(doubleSided)Tri(c,b,a,tint,false);
        }
        public Mesh ToMesh(string name)
        {
            var m=new Mesh{name=name};m.SetVertices(vertices);m.SetColors(colors);m.SetTriangles(triangles,0);m.RecalculateNormals();m.RecalculateBounds();return m;
        }
    }
    static Geometry G(Material m){if(!geometry.TryGetValue(m,out var g))geometry[m]=g=new Geometry();return g;}
    static Material Material(string name,Color pigment,Color shadow)
    {
        string path=Folder+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("DarkBeforeDawn/PrintedFantasy"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",pigment);m.SetColor("_ShadowColor",shadow);m.SetFloat("_Surface",0);m.SetFloat("_TextureStrength",0);m.SetFloat("_Wind",0);EditorUtility.SetDirty(m);return m;
    }
    [MenuItem("Tools/Background/Add or Rebuild Sky Dragon")]
    public static void Build()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Exit Play mode before building the dragon.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/TCGBoard.unity"||Camera.main==null)throw new InvalidOperationException("Open TCGBoard with its Main Camera.");
        if(Shader.Find("DarkBeforeDawn/PrintedFantasy")==null)throw new InvalidOperationException("Missing dragon pigment shader.");
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Art","ValleyDragon");
        hide=Material("Ember red scales",new Color(.39f,.095f,.035f),new Color(.045f,.022f,.027f));
        membrane=Material("Burnished wing membrane",new Color(.70f,.245f,.055f),new Color(.105f,.025f,.022f));
        gold=Material("Old gold horn and belly",new Color(.76f,.47f,.14f),new Color(.12f,.060f,.026f));
        eye=Material("Amber eyes",new Color(1,.64f,.09f),new Color(.45f,.16f,.02f));
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Add flying sky dragon");
        var previous=GameObject.Find(RootName);if(previous!=null)Undo.DestroyObjectImmediate(previous);
        var root=new GameObject(RootName);root.layer=8;Undo.RegisterCreatedObjectUndo(root,"Create sky dragon");
        var model=new GameObject("Articulated dragon").transform;model.SetParent(root.transform,false);model.gameObject.layer=8;model.localScale=Vector3.one*1.35f;
        var flight=root.AddComponent<ValleyDragonFlight>();flight.model=model;
        geometry.Clear();
        Ellipsoid(new Vector3(0,0,0),new Vector3(.60f,.70f,1.65f),hide);
        Ellipsoid(new Vector3(0,-.32f,.22f),new Vector3(.43f,.43f,1.35f),gold);
        var neck=new[]{new Vector3(0,.1f,1.1f),new Vector3(0,.30f,1.9f),new Vector3(0,.8f,2.55f),new Vector3(0,1.15f,3.3f)};
        for(int i=0;i<neck.Length-1;i++)
        {
            Tube(neck[i],neck[i+1],.43f-i*.075f,.36f-i*.075f,hide);
            Tube(neck[i]+new Vector3(0,-.22f,.03f),neck[i+1]+new Vector3(0,-.17f,.03f),.22f-i*.035f,.18f-i*.035f,gold);
        }
        Ellipsoid(new Vector3(0,1.21f,3.62f),new Vector3(.36f,.29f,.65f),hide);
        Ellipsoid(new Vector3(0,1.17f,4.17f),new Vector3(.24f,.17f,.37f),hide);
        Tube(new Vector3(0,1.02f,3.5f),new Vector3(0,.97f,4.37f),.19f,.09f,gold);
        for(int side=-1;side<=1;side+=2)
        {
            var s=(float)side;
            Tube(new Vector3(s*.23f,1.4f,3.36f),new Vector3(s*.42f,1.72f,2.86f),.10f,.045f,gold);
            Tube(new Vector3(s*.42f,1.72f,2.86f),new Vector3(s*.50f,1.98f,2.58f),.045f,0,gold);
            Ellipsoid(new Vector3(s*.322f,1.32f,3.9f),new Vector3(.045f,.07f,.10f),eye,5,7);
            Tube(new Vector3(s*.32f,1.42f,3.55f),new Vector3(s*.28f,1.41f,4.08f),.06f,.025f,hide);
            // Folded forelegs and haunches trail naturally beneath the body.
            var elbow=new Vector3(s*.83f,-.72f,.40f);
            Tube(new Vector3(s*.45f,-.20f,1),elbow,.20f,.11f,hide);
            Tube(elbow,new Vector3(s*.75f,-.80f,.94f),.11f,.075f,hide);
            var knee=new Vector3(s*.83f,-.87f,-1.02f);
            Tube(new Vector3(s*.46f,-.23f,-.75f),knee,.24f,.15f,hide);
            Tube(knee,new Vector3(s*.72f,-.78f,-1.96f),.15f,.065f,hide);
            for(int claw=0;claw<3;claw++)
            {
                float x=s*(.67f+claw*.09f);
                Tube(new Vector3(x,-.81f,.88f),new Vector3(x,-.93f,1.22f),.035f,0,gold,5);
                Tube(new Vector3(x,-.78f,-1.85f),new Vector3(x,-.94f,-2.13f),.035f,0,gold,5);
            }
        }
        for(int i=0;i<10;i++)
        {
            float z=-1.7f+i*.45f;float y=z>1.4f?.45f+(z-1.4f)*.37f:.62f;
            Fin(new Vector3(0,y,z),.27f,.45f,hide);
        }
        Flush(model,"Body");
        flight.leftWing=Wing(model,-1);flight.rightWing=Wing(model,1);
        flight.tailJoints=new Transform[8];Transform parent=model;
        for(int i=0;i<8;i++)
        {
            var joint=new GameObject("Tail joint "+(i+1)).transform;joint.gameObject.layer=8;joint.SetParent(parent,false);
            joint.localPosition=i==0?new Vector3(0,-.1f,-1.35f):new Vector3(0,0,-.9f);
            float radius=Mathf.Lerp(.34f,.04f,i/7f);
            Tube(Vector3.zero,new Vector3(0,0,-.92f),radius,radius*.80f,hide);
            Fin(new Vector3(0,radius,-.45f),.2f,.28f*(1-i*.07f),gold);
            if(i==7)
            {
                var g=G(hide);g.Tri(new Vector3(0,0,-.7f),new Vector3(0,.42f,-1.05f),new Vector3(0,0,-1.65f),Color.white,true);
                g.Tri(new Vector3(0,0,-.7f),new Vector3(0,0,-1.65f),new Vector3(0,-.42f,-1.05f),Color.white,true);
            }
            Flush(joint,"Tail "+i);flight.tailJoints[i]=joint;parent=joint;
        }
        flight.Sample(11);
        if(!AssetDatabase.IsValidFolder("Assets/Prefabs/Environment"))AssetDatabase.CreateFolder("Assets/Prefabs","Environment");
        PrefabUtility.SaveAsPrefabAssetAndConnect(root,"Assets/Prefabs/Environment/ValleyDragon.prefab",InteractionMode.AutomatedAction);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Undo.CollapseUndoOperations(group);
        Selection.activeGameObject=root;
    }
    static Transform Wing(Transform model,int side)
    {
        var pivot=new GameObject(side<0?"Left wing hinge":"Right wing hinge").transform;pivot.gameObject.layer=8;pivot.SetParent(model,false);pivot.localPosition=new Vector3(side*.45f,.35f,.7f);
        Func<float,float,float,Vector3> P=(x,y,z)=>new Vector3(side*x,y,z);
        var wrist=P(3.65f,.13f,1.7f);
        var outline=new[]{P(7.1f,0,.35f),P(5.05f,-.10f,-.65f),P(6.05f,0,-2.1f),P(3.7f,-.10f,-1.6f),P(4.1f,0,-3.45f),P(2.15f,-.1f,-2.05f),P(2.2f,0,-3.45f),P(0,0,-1.35f),Vector3.zero,P(1.9f,.23f,.35f)};
        for(int i=0;i<outline.Length-1;i++)G(membrane).Tri(wrist,outline[i],outline[i+1],Color.white*(i%2==0?1:.84f),true);
        Tube(Vector3.zero,P(1.9f,.23f,.35f),.18f,.14f,hide);
        Tube(P(1.9f,.23f,.35f),wrist,.14f,.095f,hide);
        for(int i=0;i<7;i+=2)Tube(wrist,outline[i],.072f,.016f,hide,6);
        for(int i=0;i<outline.Length-1;i++)Tube(outline[i],outline[i+1],.029f,.021f,gold,5);
        Tube(wrist,wrist+P(.22f,.3f,.28f),.085f,0,gold,6);
        Flush(pivot,side<0?"Left wing":"Right wing");return pivot;
    }
    static void Tube(Vector3 a,Vector3 b,float r0,float r1,Material material,int sides=8)
    {
        var q=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);var g=G(material);
        for(int i=0;i<sides;i++)
        {
            float u=i*Mathf.PI*2/sides,v=(i+1)*Mathf.PI*2/sides;
            var d0=q*new Vector3(Mathf.Cos(u),0,Mathf.Sin(u));var d1=q*new Vector3(Mathf.Cos(v),0,Mathf.Sin(v));
            g.Tri(a+d0*r0,b+d0*r1,b+d1*r1,Color.white);g.Tri(a+d0*r0,b+d1*r1,a+d1*r0,Color.white);
            if(r1>0)g.Tri(b,b+d1*r1,b+d0*r1,Color.white);
        }
    }
    static void Ellipsoid(Vector3 p,Vector3 scale,Material material,int rings=9,int sides=13)
    {
        Func<int,int,Vector3> V=(r,s)=>{float a=s*Mathf.PI*2/sides,b=r*Mathf.PI/rings;return p+Vector3.Scale(new Vector3(Mathf.Cos(a)*Mathf.Sin(b),Mathf.Cos(b),Mathf.Sin(a)*Mathf.Sin(b)),scale);};
        var g=G(material);
        for(int r=0;r<rings;r++)for(int s=0;s<sides;s++){g.Tri(V(r,s),V(r,s+1),V(r+1,s+1),Color.white);g.Tri(V(r,s),V(r+1,s+1),V(r+1,s),Color.white);}
    }
    static void Fin(Vector3 p,float length,float height,Material material)
    {
        var g=G(material);g.Tri(p+Vector3.forward*length,p+new Vector3(0,height,-length*.5f),p-Vector3.forward*length,Color.white,true);
    }
    static void Flush(Transform parent,string label)
    {
        foreach(var pair in geometry)
        {
            string name=label+" - "+pair.Key.name;string path=Folder+"/"+name+".asset";var mesh=pair.Value.ToMesh(name);
            var previous=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(previous!=null){previous.Clear();previous.vertices=mesh.vertices;previous.colors=mesh.colors;previous.triangles=mesh.triangles;previous.RecalculateNormals();previous.RecalculateBounds();UnityEngine.Object.DestroyImmediate(mesh);mesh=previous;EditorUtility.SetDirty(mesh);}
            else AssetDatabase.CreateAsset(mesh,path);
            var go=new GameObject(pair.Key.name,typeof(MeshFilter),typeof(MeshRenderer));go.layer=8;go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=pair.Key;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        geometry.Clear();
    }
}

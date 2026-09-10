using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

/// <summary>Isolated 3D stage composited above the board. All geometry and materials are runtime-owned.</summary>
public sealed class MatchCinematic : MonoBehaviour
{
    GameObject stage, overlay, tower;
    Camera camera3D;
    RawImage picture;
    Text title, subtitle;
    RenderTexture target;
    readonly List<Material> materials = new();
    readonly List<GameObject> dice = new();
    Material stone, gold, dark, ivory, teal;
    const int Layer = 30;
    public void Initialize(Board board)
    {
        stage = new GameObject("Tournament cinematic stage"); stage.transform.position = new Vector3(0,-2000,0);
        var match = board.Match;
        stone = match.towerStoneMaterial != null ? match.towerStoneMaterial : Material(new Color(.09f,.12f,.17f), .65f, .45f);
        gold = match.towerTrimMaterial != null ? match.towerTrimMaterial : Material(new Color(.78f,.48f,.17f), .85f, .8f);
        dark = match.towerInkMaterial != null ? match.towerInkMaterial : Material(new Color(.018f,.025f,.04f), .4f, .65f);
        ivory = match.dieLightMaterial != null ? match.dieLightMaterial : Material(new Color(.94f,.84f,.61f), .35f, .75f);
        teal = match.towerAccentMaterial != null ? match.towerAccentMaterial : Material(new Color(.035f,.45f,.53f), .7f, .8f);
        var cam = new GameObject("Cinematic camera"); cam.transform.SetParent(stage.transform,false); camera3D = cam.AddComponent<Camera>();
        camera3D.GetUniversalAdditionalCameraData().SetRenderer(board.Match.cinematicRendererIndex);
        camera3D.cullingMask = 1 << Layer; camera3D.clearFlags = CameraClearFlags.SolidColor;
        camera3D.backgroundColor = new Color(.012f,.018f,.033f,0); camera3D.fieldOfView = 35;
        target = new RenderTexture(1600,1000,24) { antiAliasing = 4 }; target.Create(); camera3D.targetTexture = target;
        Light("Warm key", new Vector3(-6,10,-8), new Color(1,.73f,.4f), 170, 35);
        Light("Cold rim", new Vector3(6,8,3), new Color(.2f,.6f,1), 210, 35);
        Light("Front fill", new Vector3(0,2,-10), new Color(.7f,.8f,1), 90, 35);
        var panel = BoardPresentation.Panel(board.transform,"Tournament overlay",new Color(.01f,.02f,.03f,.97f));
        overlay = panel.gameObject; panel.raycastTarget = true;
        BoardPresentation.Stretch(panel.rectTransform,Vector2.zero,Vector2.one);
        var imageGO = new GameObject("3D tower and dice",typeof(RectTransform),typeof(RawImage)); imageGO.transform.SetParent(overlay.transform,false);
        picture = imageGO.GetComponent<RawImage>(); picture.texture = target; picture.raycastTarget = false;
        BoardPresentation.Stretch(picture.rectTransform,new Vector2(.12f,.08f),new Vector2(.88f,.91f));
        var font = board.interfaceFont != null ? board.interfaceFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title = BoardPresentation.TextLabel(overlay.transform,"THE ASCENT",font,36,new Color(.94f,.8f,.5f),new Vector2(.1f,.88f),new Vector2(.9f,.99f),TextAnchor.MiddleCenter);
        subtitle = BoardPresentation.TextLabel(overlay.transform,"",font,20,new Color(.9f,.9f,.84f),new Vector2(.1f,.01f),new Vector2(.9f,.1f),TextAnchor.MiddleCenter);
    }
    Material Material(Color color,float metal,float smooth)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader); mat.color = color; mat.SetFloat("_Metallic",metal); mat.SetFloat("_Smoothness",smooth); materials.Add(mat); return mat;
    }
    void Light(string name, Vector3 position, Color color,float intensity,float range)
    {
        var go = new GameObject(name); go.transform.SetParent(stage.transform,false); go.transform.localPosition = position;
        var light = go.AddComponent<Light>(); light.type = LightType.Point; light.color = color; light.intensity = intensity; light.range = range; light.cullingMask = 1 << Layer;
    }
    GameObject Shape(PrimitiveType type, Transform root, Vector3 position, Vector3 scale, Material material, string name)
    {
        var go = GameObject.CreatePrimitive(type); go.name = name; go.layer = Layer; go.transform.SetParent(root,false);
        go.transform.localPosition = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material;
        Destroy(go.GetComponent<Collider>()); return go;
    }
    void Look(Vector3 position,Vector3 at)
    { camera3D.transform.localPosition = position; camera3D.transform.LookAt(stage.transform.TransformPoint(at)); }
    public IEnumerator ShowTower(int floor)
    {
        overlay.SetActive(true); overlay.transform.SetAsLastSibling(); picture.color = Color.white;
        overlay.GetComponent<Image>().color = new Color(.01f,.02f,.03f,.97f);
        BoardPresentation.Stretch(picture.rectTransform,new Vector2(.12f,.08f),new Vector2(.88f,.91f));
        BoardPresentation.Stretch(title.rectTransform,new Vector2(.1f,.88f),new Vector2(.9f,.99f));
        BoardPresentation.Stretch(subtitle.rectTransform,new Vector2(.1f,.01f),new Vector2(.9f,.1f));
        foreach (var die in dice) if(die != null) Destroy(die); dice.Clear();
        if (tower != null) Destroy(tower);
        tower = new GameObject("The tournament tower"); tower.transform.SetParent(stage.transform,false);
        const int floors = 5;
        for (int i = 0; i < floors; i++)
        {
            float y = i*2.6f;
            Shape(PrimitiveType.Cylinder,tower.transform,new Vector3(0,y,0),new Vector3(5.6f,.16f,4),gold,"Gilded tier");
            Shape(PrimitiveType.Cylinder,tower.transform,new Vector3(0,y+1.25f,0),new Vector3(4.7f,1.1f,3.4f),stone,"Obsidian floor");
            Shape(PrimitiveType.Cube,tower.transform,new Vector3(0,y+1.2f,-1.64f),new Vector3(4.2f,1.8f,.12f),dark,"Match plaque");
            for(int side=-1;side<=1;side+=2)
            {
                Shape(PrimitiveType.Cylinder,tower.transform,new Vector3(side*2.2f,y+1.3f,-.6f),new Vector3(.24f,1.2f,.24f),gold,"Pillar");
                Shape(PrimitiveType.Sphere,tower.transform,new Vector3(side*2.2f,y+2.4f,-.6f),Vector3.one*.33f,teal,"Beacon");
            }
            var label = new GameObject("Floor pairing"); label.layer = Layer; label.transform.SetParent(tower.transform,false);
            label.transform.localPosition = new Vector3(0,y+1.2f,-1.73f); label.transform.localRotation = Quaternion.Euler(0,180,0);
            // TextMesh faces toward negative Z without reversing the glyphs.
            label.transform.localRotation = Quaternion.identity;
            var text = label.AddComponent<TextMesh>(); text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
            text.characterSize = .07f; text.fontSize = 48; text.color = i == floor ? new Color(1,.83f,.43f) : new Color(.65f,.7f,.78f);
            text.text = i == 0 ? "I\nORREN\nvs\nTHE SLEEPLESS EYE" : i == 1 ? "II\nTHE SLEEPLESS EYE\nvs\n?" : (i+1)+"\n?\nvs\n?";
        }
        Shape(PrimitiveType.Cylinder,tower.transform,new Vector3(0,-.35f,0),new Vector3(7,.2f,5),dark,"Tower foundation");
        title.text = floor == 0 ? "THE ASCENT" : "VICTORY · THE ASCENT";
        subtitle.text = floor == 0 ? "Five floors. One ascent.\nFloor I · Orren vs The Sleepless Eye" : "Floor " + floor + " cleared · The next pairing is revealed";
        for(float t=0;t<1;t+=Time.unscaledDeltaTime/5f)
        {
            float s=t*t*(3-2*t);
            Look(Vector3.Lerp(new Vector3(8,11,-26),new Vector3(1.5f,3.8f,-14),s),Vector3.Lerp(new Vector3(0,6,0),new Vector3(0,1.8f,0),s));
            yield return null;
        }
        yield return new WaitForSecondsRealtime(1);
        tower.SetActive(false);
    }
    GameObject Die(Material body,Material pips,int index)
    {
        var root = new GameObject(index == 0 ? "Orren ivory die" : "Sleepless Eye obsidian die"); root.transform.SetParent(stage.transform,false);
        // Layered inset faces and rounded corner studs give the dice a jewelled, bevelled silhouette.
        Shape(PrimitiveType.Cube,root.transform,Vector3.zero,Vector3.one*.94f,body,"Die core");
        for(int x=-1;x<=1;x+=2) for(int y=-1;y<=1;y+=2) for(int z=-1;z<=1;z+=2)
            Shape(PrimitiveType.Sphere,root.transform,new Vector3(x,y,z)*.435f,Vector3.one*.14f,gold,"Gold corner");
        Vector3[] normals = { Vector3.back, Vector3.right, Vector3.up, Vector3.down, Vector3.left, Vector3.forward };
        for(int face=0;face<6;face++)
        {
            var pivot = new GameObject("Face " + (face+1)); pivot.transform.SetParent(root.transform,false);
            pivot.transform.localRotation = Quaternion.FromToRotation(Vector3.forward,normals[face]);
            Shape(PrimitiveType.Cube,pivot.transform,new Vector3(0,0,.475f),new Vector3(.84f,.84f,.04f),body,"Inset face");
            int count=face+1;
            var points = new List<Vector2>();
            if(count%2==1) points.Add(Vector2.zero);
            if(count>=2) { points.Add(new Vector2(-1,-1)); points.Add(new Vector2(1,1)); }
            if(count>=4) { points.Add(new Vector2(-1,1)); points.Add(new Vector2(1,-1)); }
            if(count==6) { points.Add(new Vector2(-1,0)); points.Add(new Vector2(1,0)); }
            foreach(var point in points) Shape(PrimitiveType.Sphere,pivot.transform,new Vector3(point.x*.24f,point.y*.24f,.507f),new Vector3(.115f,.115f,.026f),pips,"Inlaid pip");
        }
        dice.Add(root); return root;
    }
    public IEnumerator Roll(int human,int opponent)
    {
        overlay.SetActive(true); overlay.transform.SetAsLastSibling();
        // Leave the board visible around the floating 3D dice tray.
        overlay.GetComponent<Image>().color = new Color(.01f,.02f,.03f,.42f);
        BoardPresentation.Stretch(picture.rectTransform,new Vector2(.25f,.32f),new Vector2(.75f,.78f));
        foreach(var die in dice) if(die!=null) Destroy(die); dice.Clear();
        var a=Die(ivory,dark,0); var b=Die(GetComponent<TowerMatchController>().dieDarkMaterial ?? dark,gold,1);
        title.text="ROLL FOR INITIATIVE"; subtitle.text="ORREN                                       THE SLEEPLESS EYE";
        Look(new Vector3(0,0,-6),Vector3.zero);
        BoardPresentation.Stretch(title.rectTransform,new Vector2(.2f,.76f),new Vector2(.8f,.87f));
        BoardPresentation.Stretch(subtitle.rectTransform,new Vector2(.2f,.22f),new Vector2(.8f,.32f));
        int[] values={human,opponent}; var objects=new[]{a,b};
        Vector3[] normals={Vector3.back,Vector3.right,Vector3.up,Vector3.down,Vector3.left,Vector3.forward};
        for(float t=0;t<1;t+=Time.unscaledDeltaTime/2.4f)
        {
            float s=1-Mathf.Pow(1-t,3);
            for(int i=0;i<2;i++)
            {
                float side=i==0?-1:1;
                objects[i].transform.localPosition=new Vector3(side*Mathf.Lerp(3.5f,1.1f,s),Mathf.Abs(Mathf.Sin(t*Mathf.PI*4))*(1-t)*1.5f,0);
                var final=Quaternion.FromToRotation(normals[values[i]-1],Vector3.back);
                var spin=Quaternion.Euler((1-t)*1080,(1-t)*720*side,(1-t)*540);
                objects[i].transform.localRotation=Quaternion.Slerp(spin*final,final,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,t)));
            }
            yield return null;
        }
        for(int i=0;i<2;i++) { objects[i].transform.localRotation=Quaternion.FromToRotation(normals[values[i]-1],Vector3.back); objects[i].transform.localPosition=new Vector3(i==0?-1.1f:1.1f,0,0); }
        title.text=human==opponent?"TIE · ROLL AGAIN":(human>opponent?"ORREN":"THE SLEEPLESS EYE")+" STARTS";
        subtitle.text=$"ORREN  ·  {human}                         THE SLEEPLESS EYE  ·  {opponent}";
        yield return new WaitForSecondsRealtime(1.4f);
    }
    public void Hide() { if(overlay!=null) overlay.SetActive(false); }
    void OnDestroy()
    {
        if(stage!=null) Destroy(stage); if(overlay!=null) Destroy(overlay);
        if(target!=null) { target.Release(); Destroy(target); }
        foreach(var material in materials) if(material!=null) Destroy(material);
    }
}

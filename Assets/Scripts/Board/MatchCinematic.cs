using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

/// <summary>Isolated 3D stage composited above the board. All geometry and materials are runtime-owned.</summary>
public sealed partial class MatchCinematic : MonoBehaviour
{
    GameObject stage, overlay, tower;
    Camera camera3D;
    RawImage picture;
    Text title, subtitle;
    RenderTexture target;
    readonly List<Material> materials = new();
    readonly List<GameObject> dice = new();
    Mesh dieMesh;
    GameObject rollFrame;
    GameObject ascentMist;
    Text humanScore, enemyScore, rollOutcome;
    Material stone, gold, dark, ivory, teal;
    Material ivoryDice, slateDice, diceTrim, diceInk;
    Board board;
    const int Layer = 30;
    public bool IsShowing => overlay != null && overlay.activeInHierarchy;
    public void Initialize(Board board)
    {
        this.board = board;
        stage = new GameObject("Tournament cinematic stage"); stage.transform.position = new Vector3(0,-2000,0);
        var match = board.Match;
        stone = match.towerStoneMaterial != null ? match.towerStoneMaterial : Material(new Color(.09f,.12f,.17f), .65f, .45f);
        gold = match.towerTrimMaterial != null ? match.towerTrimMaterial : Material(new Color(.78f,.48f,.17f), .85f, .8f);
        dark = match.towerInkMaterial != null ? match.towerInkMaterial : Material(new Color(.018f,.025f,.04f), .4f, .65f);
        ivory = match.dieLightMaterial != null ? match.dieLightMaterial : Material(new Color(.94f,.84f,.61f), .35f, .75f);
        teal = match.towerAccentMaterial != null ? match.towerAccentMaterial : Material(new Color(.035f,.45f,.53f), .7f, .8f);
        // The landscape's building textures are much too coarse for a hand-sized die.
        ivoryDice = DiceMaterial(Color.Lerp(Pigment(match.dieLightMaterial, new Color(.66f,.48f,.25f)), new Color(.94f,.85f,.67f), .7f), 0, .46f);
        slateDice = DiceMaterial(Color.Lerp(Pigment(match.dieDarkMaterial, new Color(.32f,.4f,.35f)), new Color(.035f,.10f,.115f), .72f), .05f, .52f);
        diceTrim = DiceMaterial(new Color(.7f,.46f,.17f), .72f, .65f);
        diceInk = DiceMaterial(new Color(.015f,.035f,.04f), .1f, .48f);
        var cam = new GameObject("Cinematic camera"); cam.transform.SetParent(stage.transform,false); camera3D = cam.AddComponent<Camera>();
        camera3D.GetUniversalAdditionalCameraData().SetRenderer(board.Match.cinematicRendererIndex);
        camera3D.cullingMask = 1 << Layer; camera3D.clearFlags = CameraClearFlags.SolidColor;
        camera3D.backgroundColor = new Color(.012f,.018f,.033f,0); camera3D.fieldOfView = 35;
        target = new RenderTexture(1600,1000,24) { antiAliasing = 4 }; target.Create(); camera3D.targetTexture = target;
        Light("Warm key", new Vector3(-6,14,-8), new Color(1,.88f,.7f), 135, 40);
        Light("Moonlit rim", new Vector3(6,14,3), new Color(.65f,.69f,.79f), 170, 40);
        Light("Lantern fill", new Vector3(0,8,-12), new Color(1,.85f,.65f), 150, 40);
        var panel = BoardPresentation.Panel(board.transform,"Tournament overlay",new Color(.01f,.02f,.03f,.97f));
        overlay = panel.gameObject; panel.raycastTarget = true;
        BoardPresentation.Stretch(panel.rectTransform,Vector2.zero,Vector2.one);
        var imageGO = new GameObject("3D tower and dice",typeof(RectTransform),typeof(RawImage)); imageGO.transform.SetParent(overlay.transform,false);
        picture = imageGO.GetComponent<RawImage>(); picture.texture = target; picture.raycastTarget = false;
        ascentMist = new GameObject("Mist above the endless stair", typeof(RectTransform), typeof(CanvasRenderer), typeof(AscentMist));
        ascentMist.transform.SetParent(picture.transform, false);
        BoardPresentation.Stretch((RectTransform)ascentMist.transform, Vector2.zero, Vector2.one);
        ascentMist.GetComponent<AscentMist>().raycastTarget = false;
        BoardPresentation.Stretch(picture.rectTransform,new Vector2(.12f,.08f),new Vector2(.88f,.91f));
        var font = board.interfaceFont != null ? board.interfaceFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title = BoardPresentation.TextLabel(overlay.transform,"THE ASCENT",font,36,new Color(.94f,.8f,.5f),new Vector2(.1f,.88f),new Vector2(.9f,.99f),TextAnchor.MiddleCenter);
        if (BoardPresentation.SkinFor(board.transform).typography.mastheadFont != null)
            title.font = BoardPresentation.SkinFor(board.transform).typography.mastheadFont;
        subtitle = BoardPresentation.TextLabel(overlay.transform,"",font,20,new Color(.9f,.9f,.84f),new Vector2(.1f,.01f),new Vector2(.9f,.1f),TextAnchor.MiddleCenter);
    }
    Material Material(Color color,float metal,float smooth)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader); mat.color = color; mat.SetFloat("_Metallic",metal); mat.SetFloat("_Smoothness",smooth); materials.Add(mat); return mat;
    }
    static Color Pigment(Material source, Color fallback) => source != null && source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : fallback;
    Material DiceMaterial(Color color, float metallic, float polish)
    {
        var shader = board.Match.diceSurfaceShader != null ? board.Match.diceSurfaceShader : Shader.Find("DarkBeforeDawn/CarvedDice");
        if (shader == null) return Material(color,metallic,polish);
        var result = new Material(shader) { name = "Carved dice surface" };
        result.SetColor("_BaseColor",color); result.SetFloat("_Metallic",metallic); result.SetFloat("_Smoothness",polish);
        result.SetFloat("_GrainStrength",.035f); materials.Add(result); return result;
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
        board.preview?.Hide();
        if (rollFrame != null) { picture.transform.SetParent(overlay.transform, false); Destroy(rollFrame); }
        overlay.SetActive(true); overlay.transform.SetAsLastSibling(); picture.color = Color.white;
        camera3D.ResetAspect();
        overlay.GetComponent<Image>().color = Color.black;
        camera3D.orthographic = true;
        camera3D.backgroundColor = Color.black;
        target.filterMode = FilterMode.Point;
        ascentMist.SetActive(true);
        BoardPresentation.Stretch(picture.rectTransform,new Vector2(.12f,.08f),new Vector2(.88f,.91f));
        BoardPresentation.Stretch(title.rectTransform,new Vector2(.1f,.88f),new Vector2(.9f,.99f));
        BoardPresentation.Stretch(subtitle.rectTransform,new Vector2(.1f,.01f),new Vector2(.9f,.1f));
        foreach (var die in dice) if(die != null) Destroy(die); dice.Clear();
        if (tower != null) Destroy(tower);
        tower = new GameObject("The unending stair"); tower.transform.SetParent(stage.transform,false);
        int final = board.Match.ascentFinalLevel > 0 ? Mathf.Max(5,board.Match.ascentFinalLevel) : 0;
        floor = final > 0 ? Mathf.Clamp(floor,0,final-1) : Mathf.Max(0,floor);
        ascentMist.SetActive(final == 0 || floor < final-1);
        int first = Mathf.Max(0, floor - 2);
        int visible = final > 0 ? Mathf.Min(9,final-first) : 9;
        BuildEndlessStair(first, visible, floor, final);
        for (int i = 0; i < Mathf.Min(6,visible); i++) Pairing(first+i, i*StairRise, first+i == floor);
        title.text = floor == 0 ? "THE ASCENT" : "VICTORY · THE ASCENT";
        subtitle.text = floor == 0 ? "Beyond the mist, the stair goes on.\nLanding I · Orren vs The Sleepless Eye"
            : final > 0 && floor == final-1 ? "The mist parts. The final landing awaits."
            : "Landing " + Roman(floor) + " cleared · The ascent continues";
        // The climb can be cut short: a click or any key jumps the camera to the current floor and moves on.
        bool skipped = false;
        for(float t=0;t<1;t+=Time.unscaledDeltaTime/5f)
        {
            if (SkipRequested()) { skipped = true; t = 1; }
            float s=t*t*(3-2*t);
            float focus = (floor-first)*StairRise+2.0f;
            camera3D.orthographicSize = Mathf.Lerp(10.7f,3.15f,s);
            float cameraY = Mathf.Lerp(9.4f,focus,s);
            Look(new Vector3(0,cameraY,-20),new Vector3(0,cameraY,0));
            yield return null;
        }
        for (float wait = 0; wait < 1 && !skipped; wait += Time.unscaledDeltaTime) { if (SkipRequested()) break; yield return null; }
        tower.SetActive(false);
    }
    void Pairing(int floor, float y, bool current)
    {
        var go = new GameObject("Floor pairing " + (floor + 1), typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(tower.transform, false);
        var rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(420,180);
        rect.localPosition = new Vector3(.3f,y+2.15f,-.05f);
        rect.localScale = Vector3.one * .009f;
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera3D;
        var accent = current ? new Color(1,.83f,.43f) : new Color(.68f,.61f,.46f);
        PairingText(rect, Roman(floor+1), new Vector2(0,73), new Vector2(55,24), 15, accent);
        string left = floor == 0 ? board.humanAvatarCardName : floor == 1 ? board.opponentAvatarCardName : null;
        string right = floor == 0 ? board.opponentAvatarCardName : null;
        if (left != null && right != null) PairingText(rect, "VS", new Vector2(0,5), new Vector2(55,30), 19, accent);
        LeaderPortrait(rect, -112, left, accent);
        LeaderPortrait(rect, 112, right, accent);
        foreach (var child in go.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = Layer;
    }
    Text PairingText(Transform parent, string value, Vector2 position, Vector2 size, int fontSize, Color color)
    {
        var font = board.interfaceFont != null ? board.interfaceFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var text = BoardPresentation.TextLabel(parent,value,font,fontSize,color,Vector2.one*.5f,Vector2.one*.5f,TextAnchor.MiddleCenter);
        text.rectTransform.sizeDelta = size; text.rectTransform.anchoredPosition = position;
        text.resizeTextForBestFit = true; text.resizeTextMinSize = 10; text.resizeTextMaxSize = fontSize;
        return text;
    }
    void LeaderPortrait(Transform parent, float x, string leaderName, Color accent)
    {
        if (string.IsNullOrWhiteSpace(leaderName)) return;
        var frame = BoardPresentation.Panel(parent, string.IsNullOrWhiteSpace(leaderName) ? "Unknown leader" : "Leader " + leaderName, new Color(.09f,.065f,.038f));
        frame.rectTransform.anchorMin = frame.rectTransform.anchorMax = Vector2.one * .5f;
        // Square, like the card art it holds: the whole portrait shows, nothing is cropped away.
        float side = x > 0 ? 92 : 128;
        frame.rectTransform.sizeDelta = Vector2.one * side;
        frame.rectTransform.anchoredPosition = new Vector2(x,x > 0 ? 30 : 12);
        BoardPresentation.Border(frame.rectTransform,accent,2);
        var data = string.IsNullOrWhiteSpace(leaderName) ? null : CardCatalog.FindCardByName(leaderName);
        Sprite sprite = null;
        if (data != null && CardServices.Art != null)
            foreach (var candidate in new[] { data.spriteName, data.portraitName, data.name, data.actionClassName, data.action })
                if (!string.IsNullOrWhiteSpace(candidate) && CardServices.Art.TryGetSprite(candidate,true,out sprite)) break;
        if (sprite != null)
        {
            var portrait = BoardPresentation.Panel(frame.transform,"Leader portrait",Color.white);
            BoardPresentation.Stretch(portrait.rectTransform,Vector2.zero,Vector2.one);
            portrait.rectTransform.offsetMin = Vector2.one * 4; portrait.rectTransform.offsetMax = Vector2.one * -4;
            // Plain and still. The board's art zoom and card aura were tried here: the zoom cropped
            // the portraits and the aura's sparks, point-sampled at the tower's scale, read as stray
            // pixels crawling around the frame rather than as enchantment.
            portrait.sprite = sprite; portrait.preserveAspect = true;
        }
        else PairingText(frame.transform, string.IsNullOrWhiteSpace(leaderName) ? "?" : "NO ART",Vector2.zero,new Vector2(135,100),52,accent);
        PairingText(parent,string.IsNullOrWhiteSpace(leaderName) ? "UNREVEALED" : leaderName.ToUpperInvariant(),new Vector2(x,-68),new Vector2(195,30),14,accent);
    }
    GameObject Die(Material body,Material pips,int index)
    {
        var root = new GameObject(index == 0 ? "Orren ivory die" : "Sleepless Eye obsidian die"); root.transform.SetParent(stage.transform,false);
        if (dieMesh == null) dieMesh = CarvedDieMesh.Build();
        var solid = new GameObject("Rounded stone", typeof(MeshFilter), typeof(MeshRenderer));
        solid.transform.SetParent(root.transform,false); solid.layer = Layer;
        solid.GetComponent<MeshFilter>().sharedMesh = dieMesh;
        solid.GetComponent<MeshRenderer>().sharedMaterial = body;
        Vector3[] normals = { Vector3.back, Vector3.right, Vector3.up, Vector3.down, Vector3.left, Vector3.forward };
        for(int face=0;face<6;face++)
        {
            var pivot = new GameObject("Face " + (face+1)); pivot.transform.SetParent(root.transform,false);
            pivot.transform.localRotation = Quaternion.FromToRotation(Vector3.forward,normals[face]);
            // Hairline metal inlays sit flush with each softly rounded face.
            foreach (float side in new[] { -.35f, .35f })
            {
                Shape(PrimitiveType.Cube,pivot.transform,new Vector3(side,0,.499f),new Vector3(.008f,.69f,.002f),diceTrim,"Face inlay");
                Shape(PrimitiveType.Cube,pivot.transform,new Vector3(0,side,.499f),new Vector3(.69f,.008f,.002f),diceTrim,"Face inlay");
            }
            int count=face+1;
            var points = new List<Vector2>();
            if(count%2==1) points.Add(Vector2.zero);
            if(count>=2) { points.Add(new Vector2(-1,-1)); points.Add(new Vector2(1,1)); }
            if(count>=4) { points.Add(new Vector2(-1,1)); points.Add(new Vector2(1,-1)); }
            if(count==6) { points.Add(new Vector2(-1,0)); points.Add(new Vector2(1,0)); }
            foreach(var point in points) Shape(PrimitiveType.Sphere,pivot.transform,new Vector3(point.x*.22f,point.y*.22f,.50f),new Vector3(.125f,.125f,.014f),pips,"Inlaid pip");
        }
        dice.Add(root); return root;
    }
    public IEnumerator Roll(int human,int opponent)
    {
        camera3D.orthographic = false;
        camera3D.backgroundColor = new Color(.012f,.018f,.033f,0);
        target.filterMode = FilterMode.Bilinear;
        if (ascentMist != null) ascentMist.SetActive(false);
        if (tower != null) tower.SetActive(false);
        overlay.SetActive(true); overlay.transform.SetAsLastSibling();
        overlay.GetComponent<Image>().color = new Color(.01f,.02f,.03f,.22f);
        BuildRollFrame();
        foreach(var die in dice) if(die!=null) Destroy(die); dice.Clear();
        var a=Die(ivoryDice,diceInk,0); var b=Die(slateDice,diceTrim,1);
        title.text="THE FIRST MOVE"; subtitle.text="ROLLING FOR INITIATIVE";
        Look(new Vector3(0,0,-7),Vector3.zero);
        Canvas.ForceUpdateCanvases();
        // Render to the displayed viewport's aspect; a wide UI tray must not squash cubic dice.
        camera3D.aspect = picture.rectTransform.rect.width / Mathf.Max(1,picture.rectTransform.rect.height);
        BoardPresentation.Stretch(title.rectTransform,new Vector2(.2f,.71f),new Vector2(.8f,.8f));
        BoardPresentation.Stretch(subtitle.rectTransform,new Vector2(.2f,.65f),new Vector2(.8f,.71f));
        int[] values={human,opponent}; var objects=new[]{a,b};
        var positions = new Vector3[2];
        var sizes = new float[2];
        Vector3[] normals={Vector3.back,Vector3.right,Vector3.up,Vector3.down,Vector3.left,Vector3.forward};
        for(float t=0;t<1;t+=Time.unscaledDeltaTime/2.4f)
        {
            float s=1-Mathf.Pow(1-t,3);
            for(int i=0;i<2;i++)
            {
                float spread = camera3D.aspect * 1.04f;
                positions[i] = new Vector3(i==0 ? -spread : spread, .15f, 0); sizes[i] = 1.4f;
                float side=i==0?-1:1;
                objects[i].transform.localScale=Vector3.one*sizes[i];
                objects[i].transform.localPosition=positions[i]+new Vector3(side*(1-s)*.2f,Mathf.Abs(Mathf.Sin(t*Mathf.PI*4))*(1-t)*.25f,0)*sizes[i];
                var final=Quaternion.Euler(12,i==0 ? -14 : 14,i==0 ? -6 : 6)*Quaternion.FromToRotation(normals[values[i]-1],Vector3.back);
                var spin=Quaternion.Euler((1-t)*1080,(1-t)*720*side,(1-t)*540);
                objects[i].transform.localRotation=Quaternion.Slerp(spin*final,final,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,t)));
            }
            yield return null;
        }
        for(int i=0;i<2;i++) { objects[i].transform.localRotation=Quaternion.Euler(12,i==0 ? -14 : 14,i==0 ? -6 : 6)*Quaternion.FromToRotation(normals[values[i]-1],Vector3.back); objects[i].transform.localPosition=positions[i]; }
        humanScore.text = human.ToString(); enemyScore.text = opponent.ToString();
        humanScore.color = human >= opponent ? BoardPresentation.SkinFor(board.transform).colors.teal : new Color(.6f,.65f,.65f);
        enemyScore.color = opponent >= human ? BoardPresentation.SkinFor(board.transform).colors.gold : new Color(.6f,.65f,.65f);
        bool playerStarts = !board.Match.diceDecideFirstPlayer || human > opponent;
        rollOutcome.text = human == opponent ? "A STANDOFF  ·  ROLL AGAIN" : (playerStarts ? "YOUR COMPANY TAKES THE INITIATIVE" : "THE ENEMY TAKES THE INITIATIVE");
        subtitle.text = "THE DICE HAVE SPOKEN";
        yield return new WaitForSecondsRealtime(2f);
    }
    void BuildRollFrame()
    {
        picture.transform.SetParent(overlay.transform,false);
        if (rollFrame != null) Destroy(rollFrame);
        var skin = BoardPresentation.SkinFor(board.transform);
        var frame = BoardPresentation.Panel(overlay.transform,"Initiative table",skin.colors.ink);
        BoardPresentation.Stretch(frame.rectTransform,new Vector2(.2f,.2f),new Vector2(.8f,.8f));
        BoardSurface.Dress(frame,skin.colors.gold,true,true);
        rollFrame = frame.gameObject;
        picture.transform.SetParent(frame.transform,false);
        BoardPresentation.Stretch(picture.rectTransform,new Vector2(.02f,.27f),new Vector2(.98f,.74f));
        title.transform.SetAsLastSibling(); subtitle.transform.SetAsLastSibling();
        Text Text(string name, int size, Color tint, Vector2 min, Vector2 max)
        {
            var label = BoardPresentation.TextLabel(frame.transform,name,board.interfaceFont,size,tint,min,max,TextAnchor.MiddleCenter);
            label.resizeTextForBestFit=true; label.resizeTextMinSize=Mathf.Max(10,size-6); label.resizeTextMaxSize=size;
            return label;
        }
        Text("YOUR COMPANY",13,skin.colors.teal,new Vector2(.04f,.74f),new Vector2(.44f,.8f));
        Text("ENEMY COMPANY",13,skin.colors.gold,new Vector2(.56f,.74f),new Vector2(.96f,.8f));
        Text(board.humanAvatarCardName,16,skin.colors.ivory,new Vector2(.04f,.67f),new Vector2(.44f,.74f));
        Text(board.opponentAvatarCardName,16,skin.colors.ivory,new Vector2(.56f,.67f),new Vector2(.96f,.74f));
        humanScore=Text("\u2014",44,skin.colors.teal,new Vector2(.14f,.15f),new Vector2(.40f,.29f));
        enemyScore=Text("\u2014",44,skin.colors.gold,new Vector2(.60f,.15f),new Vector2(.86f,.29f));
        Text("VS",18,skin.colors.muted,new Vector2(.45f,.39f),new Vector2(.55f,.55f));
        rollOutcome=Text("A contest of fate",18,skin.colors.ivory,new Vector2(.05f,.025f),new Vector2(.95f,.12f));
        for(int owner=0;owner<2;owner++)
        {
            float x=owner==0 ? .025f : .865f;
            var portrait=BoardPresentation.Panel(frame.transform,"Champion seal",Color.white);
            BoardPresentation.Stretch(portrait.rectTransform,new Vector2(x,.4f),new Vector2(x+.11f,.63f));
            portrait.sprite=TravelBanner.Artwork(CardCatalog.FindCardByName(owner==0 ? board.humanAvatarCardName : board.opponentAvatarCardName));
            portrait.preserveAspect=true;
        }
    }
    /// <summary>A click or any key: the player has seen enough of the tower.</summary>
    static bool SkipRequested()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current; var mouse = UnityEngine.InputSystem.Mouse.current;
        return keyboard != null && keyboard.anyKey.wasPressedThisFrame || mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame);
    }
    public void Hide() { if(overlay!=null) overlay.SetActive(false); }
    void OnDestroy()
    {
        if(stage!=null) Destroy(stage); if(overlay!=null) Destroy(overlay);
        if(target!=null) { target.Release(); Destroy(target); }
        foreach(var material in materials) if(material!=null) Destroy(material);
        if(dieMesh!=null) Destroy(dieMesh);
        foreach (var mesh in towerMeshes) if (mesh != null) Destroy(mesh);
    }
}

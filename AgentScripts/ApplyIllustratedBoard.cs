// Run with Unity CLI eval_file in Edit mode. The asset remains editable in the Inspector.
if (UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Apply in Edit mode.");
var skin = BoardSkin.Default;
UnityEditor.Undo.RecordObject(skin, "Illustrated twilight board");
skin.displayName = "Twilight Realm";
skin.backdrop = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>("Assets/Art/Board/TwilightRealm.png");
skin.colors.ink = new UnityEngine.Color(.022f,.038f,.036f,.98f);
skin.colors.gold = new UnityEngine.Color(.79f,.64f,.39f);
skin.colors.teal = new UnityEngine.Color(.64f,.73f,.57f);
skin.colors.ivory = new UnityEngine.Color(.95f,.90f,.77f);
skin.colors.muted = new UnityEngine.Color(.64f,.68f,.61f);
skin.colors.zoneSurface = new UnityEngine.Color(.025f,.05f,.046f,.48f);
skin.colors.atmosphere = new UnityEngine.Color(.018f,.029f,.026f,.10f);
skin.colors.button = new UnityEngine.Color(.08f,.14f,.115f,.96f);
skin.colors.disabledButton = new UnityEngine.Color(.12f,.14f,.12f);
skin.tokens.borderWidth = 2;
skin.tokens.artInset = 4;
skin.chrome.headingHeight = 28;
skin.chrome.headingInset = new UnityEngine.Vector2(22,4);
skin.chrome.headerLabels[0].size = 29;
skin.typography.zoneHeadingSize = 17;
skin.chrome.handGap = 26;
foreach (var zone in skin.zones)
{
    zone.title = zone.zone switch {
        BoardZoneId.OpponentArmies => "ENEMY HOST",
        BoardZoneId.HumanArmies => "YOUR REALM",
        BoardZoneId.Environment => "THE WORLD",
        BoardZoneId.OpponentLands => "ENEMY LANDS",
        BoardZoneId.HumanLands => "YOUR LANDS",
        _ => zone.title
    };
}
var importer = UnityEditor.AssetImporter.GetAtPath("Assets/Art/Board/TwilightRealm.png") as UnityEditor.TextureImporter;
if (importer != null) {
    importer.textureType = UnityEditor.TextureImporterType.Default;
    importer.maxTextureSize = 2048;
    importer.mipmapEnabled = false;
    importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
    importer.SaveAndReimport();
}
UnityEditor.EditorUtility.SetDirty(skin);
UnityEditor.AssetDatabase.SaveAssets();
var presentation = UnityEngine.Object.FindFirstObjectByType<BoardPresentation>();
if (presentation != null) { presentation.enabled = false; presentation.enabled = true; }
return "Twilight Realm skin saved.";

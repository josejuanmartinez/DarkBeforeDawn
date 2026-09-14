UnityEditor.AssetDatabase.Refresh();
var importer=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath("Assets/Resources/Art/EndlessStair.png");
importer.textureType=UnityEditor.TextureImporterType.Default;
importer.filterMode=UnityEngine.FilterMode.Point;
importer.mipmapEnabled=false;
importer.textureCompression=UnityEditor.TextureImporterCompression.Uncompressed;
importer.maxTextureSize=2048;
importer.npotScale=UnityEditor.TextureImporterNPOTScale.None;
importer.SaveAndReimport();
var materialPath="Assets/Resources/Art/EndlessStairMaterial.mat";
var material=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(materialPath);
if(material==null)
{
    material=new UnityEngine.Material(UnityEngine.Shader.Find("Universal Render Pipeline/Unlit"));
    UnityEditor.AssetDatabase.CreateAsset(material,materialPath);
}
material.SetTexture("_BaseMap",UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>("Assets/Resources/Art/EndlessStair.png"));
material.SetColor("_BaseColor",UnityEngine.Color.white);
UnityEditor.EditorUtility.SetDirty(material);UnityEditor.AssetDatabase.SaveAssets();
return "Imported retro stair artwork with point sampling and no compression.";

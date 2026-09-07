using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(SkinManager))]
public sealed class SkinManagerEditor : Editor
{
    private Editor skinEditor;
    private bool showParameters = true;
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("skins"), true);
        var manager = (SkinManager)target;
        var active = serializedObject.FindProperty("activeSkin");
        var library = serializedObject.FindProperty("skins");
        var labels = new string[library.arraySize];
        int selected = -1;
        for (int i = 0; i < labels.Length; i++)
        {
            var skin = library.GetArrayElementAtIndex(i).objectReferenceValue as BoardSkin;
            labels[i] = skin != null ? skin.displayName + " (" + skin.name + ")" : "Missing skin";
            if (skin != null && skin == manager.ActiveSkin) selected = i;
        }
        if (labels.Length > 0)
        {
            EditorGUI.BeginChangeCheck();
            int index = EditorGUILayout.Popup("Active Skin", selected, labels);
            if (EditorGUI.EndChangeCheck() && index >= 0)
                active.objectReferenceValue = library.GetArrayElementAtIndex(index).objectReferenceValue;
        }
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.HelpBox("Duplicate a skin to create a new look. Parameters belong to the asset. Apply Active Skin rearranges the board in both Edit and Play mode.", MessageType.Info);
        using (new EditorGUI.DisabledScope(manager.ActiveSkin == null))
        {
            if (GUILayout.Button("Duplicate Active Skin")) Duplicate(manager);
            if (GUILayout.Button("Apply Active Skin"))
            {
                manager.ApplyActiveSkin();
                ApplySkinToAuthoredCards(manager);
                EditorUtility.SetDirty(manager);
                if (manager.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                SceneView.RepaintAll();
            }
        }
        if (manager.ActiveSkin == null) return;
        showParameters = EditorGUILayout.InspectorTitlebar(showParameters, manager.ActiveSkin);
        if (showParameters)
        {
            CreateCachedEditor(manager.ActiveSkin, null, ref skinEditor);
            skinEditor.OnInspectorGUI();
        }
    }
    private static void ApplySkinToAuthoredCards(SkinManager manager)
    {
        var board = manager.GetComponent<Board>();
        if (board == null) return;
        foreach (var zone in board.GetComponentsInChildren<CardZoneVisualizer>(true))
        {
            CardZoneVisualizerEditor.PreviewAuthoredChildren(zone, applySkin: true);
        }
    }
    private void Duplicate(SkinManager manager)
    {
        string source = AssetDatabase.GetAssetPath(manager.ActiveSkin);
        string directory = string.IsNullOrEmpty(source) ? "Assets" : System.IO.Path.GetDirectoryName(source).Replace('\\','/');
        string path = AssetDatabase.GenerateUniqueAssetPath(directory + "/" + manager.ActiveSkin.name + " Copy.asset");
        var skin = Instantiate(manager.ActiveSkin);
        skin.name = System.IO.Path.GetFileNameWithoutExtension(path);
        skin.displayName = skin.name;
        AssetDatabase.CreateAsset(skin, path);
        Undo.RecordObject(manager, "Add board skin");
        manager.AddSkin(skin); manager.SetSkin(skin);
        EditorUtility.SetDirty(manager);
        AssetDatabase.SaveAssets();
    }
    private void OnDisable() { if (skinEditor != null) DestroyImmediate(skinEditor); }
}

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Ported from Runeboard. The Apply button renders the named card straight onto the selected prefab
// instance so you can lay cards out without entering play mode.
//
// One change: Runeboard needed a separate CardEditorArtworkBaker to resolve sprites here, because
// its Illustrations source only worked at runtime through Addressables. CardArtLibrary is a plain
// asset, so Apply resolves art the same way in edit mode as at runtime — it just has to install the
// library first, which CardServicesInstaller would otherwise do on Awake.
[CustomEditor(typeof(CardDataProvider))]
[CanEditMultipleObjects]
public sealed class CardDataProviderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("cardName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("startAsToken"));
        DrawDeckSelector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("initializeOnStart"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Presentation", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("suppressHoverEffects"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("useCardArtFolderOnly"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("showRequirementWarnings"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("showCloseIcon"));

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Apply", GUILayout.Height(30f))) ApplyPreview();
        }

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Use Apply() or Initialize(string, bool) from runtime code.", MessageType.Info);
        }
        else
        {
            if (CardArtLibraryBuilder.EnsureArtServiceInstalled() == null)
            {
                EditorGUILayout.HelpBox(
                    "No CardArtLibrary found — cards will render without art. Build one via Tools > Cards > Rebuild Card Art Library.",
                    MessageType.Warning);
            }

            if (Resources.Load<TextAsset>("Cards") == null)
            {
                EditorGUILayout.HelpBox(
                    "No Resources/Cards.json manifest found, so card names cannot be resolved. Either add one, or drive the Card component directly with a CardData you build in code.",
                    MessageType.Warning);
            }

            EditorGUILayout.HelpBox("Apply resolves the named card and refreshes the Card preview.", MessageType.None);
        }
    }

    private void DrawDeckSelector()
    {
        SerializedProperty deckProperty = serializedObject.FindProperty("deckId");
        List<string> ids = new() { string.Empty };
        List<string> labels = new() { "Card's own deck" };

        CardsManifest manifest = CardCatalog.GetManifest();
        if (manifest?.decks != null)
        {
            foreach (DeckManifestEntry deck in manifest.decks)
            {
                if (deck == null || string.IsNullOrWhiteSpace(deck.deckId)) continue;
                ids.Add(deck.deckId);
                labels.Add(string.IsNullOrWhiteSpace(deck.nation)
                    ? deck.deckId
                    : $"{deck.deckId} ({deck.nation})");
            }
        }

        int selectedIndex = System.Math.Max(0, ids.FindIndex(id =>
            string.Equals(id, deckProperty.stringValue, System.StringComparison.OrdinalIgnoreCase)));

        EditorGUI.showMixedValue = deckProperty.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        int nextIndex = EditorGUILayout.Popup(new GUIContent("Deck Icon"), selectedIndex, labels.ToArray());
        if (EditorGUI.EndChangeCheck()) deckProperty.stringValue = ids[nextIndex];
        EditorGUI.showMixedValue = false;
    }

    private void ApplyPreview()
    {
        serializedObject.ApplyModifiedProperties();
        CardArtLibraryBuilder.EnsureArtServiceInstalled();

        foreach (Object selected in targets)
        {
            CardDataProvider provider = selected as CardDataProvider;
            if (provider == null) continue;

            Component[] hierarchy = provider.GetComponentsInChildren<Component>(true);
            Undo.RecordObjects(hierarchy, "Apply Card Data");

            if (!provider.Apply()) continue;

            foreach (Component component in hierarchy)
            {
                if (component == null) continue;
                EditorUtility.SetDirty(component);
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }

            EditorUtility.SetDirty(provider.gameObject);
        }

        serializedObject.Update();
    }
}

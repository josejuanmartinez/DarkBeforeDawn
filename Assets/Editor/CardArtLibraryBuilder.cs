using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Builds the CardArtLibrary asset the card face looks art up in.
//
// Runeboard did this at runtime through Addressables: every sprite under Assets/Art/Cards carried the
// "default" label, and Illustrations streamed them all in on startup. This scans the same folders at
// edit time instead and writes direct sprite references into one asset, so there is no Addressables
// package to add, no async wait, and no window during startup where art lookups return null.
//
// Run it after adding, renaming or removing card art.
public static class CardArtLibraryBuilder
{
    private const string LibraryAssetPath = "Assets/Art/CardArtLibrary.asset";

    // Anything under CardArtRoot is flagged isCardArt, which is what lets the center preview refuse
    // a same-named sprite from a general UI folder (Card.UseCardArtFolderOnly).
    private const string CardArtRoot = "Assets/Art/Cards";
    private static readonly string[] AdditionalRoots = { "Assets/Art/UI" };

    [MenuItem("Tools/Cards/Rebuild Card Art Library")]
    public static void Rebuild()
    {
        List<string> searchRoots = new() { CardArtRoot };
        foreach (string root in AdditionalRoots)
        {
            if (AssetDatabase.IsValidFolder(root)) searchRoots.Add(root);
        }

        if (!AssetDatabase.IsValidFolder(CardArtRoot))
        {
            EditorUtility.DisplayDialog("Rebuild Card Art Library",
                $"No folder at {CardArtRoot}. Card art is expected there.", "OK");
            return;
        }

        List<CardArtLibrary.Entry> entries = new();
        HashSet<Sprite> seen = new();
        string[] guids = AssetDatabase.FindAssets("t:Sprite", searchRoots.ToArray());

        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (EditorUtility.DisplayCancelableProgressBar(
                        "Rebuilding Card Art Library",
                        path,
                        guids.Length == 0 ? 1f : (float)i / guids.Length))
                {
                    return;
                }

                bool isCardArt = path.StartsWith(CardArtRoot + "/", System.StringComparison.Ordinal);

                // A texture sliced into several sprites yields more than one sub-asset, so load them
                // all rather than only the main one.
                foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (sub is not Sprite sprite || !seen.Add(sprite)) continue;
                    entries.Add(new CardArtLibrary.Entry { sprite = sprite, isCardArt = isCardArt });
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        CardArtLibrary library = AssetDatabase.LoadAssetAtPath<CardArtLibrary>(LibraryAssetPath);
        bool created = library == null;
        if (created)
        {
            library = ScriptableObject.CreateInstance<CardArtLibrary>();
            library.SetEntries(entries);
            AssetDatabase.CreateAsset(library, LibraryAssetPath);
        }
        else
        {
            library.SetEntries(entries);
            EditorUtility.SetDirty(library);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"CardArtLibrary {(created ? "created" : "updated")} at {LibraryAssetPath} with {entries.Count} sprites.", library);
        Selection.activeObject = library;
        EditorGUIUtility.PingObject(library);
    }

    // Edit-time lookups (the CardDataProvider inspector's Apply button) need an art source installed,
    // and CardServicesInstaller only runs in play mode. This finds the built library and installs it.
    public static CardArtLibrary EnsureArtServiceInstalled()
    {
        if (CardServices.Art is CardArtLibrary existing && existing != null) return existing;

        CardArtLibrary library = AssetDatabase.LoadAssetAtPath<CardArtLibrary>(LibraryAssetPath);
        if (library == null)
        {
            string[] found = AssetDatabase.FindAssets("t:CardArtLibrary");
            if (found.Length > 0)
            {
                library = AssetDatabase.LoadAssetAtPath<CardArtLibrary>(AssetDatabase.GUIDToAssetPath(found[0]));
            }
        }

        if (library != null) CardServices.Art = library;
        return library;
    }
}

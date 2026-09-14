// Run through Unity CLI eval_file in Edit mode. Keeps the active skin fully editable.
if (UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Apply the skin in Edit mode.");
var skin = BoardSkin.Default;
UnityEditor.Undo.RecordObject(skin, "Retro fantasy board palette");
skin.colors.ink = new UnityEngine.Color(.075f,.048f,.029f,.97f);
skin.colors.gold = new UnityEngine.Color(.83f,.63f,.35f);
skin.colors.teal = new UnityEngine.Color(.65f,.72f,.43f); // The existing player accent slot: sage heraldry.
skin.colors.ivory = new UnityEngine.Color(.95f,.88f,.72f);
skin.colors.muted = new UnityEngine.Color(.72f,.65f,.52f);
skin.colors.zoneSurface = new UnityEngine.Color(.13f,.085f,.045f,.86f);
skin.colors.atmosphere = new UnityEngine.Color(.04f,.029f,.018f,.16f);
skin.colors.button = new UnityEngine.Color(.25f,.17f,.075f);
skin.colors.disabledButton = new UnityEngine.Color(.22f,.19f,.14f);
skin.chrome.headerLabels[1].text = "THE GOLDEN VALE";
skin.typography.zoneHeadingSize = 15;
UnityEditor.EditorUtility.SetDirty(skin);
UnityEditor.AssetDatabase.SaveAssets();
foreach (var manager in UnityEngine.Object.FindObjectsByType<SkinManager>(UnityEngine.FindObjectsSortMode.None)) manager.ApplyActiveSkin();
return "Saved leather, brass, parchment and sage board palette.";

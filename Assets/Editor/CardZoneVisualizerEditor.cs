using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Edit-time half of the zone authoring flow.
//
// A card prefab instance dropped onto an anchor lands at the offset baked into the prefab, which is
// nowhere near the anchor strip, so without this the cards are invisible until Play mode and the
// authoring flow reads as broken. This snaps them into the same slots Arrange would give them,
// through CardZoneVisualizer.LayoutSlots, so the Scene view shows what Play mode will build.
//
// Nothing here runs in Play mode and nothing here is needed for the zone to work: it is preview
// only. AdoptAuthoredChildren still does the real conversion at Start.
[CustomEditor(typeof(CardZoneVisualizer), true)]
public sealed class CardZoneVisualizerEditor : Editor
{
    [InitializeOnLoadMethod]
    private static void Hook()
    {
        // Fires when a prefab is dropped into the hierarchy, reparented or deleted — the moments an
        // authored zone's contents change.
        EditorApplication.hierarchyChanged += ArrangeAllAuthoredZones;
    }

    private static void ArrangeAllAuthoredZones()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        foreach (var zone in Object.FindObjectsByType<CardZoneVisualizer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            PreviewAuthoredChildren(zone);
        }
    }

    /// <summary>Renders and lays out the Card instances parented to this zone, without consuming them.</summary>
    public static int PreviewAuthoredChildren(CardZoneVisualizer zone)
    {
        bool asToken = !zone.UsesFullCards;
        var slots = new List<RectTransform>();
        var touched = new List<Object>();
        Vector2 natural = Vector2.zero;
        for (int i = 0; i < zone.transform.childCount; i++)
        {
            var child = zone.transform.GetChild(i);
            var card = child.GetComponent<Card>();
            if (card == null || child is not RectTransform rect) continue;

            // Card.prefab is stored deactivated, so a freshly dropped instance arrives switched off.
            if (!child.gameObject.activeSelf) child.gameObject.SetActive(true);

            // A zone takes one prefab; the wrong one has no subtree to show, so only slot it.
            bool accepted = card.IsTokenOnlyPresentation == asToken;
            var provider = accepted ? child.GetComponent<CardDataProvider>() : null;
            if (provider != null && !string.IsNullOrWhiteSpace(provider.cardName))
            {
                if (provider.startAsToken != asToken)
                {
                    provider.startAsToken = asToken;
                    touched.Add(provider);
                }
                provider.Apply();
            }

            // Slotted whether or not it resolved to a card: an unnamed instance left at the offset
            // baked into the prefab lands on top of a completely different anchor, which reads as
            // the card having gone to the wrong zone rather than as a card still missing its name.
            Vector2 footprint = accepted
                ? BoardCardView.PrepareForBoard(card, asToken)
                : rect.rect.size;

            slots.Add(rect);
            foreach (var descendant in child.GetComponentsInChildren<RectTransform>(true)) touched.Add(descendant);
            if (natural == Vector2.zero) natural = footprint;
        }
        if (slots.Count == 0) return 0;
        zone.LayoutSlots(slots, natural);

        // These are prefab instances, so a plain field assignment is not yet an override: without
        // registering it, the next domain reload reverts the object to the prefab's own value and
        // the layout silently springs back. SetDirty alone does not record it either.
        foreach (var obj in touched)
        {
            EditorUtility.SetDirty(obj);
            if (PrefabUtility.IsPartOfPrefabInstance(obj)) PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
        }
        return slots.Count;
    }

    /// <summary>Rebuilds this zone's card instances from their prefab, keeping only what was authored.</summary>
    /// <remarks>
    /// The preview writes prefab overrides so its layout survives a domain reload, which means a
    /// presentation bug reaches the scene as baked-in overrides and outlives the fix. Presenting is
    /// idempotent now, so this is a repair tool rather than something the preview needs to do: it
    /// throws away every override except the card's identity and lets the preview derive the rest.
    /// </remarks>
    public static int RevertCardsToPrefab(CardZoneVisualizer zone)
    {
        int reverted = 0;
        for (int i = 0; i < zone.transform.childCount; i++)
        {
            var child = zone.transform.GetChild(i);
            if (child.GetComponent<Card>() == null) continue;
            if (!PrefabUtility.IsPartOfPrefabInstance(child.gameObject)) continue;

            var provider = child.GetComponent<CardDataProvider>();
            string cardName = provider != null ? provider.cardName : null;
            string deckId = provider != null ? provider.deckId : null;
            string objectName = child.name;

            PrefabUtility.RevertPrefabInstance(child.gameObject, InteractionMode.AutomatedAction);

            child.name = objectName;
            provider = child.GetComponent<CardDataProvider>();
            if (provider != null)
            {
                provider.cardName = cardName;
                provider.deckId = deckId;
                EditorUtility.SetDirty(provider);
                PrefabUtility.RecordPrefabInstancePropertyModifications(provider);
            }
            reverted++;
        }
        if (reverted > 0) PreviewAuthoredChildren(zone);
        return reverted;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var zone = (CardZoneVisualizer)target;

        EditorGUILayout.Space();
        int authored = 0, unnamed = 0, wrongPrefab = 0;
        for (int i = 0; i < zone.transform.childCount; i++)
        {
            var child = zone.transform.GetChild(i);
            var card = child.GetComponent<Card>();
            if (card == null) continue;
            authored++;
            if (card.IsTokenOnlyPresentation == zone.UsesFullCards) { wrongPrefab++; continue; }
            var provider = child.GetComponent<CardDataProvider>();
            if (provider == null || string.IsNullOrWhiteSpace(provider.cardName)) unnamed++;
        }

        if (authored == 0)
        {
            EditorGUILayout.HelpBox(
                $"Drag Assets/Prefabs/{zone.AcceptedPrefabName} onto this anchor in the Hierarchy, once "
                + "per card, then set Card Name on each instance's Card Data Provider.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                $"{authored} authored card(s), laid out here as a preview; entering Play mode reads "
                + "their names, removes them and builds the real board visuals.", MessageType.None);
        }
        if (wrongPrefab > 0)
        {
            EditorGUILayout.HelpBox(
                $"{wrongPrefab} instance(s) are the wrong prefab for this zone, which takes "
                + $"{zone.AcceptedPrefabName}. They render blank and Play mode ignores them.",
                MessageType.Error);
        }
        if (unnamed > 0)
        {
            EditorGUILayout.HelpBox(
                $"{unnamed} of them still have no Card Name, so they render blank and Play mode "
                + "will skip them. Set Card Name on each, or delete the instance.", MessageType.Warning);
        }

        if (GUILayout.Button("Refresh Authored Cards")) PreviewAuthoredChildren(zone);
        if (GUILayout.Button("Revert Cards To Prefab"))
        {
            // Use when a card looks wrong rather than merely misplaced: it discards every override
            // on the instances except their card names and rebuilds the presentation from scratch.
            Debug.Log($"Reverted {RevertCardsToPrefab(zone)} card(s) under '{zone.name}' to prefab.", zone);
        }
    }
}

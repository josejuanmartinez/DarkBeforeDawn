using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ValleyDayNightBuilder
{
    [MenuItem("Tools/Background/Add Day and Night Cycle")]
    public static void Build()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Exit Play mode before installing the cycle.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/TCGBoard.unity")throw new InvalidOperationException("Open TCGBoard first.");
        var existing=UnityEngine.Object.FindAnyObjectByType<ValleyDayNightCycle>();
        if(existing!=null){Selection.activeGameObject=existing.gameObject;return;}
        var sunlight=RenderSettings.sun;
        if(sunlight==null)throw new InvalidOperationException("The valley sun is missing.");
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Add valley day and night");
        var root=new GameObject("Valley Day and Night");Undo.RegisterCreatedObjectUndo(root,"Create day/night controller");
        var moonObject=new GameObject("Moonlight",typeof(Light));moonObject.transform.SetParent(root.transform,false);
        var moon=moonObject.GetComponent<Light>();moon.type=LightType.Directional;moon.cullingMask=1<<8;moon.shadows=LightShadows.Soft;moon.shadowBias=.03f;moon.shadowNormalBias=.2f;
        var cycle=root.AddComponent<ValleyDayNightCycle>();cycle.sun=sunlight;cycle.moon=moon;cycle.cycleSeconds=360;cycle.ApplyTime(.62f);
        EditorUtility.SetDirty(cycle);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Undo.CollapseUndoOperations(group);Selection.activeGameObject=root;
    }
}

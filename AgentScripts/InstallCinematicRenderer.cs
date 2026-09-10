var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
if(pipeline==null) throw new System.Exception("Expected URP pipeline.");
const string path="Assets/Settings/TournamentRenderer.asset";
var renderer=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>(path);
if(renderer==null) {renderer=UnityEngine.ScriptableObject.CreateInstance<UnityEngine.Rendering.Universal.UniversalRendererData>(); UnityEditor.AssetDatabase.CreateAsset(renderer,path);}
var serialized=new UnityEditor.SerializedObject(pipeline); var list=serialized.FindProperty("m_RendererDataList");
int index=-1; for(int i=0;i<list.arraySize;i++) if(list.GetArrayElementAtIndex(i).objectReferenceValue==renderer) index=i;
if(index<0) {index=list.arraySize; list.InsertArrayElementAtIndex(index); list.GetArrayElementAtIndex(index).objectReferenceValue=renderer; serialized.ApplyModifiedProperties();}
var m=UnityEngine.Object.FindFirstObjectByType<TowerMatchController>(); UnityEditor.Undo.RecordObject(m,"Set cinematic renderer"); m.cinematicRendererIndex=index;
UnityEditor.EditorUtility.SetDirty(m); UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(m.gameObject.scene); UnityEditor.SceneManagement.EditorSceneManager.SaveScene(m.gameObject.scene);
return index;

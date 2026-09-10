// Run with unity command eval_file Tests/CardKeywordVisualChecks.cs
var root = new UnityEngine.GameObject("Keyword visual QA", typeof(UnityEngine.RectTransform), typeof(UnityEngine.Canvas));
var cameraObject = new UnityEngine.GameObject("Keyword QA camera", typeof(UnityEngine.Camera));
var camera = cameraObject.GetComponent<UnityEngine.Camera>();
var target = new UnityEngine.RenderTexture(1400, 840, 24);
var oldTarget = UnityEngine.RenderTexture.active;
var oldArt = CardServices.Art;
CardServices.Art=UnityEditor.AssetDatabase.LoadAssetAtPath<CardArtLibrary>("Assets/Art/CardArtLibrary.asset");
try {
 camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 420;
 camera.transform.position = new UnityEngine.Vector3(0,0,-10);
 camera.clearFlags = UnityEngine.CameraClearFlags.SolidColor; camera.backgroundColor = new UnityEngine.Color(.025f,.03f,.045f);
 camera.cullingMask = 1 << 31; camera.targetTexture = target;
 var canvas = root.GetComponent<UnityEngine.Canvas>(); canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
 var deck = UnityEngine.JsonUtility.FromJson<DeckData>(System.IO.File.ReadAllText("Assets/Resources/Cards/Meta/CharacterCards.json"));
 var data = deck.cards.Find(c => c.name == "Corran").Clone();
 data.commander=3; data.agent=2; data.emmissary=1; data.mage=4;
 data.statusEffects = new System.Collections.Generic.List<StatusEffects>{StatusEffects.Poisoned,StatusEffects.Frozen,StatusEffects.Encouraged};
 var list = new System.Collections.Generic.List<Card>();
 for (int i=0;i<3;i++) {
  string prefab = i==0 ? "Card" : i==1 ? "TokenCard" : "TokenCardMasked";
  var instance=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Prefabs/"+prefab+".prefab"),root.transform);
  instance.SetActive(true);
  var card=instance.GetComponent<Card>(); card.TypewriterEffect=false; card.UseCardArtFolderOnly=true; card.ShowCloseIcon=false; card.Initialize(data.Clone(), i!=0);
  var rect=(UnityEngine.RectTransform)instance.transform;
  if(i==0) {card.ShowRealCard(); BoardPresentation.StyleFullCard(card); rect.sizeDelta=new UnityEngine.Vector2(300,420); rect.localScale=UnityEngine.Vector3.one*1.5f;}
  else {card.ApplyCompactInfoMaterial(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Art/Fonts/LiberationSans SDF - Card Stats Own.mat")); card.CompactTokenInPlace(); card.ShowToken(); rect.localScale=UnityEngine.Vector3.one*1.5f;}
  rect.anchorMin=rect.anchorMax=rect.pivot=UnityEngine.Vector2.one*.5f;
  rect.anchoredPosition=new UnityEngine.Vector2(i==0?-405:i==1?5:350,i==0?0:140);
  list.Add(card);
 }
 var hover=list[0].GetComponent<CardKeywordHover>();
 var show=typeof(CardKeywordHover).GetMethod("Show",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
 foreach(var corner in new[]{UnityEngine.Vector2.zero,new UnityEngine.Vector2(UnityEngine.Screen.width,UnityEngine.Screen.height)}) {
  show.Invoke(hover,new object[]{"ability:Encouraging",corner,TMPro.TMP_Settings.defaultFontAsset});
  var panel=(UnityEngine.RectTransform)typeof(CardKeywordHover).GetField("panel",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(hover);
  if(panel.anchoredPosition.x<0 || panel.anchoredPosition.y<0 || panel.anchoredPosition.x+panel.sizeDelta.x>UnityEngine.Screen.width || panel.anchoredPosition.y+panel.sizeDelta.y>UnityEngine.Screen.height) throw new System.Exception("Popup off screen");
 }
 var popup=(UnityEngine.GameObject)typeof(CardKeywordHover).GetField("popupCanvas",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(hover);
 var popupPanel=(UnityEngine.RectTransform)popup.transform.GetChild(0); popupPanel.SetParent(root.transform,false);
 popupPanel.anchorMin=popupPanel.anchorMax=UnityEngine.Vector2.one*.5f; popupPanel.pivot=UnityEngine.Vector2.one*.5f;
 popupPanel.anchoredPosition=new UnityEngine.Vector2(170,-190); popupPanel.localScale=UnityEngine.Vector3.one*1.4f;
 foreach(UnityEngine.Transform t in root.GetComponentsInChildren<UnityEngine.Transform>(true))t.gameObject.layer=31;
 UnityEngine.Canvas.ForceUpdateCanvases();
 foreach(var text in root.GetComponentsInChildren<TMPro.TMP_Text>())text.ForceMeshUpdate();
 camera.Render(); UnityEngine.RenderTexture.active=target;
 var texture=new UnityEngine.Texture2D(1400,840,UnityEngine.TextureFormat.RGB24,false);
 texture.ReadPixels(new UnityEngine.Rect(0,0,1400,840),0,0); texture.Apply();
 System.IO.File.WriteAllBytes("Tests/CardKeywordVisual.png",texture.EncodeToPNG());
 UnityEngine.Object.DestroyImmediate(texture);
 UnityEngine.Object.DestroyImmediate(popup);
 return "Rendered full card, both tokens and actual tooltip. Popup clamping passed.";
} finally {CardServices.Art=oldArt; UnityEngine.RenderTexture.active=oldTarget; camera.targetTexture=null; UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(target);}

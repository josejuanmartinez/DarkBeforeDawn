if(UnityEngine.Application.isPlaying) throw new System.Exception("Run in Edit mode");
var b=UnityEngine.Object.FindFirstObjectByType<Board>();
int count=0;
foreach(var zone in b.GetComponentsInChildren<CardZoneVisualizer>()) {
 if(!zone.UsesFullCards) continue;
 CardZoneVisualizerEditor.PreviewAuthoredChildren(zone,true);
 CardZoneVisualizerEditor.PreviewAuthoredChildren(zone,false);
 foreach(UnityEngine.Transform child in zone.transform) {
  var card=child.GetComponent<Card>(); if(card==null) continue;
  var root=(UnityEngine.RectTransform)child;
  var face=child.Find("RealCard") as UnityEngine.RectTransform;
  var expected=BoardPresentation.SkinFor(child).cards.size;
  if(root.sizeDelta!=expected || face.sizeDelta!=expected || face.localScale!=UnityEngine.Vector3.one) throw new System.Exception("Full-card frame mismatch: "+child.name);
  count++;
 }
}
return count+" full cards retain matching face/frame sizes after repeated editor layout.";

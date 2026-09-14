// Run through Unity CLI eval_file while TCGBoard is in Play mode.
if (!UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Enter Play mode first.");
var b=UnityEngine.Object.FindFirstObjectByType<Board>();
int checkedSizes=0;
int checkedPreviews=0;
foreach(var z in b.GetComponentsInChildren<CardZoneVisualizer>())
{
 var rect=(UnityEngine.RectTransform)z.transform; var original=rect.sizeDelta;
 try { foreach(var size in new[]{new UnityEngine.Vector2(260,100),new UnityEngine.Vector2(100,260),new UnityEngine.Vector2(800,180)})
 {
  rect.SetSizeWithCurrentAnchors(UnityEngine.RectTransform.Axis.Horizontal,size.x);
  rect.SetSizeWithCurrentAnchors(UnityEngine.RectTransform.Axis.Vertical,size.y); z.Arrange();
  foreach(var view in z.GetComponentsInChildren<BoardCardView>())
  {
   var pose=view.Rect.localRotation; view.Rect.localRotation=UnityEngine.Quaternion.identity;
   var corners=new UnityEngine.Vector3[4]; view.Rect.GetWorldCorners(corners); view.Rect.localRotation=pose;
   foreach(var corner in corners) {var p=rect.InverseTransformPoint(corner); if(p.x<rect.rect.xMin-.1f||p.x>rect.rect.xMax+.1f||p.y<rect.rect.yMin-.1f||p.y>rect.rect.yMax+.1f) throw new System.Exception("Responsive overflow");}
  }
  checkedSizes++;
 }
 } finally { rect.sizeDelta=original;z.Arrange(); }
}
foreach(var deck in new[]{b.humanDiscard,b.humanVictoryPoints,b.opponentDiscard,b.opponentVictoryPoints})
{
 var top = deck.GetComponentInChildren<BoardCardView>();
 if (top == null) continue; // Empty match piles have no inspection face.
 b.preview.Show(top);
 var root=(UnityEngine.RectTransform)b.preview.transform;var corners=new UnityEngine.Vector3[4];b.preview.PreviewRect.GetWorldCorners(corners);
 foreach(var corner in corners) if(!root.rect.Contains(root.InverseTransformPoint(corner))) throw new System.Exception("Corner preview overflow");
 checkedPreviews++;
}
b.preview.Hide();
return checkedSizes+" responsive zone sizes and "+checkedPreviews+" populated edge previews passed.";

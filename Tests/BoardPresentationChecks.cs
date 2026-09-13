// Run through Unity CLI eval_file while TCGBoard is in Play mode.
if (!UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Enter Play mode first.");
var b=UnityEngine.Object.FindFirstObjectByType<Board>();
var all=System.Linq.Enumerable.ToList(CardCatalog.AllCards());
var sample=new System.Collections.Generic.List<CardData>();
foreach(var card in all) { var copy=card.Clone(); sample.Add(copy); if(sample.Count==100) break; }
int checks=0;
void Assert(bool condition, string message) { if(!condition) throw new System.Exception(message); checks++; }
void CheckFit(CardZoneVisualizer zone)
{
    zone.Arrange(); var area=(UnityEngine.RectTransform)zone.transform;
    foreach(var view in zone.GetComponentsInChildren<BoardCardView>())
    {
        var corners=new UnityEngine.Vector3[4]; view.Rect.GetWorldCorners(corners);
        foreach(var corner in corners) { var point=area.InverseTransformPoint(corner); Assert(point.x>=area.rect.xMin-.1f && point.x<=area.rect.xMax+.1f && point.y>=area.rect.yMin-.1f && point.y<=area.rect.yMax+.1f,"Slot overflow in "+zone.name); }
    }
}
foreach(int count in new[]{0,1,5,7}) { b.hand.SetCards(sample.GetRange(0,count)); CheckFit(b.hand); }
Assert(!b.hand.TryAdd(sample[0]),"Hand accepted eighth card");
bool rejected=false; try{b.hand.SetCards(sample.GetRange(0,8));}catch(System.ArgumentException){rejected=true;}
Assert(rejected && b.hand.Count==7,"SetCards was not atomic at capacity");
foreach(int count in new[]{0,1,19,100}) { b.humanLands.SetCards(sample.GetRange(0,count)); CheckFit(b.humanLands); }
foreach(int count in new[]{1,7,30}) { b.humanArmies.SetCards(sample.GetRange(0,count)); CheckFit(b.humanArmies); b.environmental.SetCards(sample.GetRange(0,count)); CheckFit(b.environmental); }
b.humanDiscard.SetCards(new CardData[0]); Assert(b.humanDiscard.SelectedIndex==-1,"Empty deck selection");
b.humanDiscard.SetCards(sample.GetRange(0,1)); b.humanDiscard.Browse(1); Assert(b.humanDiscard.SelectedIndex==0,"Single card deck");
b.humanDiscard.SetCards(sample.GetRange(0,7));
var source=b.humanDiscard.GetComponentInChildren<BoardCardView>();
UnityEngine.EventSystems.ExecuteEvents.Execute(source.gameObject,new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current),UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
Assert(b.preview.IsShowing,"Pointer enter did not show preview");
var prev=b.preview.PreviewRect.Find("Previous").GetComponent<UnityEngine.UI.Button>();
UnityEngine.EventSystems.ExecuteEvents.Execute(prev.gameObject,new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current),UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
Assert(b.humanDiscard.SelectedIndex==5,"Previous arrow failed");
Assert(b.humanDiscard.Cards[6]==sample[6],"Browsing reordered data");
var root=(UnityEngine.RectTransform)b.preview.transform;
var pc=new UnityEngine.Vector3[4]; b.preview.PreviewRect.GetWorldCorners(pc);
foreach(var corner in pc) { var p=root.InverseTransformPoint(corner); Assert(root.rect.Contains(p),"Preview outside screen"); }
b.preview.Hide(); b.preview.Show(source); Assert(b.humanDiscard.SelectedIndex==6,"Reopen failed to reset last card");
b.humanDiscard.Remove(sample[6]); Assert(!b.preview.IsShowing,"Collection change did not close stale preview");
foreach(var z in b.GetComponentsInChildren<CardZoneVisualizer>()) { z.SetCards(sample.GetRange(0,z.layout==BoardCardLayout.TokenGrid ? 12 : 5)); CheckFit(z); }
b.preview.Hide();
return checks+" assertions passed: capacity, atomic rejection, empty/single/deck navigation, pointer enter, arrow click, order preservation, preview bounds/reset/cleanup, and dense layout containment.";

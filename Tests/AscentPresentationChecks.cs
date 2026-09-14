// Run in Play mode once the cinematic exists. Restart Play after this review fixture.
var board=UnityEngine.Object.FindFirstObjectByType<Board>();
var c=board.GetComponent<MatchCinematic>();
if(c==null)throw new System.InvalidOperationException("Wait for the cinematic.");
board.Match.StopAllCoroutines();c.StopAllCoroutines();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var camera=(UnityEngine.Camera)typeof(MatchCinematic).GetField("camera3D",flags).GetValue(c);
var root=(UnityEngine.GameObject)typeof(MatchCinematic).GetField("stage",flags).GetValue(c);
int checks=0;
void Check(bool value,string message){if(!value)throw new System.Exception(message);checks++;}
System.Collections.IEnumerator Run()
{
    var ascent=c.ShowTower(0);ascent.MoveNext();
    yield return new UnityEngine.WaitForSecondsRealtime(.2f);
    // The portraits sit whole and still in their niches: no art zoom (it cropped them) and no card aura
    // (its sparks read as stray pixels at this scale).
    var portraits=root.GetComponentsInChildren<UnityEngine.UI.Image>().Where(i=>i.name=="Leader portrait").ToArray();
    Check(portraits.Length>=2,"Known character portraits are missing.");
    Check(portraits.All(p=>p.sprite!=null && p.preserveAspect && p.GetComponent<ZoomImage>()==null),"Portrait art is cropped or zoomed.");
    Check(root.GetComponentsInChildren<FantasyCardAura>().Length==0,"Tower portraits carry the card aura.");
    Check(portraits.All(p=>{var f=(UnityEngine.RectTransform)p.transform.parent;return UnityEngine.Mathf.Approximately(f.sizeDelta.x,f.sizeDelta.y);}),"Portrait frames are not square.");
    Check(camera.orthographic,"Ascent artwork is distorted by perspective.");
    Check(root.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.text=="V"),"Fifth landing is not represented.");
    camera.orthographicSize=3.15f;camera.transform.localPosition=new UnityEngine.Vector3(0,2,-20);
    UnityEngine.ScreenCapture.CaptureScreenshot("Docs/Fantasy-Stair-Close.png");
    yield return new UnityEngine.WaitForSecondsRealtime(.2f);
    var high=c.ShowTower(12);high.MoveNext();yield return null;
    Check(root.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.text=="XIII"),"Ascent does not extend beyond five floors.");
    int endpoint=board.Match.ascentFinalLevel;
    board.Match.ascentFinalLevel=30;
    var end=c.ShowTower(29);end.MoveNext();yield return null;
    var mist=(UnityEngine.GameObject)typeof(MatchCinematic).GetField("ascentMist",flags).GetValue(c);
    Check(!mist.activeSelf,"Final landing does not reveal the end.");
    var meshes=root.GetComponentsInChildren<UnityEngine.MeshFilter>();
    Check(meshes.All(m=>m.sharedMesh.bounds.max.y<=3*3.6f+.01f),"Architecture continues past the final landing.");
    board.Match.ascentFinalLevel=endpoint;
    var roll=c.Roll(3,4);roll.MoveNext();
    Check(!camera.orthographic && !mist.activeSelf,"Dice retain the stair projection or mist.");
    c.Hide();
    System.IO.File.WriteAllText("Temp/AscentPresentationResult.txt","PASS: "+checks+" checks; whole still portraits, square frames, five visible levels, higher landings, final endpoint, and dice camera restoration.");
}
System.Collections.IEnumerator Guard()
{
    var run=Run();while(true){object current;try{if(!run.MoveNext())break;current=run.Current;}catch(System.Exception e){System.IO.File.WriteAllText("Temp/AscentPresentationResult.txt","FAIL: "+e);throw;}yield return current;}
}
System.IO.File.WriteAllText("Temp/AscentPresentationResult.txt","RUNNING");c.StartCoroutine(Guard());return "Ascent checks running.";

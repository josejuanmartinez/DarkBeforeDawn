var b=UnityEngine.Object.FindFirstObjectByType<Board>();
b.BeginTurn(false);
var button=b.transform.Find("Your materials/End turn").GetComponent<UnityEngine.UI.Button>();
if(b.transform.Find("Opponent materials/End turn")!=null || b.transform.Find("Opponent materials/Begin turn")!=null) throw new System.Exception("Opponent button exists");
button.onClick.Invoke(); if(!b.IsOpponentTurn) throw new System.Exception("Turn did not pass to opponent");
button.onClick.Invoke(); if(b.IsOpponentTurn) throw new System.Exception("Turn did not return");
int count=0;
foreach(var root in new[]{b.transform.Find("Your materials"),b.transform.Find("Opponent materials")})
foreach(var label in root.GetComponentsInChildren<TMPro.TMP_Text>()) {
 label.ForceMeshUpdate();
 if(label.spriteAsset==null || label.spriteAsset.GetSpriteIndexFromName(label.name.Replace(" amount", "").ToLowerInvariant()) < 0) throw new System.Exception("Missing material sprite: "+label.name);
 count++;
}
if(count!=14) throw new System.Exception("Expected fourteen material sprites");
return "Single control, both turn transitions, and all 14 material sprites verified.";


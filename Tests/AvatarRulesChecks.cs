void Check(bool ok, string message) { if (!ok) throw new System.Exception(message); }
var r = new MatchRules();
Check(r.Players.All(p => p.Life == 20), "Starting health");
var army = new CardData {name="Test army",type="Army",attack=4,defense=4,commanderSkillRequired=9,agentSkillRequired=9};
r.Players[0].Hand.Add(army); r.Begin(0); while (r.Stage != MatchStage.Muster) r.Next();
Check(r.Play(army), "Army must need no character or destination");
while (r.Active == 0) r.Next(); while (r.Active == 1) r.Next(); while (r.Stage != MatchStage.Attack) r.Next();
Check(r.Stage == MatchStage.Attack && r.Active == 0, "Attack setup");
Check(r.Attack(r.Players[0].Field[0]), "Attack"); r.Next(); r.Next();
Check(r.Players[1].Life == 16, "Unblocked attack damages avatar");
var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Prefabs/Card.prefab");
if(prefab == null) {
 var id = UnityEditor.AssetDatabase.FindAssets("Card t:Prefab").First(g => System.IO.Path.GetFileName(UnityEditor.AssetDatabase.GUIDToAssetPath(g)) == "Card.prefab");
 prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(UnityEditor.AssetDatabase.GUIDToAssetPath(id));
}
var go = UnityEngine.Object.Instantiate(prefab);
try {
 var card = go.GetComponent<Card>(); card.TypewriterEffect=false; card.Initialize(army,false);
 var field=typeof(Card).GetField("descriptionText",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
 var description=(TMPro.TMP_Text)field.GetValue(card); var full=description.text;
 card.SetAvatarUnlocked(false); Check(description.text=="Play from hand to unlock abilities", "Locked abilities");
 var art=go.GetComponentsInChildren<UnityEngine.UI.Image>(true).First(i=>i.name=="Image"); Check(art.color.a < .5f,"Locked art opacity");
 card.SetAvatarUnlocked(true); Check(description.text==full && art.color.a==1f,"Unlocked face");
} finally { UnityEngine.Object.DestroyImmediate(go); }
return "PASS: 20 starting health, army without characters, unblocked damage, locked avatar prompt/transparency and restored full face.";

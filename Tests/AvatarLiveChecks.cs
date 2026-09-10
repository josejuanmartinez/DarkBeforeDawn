var board=UnityEngine.Object.FindFirstObjectByType<Board>();
var match=board.Match;
if(match?.Rules==null) throw new System.Exception("Match not initialized yet");
var zones=board.GetComponentsInChildren<AvatarZoneVisualizer>();
if(zones.Length!=2)throw new System.Exception("Expected two avatars");
foreach(var zone in zones) {
 var player=match.Rules.Players[zone.Owner];
 var face=zone.GetComponentInChildren<Card>();
 var presentation=face.GetComponent<AvatarCardPresentation>();
 if(presentation==null)throw new System.Exception("Missing avatar binding");
 var description=(TMPro.TMP_Text)typeof(Card).GetField("descriptionText",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(face);
 presentation.SendMessage("LateUpdate");
 if(description.text!="Play from hand to unlock abilities")throw new System.Exception("Avatar locked text");
 if(!player.Deck.Concat(player.Hand).Any(c=>c.cardId==face.cardData.cardId))throw new System.Exception("Avatar absent from playable deck");
 var unit=new MatchRules.Unit{Card=face.cardData.Clone(),Owner=zone.Owner};
 var life=player.Life;
 try {
  player.Field.Add(unit); player.Life=16;
  presentation.SendMessage("LateUpdate"); zone.SendMessage("LateUpdate");
  if(description.text=="Play from hand to unlock abilities")throw new System.Exception("Avatar failed to unlock");
  if(!zone.HealthLabel.text.Contains("16 HEALTH"))throw new System.Exception("Health label stale");
 } finally {player.Field.Remove(unit);player.Life=life;presentation.SendMessage("LateUpdate");zone.SendMessage("LateUpdate");}
}
return "PASS: both live avatars, deck inclusion, field-based unlock, health display and relock.";

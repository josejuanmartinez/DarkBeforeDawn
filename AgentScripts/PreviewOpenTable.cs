// Temporary populated visual fixture. Run after the opening hand; restart Play to restore a fresh game.
var b=UnityEngine.Object.FindFirstObjectByType<Board>();
var match=b.Match;
if(match.Rules==null || b.hand.Count==0) throw new System.Exception("Wait for the opening hand.");
match.StopAllCoroutines();
foreach(int owner in new[]{0,1})
{
    var player=match.Rules.Players[owner];
    foreach(var type in new[]{CardTypeEnum.Army,CardTypeEnum.Character,CardTypeEnum.Land})
    {
        int count=type==CardTypeEnum.Land?3:type==CardTypeEnum.Army?2:1;
        foreach(var card in player.Deck.Where(c=>c.GetCardType()==type).Take(count).ToArray())
        {
            player.Deck.Remove(card);
            player.Field.Add(new MatchRules.Unit {Card=card,Owner=owner,EnteredTurn=0});
        }
    }
}
match.SendMessage("Sync");
return "Populated preview fixture. Restart Play after capture.";

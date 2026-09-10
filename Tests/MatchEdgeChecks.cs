void Check(bool ok,string message) {if(!ok)throw new System.Exception(message);}
CardData Card(string type,string name) => new CardData {type=type,name=name,attack=3,defense=3};
var r=new MatchRules(); var a=new MatchRules.Unit {Card=Card("Army","Hunter"),Owner=0}; a.Card.tags.Add("ChooseTarget");
var d=new MatchRules.Unit {Card=Card("Character","Target"),Owner=1};
r.Players[0].Field.Add(a);r.Players[1].Field.Add(d);
var env=new MatchRules.Unit {Card=Card("Environmental","Old storm"),Owner=1};r.Players[1].Field.Add(env);
r.Begin(0);r.Next();Check(!r.Players[1].Field.Contains(env)&&r.Players[1].Discard.Contains(env.Card),"Old environment survived realm cleanup");
while(r.Stage!=MatchStage.Attack)r.Next();
Check(!r.Attack(a),"Targeting ability accepted no target");Check(!r.Attack(a,a),"Friendly target accepted");Check(r.Attack(a,d),"Enemy target rejected");r.Next();r.Next();
Check(!r.Players[0].Field.Contains(a)&&!r.Players[1].Field.Contains(d),"Targeted fight not simultaneous");
r.Spoils.Enqueue(new MatchRules.Loot{Card=Card("Object","Abandoned"),Owner=0});r.OfferLoot();Check(r.Spoils.Count==0&&r.Players[0].Discard.Any(c=>c.name=="Abandoned"),"No-recipient loot deadlock");
var unit=new CardData {type="Army",troopType=TroopsTypeEnum.hi};Check(unit.GetCombatStats()==(3,4),"Printed troop fallback differs from combat");
var hero=new CardData {type="Character",commander=2,mage=1};Check(hero.GetCombatStats()==(3,3),"Printed character fallback differs from combat");
var win=new MatchRules();win.Players[1].Life=2;var killer=new MatchRules.Unit{Card=Card("Army","Attacker"),Owner=0};win.Players[0].Field.Add(killer);win.Begin(0);while(win.Stage!=MatchStage.Attack)win.Next();win.Attack(killer);win.Next();win.Next();Check(win.Winner==0,"Unblocked lethal did not end match");
return "PASS: environment cleanup, required targeting, ownership validation, targeted simultaneous damage, no-recipient spoils, shared printed stats, lethal victory.";

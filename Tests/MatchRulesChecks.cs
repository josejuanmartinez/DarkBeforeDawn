var r = new MatchRules(); r.Roll=()=>3; // every die shows 3, so duels are decided by the printed attacks
void Check(bool ok, string message) { if(!ok) throw new System.Exception(message); }
CardData Card(string name,string type,int attack=0,int defense=0) => new CardData { name=name,type=type,attack=attack,defense=defense };
for(int i=0;i<10;i++) r.Players[0].Deck.Add(Card("Land"+i,"Land"));
r.Players[0].HandLimit=7;
var halted=new MatchRules.Unit {Card=Card("Halted","Army",2,2),Owner=0,Tapped=true};
halted.Card.statusEffects.Add(StatusEffects.Halted); r.Players[0].Field.Add(halted);
var ready=new MatchRules.Unit {Card=Card("Ready","Army",5,3),Owner=0,Tapped=true}; r.Players[0].Field.Add(ready);
r.Begin(0); Check(r.Players[0].Hand.Count==7,"Hand limit not respected"); Check(halted.Tapped&&!ready.Tapped,"Untap/Halted failed");
var land=r.Players[0].Hand[0]; Check(!r.Play(land),"Land played in draw stage"); r.Next(); Check(r.Play(land),"Land rejected in realm");
var pc=Card("Home","PC"); pc.region="Missing"; r.Players[0].Settlements.Add(pc); r.Next(); Check(r.Stage==MatchStage.Destination&&!r.ChooseDestination(pc),"PC without land offered as destination"); pc.region=land.name; Check(r.ChooseDestination(pc),"PC with land rejected as destination");
r.Next(); Check(r.Stage==MatchStage.Travel,"Destination must go to Travel"); r.Next(); Check(r.Stage==MatchStage.Muster,"The road must end at Muster"); var lu=r.Players[0].Field.Find(u=>u.Card==land); Check(r.TapLand(lu)&&!r.TapLand(lu),"Double tapping land accepted");
var hero=Card("Hero","Character",2,2); hero.startingPC="Elsewhere"; r.Players[0].Hand.Add(hero); Check(!r.Play(hero),"Character away from its home accepted"); hero.startingPC="Home"; Check(r.Play(hero),"Character deployment failed"); Check(r.Players[0].Field.Find(u=>u.Card==hero).Tapped,"A recruit must enter tapped");
var hu=r.Players[0].Field.Find(u=>u.Card==hero); var item=Card("Relic","Object"); item.objectType=ObjectTypeEnum.Jewel; pc.objectTypes.Add(ObjectTypeEnum.Jewel); r.Players[0].Hand.Add(item); Check(!r.Play(item),"Object played without character"); Check(!r.Play(item,hu)&&r.Message.Contains("already"),"Second play at a used destination accepted");
r.Next(); var ev=Card("Unsupported event","Event"); r.Players[0].Hand.Add(ev); Check(!r.Play(ev)&&r.Players[0].Hand.Contains(ev),"Unsupported event consumed");
r.Next(); Check(r.Stage==MatchStage.Spoils&&r.MustDiscard(0)&&!r.Next(),"An oversized hand must not end the turn"); while(r.MustDiscard(0)) r.Discard(r.Players[0].Hand[0]);
Check(r.Next()&&r.Active==1&&r.Stage==MatchStage.Draw,"Turn handoff failed: attacks belong to the road, not a stage of their own");
// Our units fall on the enemy company as it travels: its turn, our attack window.
var d1=new MatchRules.Unit {Card=Card("Defender A","Army",1,2),Owner=1}; var d2=new MatchRules.Unit {Card=Card("Defender B","Character",4,5),Owner=1};
r.Players[1].Field.Add(d1); r.Players[1].Field.Add(d2); var enemyHome=Card("Enemy home","PC"); enemyHome.region="Far"; r.Players[1].Settlements.Add(enemyHome); r.StartAt(1,enemyHome);
r.Next(); r.Next(); r.Next(); Check(r.Stage==MatchStage.Travel&&r.Phase==TravelPhase.Attack&&r.Attacker==0,"Enemy travel must open our attack window");
hu.Tapped=true; Check(!r.Attack(hu),"A tapped unit attacked"); Check(r.Attack(ready)&&!r.Attack(ready),"Attack tapping failed");
r.Next(); Check(r.Phase==TravelPhase.Defend&&r.Block(d1,r.Attacks[0])&&!r.Block(d1,r.Attacks[0]),"Tapped defender accepted"); Check(r.Block(d2,r.Attacks[0]),"Multiple blockers failed");
r.Next(); Check(r.Players[0].Field.Contains(ready)&&!r.Players[1].Field.Contains(d1)&&r.Players[1].Field.Contains(d2)&&!d2.Wounded&&d2.Tapped,"Duels: 5+3 beats 1+3 by 4 over defense 2 (killed), then beats 4+3 by 1 under defense 5 (tapped)");
Check(r.Fights.Count==2&&r.Fights[0].Blow==Blow.Killed&&r.Fights[1].Blow==Blow.Tapped&&r.Fights[0].AttackerRoll==3,"Fight log wrong");
Check(r.Stage==MatchStage.Muster&&r.Active==1,"The road must end at Muster");
while(r.Stage!=MatchStage.Spoils) r.Next();
r.Spoils.Enqueue(new MatchRules.Loot {Card=Card("Spoil","Object"),Owner=0}); Check(!r.Next(),"Unresolved spoils skipped"); r.OfferLoot(); Check(r.Recipients().Contains(d2),"Enemy loot recipients missing"); Check(r.Transfer(d2)&&d2.Objects.Count==1,"Enemy transfer failed");
Check(r.Next()&&r.Active==0&&r.Stage==MatchStage.Draw,"Turn handoff failed");
return "PASS: refill to 7, Halted, stage gates, land-gated destinations, character deployment at home, one play per destination, event safety, road attacks with summon timing, attack tap, multiple blockers, simultaneous damage, spoils and handoff.";

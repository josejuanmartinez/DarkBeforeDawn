// Unity CLI eval_file in Play mode. Restart Play afterwards to discard the temporary skin.
if (!UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Enter Play first.");
var board = UnityEngine.Object.FindAnyObjectByType<Board>();
var presentation = board.GetComponent<BoardPresentation>();
var manager = presentation.skinManager;
var original = manager.ActiveSkin;
var zones = board.GetComponentsInChildren<CardZoneVisualizer>();
var snapshot = zones.ToDictionary(z => z, z => z.Cards.ToArray());
int checks = 0;
void Check(bool condition,string message) { if (!condition) throw new System.Exception(message); checks++; }
Check(original == BoardSkin.Default && original.displayName == "Default", "Default skin was not loaded.");
Check(original.zones.Length == 12, "Default skin lost board zones.");
var variant = UnityEngine.Object.Instantiate(original);
variant.name = "Temporary skin test";
variant.colors.gold = UnityEngine.Color.magenta;
variant.colors.ink = new UnityEngine.Color(.12f,.08f,.16f,1);
variant.cards.size = new UnityEngine.Vector2(320,430);
variant.tokens.captionHeight = 36;
variant.preview.preferredHeight = 430;
variant.chrome.handGap = 24;
try
{
    bool rejected = false;
    try { manager.SetSkin(variant); } catch (System.ArgumentException) { rejected = true; }
    Check(rejected && manager.ActiveSkin == original, "Unregistered skin changed selection.");
    manager.AddSkin(variant);
    var discard = board.humanDiscard;
    discard.Browse(-1);
    int selection = discard.SelectedIndex;
    board.preview.Show(board.hand.GetComponentInChildren<BoardCardView>());
    int viewCount = board.GetComponentsInChildren<BoardCardView>().Length;
    for (int i=0; i<3; i++)
    {
        manager.SetSkin(variant);
        UnityEngine.Canvas.ForceUpdateCanvases();
        Check(presentation.Skin == variant, "Presentation failed to load selection.");
        Check(!board.preview.IsShowing, "Skin switch left stale inspection open.");
        Check(board.hand.gap == 24, "Hand spacing was not loaded.");
        Check(board.hand.GetComponentInChildren<BoardCardView>().NaturalSize == variant.cards.size, "Card geometry was not loaded.");
        Check(board.GetComponentsInChildren<BoardCardView>().Length == viewCount, "Skin switch duplicated cards.");
        Check(board.GetComponentsInChildren<UnityEngine.UI.Image>().Count(g=>g.name=="Zone surface") == 12, "Skin switch duplicated zones.");
        Check(discard.SelectedIndex == selection, "Skin switch reset pile selection.");
        foreach (var zone in zones) Check(zone.Cards.SequenceEqual(snapshot[zone]), "Skin switch changed card data/order.");
        var masthead = board.GetComponentsInChildren<UnityEngine.UI.Image>().First(g=>g.name=="Board masthead");
        Check(masthead.GetComponentInChildren<BoardSurface>().surface == variant.colors.ink, "Board surface did not load skin color.");
        board.preview.Show(board.hand.GetComponentInChildren<BoardCardView>());
        Check(board.preview.preferredHeight == variant.preview.preferredHeight, "Preview size was not loaded.");
        Check(board.preview.PreviewRect.GetComponentInChildren<BoardSurface>().surface == variant.colors.ink, "Preview color did not load.");
        manager.SetSkin(original);
        Check(board.hand.GetComponentInChildren<BoardCardView>().NaturalSize == original.cards.size, "Default geometry did not restore.");
    }
    Check(original.colors.gold != variant.colors.gold && original.cards.size == new UnityEngine.Vector2(300,410), "Default asset was mutated.");
}
finally
{
    manager.SetSkin(original);
    UnityEngine.Object.Destroy(variant);
}
return checks + " skin checks passed: Default, invalid selection, repeat switching, card/zone cleanup, colors, geometry, preview settings, collection order and pile selection preservation.";

// Run via Unity CLI eval_file in Play mode. Exercises real UI raycasts and inspection events.
if (!UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Enter Play mode first.");
var board = UnityEngine.Object.FindAnyObjectByType<Board>();
var events = UnityEngine.EventSystems.EventSystem.current;
int checks = 0;
void Check(bool condition, string message) { if (!condition) throw new System.Exception(message); checks++; }
board.preview.Hide();
UnityEngine.Canvas.ForceUpdateCanvases();
foreach (var zone in board.GetComponentsInChildren<CardZoneVisualizer>())
{
    foreach (var view in zone.GetComponentsInChildren<BoardCardView>())
    {
        var canvas = view.GetComponentInParent<UnityEngine.Canvas>().rootCanvas;
        var camera = canvas.renderMode == UnityEngine.RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var pointer = new UnityEngine.EventSystems.PointerEventData(events) {
            position = UnityEngine.RectTransformUtility.WorldToScreenPoint(camera, view.Rect.TransformPoint(view.Rect.rect.center))
        };
        var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        events.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<BoardCardView>() == view, "Card hit area is obstructed: " + view.name);
    }
}
var first = board.hand.GetComponentsInChildren<BoardCardView>()[0];
var second = board.hand.GetComponentsInChildren<BoardCardView>()[1];
var click = new UnityEngine.EventSystems.PointerEventData(events) { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
var original = first.Rect.anchoredPosition;
UnityEngine.EventSystems.ExecuteEvents.Execute(first.gameObject, click, UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
Check(board.preview.IsShowing, "Hover did not open.");
var shown = board.preview.PreviewRect;
Check(shown.Find("Close") == null && shown.Find("Card action") == null, "Hover preview carries pin-era buttons.");
Check(!shown.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t => t.text.Contains("PIN")), "Hover preview still talks about pinning.");
// A click never sticks the preview: on the authoring board it plays the hand card (or fails to afford it) and nothing pins.
int handBefore = board.hand.Cards.Count;
UnityEngine.EventSystems.ExecuteEvents.Execute(first.gameObject, click, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
Check(board.hand.Cards.Count == handBefore || board.hand.Cards.Count == handBefore - 1, "Click did something other than play the card.");
if (board.hand.Cards.Count == handBefore)
{
    // Not affordable: the preview stays a plain hover, and another hover replaces it.
    Check(board.preview.IsShowing && board.preview.PreviewRect == shown, "A click that played nothing changed the hover preview.");
    UnityEngine.EventSystems.ExecuteEvents.Execute(second.gameObject, click, UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
    Check(board.preview.IsShowing && board.preview.PreviewRect != shown, "Hover after a click did not move to the new card.");
    Check(first.Rect.anchoredPosition == original, "Inspection moved the original slot.");
}
else Check(!board.preview.IsShowing || board.preview.PreviewRect != shown, "Playing the card left its preview up.");
board.preview.Hide();
var handNow = board.hand.GetComponentsInChildren<BoardCardView>();
if (handNow.Length > 0)
{
    board.preview.Show(handNow[0]);
    var cards = new System.Collections.Generic.List<CardData>(board.hand.Cards);
    board.hand.SetCards(cards);
    Check(!board.preview.IsShowing, "Collection rebuild left a stale preview.");
}
foreach (var zone in new CardZoneVisualizer[] {board.humanArmies, board.environmental, board.humanDiscard})
{
    var view = zone.GetComponentInChildren<BoardCardView>();
    if (view == null) continue;
    board.preview.Show(view);
    Check(board.preview.IsShowing, "Could not inspect token or deck.");
    var root = (UnityEngine.RectTransform)board.preview.transform;
    var corners = new UnityEngine.Vector3[4]; board.preview.PreviewRect.GetWorldCorners(corners);
    foreach (var corner in corners) Check(root.rect.Contains(root.InverseTransformPoint(corner)), "Preview outside screen.");
    board.preview.Hide();
}
return checks + " checks passed: card raycasts, hover, click never pins, hover replacement, source replacement, token/environment/deck inspection and bounds.";

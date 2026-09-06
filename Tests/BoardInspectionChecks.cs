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
Check(board.preview.IsShowing && !board.preview.IsPinned, "Hover did not open.");
UnityEngine.EventSystems.ExecuteEvents.Execute(first.gameObject, click, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
Check(board.preview.IsPinned, "Click did not pin.");
var pinned = board.preview.PreviewRect;
UnityEngine.EventSystems.ExecuteEvents.Execute(second.gameObject, click, UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
Check(board.preview.PreviewRect == pinned, "Another hover replaced pinned card.");
Check(first.Rect.anchoredPosition == original, "Inspection moved the original slot.");
var close = pinned.Find("Close").GetComponent<UnityEngine.UI.Button>();
UnityEngine.EventSystems.ExecuteEvents.Execute(close.gameObject, click, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
Check(!board.preview.IsShowing && !board.preview.IsPinned, "Close did not dismiss.");
board.preview.Pin(first);
board.preview.Pin(first);
Check(!board.preview.IsShowing, "Second click did not unpin.");
board.preview.Pin(first);
var cards = new System.Collections.Generic.List<CardData>(board.hand.Cards);
board.hand.SetCards(cards);
Check(!board.preview.IsShowing, "Collection rebuild left a stale pinned preview.");
foreach (var zone in new CardZoneVisualizer[] {board.humanArmies, board.environmental, board.humanDiscard})
{
    var view = zone.GetComponentInChildren<BoardCardView>();
    if (view == null) continue;
    board.preview.Pin(view);
    Check(board.preview.IsPinned, "Could not pin token or deck.");
    var root = (UnityEngine.RectTransform)board.preview.transform;
    var corners = new UnityEngine.Vector3[4]; board.preview.PreviewRect.GetWorldCorners(corners);
    foreach (var corner in corners) Check(root.rect.Contains(root.InverseTransformPoint(corner)), "Pinned preview outside screen.");
    board.preview.Hide();
}
return checks + " checks passed: card raycasts, hover, click-to-pin, pinned stability, close, unpin, source replacement, token/environment/deck pinning and bounds.";

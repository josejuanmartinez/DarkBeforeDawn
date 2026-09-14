// Run through Unity CLI eval_file after the opening cinematic in Play mode.
if (!UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Enter Play mode first.");
var board = UnityEngine.Object.FindFirstObjectByType<Board>();
int checks = 0;
void Assert(bool condition, string message) { if (!condition) throw new System.Exception(message); checks++; }
foreach (var surface in board.GetComponentsInChildren<BoardSurface>())
    Assert(!surface.raycastTarget, "Decorative furniture intercepts input.");
var hand = board.hand.GetComponentInChildren<BoardCardView>();
Assert(hand != null, "Expected a starting hand.");
board.preview.Show(hand);
Assert(board.preview.IsShowing, "Card inspection failed.");
Assert(board.preview.PreviewRect.GetComponentInChildren<BoardSurface>() != null, "Preview is missing the board treatment.");
board.preview.Hide();
var bar = (UnityEngine.RectTransform)board.transform.Find("Match stages");
Assert(bar != null && bar.anchorMin.y >= .928f, "Match toolbar overlaps the battlefield.");
var nextRoot = BoardSkin.Default.openTable ? board.transform.Find("CONTINUE") : bar.Find("CONTINUE");
var next = nextRoot.GetComponent<UnityEngine.UI.Button>();
Assert(next.targetGraphic is BoardSurface, "Action feedback does not target the visible surface.");
bool wasVisible = next.gameObject.activeSelf;
next.gameObject.SetActive(true);
UnityEngine.Canvas.ForceUpdateCanvases();
var eventSystem = UnityEngine.EventSystems.EventSystem.current;
var canvas = next.GetComponentInParent<UnityEngine.Canvas>();
var point = UnityEngine.RectTransformUtility.WorldToScreenPoint(canvas.renderMode == UnityEngine.RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, next.transform.position);
var pointer = new UnityEngine.EventSystems.PointerEventData(eventSystem) { position = point };
var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
eventSystem.RaycastAll(pointer, hits);
next.gameObject.SetActive(wasVisible);
Assert(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() == next, "Next-stage button is blocked by decorative UI.");
foreach (var zone in board.GetComponentsInChildren<CardZoneVisualizer>())
{
    var rect = (UnityEngine.RectTransform)zone.transform;
    var original = rect.sizeDelta;
    try
    {
        foreach (var size in new[] { new UnityEngine.Vector2(260,100), new UnityEngine.Vector2(100,260), new UnityEngine.Vector2(800,180) })
        {
            rect.SetSizeWithCurrentAnchors(UnityEngine.RectTransform.Axis.Horizontal, size.x);
            rect.SetSizeWithCurrentAnchors(UnityEngine.RectTransform.Axis.Vertical, size.y);
            zone.Arrange();
            foreach (var view in zone.GetComponentsInChildren<BoardCardView>())
            {
                // Measure the layout slot independently of the live ready-card rocking pose.
                var pose = view.Rect.localRotation;
                view.Rect.localRotation = UnityEngine.Quaternion.identity;
                var corners = new UnityEngine.Vector3[4]; view.Rect.GetWorldCorners(corners);
                view.Rect.localRotation = pose;
                foreach (var corner in corners)
                {
                    var p = rect.InverseTransformPoint(corner);
                    Assert(p.x >= rect.rect.xMin-.1f && p.x <= rect.rect.xMax+.1f && p.y >= rect.rect.yMin-.1f && p.y <= rect.rect.yMax+.1f, "Card overflows resized zone " + zone.name + "/" + view.name + " at " + size + ": " + p + " outside " + rect.rect);
                }
            }
        }
    }
    finally { rect.sizeDelta = original; zone.Arrange(); }
}
var healthBars = board.GetComponentsInChildren<AvatarHealthBar>();
Assert(healthBars.Length == 2,"Expected both champions to have health bars.");
int originalLife = board.Match.Rules.Players[0].Life;
try
{
    board.Match.Rules.Players[0].Life = 7;
    foreach (var health in healthBars) health.SendMessage("Update");
    var humanHealth = board.transform.Find("Your avatar").GetComponentInChildren<AvatarHealthBar>();
    Assert(humanHealth.GetComponentInChildren<TMPro.TMP_Text>().text.Contains("7 /"),"Health does not follow match damage.");
}
finally { board.Match.Rules.Players[0].Life=originalLife; foreach(var health in healthBars) health.SendMessage("Update"); }
return checks + " assertions passed: decoration input transparency, card inspection, toolbar placement, button raycast, responsive card containment and live health.";

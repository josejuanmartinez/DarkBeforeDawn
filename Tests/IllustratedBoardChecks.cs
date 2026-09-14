// Unity CLI eval_file in Play mode after the opening cinematic.
var board = UnityEngine.Object.FindFirstObjectByType<Board>();
int checks = 0;
void Check(bool value, string message) { if (!value) throw new System.Exception(message); checks++; }
var backdrop = board.transform.Find("Illustrated landscape")?.GetComponent<UnityEngine.UI.RawImage>();
Check(backdrop != null && backdrop.texture != null && !backdrop.raycastTarget, "Missing or interactive landscape.");
Check(backdrop.GetComponent<UnityEngine.UI.AspectRatioFitter>() != null, "Landscape can stretch on resize.");
var hand = board.hand.GetComponentInChildren<BoardCardView>();
Check(hand != null, "Wait until the opening hand is dealt.");
var face = hand.GetComponentInChildren<Card>();
Check(face.transform.Find("Hand type ribbon") != null, "Hand is missing its illustrated face.");
Check(!face.transform.Find("RealCard/DescriptionBackground").gameObject.activeSelf, "Hand rules still obscure artwork.");
board.preview.Show(hand);
Check(board.preview.IsShowing, "Inspection failed.");
var previewFace = board.preview.GetComponentInChildren<Card>();
Check(previewFace != null && previewFace.transform.Find("RealCard/DescriptionBackground").gameObject.activeSelf, "Full rules missing from inspection.");
Check(previewFace.transform.Find("Hand type ribbon") == null, "Hand ribbon leaked into full inspection.");
board.preview.Hide();
return checks + " illustrated board checks passed.";

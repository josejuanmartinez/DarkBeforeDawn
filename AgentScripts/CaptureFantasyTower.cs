// Play-mode review fixture. Restart Play afterwards to restore the match flow.
var board=UnityEngine.Object.FindFirstObjectByType<Board>();
var cinematic=UnityEngine.Object.FindFirstObjectByType<MatchCinematic>();
if (cinematic == null) throw new System.InvalidOperationException("Wait for the opening cinematic to initialize.");
board.Match.StopAllCoroutines();
cinematic.StopAllCoroutines();
var tower=cinematic.ShowTower(0); tower.MoveNext();
UnityEngine.ScreenCapture.CaptureScreenshot("Docs/Fantasy-Tower.png");
return "Tower establishing view captured.";

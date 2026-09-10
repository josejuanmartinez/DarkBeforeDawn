var c=UnityEngine.Object.FindFirstObjectByType<MatchCinematic>();
c.StopAllCoroutines();
System.Collections.IEnumerator Capture() {
    yield return c.Roll(5,2);
    UnityEngine.ScreenCapture.CaptureScreenshot("Temp/DiceUI.png");
}
c.StartCoroutine(Capture()); return true;

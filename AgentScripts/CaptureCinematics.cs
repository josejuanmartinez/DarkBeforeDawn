var c=UnityEngine.Object.FindFirstObjectByType<MatchCinematic>();
System.Collections.IEnumerator Capture() {
    var tower=c.ShowTower(0);
    while(tower.MoveNext()) { yield return tower.Current; if(UnityEngine.Time.frameCount%30==0) UnityEngine.ScreenCapture.CaptureScreenshot("Temp/TowerUI.png"); }
    var dice=c.Roll(5,2);
    while(dice.MoveNext()) yield return dice.Current;
    UnityEngine.ScreenCapture.CaptureScreenshot("Temp/DiceUI.png");
}
c.StartCoroutine(Capture()); return true;

var c=UnityEngine.Object.FindFirstObjectByType<MatchCinematic>();
c.StopAllCoroutines(); var tower=c.ShowTower(0); tower.MoveNext();
UnityEngine.ScreenCapture.CaptureScreenshot("Temp/TowerUI.png"); return true;

using UnityEngine;

/// <summary>Camera-relative east-to-west flight, with the return hidden beyond the screen.</summary>
[DisallowMultipleComponent]
public sealed class ValleyDragonFlight : MonoBehaviour
{
    [Header("Sky crossing")]
    [Min(5)] public float crossingSeconds = 28;
    [Min(0)] public float pauseSeconds = 9;
    [Range(.55f,.95f)] public float skyHeight = .84f;
    [Min(40)] public float distance = 115;
    [Range(0,.2f)] public float initialProgress = .08f;
    [Header("Flight motion")]
    [Range(.1f,2)] public float wingbeatsPerSecond = .48f;
    [Range(0,60)] public float wingAmplitude = 34;
    public Transform model;
    public Transform leftWing;
    public Transform rightWing;
    public Transform[] tailJoints;
    private Camera flightCamera;
    private float elapsed;

    private void OnEnable()
    {
        elapsed = 0;
        Sample(Application.isPlaying ? 0 : crossingSeconds * .36f);
    }
    private void LateUpdate()
    {
        elapsed += Time.deltaTime;
        Sample(elapsed);
    }

    /// <summary>Deterministic pose sampling for previews and flight checks.</summary>
    public void Sample(float seconds)
    {
        if (flightCamera == null) flightCamera = Camera.main;
        if (flightCamera == null || model == null) return;
        float duration = Mathf.Max(5,crossingSeconds);
        float phase = Mathf.Repeat(Mathf.Max(0,seconds) + duration*initialProgress, duration + Mathf.Max(0,pauseSeconds));
        bool visible = phase < duration;
        if (model.gameObject.activeSelf != visible) model.gameObject.SetActive(visible);
        float progress = Mathf.Clamp01(phase/duration);
        // These margins put the entire mesh outside the viewport before it resets.
        float x = Mathf.Lerp(1.18f,-.18f,progress);
        float y = skyHeight + Mathf.Sin(progress*Mathf.PI)*.035f + Mathf.Sin(seconds*.9f)*.005f;
        transform.position = flightCamera.ViewportToWorldPoint(new Vector3(x,y,distance));
        transform.rotation = flightCamera.transform.rotation * Quaternion.Euler(-4,-100,Mathf.Sin(seconds*.65f)*3);
        float beat = seconds * wingbeatsPerSecond * Mathf.PI * 2;
        if(leftWing != null) leftWing.localRotation=Quaternion.Euler(0,0,-(18+Mathf.Sin(beat)*wingAmplitude));
        if(rightWing != null) rightWing.localRotation=Quaternion.Euler(0,0,18+Mathf.Sin(beat+.13f)*wingAmplitude);
        if(tailJoints != null)
            for(int i=0;i<tailJoints.Length;i++)
                if(tailJoints[i]!=null) tailJoints[i].localRotation=Quaternion.Euler(7+Mathf.Sin(seconds*1.6f-i*.55f)*10,Mathf.Sin(seconds*.9f-i*.4f)*5,0);
    }
}

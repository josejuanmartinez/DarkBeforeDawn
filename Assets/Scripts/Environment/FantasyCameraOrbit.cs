using UnityEngine;

/// <summary>Unhurried, frame-rate-independent orbit around the board backdrop.</summary>
[DisallowMultipleComponent]
public sealed class FantasyCameraOrbit : MonoBehaviour
{
    public Vector3 focus = new Vector3(0, 4, 0);
    [Min(1)] public float radius = 34;
    public float height = 17;
    [Range(-5, 5)] public float degreesPerSecond = .65f;
    public float startingAngle = -72;
    private float angle;

    private void OnEnable() { angle = startingAngle; Apply(); }
    private void LateUpdate()
    {
        angle = Mathf.Repeat(angle + degreesPerSecond * Time.deltaTime, 360);
        Apply();
    }
    public void Apply()
    {
        if (!Application.isPlaying) angle = startingAngle;
        float radians = angle * Mathf.Deg2Rad;
        transform.position = new Vector3(focus.x + Mathf.Cos(radians) * radius, height,
            focus.z + Mathf.Sin(radians) * radius);
        transform.LookAt(focus);
    }
}

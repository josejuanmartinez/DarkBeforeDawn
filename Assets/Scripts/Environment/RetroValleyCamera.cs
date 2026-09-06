using UnityEngine;

/// <summary>A bounded drift keeps the illustrated valley composed behind the cards.</summary>
[DisallowMultipleComponent]
public sealed class RetroValleyCamera : MonoBehaviour
{
    public Vector3 position = new Vector3(0, 13, -42);
    public Vector3 focus = new Vector3(0, 16, 45);
    [Range(0, 2)] public float drift = .65f;
    private float elapsed;

    private void OnEnable() { elapsed = 0; Apply(); }
    private void LateUpdate() { elapsed += Time.deltaTime; Apply(); }
    public void Apply()
    {
        var offset = new Vector3(Mathf.Sin(elapsed * .065f), Mathf.Sin(elapsed * .09f) * .22f, 0) * drift;
        transform.SetPositionAndRotation(position + offset, Quaternion.LookRotation(focus - position - offset));
    }
}

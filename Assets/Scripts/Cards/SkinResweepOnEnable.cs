using Unity.Scripting.LifecycleManagement;
using UnityEngine;

// Present on the card prefabs, which were authored in Runeboard with a skin material already baked
// in (the Bakshi material on the art Image). There, MaterialManager swept the whole scene once at
// skin-change time, so anything instantiated afterwards — a card dealt into the hand, a preview
// clone — was stuck on its spawn-time material; this component re-applied the active skin to just
// its own subtree on enable.
//
// MaterialManager was part of the gameplay layer and did not come across. Rather than leave the
// prefabs carrying a missing script, the component survives with the same GUID and an open hook:
// assign SkinResweep.Apply and it runs, leave it null and this does nothing.
public class SkinResweepOnEnable : MonoBehaviour
{
    private void OnEnable()
    {
        SkinResweep.Apply?.Invoke(gameObject);
    }
}

public static partial class SkinResweep
{
    /// <summary>
    /// Invoked with a GameObject whose subtree should be re-materialed to the active skin. Install
    /// this if this project grows a skin system; until then card prefabs simply keep the material
    /// they were authored with.
    /// </summary>
    [AutoStaticsCleanup]
    public static System.Action<GameObject> Apply;
}

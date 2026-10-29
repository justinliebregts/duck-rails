using UnityEngine;

// Optional per-item overrides for how a Grabbable looks inside a bag slot. Items without one are auto-fitted.
[DisallowMultipleComponent]
public class BagItem : MonoBehaviour
{
    [Tooltip("Shown in the slot instead of a clone of this item's meshes.")]
    public GameObject miniPrefab;
    public Vector3 miniEuler;
    [Tooltip("Multiplier on the auto-fit scale (1 = fill the slot).")]
    public float miniScale = 1f;
    [Tooltip("Items that can't go in a bag (e.g. too big, quest objects).")]
    public bool canStash = true;
}

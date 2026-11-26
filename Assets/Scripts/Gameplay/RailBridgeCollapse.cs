using UnityEngine;

/// <summary>Deterministic local scenery driven by the retained expedition finish timestamp.</summary>
public sealed class RailBridgeCollapse : MonoBehaviour
{
    public const float Duration = 3.2f;
    public Transform[] sections;
    public GameObject explosionPrefab;
    public GameObject[] fires;
    Transform source;
    Renderer[] originalRenderers;
    Collider[] originalColliders;
    bool[] rendererStates, colliderStates;
    Collider[][] walkColliders;
    Quaternion startRotation;
    GameObject burst;
    bool fireStarted;

    public void Begin(Transform hinge, float progress, Vector3 impact, float age)
    {
        source = hinge;
        transform.SetPositionAndRotation(hinge.position, hinge.parent.rotation);
        startRotation = Quaternion.Euler(-80 * (1 - progress), 0, 0);
        originalRenderers = hinge.GetComponentsInChildren<Renderer>(true);
        originalColliders = hinge.GetComponentsInChildren<Collider>(true);
        rendererStates = new bool[originalRenderers.Length];
        colliderStates = new bool[originalColliders.Length];
        for (int i = 0; i < originalRenderers.Length; i++) { rendererStates[i] = originalRenderers[i].enabled; originalRenderers[i].enabled = false; }
        for (int i = 0; i < originalColliders.Length; i++) { colliderStates[i] = originalColliders[i].enabled; originalColliders[i].enabled = false; }
        // Rejoining after impact must not replay the explosion or its sound.
        if (age < 1f)
        {
            burst = Instantiate(explosionPrefab, impact, Quaternion.identity, transform);
            foreach (var ps in burst.GetComponentsInChildren<ParticleSystem>(true)) { var main = ps.main; main.loop = false; }
            if (RailAudio.Bank != null) RailAudio.Play(RailAudio.Bank.bridgeCrash, impact);
            Destroy(burst, 10);
        }
        Sample(age);
    }

    public static void Pose(int index, Quaternion start, float age, out Vector3 position, out Quaternion rotation)
    {
        float t = Mathf.Clamp01((age - index * .14f) / 2.4f);
        float fall = t * t;
        var origin = start * new Vector3(0, 0, index * 6 + 3);
        var endRotation = Quaternion.Euler(index % 2 == 0 ? 9 : -12, (index - 2) * 7, index % 2 == 0 ? 12 : -9);
        // Rest the lowest vertex on the river bed (calibrated from each generated mesh below).
        var end = new Vector3(index % 2 == 0 ? -1.2f : 1.4f, -8.6f, 5 + index * 5);
        position = Vector3.Lerp(origin, end, fall);
        rotation = Quaternion.Slerp(start, endRotation, Mathf.SmoothStep(0, 1, t));
    }

    public void Sample(float age)
    {
        if (walkColliders == null)
        {
            walkColliders = new Collider[sections.Length][];
            for (int i = 0; i < sections.Length; i++) walkColliders[i] = sections[i].GetComponentsInChildren<Collider>(true);
        }
        for (int i = 0; i < sections.Length; i++)
        {
            Pose(i, startRotation, age, out var p, out var q);
            // Offline mesh-derived rest height, independent of raised start pose.
            float t = Mathf.Clamp01((age - i * .14f) / 2.4f);
            p.y += (restHeights[i] + 8.6f) * t * t;
            sections[i].SetLocalPositionAndRotation(p, q);
            // Rest pose is identical every run; only solid once landed so falling spans never shove players.
            foreach (var c in walkColliders[i]) c.enabled = t >= 1f;
        }
        if (!fireStarted && age >= 2.1f)
        {
            fireStarted = true;
            foreach (var fire in fires) fire.SetActive(true);
        }
    }
    public float[] restHeights;

    public void Restore()
    {
        if (source != null && originalRenderers != null)
        {
            for (int i = 0; i < originalRenderers.Length; i++) if (originalRenderers[i]) originalRenderers[i].enabled = rendererStates[i];
            for (int i = 0; i < originalColliders.Length; i++) if (originalColliders[i]) originalColliders[i].enabled = colliderStates[i];
        }
        source = null;
    }
    void OnDestroy() => Restore();
}

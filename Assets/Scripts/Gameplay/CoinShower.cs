using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Coins burst out of the crate one after another, hang for a moment, then stream into the depositor.
// The depositor sees them land in their watch, which counts up as each one lands; everyone else sees them fly into the depositor's body.
// Coins are plain data drawn with GPU instancing: one draw call per frame for every coin in the air, no GameObjects.
public class CoinShower : MonoBehaviour
{
    // Without a chest bone, coins aim this far below the player's head so they don't fly into the camera.
    const float PlayerChestDrop = 0.4f;
    // Most instances Unity accepts in one instanced draw.
    const int MaxInstancesPerDraw = 1023;

    [SerializeField] Mesh coinMesh;
    [Tooltip("Must have Enable GPU Instancing ticked.")]
    [SerializeField] Material coinMaterial;
    [Tooltip("Where coins pop out from. Defaults to just above this object.")]
    [SerializeField] Transform spawnPoint;

    [Header("Size")]
    [SerializeField] float coinScale = 0.35f;
    [Tooltip("Coins shrink to this fraction of their size as they reach the watch.")]
    [SerializeField] float landedScale = 0.3f;

    [Header("Timing (last coin lands after spawnSpread + popDuration + hoverDuration + flyDuration)")]
    [Tooltip("Seconds between the first and last coin popping out.")]
    [SerializeField] float spawnSpread = 0.9f;
    [SerializeField] float popDuration = 0.45f;
    [SerializeField] float hoverDuration = 0.15f;
    [SerializeField] float flyDuration = 1f;

    [Header("Motion")]
    [SerializeField] float popRadius = 0.5f;
    [SerializeField] float popHeight = 0.5f;
    [Tooltip("How far the flight path bows up and sideways on its way to the watch.")]
    [SerializeField] float flyArc = 0.4f;
    [Tooltip("Degrees per second.")]
    [SerializeField] float spinSpeed = 720f;

    struct Coin
    {
        public float startTime;
        public Vector3 popPos;
        public Vector3 arcOffset;
        public float yaw;
        public Watch watch;
        public PhotonPlayer player;
    }

    static readonly Matrix4x4[] matrices = new Matrix4x4[MaxInstancesPerDraw];
    readonly List<Coin> active = new List<Coin>();

    /// <summary>Coins fly to the local player's watch, which counts up as they land.</summary>
    public void Play(int count)
    {
        Watch watch = Watch.Local;
        PhotonPlayer player = watch == null ? PhotonPlayer.Local : null;
        if (watch == null && player == null)
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice1))
                    logNotice1.Write("CoinShower: no local Watch or PhotonPlayer, skipping coin shower.", this);
            }
            return;
        }
        if (Spawn(count, watch, player) && watch != null)
            watch.AddCoinsInFlight(count);
    }

    /// <summary>Coins fly into another player's body.</summary>
    public void PlayTo(int count, PhotonPlayer player)
    {
        if (player == null)
            return;
        Spawn(count, null, player);
    }

    bool Spawn(int count, Watch watch, PhotonPlayer player)
    {
        if (count <= 0)
            return false;
        if (coinMesh == null || coinMaterial == null)
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice2))
                    logNotice2.Write("CoinShower: coinMesh or coinMaterial is not assigned.", this);
            }
            return false;
        }
        if (!coinMaterial.enableInstancing)
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice3))
                    logNotice3.Write($"CoinShower: enable GPU Instancing on {coinMaterial.name}, coins can't draw without it.", coinMaterial);
            }
            return false;
        }

        Vector3 origin = Origin();
        float now = Time.time;
        for (int i = 0; i < count; i++)
        {
            Vector2 ring = Random.insideUnitCircle * popRadius;
            active.Add(new Coin
            {
                startTime = now + spawnSpread * i / count,
                popPos = origin + new Vector3(ring.x, popHeight * Random.Range(0.6f, 1.2f), ring.y),
                arcOffset = (Vector3.up + Random.insideUnitSphere * 0.75f) * flyArc,
                yaw = Random.Range(0f, 360f),
                watch = watch,
                player = player,
            });
        }
        return true;
    }

    Vector3 Origin()
    {
        return spawnPoint != null ? spawnPoint.position : transform.position + Vector3.up * 0.5f;
    }

    void LateUpdate()
    {
        if (active.Count == 0)
            return;

        float now = Time.time;
        Vector3 origin = Origin();
        float flyStart = popDuration + hoverDuration;
        int drawCount = 0;
        Vector3 min = origin;
        Vector3 max = origin;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            Coin coin = active[i];
            float age = now - coin.startTime;
            if (age < 0f)
                continue;

            Vector3 pos;
            float scale;
            if (age < popDuration)
            {
                float u = age / popDuration;
                float e = 1f - (1f - u) * (1f - u) * (1f - u);
                pos = Vector3.LerpUnclamped(origin, coin.popPos, e);
                scale = coinScale * EaseOutBack(u);
            }
            else if (age < flyStart)
            {
                pos = coin.popPos + Vector3.up * (Mathf.Sin((age - popDuration) * 12f) * 0.01f);
                scale = coinScale;
            }
            else
            {
                float u = (age - flyStart) / flyDuration;
                if (u >= 1f || !TryGetTargetPosition(coin, out Vector3 targetPos))
                {
                    Land(i);
                    continue;
                }
                float e = u * u;
                Vector3 control = coin.popPos + coin.arcOffset;
                pos = Bezier(coin.popPos, control, targetPos, e);
                scale = coinScale * Mathf.Lerp(1f, landedScale, e);
            }

            matrices[drawCount++] = Matrix4x4.TRS(pos, Quaternion.Euler(0f, coin.yaw + spinSpeed * age, 0f), Vector3.one * scale);
            min = Vector3.Min(min, pos);
            max = Vector3.Max(max, pos);
            if (drawCount == MaxInstancesPerDraw)
            {
                Draw(drawCount, min, max);
                drawCount = 0;
            }
        }

        if (drawCount > 0)
            Draw(drawCount, min, max);
    }

    void Draw(int count, Vector3 min, Vector3 max)
    {
        Bounds bounds = new Bounds((min + max) * 0.5f, max - min);
        bounds.Expand(coinMesh.bounds.size.magnitude * coinScale * 2f);
        var renderParams = new RenderParams(coinMaterial)
        {
            worldBounds = bounds,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
        };
        Graphics.RenderMeshInstanced(renderParams, coinMesh, 0, matrices, count);
    }

    void Land(int index)
    {
        Watch watch = active[index].watch;
        active.RemoveAt(index);
        if (watch != null)
            watch.CoinLanded();
    }

    static bool TryGetTargetPosition(Coin coin, out Vector3 position)
    {
        if (coin.watch != null)
        {
            position = coin.watch.CoinTarget.position;
            return true;
        }
        if (coin.player != null)
        {
            // The body only follows the pose stream on remote copies; the local copy falls back to the head.
            Transform chest = coin.player.photonView.IsMine ? null : coin.player.Chest;
            if (chest != null)
            {
                position = chest.position;
                return true;
            }
            coin.player.TryGetHeadPosition(out position);
            position += Vector3.down * PlayerChestDrop;
            return true;
        }
        position = default;
        return false;
    }

    void OnDisable()
    {
        // Anything still in the air counts as landed so the watch never gets stuck waiting.
        for (int i = active.Count - 1; i >= 0; i--)
            Land(i);
    }

    static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float s = 1f - t;
        return s * s * a + 2f * s * t * b + t * t * c;
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

#if UNITY_EDITOR
    [ContextMenu("Debug: Play coin shower")]
    void DebugPlay()
    {
        if (!Application.isPlaying)
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice4))
                    logNotice4.Write("CoinShower: enter Play mode first.", this);
            }
            return;
        }
        Play(100);
    }
#endif
}

using System.Collections;
using Game.Audio;
using UnityEngine;
using Photon.Pun;

[DefaultExecutionOrder(-16300)] // Move cabin/passenger before DesktopPlayer (-16200) and VR locomotion.
public class HelicopterManager : MonoBehaviourPunCallbacks, IPunObservable
{
    public static HelicopterManager instance;
    const float LeaveWarningLeadSeconds = 7f;

    enum Phase : byte
    {
        Waiting = 0,
        Landing = 1,
        Staying = 2,
        Departing = 3
    }

    public Transform landingParent;
    Transform[] landingPoints;
    public float spawnHeight = 30f;

    public float spawnInterval = 180f;
    public float spawnTimer = 0;

    public float stayInterval = 120f;
    public float stayTimer = 0;

    [Tooltip("Park on the first landing pad at startup and never depart.")]
    public bool stayForever = true;

    public float landDuration = 10f;

    [SerializeField] Helicopter helicopter;
    [Tooltip("Default ground effect. A HelicopterGroundDust child on a landing point overrides this prefab.")]
    [SerializeField] HelicopterGroundDust groundDustPrefab;
    [SerializeField] SoundEvent leaveWarningSound;
    HelicopterGroundDust[] groundDust;
    HelicopterGroundDust activeGroundDust;

    Phase phase;
    int landingIndex;
    float landTimer;
    float visualLand;
    Phase visualPhase;
    Coroutine schedule;
    bool leaveWarningPlayed;
    int leaveWarningArrivalSerial;
    bool hasCabinPose;
    int cabinArrivalSerial;
    int cabinLandingIndex;

    public void ReleasePassenger()
    {
        if (helicopter != null) helicopter.ReleasePassenger();
    }

    public bool IsSupportCollider(Collider collider) => helicopter != null && helicopter.OwnsSupport(collider);

    public void ReportBodySupport(Transform source, Collider collider, Vector3 point, Vector3 normal)
    {
        if (helicopter != null && helicopter.isActiveAndEnabled) helicopter.ReportBodySupport(source, collider, point, normal);
    }

    public void ReportHandSupport(Transform source, Collider left, Collider right)
    {
        if (helicopter != null && helicopter.isActiveAndEnabled) helicopter.ReportHandSupport(source, left, right);
    }

    public void RefreshPassenger()
    {
        if (hasCabinPose && helicopter != null && helicopter.isActiveAndEnabled) helicopter.RefreshPassenger();
    }

    public bool TryGetPassengerFrame(int arrival, int pad, out Transform frame)
    {
        frame = null;
        if (!isActiveAndEnabled || !hasCabinPose || helicopter == null || !helicopter.gameObject.activeInHierarchy
            || phase == Phase.Waiting || arrival <= 0 || arrival != ArrivalSerial || pad != landingIndex
            || cabinArrivalSerial != arrival || cabinLandingIndex != pad) return false;
        frame = helicopter.transform;
        return true;
    }

    public bool TryGetLocalPassengerFrame(out int arrival, out int pad, out Transform frame)
    {
        arrival = ArrivalSerial;
        pad = landingIndex;
        frame = null;
        return helicopter != null && helicopter.HasLocalPassenger && TryGetPassengerFrame(arrival, pad, out frame);
    }

    public bool inHelicopter = false;
    public int ArrivalSerial { get; private set; }
    public bool IsAvailableForVisit => isActiveAndEnabled && helicopter != null
        && landingPoints != null && landingIndex >= 0 && landingIndex < landingPoints.Length
        && landingPoints[landingIndex] != null && (phase == Phase.Landing || phase == Phase.Staying);

    public bool IsLanding => IsAvailableForVisit && phase == Phase.Landing;

    // Host-owned schedule, including the remaining departure animation.
    public bool TryGetNextArrivalSeconds(out float seconds)
    {
        seconds = 0f;
        if (!isActiveAndEnabled || helicopter == null || stayForever || landingPoints == null
            || !System.Array.Exists(landingPoints, point => point != null) || IsAvailableForVisit) return false;
        seconds = phase == Phase.Departing
            ? Mathf.Max(0f, landDuration - landTimer) + Mathf.Max(0f, spawnInterval)
            : Mathf.Max(0f, spawnTimer > 0f ? spawnTimer : (schedule == null ? spawnInterval : 0f));
        return true;
    }

    void Awake() {
        instance = this;
        CacheLandingPoints();
        groundDust = new HelicopterGroundDust[landingPoints.Length];
        for (int i = 0; i < landingPoints.Length; i++)
        {
            groundDust[i] = landingPoints[i].GetComponentInChildren<HelicopterGroundDust>(true);
            if (groundDust[i] == null && groundDustPrefab != null)
                groundDust[i] = Instantiate(groundDustPrefab, landingPoints[i], false);
        }
    }

    void CacheLandingPoints()
    {
        if (landingParent == null)
        {
            landingPoints = new Transform[0];
            return;
        }

        int count = landingParent.childCount;
        landingPoints = new Transform[count];
        for (int i = 0; i < count; i++)
            landingPoints[i] = landingParent.GetChild(i);
    }

    void Start()
    {
        if (stayForever)
        {
            Park();
            return;
        }

        helicopter.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!stayForever && PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
        {
            if (schedule == null)
                schedule = StartCoroutine(AdvanceSchedule());
        }
        else if (schedule != null)
        {
            StopCoroutine(schedule);
            schedule = null;
        }

        if (stayForever)
            Park();

        TryPlayLeaveWarning();
        ApplyLocal();
    }

    public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
    {
        // A former host may have slept through several changes. Its coroutine's
        // program counter no longer matches the replicated phase/timers.
        StopSchedule();
    }

    void StopSchedule()
    {
        if (schedule != null) StopCoroutine(schedule);
        schedule = null;
    }

    void TryPlayLeaveWarning()
    {
        if (stayForever || leaveWarningSound == null || helicopter == null)
            return;

        if (phase != Phase.Staying || stayTimer > LeaveWarningLeadSeconds)
        {
            if (phase != Phase.Staying)
                leaveWarningPlayed = false;
            return;
        }

        if (leaveWarningPlayed && leaveWarningArrivalSerial == ArrivalSerial)
            return;

        leaveWarningPlayed = true;
        leaveWarningArrivalSerial = ArrivalSerial;
        if (AudioManager.instance != null)
            AudioManager.instance.PlayAttached(leaveWarningSound, helicopter.transform, this);
    }

    void Park()
    {
        if (phase != Phase.Staying || ArrivalSerial == 0) ArrivalSerial++;
        landingIndex = 0;
        landTimer = landDuration;
        visualLand = landDuration;
        stayTimer = stayInterval;
        phase = Phase.Staying;
        visualPhase = Phase.Staying;
    }

    IEnumerator AdvanceSchedule()
    {
        if (phase == Phase.Waiting && spawnTimer <= 0f)
            spawnTimer = spawnInterval;

        while (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
        {
            if (phase == Phase.Waiting)
            {
                while (spawnTimer > 0f)
                {
                    yield return new WaitForSeconds(1f);
                    if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) yield break;
                    spawnTimer -= 1f;
                }

                landingIndex = Random.Range(0, landingPoints.Length);
                landTimer = 0f;
                stayTimer = 0f;
                phase = Phase.Landing;
                ArrivalSerial++;
            }
            else if (phase == Phase.Landing)
            {
                while (landTimer < landDuration)
                {
                    yield return new WaitForSeconds(1f);
                    if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) yield break;
                    landTimer += 1f;
                }

                landTimer = landDuration;
                stayTimer = stayInterval;
                phase = Phase.Staying;
            }
            else if (phase == Phase.Staying)
            {
                while (stayTimer > 0f)
                {
                    yield return new WaitForSeconds(1f);
                    if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) yield break;
                    stayTimer -= 1f;
                }

                landTimer = 0f;
                phase = Phase.Departing;
            }
            else
            {
                while (landTimer < landDuration)
                {
                    yield return new WaitForSeconds(1f);
                    if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) yield break;
                    landTimer += 1f;
                }

                phase = Phase.Waiting;
                spawnTimer = spawnInterval;
                stayTimer = 0f;
                landTimer = 0f;
            }
        }
    }

    void ApplyLocal()
    {
        if (phase != visualPhase)
        {
            visualPhase = phase;
            visualLand = landTimer;
        }

        if (phase == Phase.Waiting)
        {
            hasCabinPose = false;
            StopGroundDust();
            if (helicopter.gameObject.activeSelf)
                helicopter.gameObject.SetActive(false);
            return;
        }

        if (landingIndex < 0 || landingIndex >= landingPoints.Length)
        {
            hasCabinPose = false;
            ReleasePassenger();
            StopGroundDust();
            return;
        }

        Transform pad = landingPoints[landingIndex];
        if (pad == null)
        {
            hasCabinPose = false;
            ReleasePassenger();
            StopGroundDust();
            return;
        }

        bool continuous = hasCabinPose && helicopter.gameObject.activeSelf
            && cabinArrivalSerial == ArrivalSerial && cabinLandingIndex == landingIndex;
        if (!helicopter.gameObject.activeSelf)
            helicopter.gameObject.SetActive(true);

        Vector3 landEnd = pad.position;
        Vector3 landStart = landEnd + Vector3.up * spawnHeight;

        if (phase == Phase.Landing || phase == Phase.Departing)
        {
            helicopter.StartPropeller();
            visualLand += Time.deltaTime;
            float ahead = landTimer + 1f;
            if (ahead > landDuration)
                ahead = landDuration;
            if (visualLand > ahead)
                visualLand = ahead;
            if (visualLand < landTimer)
                visualLand = landTimer;
        }

        Vector3 position;
        if (phase == Phase.Landing)
            position = Vector3.Lerp(landStart, landEnd, Blend(visualLand));
        else if (phase == Phase.Staying) {
            helicopter.StopPropeller();
            position = landEnd;
        } else
            position = Vector3.Lerp(landEnd, landStart, Blend(visualLand));

        helicopter.MoveCabin(position, pad.rotation, continuous);
        hasCabinPose = true;
        cabinArrivalSerial = ArrivalSerial;
        cabinLandingIndex = landingIndex;

        HelicopterGroundDust dust = groundDust[landingIndex];
        if (activeGroundDust != dust)
        {
            StopGroundDust();
            activeGroundDust = dust;
        }
        // Use the landing animation before cosmetic bobbing, including full wash while parked.
        if (activeGroundDust != null)
            activeGroundDust.SetHeight(position.y - landEnd.y);
    }

    void StopGroundDust()
    {
        if (activeGroundDust != null)
            activeGroundDust.StopDust();
        activeGroundDust = null;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        hasCabinPose = false;
        ReleasePassenger();
        StopSchedule();
        StopGroundDust();
        if (groundDust == null) return;
        foreach (var dust in groundDust)
            if (dust != null) dust.StopDust(true);
    }

    float Blend(float timer)
    {
        float progress = landDuration <= 0f
            ? 1f
            : Mathf.Clamp01(timer / landDuration);

        return Mathf.SmoothStep(0f, 1f, progress);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(spawnTimer);
            stream.SendNext(stayTimer);
            stream.SendNext(landTimer);
            stream.SendNext(landingIndex);
            stream.SendNext((byte)phase);
            stream.SendNext(ArrivalSerial);
            return;
        }

        spawnTimer = (float)stream.ReceiveNext();
        stayTimer = (float)stream.ReceiveNext();
        landTimer = (float)stream.ReceiveNext();
        landingIndex = (int)stream.ReceiveNext();
        phase = (Phase)(byte)stream.ReceiveNext();
        ArrivalSerial = (int)stream.ReceiveNext();
        if (stayForever)
            Park();
    }
}

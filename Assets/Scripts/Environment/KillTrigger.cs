using UnityEngine;

[DefaultExecutionOrder(11000)] // After PhotonGrabbable's weapon sweeps (10000).
public class KillTrigger : MonoBehaviour
{
    readonly System.Collections.Generic.HashSet<Collider> deferredMeleeContacts = new();
    readonly System.Collections.Generic.List<Collider> pendingEnemyContacts = new();
    public event System.Action PlayerKilled;
    // Optional companion weapon filter; ordinary environmental/enemy triggers are unchanged.
    public System.Func<Collider, bool> ContactAllowed;
    public DuckCompanion CompanionKiller;
    public int CompanionAttack;

    private void OnTriggerEnter(Collider other) {
        QueueOrContact(other);
    }

    private void OnTriggerStay(Collider other) {
        // Retry only contacts deferred by melee grace. Other kill triggers retain
        // their entry-only behavior, including companion weapons and hazards.
        if (deferredMeleeContacts.Contains(other)) QueueOrContact(other);
    }

    void QueueOrContact(Collider other)
    {
        // Weapon sweeps run in LateUpdate. Resolve enemy/player contact after
        // those sweeps so a same-frame defensive hit wins the ordering race.
        if (other != null && other.attachedRigidbody != null
            && other.attachedRigidbody.CompareTag("Player")
            && GetComponentInParent<Game.AI.Enemy>() != null)
        {
            if (!pendingEnemyContacts.Contains(other)) pendingEnemyContacts.Add(other);
        }
        else TryContact(other);
    }

    void LateUpdate()
    {
        // A kill listener can disable this trigger and clear the queue.
        while (pendingEnemyContacts.Count > 0)
        {
            int last = pendingEnemyContacts.Count - 1;
            Collider contact = pendingEnemyContacts[last];
            pendingEnemyContacts.RemoveAt(last);
            TryContact(contact);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        deferredMeleeContacts.Remove(other);
        pendingEnemyContacts.Remove(other);
    }
    private void OnDisable()
    {
        deferredMeleeContacts.Clear();
        pendingEnemyContacts.Clear();
    }

    public bool TryContact(Collider other) {
        using var sample = DeathProfile.Contact.Auto();
        DeathProfile.Contacts++;
        if (!isActiveAndEnabled || other == null || (ContactAllowed != null && !ContactAllowed(other))) return false;
        // Physics can queue more contacts before the explosion disables the colliders.
        var enemy = GetComponentInParent<Game.AI.Enemy>();
        if (enemy != null && enemy.IsDormant) return false;
        var duck = other.GetComponentInParent<DuckCompanion>();
        if (duck != null)
        {
            var spider = enemy != null ? enemy.Gait as Game.Animation.SpiderGait : null;
            Vector3 scarPosition = spider != null ? other.bounds.center : default;
            bool killed = duck.TryKill();
            if (killed)
            {
                if (spider != null) spider.ObserveKillScar(scarPosition);
                if (enemy != null) enemy.SubmitPlayerKill(duck.transform.position);
                DeathProfile.Accepted++;
                using (DeathProfile.Listeners.Auto()) PlayerKilled?.Invoke();
            }
            return killed;
        }
        if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player") && GR.Player.instance != null && !GR.Player.instance.IsDead) {
            if (enemy != null && enemy.ProtectsLocalPlayer)
            {
                deferredMeleeContacts.Add(other);
                return false;
            }
            deferredMeleeContacts.Remove(other);
            var spider = enemy != null ? enemy.Gait as Game.Animation.SpiderGait : null;
            Vector3 scarPosition = spider != null ? other.bounds.center : default;
            if (CompanionKiller != null) GR.Player.instance.SetDeadFromDuck(CompanionKiller, CompanionAttack);
            else if (enemy != null)
            {
                Vector3 impact = enemy.PlayerDeathLaunch(GR.Player.instance.transform.position,
                    enemy.Root != null ? enemy.Root.position : enemy.transform.position);
                GR.Player.instance.SetDead(enemy.gameObject.name, impact, enemy.PlayerDeathRotation(impact));
            }
            else GR.Player.instance.SetDead("hazard");
            if (spider != null) spider.ObserveKillScar(scarPosition);
            if (enemy != null) enemy.SubmitPlayerKill(other.attachedRigidbody.position);
            DeathProfile.Accepted++;
            using (DeathProfile.Listeners.Auto()) PlayerKilled?.Invoke();
            return true;
        }
        return false;
    }
}

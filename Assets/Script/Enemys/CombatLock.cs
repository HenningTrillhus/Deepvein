using System.Reflection;
using UnityEngine;

/// <summary>
/// "Er spilleren i en kamp?" Brukes av trehogging, steinhakking og plukking:
///   - en fiende har sett deg og følger etter (EnemyAI er i Chase), eller
///   - en levende fiende er nærmere enn Near Distance (units; en tile = 1 unit).
/// Er det sånn, kan man ikke hogge trær, hakke steiner eller plukke props: trærne blir ikke hvite, [E] kommer ikke opp, og et klikk er bare et slag.
/// Ingenting må settes opp: CombatLock.Active kan alltid leses. Vil du endre avstanden, legg komponenten CombatLockSettings på spilleren.
/// </summary>
public static class CombatLock
{
    /// <summary>Nærmere enn dette (units) til en levende fiende = kamp.</summary>
    public static float NearDistance = 4f;
    /// <summary>Fiender som har sett deg og følger etter = kamp, uansett avstand.</summary>
    public static bool ChaseBlocks = true;
    /// <summary>Hvor ofte (sekunder) listen over fiender sjekkes.</summary>
    public static float CheckEvery = 0.1f;

    static float nextCheck = -1f;
    static bool active;
    static string reason = "";
    static Transform player;
    static FieldInfo stateField;
    static bool triedField;

    public static bool Active { get { Refresh(); return active; } }
    /// <summary>Hvorfor det er kamp akkurat nå (til feilsøking).</summary>
    public static string Reason { get { Refresh(); return reason; } }

    static bool IsChasing(EnemyAI ai)
    {
        if (!triedField)
        {
            triedField = true;
            stateField = typeof(EnemyAI).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
        }
        if (stateField == null) return false;
        object v = stateField.GetValue(ai);
        return v != null && v.ToString() == "Chase";
    }

    static void Refresh()
    {
        if (Time.unscaledTime < nextCheck && Application.isPlaying) return;
        nextCheck = Time.unscaledTime + CheckEvery;
        active = false; reason = "";
        if (player == null)
        {
            var pm = Object.FindFirstObjectByType<PlayerMovement>();
            if (pm != null) player = pm.transform;
        }
        if (player == null) return;
        Vector2 pp = player.position;
        foreach (var ai in Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
        {
            if (ai == null || !ai.isActiveAndEnabled) continue;
            var sm = ai.GetComponent<EnemyStateMachine>();
            if (sm != null && sm.IsDead) continue;
            float d = Vector2.Distance(ai.transform.position, pp);
            if (d <= NearDistance) { active = true; reason = ai.name + " er " + d.ToString("0.0") + " unna"; return; }
            if (ChaseBlocks && IsChasing(ai)) { active = true; reason = ai.name + " følger etter (" + d.ToString("0.0") + " unna)"; return; }
        }
    }
}

/// <summary>Valgfritt: legg på spilleren for å endre kamp-avstanden i Inspector.</summary>
[DisallowMultipleComponent]
public class CombatLockSettings : MonoBehaviour
{
    [Tooltip("Nærmere enn dette (units) til en levende fiende = kamp: ingen hogging, hakking eller plukking, bare slag.")]
    public float nearDistance = 4f;
    [Tooltip("Fiender som har sett deg og følger etter = kamp, uansett avstand.")]
    public bool chaseBlocks = true;

    void Awake() { Apply(); }
    void OnValidate() { Apply(); }
    void Apply() { CombatLock.NearDistance = nearDistance; CombatLock.ChaseBlocks = chaseBlocks; }
}

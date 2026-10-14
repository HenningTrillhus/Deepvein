using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fiendene "forstår hverandre": bare Max Attackers (1) fiende av gangen går på spilleren, de andre venter på avstand og går litt
/// fram og tilbake. Hver fiende som jager melder seg hvert bilde (Join). Regissøren deler ut "angrepsbrikken" (token):
///   - den nærmeste som jager får den først, og den går videre til en annen etter at holderen har slått (litt pause: Pass Delay)
///   - holder en fiende den lenge uten å slå (står fast), går den videre
///   - dør eller slutter fienden å jage, frigjøres den
/// EnemyAI spør HasToken for å gå helt fram, og EnemyAttack slår ikke uten den.
/// Alt er statisk, ingenting må settes opp.
/// </summary>
public static class EnemyDirector
{
    public static int MaxAttackers = 1;
    /// <summary>Pause (sekunder) etter et slag før brikken går til neste fiende.</summary>
    public static float PassDelay = 0.7f;
    /// <summary>Holder en fiende brikken så lenge uten å slå, går den videre.</summary>
    public static float MaxHold = 6f;

    static readonly Dictionary<EnemyAI, float> joined = new Dictionary<EnemyAI, float>();
    static readonly List<EnemyAI> holders = new List<EnemyAI>();
    static readonly Dictionary<EnemyAI, float> heldSince = new Dictionary<EnemyAI, float>();
    static readonly List<EnemyAI> scratch = new List<EnemyAI>();
    static EnemyAI lastHolder;
    static float tokenFreeAt;
    static int lastFrame = -1;

    /// <summary>En fiende som jager spilleren melder seg (kalles hvert bilde mens den jager).</summary>
    public static void Join(EnemyAI ai) { if (ai != null) joined[ai] = Time.time; }

    public static bool HasToken(EnemyAI ai)
    {
        Refresh();
        return ai != null && holders.Contains(ai);
    }

    /// <summary>Fienden er ferdig med et slag: brikken frigjøres og går videre etter en kort pause.</summary>
    public static void AttackFinished(EnemyAI ai)
    {
        if (ai != null && holders.Remove(ai))
        {
            heldSince.Remove(ai);
            lastHolder = ai;
            tokenFreeAt = Time.time + PassDelay;
        }
    }

    public static int WaitingCount { get { Refresh(); return Mathf.Max(0, joined.Count - holders.Count); } }

    static bool Alive(EnemyAI ai)
    {
        if (ai == null || !ai.isActiveAndEnabled) return false;
        var sm = ai.GetComponent<EnemyStateMachine>();
        return sm == null || !sm.IsDead;
    }

    static void Refresh()
    {
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;

        // forget the ones that stopped chasing (they did not Join for a moment), died or were destroyed
        scratch.Clear();
        foreach (var kv in joined)
            if (!Alive(kv.Key) || Time.time - kv.Value > 0.3f) scratch.Add(kv.Key);
        foreach (var ai in scratch) { joined.Remove(ai); }

        for (int i = holders.Count - 1; i >= 0; i--)
        {
            var h = holders[i];
            bool drop = !joined.ContainsKey(h);
            if (!drop && heldSince.TryGetValue(h, out float since) && Time.time - since > MaxHold) { drop = true; lastHolder = h; }
            if (drop) { holders.RemoveAt(i); heldSince.Remove(h); if (!joined.ContainsKey(h)) tokenFreeAt = Mathf.Max(tokenFreeAt, Time.time); }
        }

        // give out free tokens: the nearest one that is not holding, and (if there is a choice) not the one that had it last
        int guard = 0;
        while (holders.Count < MaxAttackers && Time.time >= tokenFreeAt && guard++ < 4)
        {
            EnemyAI best = null; float bestD = float.MaxValue;
            foreach (var kv in joined)
            {
                var ai = kv.Key;
                if (holders.Contains(ai)) continue;
                float d = ai.DistanceToPlayer;
                if (ai == lastHolder && joined.Count - holders.Count > 1) d += 100f;       // somebody else gets a turn first
                if (d < bestD) { bestD = d; best = ai; }
            }
            if (best == null) break;
            holders.Add(best); heldSince[best] = Time.time;
        }
    }
}

/// <summary>Spiller og fiender dytter ikke på hverandre: kollisjonen mellom dem skrus av (treff, knockback og blokk virker som før).</summary>
public static class EnemyPhysics
{
    public static void IgnoreOthers(GameObject enemy)
    {
        var mine = enemy.GetComponentsInChildren<Collider2D>();
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            foreach (var p in player.GetComponentsInChildren<Collider2D>())
                foreach (var m in mine) if (p != null && m != null) Physics2D.IgnoreCollision(p, m, true);
        foreach (var other in Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
        {
            if (other == null || other.gameObject == enemy) continue;
            foreach (var o in other.GetComponentsInChildren<Collider2D>())
                foreach (var m in mine) if (o != null && m != null) Physics2D.IgnoreCollision(o, m, true);
        }
    }
}

using System;
using UnityEngine;

/// <summary>
/// Stamina for running, rolling and jumping. Put it on the same object as PlayerMovement (the builder does it).
///   Sprint: drains Sprint Cost Per Second (5 of 100) for as long as you sprint.
///   Roll: costs Roll Cost (30).     Jump: costs Jump Cost (15), but a jump is never blocked.
///   Regen: starts Regen Delay seconds (1) after the last time you spent stamina, then eases up to Regen Per Second (15)
///   over Regen Ramp Time seconds, so it starts gently instead of snapping on.
///   Empty: you are exhausted and cannot sprint or roll until it is back above Exhausted Until.
/// The shield has its own blue bar (PlayerBlock), this one is separate.
/// </summary>
[DisallowMultipleComponent]
public class PlayerStamina : MonoBehaviour
{
    [Header("Mengde")]
    public float max = 100f;

    [Header("Kostnad")]
    [Tooltip("Per sekund mens du løper (Shift).")]
    public float sprintCostPerSecond = 5f;
    public float rollCost = 30f;
    public float jumpCost = 15f;
    [Tooltip("Hver gang han klatrer opp på en kant (fra bakken eller i lufta).")]
    public float climbCost = 30f;

    [Header("Fylles opp igjen")]
    [Tooltip("Sekunder etter at du sist brukte stamina før den begynner å stige.")]
    public float regenDelay = 1f;
    public float regenPerSecond = 15f;
    [Tooltip("Sekunder fra regenereringen starter til den går for full fart. Mykere start. 0 = brå.")]
    public float regenRampTime = 0.6f;
    [Tooltip("Er den tom, må den opp til dette før du kan løpe eller rulle igjen.")]
    public float exhaustedUntil = 15f;

    public float Current { get; private set; }
    public float Percent { get { return max > 0f ? Current / max : 0f; } }
    public bool IsExhausted { get; private set; }
    /// <summary>Seconds since stamina was last spent (used to fade the bar).</summary>
    public float TimeSinceSpent { get; private set; } = 999f;
    public event Action Changed;

    [SerializeField, HideInInspector] int settingsVersion;
    float regenRamp;

    PlayerMovement movement;

    void Awake()
    {
        Current = max;
        movement = GetComponent<PlayerMovement>();
    }

    public bool CanSprint { get { return !IsExhausted && Current > 0f; } }
    public bool CanRoll { get { return !IsExhausted && Current > 0f; } }
    public bool CanClimb { get { return !IsExhausted && Current > 0f; } }

    /// <summary>Takes stamina (never below 0). Resets the regen delay.</summary>
    public void Spend(float amount)
    {
        if (amount <= 0f) return;
        Current = Mathf.Max(0f, Current - amount);
        TimeSinceSpent = 0f;
        if (Current <= 0f) IsExhausted = true;
        if (Changed != null) Changed();
    }

    public void SpendRoll() { Spend(rollCost); }
    public void SpendJump() { Spend(jumpCost); }
    public void SpendClimb() { Spend(climbCost); }

    /// <summary>Sets it directly (for a potion, level up or a test).</summary>
    public void SetCurrent(float value)
    {
        Current = Mathf.Clamp(value, 0f, max);
        if (Current >= exhaustedUntil) IsExhausted = false;
        if (Changed != null) Changed();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        TimeSinceSpent += dt;

        if (movement != null && movement.IsSprinting)
            Spend(sprintCostPerSecond * dt);

        if (TimeSinceSpent >= regenDelay && Current < max)
        {
            // eases up to full speed instead of starting at full speed
            regenRamp = regenRampTime <= 0.001f ? 1f : Mathf.MoveTowards(regenRamp, 1f, dt / regenRampTime);
            float k = regenRamp * regenRamp * (3f - 2f * regenRamp);          // smoothstep
            Current = Mathf.Min(max, Current + regenPerSecond * k * dt);
            if (IsExhausted && Current >= exhaustedUntil) IsExhausted = false;
            if (Changed != null) Changed();
        }
        else if (TimeSinceSpent < regenDelay) regenRamp = 0f;
    }

#if UNITY_EDITOR
    // Players that already exist in the scene have the old value (10) saved; move them to the new default once.
    void OnValidate()
    {
        if (settingsVersion < 2) { regenPerSecond = 15f; settingsVersion = 2; }
    }
#endif
}

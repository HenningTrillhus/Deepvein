using System;
using UnityEngine;

/// <summary>
/// Stamina for running, rolling and jumping. Put it on the same object as PlayerMovement (the builder does it).
///   Sprint: drains Sprint Cost Per Second (5 of 100) for as long as you sprint.
///   Roll: costs Roll Cost (30).     Jump: costs Jump Cost (15), but a jump is never blocked.
///   Regen: starts Regen Delay seconds (1) after the last time you spent stamina, then Regen Per Second (10).
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

    [Header("Fylles opp igjen")]
    [Tooltip("Sekunder etter at du sist brukte stamina før den begynner å stige.")]
    public float regenDelay = 1f;
    public float regenPerSecond = 10f;
    [Tooltip("Er den tom, må den opp til dette før du kan løpe eller rulle igjen.")]
    public float exhaustedUntil = 15f;

    public float Current { get; private set; }
    public float Percent { get { return max > 0f ? Current / max : 0f; } }
    public bool IsExhausted { get; private set; }
    /// <summary>Seconds since stamina was last spent (used to fade the bar).</summary>
    public float TimeSinceSpent { get; private set; } = 999f;
    public event Action Changed;

    PlayerMovement movement;

    void Awake()
    {
        Current = max;
        movement = GetComponent<PlayerMovement>();
    }

    public bool CanSprint { get { return !IsExhausted && Current > 0f; } }
    public bool CanRoll { get { return !IsExhausted && Current > 0f; } }

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
            Current = Mathf.Min(max, Current + regenPerSecond * dt);
            if (IsExhausted && Current >= exhaustedUntil) IsExhausted = false;
            if (Changed != null) Changed();
        }
    }
}

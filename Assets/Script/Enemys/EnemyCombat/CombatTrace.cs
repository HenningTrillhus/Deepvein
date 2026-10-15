using UnityEngine;

/// <summary>
/// Hvem slo egentlig? Settes av den som slår rett før TakeDamage kalles, så den som blir truffet (skjold, parry) vet nøyaktig hvilken
/// fiende det var, i stedet for å gjette "den nærmeste". Er alltid null utenfor selve TakeDamage-kallet.
/// </summary>
public static class CombatTrace
{
    public static GameObject Attacker;

    public static void Hit(IDamageable target, GameObject attacker, int damage, Vector2 knockback)
    {
        GameObject old = Attacker;
        Attacker = attacker;
        try { target.TakeDamage(damage, knockback); }
        finally { Attacker = old; }
    }
}

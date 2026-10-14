using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using DeepVain.Items;
using DeepVain.Levels;

/// <summary>
/// Plukking: står spilleren rett ved noe plukkbart (Forageable) kommer et [E]-merke over det. Trykk E:
/// spilleren snur seg mot det, spiller plukke-animasjonen (PickUp), og midt i animasjonen havner tingene i sekken
/// med en melding i hjørnet (som ved kister). Planten bytter til plukket tilstand (eller forsvinner, for sopp).
///
/// Legges på spilleren (samme objekt som PlayerMovement). Settes opp av menyen DeepVain > Props > Build forageables.
/// Mens han plukker holdes han i ro (via PlayerMovement.SetForcedMove med en tilnærmet null-verdi), og hopp avbryter plukkingen.
/// Er sekken full, plukkes ingenting (planten beholder det den har).
/// </summary>
[DisallowMultipleComponent]
public class ForagePicker : MonoBehaviour
{
    [Header("Tast og rekkevidde")]
    public Key pickKey = Key.E;
    [Tooltip("Hvor langt til siden (units) fra spilleren planten kan stå.")]
    public float reach = 1.2f;
    [Tooltip("Hvor langt opp/ned (units).")]
    public float reachY = 1.6f;

    [Header("Animasjon")]
    [Tooltip("Sekunder fra E til tingene havner i sekken (midt i PickUp-animasjonen).")]
    public float giveDelay = 0.35f;
    [Tooltip("Sekunder hele plukkingen varer (lengden på PickUp-animasjonen, 0.62).")]
    public float totalTime = 0.62f;

    [Header("Feilsøking")]
    [Tooltip("Skriver i Console hvorfor ingenting ble plukket når du trykker E uten at noe skjer.")]
    public bool debugLog = true;

    [Header("[E]-merket")]
    public bool showPrompt = true;
    public string promptSortingLayer = "Default";
    public int promptSortingOrder = 50;

    PlayerMovement movement;
    PlayerAnimator anim;
    PlayerAttack attack;
    PlayerBlock block;
    PlayerHealth health;
    TreeChopper chopper;
    Inventory inventory;
    Rigidbody2D rb;

    bool busy, cancel;
    Forageable current;
    GameObject prompt;
    Sprite promptSprite;

    public bool IsPicking { get { return busy; } }

    /// <summary>Kommer hver gang noe er plukket og lagt i sekken (planten, antall). LevelXp gir Foraging-XP.</summary>
    public event System.Action<Forageable, int> Picked;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        anim = GetComponent<PlayerAnimator>();
        attack = GetComponent<PlayerAttack>();
        block = GetComponent<PlayerBlock>();
        health = GetComponent<PlayerHealth>();
        chopper = GetComponent<TreeChopper>();
        rb = GetComponent<Rigidbody2D>();
        inventory = GetComponentInParent<Inventory>();
        if (inventory == null) inventory = FindFirstObjectByType<Inventory>();
    }

    void OnEnable() { if (movement != null) movement.PlayerInput += OnPlayerInput; }
    void OnDisable()
    {
        if (movement != null) { movement.PlayerInput -= OnPlayerInput; movement.SetForcedMove(0f); movement.SetForcedFacing(0); }
        busy = false;
        if (prompt != null) prompt.SetActive(false);
    }

    void OnPlayerInput() { if (busy) cancel = true; }      // a jump (or a key) stops it; walking keys are ignored while picking

    bool CanPick()
    {
        if (busy) return false;
        if (CombatLock.Active) return false;                      // an enemy sees you / is close: no [E] mark and E does nothing
        if (movement != null && (movement.IsClimbing || movement.IsLedgeClimbing || movement.IsRolling || movement.IsCrouching || !movement.IsGrounded)) return false;
        if (attack != null && attack.IsAttacking) return false;
        if (block != null && block.IsBlocking) return false;
        if (health != null && (health.IsStunned || health.IsDead)) return false;
        if (chopper != null && chopper.IsBusyWithTree) return false;
        return true;
    }

    Forageable Nearest()
    {
        Forageable best = null; float bestD = float.MaxValue;
        Vector3 p = transform.position;
        foreach (var f in Forageable.All)
        {
            if (f == null || f.IsPicked) continue;
            float dx = Mathf.Abs(f.transform.position.x - p.x), dy = Mathf.Abs(f.transform.position.y - p.y);
            if (dx > reach || dy > reachY) continue;
            if (dx < bestD) { bestD = dx; best = f; }
        }
        return best;
    }

    void Update()
    {
        var kb = Keyboard.current;
        bool pressed = kb != null && kb[pickKey].wasPressedThisFrame;
        if (busy && CombatLock.Active) cancel = true;            // a fight starts while picking: stop
        var target = busy ? null : Nearest();
        bool can = CanPick();
        UpdatePrompt(target != null && can ? target : null);
        if (pressed && !busy && debugLog && (target == null || !can)) Diagnose(target, can);
        if (target == null || !can) return;
        if (pressed) StartCoroutine(PickRoutine(target));
    }

    /// <summary>Tells in the Console why E did nothing: how many forageables exist, how far the nearest is, and what blocks picking.</summary>
    void Diagnose(Forageable target, bool can)
    {
        Forageable nearest = null; float nd = float.MaxValue;
        foreach (var f in Forageable.All)
        {
            if (f == null) continue;
            float d = Mathf.Abs(f.transform.position.x - transform.position.x);
            if (d < nd) { nd = d; nearest = f; }
        }
        string msg = "[ForagePicker] E ble trykket, men ingenting ble plukket. Plukkbare i scenen: " + Forageable.All.Count;
        if (nearest == null) msg += ". Ingen er registrert (har prefaben Forageable på seg, og er den aktiv?).";
        else
        {
            float dy = Mathf.Abs(nearest.transform.position.y - transform.position.y);
            msg += ". Nærmeste: \"" + nearest.name + "\" " + nd.ToString("0.0") + " til siden og " + dy.ToString("0.0") + " opp/ned"
                 + " (rekkevidde " + reach.ToString("0.0") + " / " + reachY.ToString("0.0") + ")" + (nearest.IsPicked ? ", allerede plukket" : "")
                 + (nearest.item == null ? ", MANGLER Item" : "") + ".";
        }
        if (!can)
        {
            var why = new System.Collections.Generic.List<string>();
            if (movement != null)
            {
                if (movement.IsClimbing) why.Add("klatrer"); if (movement.IsLedgeClimbing) why.Add("kantklatrer"); if (movement.IsRolling) why.Add("ruller");
                if (movement.IsCrouching) why.Add("bøyd"); if (!movement.IsGrounded) why.Add("ikke på bakken");
            }
            if (attack != null && attack.IsAttacking) why.Add("slår");
            if (block != null && block.IsBlocking) why.Add("blokkerer");
            if (health != null && (health.IsStunned || health.IsDead)) why.Add("stunnet/død");
            if (chopper != null && chopper.IsBusyWithTree) why.Add("hugger");
            msg += " Kan ikke plukke nå: " + string.Join(", ", why.ToArray()) + ".";
        }
        Debug.Log(msg, this);
    }

    IEnumerator PickRoutine(Forageable plant)
    {
        busy = true; cancel = false; current = plant;
        int dir = plant.transform.position.x < transform.position.x ? -1 : 1;
        if (movement != null)
        {
            movement.SetForcedFacing(dir);
            movement.SetForcedMove(0.002f);                  // "stand still": ignores the walking keys for a moment
        }
        if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (anim != null) anim.PickUp();

        float t = 0f;
        float speed = Mathf.Clamp(1f - PlayerLevels.Bonus("forage_speed"), 0.4f, 1f);       // skill tree: Quick Hands ...
        float giveAt = giveDelay * speed, doneAt = totalTime * (0.5f + 0.5f * speed);
        while (t < giveAt && !cancel) { t += Time.deltaTime; yield return null; }

        if (!cancel && plant != null && !plant.IsPicked)
        {
            int amount = plant.RollAmount() + Mathf.RoundToInt(PlayerLevels.Bonus("forage_bonus"));       // skill tree: Keen Eye ...
            int left = inventory != null && plant.item != null ? inventory.Add(plant.item, amount) : amount;
            int got = amount - left;
            if (got > 0)
            {
                LootToastUI.Show(plant.item, got);           // the strip in the corner, like the chests
                plant.Pick();
                if (Picked != null) Picked(plant, got);
            }
            else Debug.Log("[ForagePicker] Sekken er full, så " + (plant.item != null ? plant.item.displayName : "tingen") + " ble ikke plukket.", this);
            if (left > 0 && got > 0) Debug.Log("[ForagePicker] " + left + " fikk ikke plass.", this);
        }

        while (t < doneAt && !cancel) { t += Time.deltaTime; yield return null; }

        if (movement != null) { movement.SetForcedMove(0f); movement.SetForcedFacing(0); }
        busy = false; current = null;
    }

    // ---- the [E] mark
    void UpdatePrompt(Forageable target)
    {
        if (!showPrompt) { if (prompt != null) prompt.SetActive(false); return; }
        if (target == null) { if (prompt != null) prompt.SetActive(false); return; }
        if (prompt == null) CreatePrompt();
        prompt.SetActive(true);
        var sr = target.GetComponentInChildren<SpriteRenderer>();
        float top = sr != null ? sr.bounds.max.y : target.transform.position.y + target.promptHeight;
        prompt.transform.position = new Vector3(target.transform.position.x, top + 0.3f, 0f);
    }

    void CreatePrompt()
    {
        prompt = new GameObject("ForagePrompt");
        var sr = prompt.AddComponent<SpriteRenderer>();
        if (promptSprite == null) promptSprite = MakeKeySprite();
        sr.sprite = promptSprite;
        sr.sortingLayerName = promptSortingLayer;
        sr.sortingOrder = promptSortingOrder;
    }

    /// <summary>A small pixel-art key cap with the letter E, drawn here so no picture is needed.</summary>
    static Sprite MakeKeySprite()
    {
        const int S = 13;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var clear = new Color32(0, 0, 0, 0);
        var border = new Color32(0x16, 0x0d, 0x1c, 255);
        var cap = new Color32(0xf0, 0xe4, 0xc8, 255);
        var capD = new Color32(0xc8, 0xb0, 0x88, 255);
        var ink = new Color32(0x3a, 0x24, 0x10, 255);
        var px = new Color32[S * S];
        for (int i = 0; i < px.Length; i++) px[i] = clear;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                bool edge = x == 0 || y == 0 || x == S - 1 || y == S - 1;
                bool corner = (x == 0 || x == S - 1) && (y == 0 || y == S - 1);
                if (corner) continue;
                px[y * S + x] = edge ? border : (y <= 2 ? capD : cap);
            }
        // the letter E, 5 wide and 7 tall, in the middle
        string[] E = { "#####", "#....", "#....", "####.", "#....", "#....", "#####" };
        for (int r = 0; r < 7; r++)
            for (int c = 0; c < 5; c++)
                if (E[r][c] == '#') px[(10 - r) * S + (4 + c)] = ink;
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0f), 40f);
    }
}

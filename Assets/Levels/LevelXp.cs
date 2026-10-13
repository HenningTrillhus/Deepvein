using UnityEngine;

namespace DeepVain.Levels
{
    /// <summary>
    /// Gir XP for det spilleren gjør. Legges på spilleren (ved PlayerLevels). Lytter på:
    ///   RockMiner.RockBroken     -> Mining      (kullstein gir mer)
    ///   TreeChopper.TreeFelled   -> Woodcutting
    ///   PlayerAttack.EnemyHit    -> Combat      (per treff)
    ///   løping på bakken         -> Running     (per distanse)
    ///   ForagePicker.Picked      -> Foraging    (bær, sopp, urter, bomull: alt som plukkes med E)
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelXp : MonoBehaviour
    {
        [Header("XP per ting")]
        public int miningPerRock = 8;
        public int miningPerCoalRock = 14;
        public int woodcuttingPerTree = 10;
        public int combatPerHit = 3;
        [Tooltip("XP hver gang man plukker noe med E (bær, sopp, urter, bomull).")]
        public int foragingPerPick = 6;
        [Tooltip("Hvor mange units man må løpe for 1 Running-XP.")]
        public float unitsPerRunningXp = 8f;

        public static LevelXp Instance { get; private set; }

        PlayerLevels levels;
        RockMiner miner;
        TreeChopper chopper;
        PlayerAttack attack;
        ForagePicker picker;
        PlayerMovement movement;
        Rigidbody2D rb;
        float lastX, runAccum;

        void Awake()
        {
            Instance = this;
            levels = GetComponent<PlayerLevels>();
            if (levels == null) levels = FindFirstObjectByType<PlayerLevels>();
            miner = GetComponent<RockMiner>();
            chopper = GetComponent<TreeChopper>();
            attack = GetComponent<PlayerAttack>();
            picker = GetComponent<ForagePicker>();
            movement = GetComponent<PlayerMovement>();
            rb = GetComponent<Rigidbody2D>();
            lastX = transform.position.x;
        }

        void OnEnable()
        {
            if (miner != null) miner.RockBroken += OnRock;
            if (chopper != null) chopper.TreeFelled += OnTree;
            if (attack != null) attack.EnemyHit += OnEnemyHit;
            if (picker != null) picker.Picked += OnPicked;
        }

        void OnDisable()
        {
            if (miner != null) miner.RockBroken -= OnRock;
            if (chopper != null) chopper.TreeFelled -= OnTree;
            if (attack != null) attack.EnemyHit -= OnEnemyHit;
            if (picker != null) picker.Picked -= OnPicked;
        }

        void OnPicked(Forageable plant, int amount) { if (levels != null) levels.AddXp(SkillType.Foraging, foragingPerPick); }

        void OnRock(GameObject rock)
        {
            if (levels == null) return;
            var m = rock != null ? rock.GetComponent<Mineable>() : null;
            levels.AddXp(SkillType.Mining, m != null && m.coal ? miningPerCoalRock : miningPerRock);
        }

        void OnTree(GameObject tree) { if (levels != null) levels.AddXp(SkillType.Woodcutting, woodcuttingPerTree); }

        void OnEnemyHit(GameObject enemy) { if (levels != null) levels.AddXp(SkillType.Combat, combatPerHit); }

        void Update()
        {
            if (levels == null) return;
            float x = transform.position.x;
            float dx = Mathf.Abs(x - lastX);
            lastX = x;
            if (dx > 1f) return;                                   // teleport / chunk jump
            bool grounded = movement == null || movement.IsGrounded;
            if (grounded && dx > 0f) runAccum += dx;
            if (runAccum >= unitsPerRunningXp)
            {
                int n = Mathf.FloorToInt(runAccum / unitsPerRunningXp);
                runAccum -= n * unitsPerRunningXp;
                levels.AddXp(SkillType.Running, n);
            }
        }
    }
}

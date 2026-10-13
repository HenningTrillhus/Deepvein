using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepVain.Levels
{
    /// <summary>
    /// Nivåene til spilleren: XP og nivå per skill, hovednivå, skill-poeng og hvilke noder i skill-treet som er låst opp.
    /// Legges på spilleren (samme objekt som Inventory). Lagres automatisk (PlayerPrefs) og leses inn ved start.
    ///   AddXp(skill, n)         gir XP (kalles av LevelXp når man hakker, hugger, slår, løper)
    ///   Bonus("mine_speed")     summen av effektene til alle opplåste noder med den nøkkelen (0 hvis ingen)
    /// XP-kurven: fra nivå L til L+1 trengs 60 + 40*L XP (100, 140, 180 ...). Hvert nivå i et skill gir 1 skill-poeng til skillets tre.
    /// Hovednivået får all XP fra alle skills: fra nivå L til L+1 trengs 150 + 100*L XP.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerLevels : MonoBehaviour
    {
        public static PlayerLevels Instance { get; private set; }

        public const int MaxSkillLevel = 30, MaxMainLevel = 50;
        const string PrefsKey = "DeepVain.Levels";

        [Header("Skills (lages av menyen DeepVain > Levels > 1)")]
        public SkillDef[] skills;

        [Header("Lagring og test")]
        [Tooltip("Lagre og last inn mellom spilløkter.")]
        public bool persist = true;
        [Tooltip("F9 gir 60 XP til alle skills (for å teste). Skru av før du gir ut spillet.")]
        public bool debugKeyF9 = true;

        readonly int[] xp = new int[Enum.GetValues(typeof(SkillType)).Length];
        int mainXp;
        readonly HashSet<string> unlocked = new HashSet<string>();      // "Mining:mine_speed_1"

        public event Action OnChanged;
        public event Action<SkillType, int> OnXpGained;                 // skill, amount
        public event Action<SkillType, int> OnSkillLevelUp;             // skill, new level
        public event Action<int> OnMainLevelUp;

        // ------------------------------------------------------------------ curve
        public static int XpToNext(int level) { return 60 + 40 * Mathf.Max(1, level); }
        public static int MainXpToNext(int level) { return 150 + 100 * Mathf.Max(1, level); }

        static int LevelOf(int totalXp, int max, Func<int, int> need, out int into, out int toNext)
        {
            int level = 1, left = Mathf.Max(0, totalXp);
            while (level < max && left >= need(level)) { left -= need(level); level++; }
            into = left; toNext = level >= max ? 0 : need(level);
            return level;
        }

        // ------------------------------------------------------------------ reading
        public int GetXp(SkillType s) { return xp[(int)s]; }
        public int GetLevel(SkillType s) { int a, b; return LevelOf(xp[(int)s], MaxSkillLevel, XpToNext, out a, out b); }
        public int GetXpInto(SkillType s) { int a, b; LevelOf(xp[(int)s], MaxSkillLevel, XpToNext, out a, out b); return a; }
        public int GetXpToNext(SkillType s) { int a, b; LevelOf(xp[(int)s], MaxSkillLevel, XpToNext, out a, out b); return b; }
        public float GetProgress(SkillType s) { int to = GetXpToNext(s); return to <= 0 ? 1f : GetXpInto(s) / (float)to; }

        public int MainLevel { get { int a, b; return LevelOf(mainXp, MaxMainLevel, MainXpToNext, out a, out b); } }
        public int MainXpInto { get { int a, b; LevelOf(mainXp, MaxMainLevel, MainXpToNext, out a, out b); return a; } }
        public int MainXpToNextLevel { get { int a, b; LevelOf(mainXp, MaxMainLevel, MainXpToNext, out a, out b); return b; } }
        public int TotalXp { get { return mainXp; } }

        public SkillDef Def(SkillType s)
        {
            if (skills != null) foreach (var d in skills) if (d != null && d.skill == s) return d;
            return null;
        }

        // ------------------------------------------------------------------ XP
        public void AddXp(SkillType s, int amount)
        {
            if (amount <= 0) return;
            var def = Def(s);
            if (def != null && !def.available) return;
            int beforeSkill = GetLevel(s), beforeMain = MainLevel;
            xp[(int)s] += amount; mainXp += amount;
            if (OnXpGained != null) OnXpGained(s, amount);
            int afterSkill = GetLevel(s);
            if (afterSkill > beforeSkill && OnSkillLevelUp != null) OnSkillLevelUp(s, afterSkill);
            int afterMain = MainLevel;
            if (afterMain > beforeMain && OnMainLevelUp != null) OnMainLevelUp(afterMain);
            Changed();
        }

        // ------------------------------------------------------------------ the tree
        static string Key(SkillType s, string nodeId) { return s + ":" + nodeId; }

        public bool IsUnlocked(SkillType s, SkillNode n)
        {
            if (n == null) return false;
            if (string.IsNullOrEmpty(n.parentId) && n.cost <= 0) return true;        // the root is always open
            return unlocked.Contains(Key(s, n.id));
        }

        public int PointsSpent(SkillType s)
        {
            var def = Def(s); if (def == null) return 0;
            int sum = 0;
            foreach (var n in def.nodes) if (n != null && n.cost > 0 && unlocked.Contains(Key(s, n.id))) sum += n.cost;
            return sum;
        }

        /// <summary>Hvert nivå over 1 gir ett poeng.</summary>
        public int PointsFree(SkillType s) { return Mathf.Max(0, (GetLevel(s) - 1) - PointsSpent(s)); }

        public int UnlockedCount(SkillType s)
        {
            var def = Def(s); if (def == null) return 0;
            int c = 0; foreach (var n in def.nodes) if (n != null && IsUnlocked(s, n)) c++;
            return c;
        }

        public enum NodeState { Owned, Available, Locked }

        public NodeState StateOf(SkillType s, SkillNode n)
        {
            if (IsUnlocked(s, n)) return NodeState.Owned;
            var def = Def(s);
            var parent = def != null ? def.Find(n.parentId) : null;
            bool parentOk = parent == null || IsUnlocked(s, parent);
            if (parentOk && GetLevel(s) >= n.requiredLevel && PointsFree(s) >= n.cost) return NodeState.Available;
            return NodeState.Locked;
        }

        /// <summary>Hvorfor en node er låst, som tekst til brukeren.</summary>
        public string WhyLocked(SkillType s, SkillNode n)
        {
            var def = Def(s);
            var parent = def != null ? def.Find(n.parentId) : null;
            if (parent != null && !IsUnlocked(s, parent)) return "Unlock " + parent.displayName + " first";
            if (GetLevel(s) < n.requiredLevel) return "Reach level " + n.requiredLevel;
            if (PointsFree(s) < n.cost) return "Needs " + n.cost + " skill point" + (n.cost == 1 ? "" : "s");
            return "";
        }

        public bool Unlock(SkillType s, SkillNode n)
        {
            if (n == null || StateOf(s, n) != NodeState.Available) return false;
            unlocked.Add(Key(s, n.id));
            Changed();
            return true;
        }

        /// <summary>Sum av effektene til alle opplåste noder med denne nøkkelen. 0 hvis ingen.</summary>
        public float BonusValue(string effectKey)
        {
            if (string.IsNullOrEmpty(effectKey) || skills == null) return 0f;
            float sum = 0f;
            foreach (var d in skills)
            {
                if (d == null) continue;
                foreach (var n in d.nodes)
                    if (n != null && n.effectKey == effectKey && IsUnlocked(d.skill, n)) sum += n.effectValue;
            }
            return sum;
        }

        /// <summary>Kort å bruke fra andre script: PlayerLevels.Bonus("mine_speed"). Gir 0 hvis spilleren ikke har PlayerLevels.</summary>
        public static float Bonus(string effectKey) { return Instance != null ? Instance.BonusValue(effectKey) : 0f; }

        // ------------------------------------------------------------------ saving
        [Serializable] class Save { public int[] xp; public int mainXp; public string[] unlocked; }

        void Awake()
        {
            Instance = this;
            if (persist) Load();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (persist) SaveNow();
        }

        void OnApplicationQuit() { if (persist) SaveNow(); }

        float saveAt = -1f;
        void Changed()
        {
            if (OnChanged != null) OnChanged();
            if (persist) saveAt = Time.unscaledTime + 1f;
        }

        void Update()
        {
            if (saveAt > 0f && Time.unscaledTime >= saveAt) { saveAt = -1f; SaveNow(); }
            if (debugKeyF9 && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f9Key.wasPressedThisFrame)
                foreach (SkillType s in Enum.GetValues(typeof(SkillType))) AddXp(s, 60);
        }

        public void SaveNow()
        {
            try
            {
                var save = new Save { xp = (int[])xp.Clone(), mainXp = mainXp, unlocked = new List<string>(unlocked).ToArray() };
                PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(save));
                PlayerPrefs.Save();
            }
            catch (Exception e) { Debug.LogWarning("[PlayerLevels] Kunne ikke lagre: " + e.Message, this); }
        }

        void Load()
        {
            try
            {
                if (!PlayerPrefs.HasKey(PrefsKey)) return;
                var save = JsonUtility.FromJson<Save>(PlayerPrefs.GetString(PrefsKey));
                if (save == null) return;
                if (save.xp != null) for (int i = 0; i < xp.Length && i < save.xp.Length; i++) xp[i] = Mathf.Max(0, save.xp[i]);
                mainXp = Mathf.Max(0, save.mainXp);
                unlocked.Clear();
                if (save.unlocked != null) foreach (var u in save.unlocked) unlocked.Add(u.StartsWith("Forging:") ? "Foraging:" + u.Substring(8) : u);   // the skill was renamed
            }
            catch (Exception e) { Debug.LogWarning("[PlayerLevels] Kunne ikke lese lagring: " + e.Message, this); }
        }

        [ContextMenu("Reset levels (sletter lagringen)")]
        public void ResetAll()
        {
            for (int i = 0; i < xp.Length; i++) xp[i] = 0;
            mainXp = 0; unlocked.Clear();
            PlayerPrefs.DeleteKey(PrefsKey);
            if (OnChanged != null) OnChanged();
        }

        [ContextMenu("Give 60 XP to every skill")]
        void GiveTestXp() { foreach (SkillType s in Enum.GetValues(typeof(SkillType))) AddXp(s, 60); }
    }
}

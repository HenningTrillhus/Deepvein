using System.Collections.Generic;
using UnityEngine;

namespace DeepVain.Levels
{
    public enum SkillType { Foraging, Woodcutting, Mining, Combat, Running, Fishing, Swordsmanship, Magic, Archery }

    /// <summary>One node in a skill tree.</summary>
    [System.Serializable]
    public class SkillNode
    {
        [Tooltip("Unik id i dette skillet, f.eks. mine_speed_1.")]
        public string id;
        public string displayName;
        public Sprite icon;
        [TextArea(1, 3)] public string description;
        [Tooltip("Id til noden som må være låst opp først. Tomt = rot (er alltid åpen).")]
        public string parentId;
        [Min(1)] public int requiredLevel = 1;
        [Tooltip("Skill-poeng det koster. 0 = gratis (roten).")]
        [Min(0)] public int cost = 1;
        [Range(0, 2), Tooltip("Kolonne i treet (0 venstre, 1 midten, 2 høyre).")]
        public int column;
        [Range(0, 3), Tooltip("Rad i treet (0 øverst).")]
        public int row;
        [Tooltip("Hva noden gjør: nøkkelen som spillet leser, f.eks. mine_speed, stone_bonus, coal_bonus. Tomt = ingen effekt ennå.")]
        public string effectKey;
        public float effectValue;
    }

    /// <summary>
    /// Ett skill (Mining, Woodcutting ...): navn, ikon, farge og skill-treet. Lages av menyen DeepVain > Levels > 1 - Create skills and trees.
    /// Tekst og tall kan endres fritt i Inspector uten å røre koden.
    /// </summary>
    [CreateAssetMenu(menuName = "DeepVain/Skill", fileName = "Skill")]
    public class SkillDef : ScriptableObject
    {
        public SkillType skill;
        public string displayName = "Skill";
        public Sprite icon;
        public Color color = new Color(0.35f, 0.5f, 0.75f, 1f);
        [Tooltip("Av = vises som låst (kommer senere).")]
        public bool available = true;
        public List<SkillNode> nodes = new List<SkillNode>();

        public SkillNode Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var n in nodes) if (n != null && n.id == id) return n;
            return null;
        }
    }
}

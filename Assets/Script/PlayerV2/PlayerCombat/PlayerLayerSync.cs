using System;
using UnityEngine;

namespace DeepVain.Player
{
    /// <summary>
    /// Put this on the same GameObject as the Animator. The animation clips only animate the two numbers
    /// "animId" and "frame"; in LateUpdate this script sets the sprite of every layer from them,
    /// so all layers are always on the exact same frame.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class PlayerLayerSync : MonoBehaviour
    {
        public enum Gender { Both, Male, Female }

        [Serializable]
        public class Slot
        {
            public string name;
            public SpriteRenderer renderer;
            public LayerSpriteSet set;
            public Gender gender = Gender.Both;
        }

        public Slot[] slots;
        [Tooltip("Same order as the animations in the setup tool. animId picks one of these.")]
        public string[] animationNames;
        public bool female;
        [Tooltip("Set by PlayerHealth to blink the whole player after a hit.")]
        public bool blinkHidden;

        [Header("Sorting (draw order of all layers)")]
        [Tooltip("Same Sorting Layer as your world / old player. Layers get Base Sorting Order + 0, 1, 2 ... back to front.")]
        public string sortingLayer = "Default";
        public int baseSortingOrder = 10;

        // ---- animated by the clips (do not set these by hand) ----
        public float animId;
        public float frame;

        void OnEnable() { ApplySorting(); }
        void OnValidate() { ApplySorting(); }
        void LateUpdate() { Apply(); }

        public void ApplySorting()
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s == null || s.renderer == null) continue;
                s.renderer.sortingLayerName = sortingLayer;
                s.renderer.sortingOrder = baseSortingOrder + i;
            }
        }

        public void Apply()
        {
            if (slots == null || animationNames == null || animationNames.Length == 0) return;
            int a = Mathf.Clamp(Mathf.RoundToInt(animId), 0, animationNames.Length - 1);
            int f = Mathf.Max(0, Mathf.FloorToInt(frame + 0.001f));
            string anim = animationNames[a];
            foreach (var s in slots)
            {
                if (s == null || s.renderer == null) continue;
                bool on = s.gender == Gender.Both || (s.gender == Gender.Female) == female;
                s.renderer.enabled = on && !blinkHidden;
                if (on) s.renderer.sprite = s.set != null ? s.set.Get(anim, f) : null;
            }
        }

        /// <summary>Swap the sprites of one layer (e.g. a helmet) - animations are untouched.</summary>
        public void SetEquipment(string slotName, LayerSpriteSet set)
        {
            foreach (var s in slots) if (s != null && s.name == slotName) s.set = set;
            Apply();
        }

        public void SetFemale(bool value) { female = value; Apply(); }

        /// <summary>The animation currently shown (handy for debugging / hit boxes).</summary>
        public string CurrentAnimation
        {
            get
            {
                if (animationNames == null || animationNames.Length == 0) return "";
                return animationNames[Mathf.Clamp(Mathf.RoundToInt(animId), 0, animationNames.Length - 1)];
            }
        }
        public int CurrentFrame { get { return Mathf.Max(0, Mathf.FloorToInt(frame + 0.001f)); } }
    }
}

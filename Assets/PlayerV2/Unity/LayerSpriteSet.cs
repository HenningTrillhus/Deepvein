using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepVain.Player
{
    /// <summary>
    /// All sprites of ONE layer (e.g. "Torso", or an armour piece): for every animation, the list of frames.
    /// Swap armour by giving a slot in PlayerLayerSync a different LayerSpriteSet - the animations stay the same.
    /// </summary>
    [CreateAssetMenu(menuName = "DeepVain/Layer Sprite Set", fileName = "LayerSpriteSet")]
    public class LayerSpriteSet : ScriptableObject
    {
        [Serializable]
        public class AnimFrames
        {
            public string anim;
            public Sprite[] frames;
        }

        public List<AnimFrames> animations = new List<AnimFrames>();

        Dictionary<string, Sprite[]> lookup;

        void OnEnable() { lookup = null; }
        void OnValidate() { lookup = null; }

        public Sprite Get(string anim, int frame)
        {
            if (lookup == null)
            {
                lookup = new Dictionary<string, Sprite[]>();
                foreach (var a in animations)
                    if (a != null && !string.IsNullOrEmpty(a.anim)) lookup[a.anim] = a.frames;
            }
            if (!lookup.TryGetValue(anim, out var frames) || frames == null || frames.Length == 0) return null;
            return frames[Mathf.Clamp(frame, 0, frames.Length - 1)];
        }
    }
}

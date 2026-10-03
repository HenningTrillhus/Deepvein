using UnityEngine;

namespace DeepVain.Player
{
    /// <summary>
    /// Changes skin / hair / clothes colours by swapping the fixed key colours of the sprites (see Colour_Keys.txt).
    /// Needs the layers to use the "DeepVain/PlayerPaletteSwap" material (the setup tool does that).
    /// Every ramp has 5 colours: lightest, light, base, shade, darkest.
    /// </summary>
    public class PlayerPalette : MonoBehaviour
    {
        static Color C(string hex) { Color c; ColorUtility.TryParseHtmlString(hex, out c); return c; }
        static Color[] Ramp(params string[] hex) { var r = new Color[hex.Length]; for (int i = 0; i < hex.Length; i++) r[i] = C(hex[i]); return r; }

        public static readonly Color[][] SkinPresets =
        {
            Ramp("#fbe2c0", "#f0c096", "#d49a70", "#a8685a", "#6e4054"),   // light (default)
            Ramp("#f2cfa0", "#e0aa78", "#bc8458", "#8c5848", "#58343c"),   // tan
            Ramp("#d8a67c", "#bc8460", "#94603f", "#6e4034", "#44262c"),   // brown
            Ramp("#b88460", "#9a6648", "#74462e", "#522c28", "#321a20"),   // dark
        };
        public static readonly Color[][] HairPresets =
        {
            Ramp("#e0aa5c", "#c07e40", "#8c5430", "#603420", "#3c2018"),   // brown (default)
            Ramp("#fff0b0", "#f0d070", "#d0a840", "#a07830", "#5a3c20"),   // blonde
            Ramp("#6c6c88", "#484860", "#2c2c40", "#1c1c2c", "#0c0c18"),   // black
            Ramp("#ffb070", "#e87a40", "#c04a28", "#8a2c24", "#4a1620"),   // red
            Ramp("#ffffff", "#e8e8f0", "#c4c4d4", "#9090a8", "#585870"),   // white
        };

        // current colours (editable in the inspector)
        public Color[] skin = Ramp("#fbe2c0", "#f0c096", "#d49a70", "#a8685a", "#6e4054");
        public Color[] hair = Ramp("#e0aa5c", "#c07e40", "#8c5430", "#603420", "#3c2018");
        public Color[] shirt = Ramp("#e4dcc4", "#c8bea0", "#a49878", "#7c7058", "#524838");
        public Color[] shorts = Ramp("#a08068", "#826450", "#654a3c", "#483232", "#2c2028");

        static readonly int NewColorsId = Shader.PropertyToID("_NewColors");
        static readonly int UseId = Shader.PropertyToID("_UsePalette");
        MaterialPropertyBlock mpb;

        void OnEnable() { Apply(); }
        void OnValidate() { if (isActiveAndEnabled) Apply(); }

        public void SetSkinPreset(int i) { skin = SkinPresets[Mathf.Clamp(i, 0, SkinPresets.Length - 1)]; Apply(); }
        public void SetHairPreset(int i) { hair = HairPresets[Mathf.Clamp(i, 0, HairPresets.Length - 1)]; Apply(); }

        public void Apply()
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            var arr = new Vector4[20];
            Fill(arr, 0, skin); Fill(arr, 5, hair); Fill(arr, 10, shirt); Fill(arr, 15, shorts);
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.GetPropertyBlock(mpb);
                mpb.SetVectorArray(NewColorsId, arr);
                mpb.SetFloat(UseId, 1f);
                sr.SetPropertyBlock(mpb);
            }
        }

        static void Fill(Vector4[] arr, int start, Color[] ramp)
        {
            for (int i = 0; i < 5; i++)
            {
                Color c = (ramp != null && i < ramp.Length) ? ramp[i] : Color.magenta;
                if (QualitySettings.activeColorSpace == ColorSpace.Linear) c = c.linear;
                arr[start + i] = new Vector4(c.r, c.g, c.b, 1f);
            }
        }
    }
}

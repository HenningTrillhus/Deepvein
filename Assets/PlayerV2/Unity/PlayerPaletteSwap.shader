// Sprite shader that replaces 20 fixed key colours (skin, hair, shirt, shorts) with colours from PlayerPalette.
// Built-in style sprite shader: works in the Built-in pipeline and as an unlit sprite in URP.
Shader "DeepVain/PlayerPaletteSwap"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _UsePalette ("Use palette (0/1)", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            fixed4 _Color;
            float _UsePalette;
            float4 _NewColors[20];

            // skin 0-4, hair 5-9, shirt 10-14, shorts 15-19 (sRGB)
            static const float3 KEYS[20] =
            {
                float3(0.98431, 0.88627, 0.75294),
                float3(0.94118, 0.75294, 0.58824),
                float3(0.83137, 0.60392, 0.43922),
                float3(0.65882, 0.40784, 0.35294),
                float3(0.43137, 0.25098, 0.32941),
                float3(0.87843, 0.66667, 0.36078),
                float3(0.75294, 0.49412, 0.25098),
                float3(0.54902, 0.32941, 0.18824),
                float3(0.37647, 0.20392, 0.12549),
                float3(0.23529, 0.12549, 0.09412),
                float3(0.89412, 0.86275, 0.76863),
                float3(0.78431, 0.74510, 0.62745),
                float3(0.64314, 0.59608, 0.47059),
                float3(0.48627, 0.43922, 0.34510),
                float3(0.32157, 0.28235, 0.21961),
                float3(0.62745, 0.50196, 0.40784),
                float3(0.50980, 0.39216, 0.31373),
                float3(0.39608, 0.29020, 0.23529),
                float3(0.28235, 0.19608, 0.19608),
                float3(0.17255, 0.12549, 0.15686)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                if (_UsePalette > 0.5 && c.a > 0.5)
                {
                    for (int k = 0; k < 20; k++)
                    {
                        float3 key = KEYS[k];
                        #if !defined(UNITY_COLORSPACE_GAMMA)
                        key = GammaToLinearSpace(key);
                        #endif
                        float3 d = abs(c.rgb - key);
                        if (d.r < 0.012 && d.g < 0.012 && d.b < 0.012)
                        {
                            c.rgb = _NewColors[k].rgb;
                            break;
                        }
                    }
                }
                c *= i.color;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}

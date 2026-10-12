// Sprite shader for URP 2D: a white diagonal band sweeps across the picture now and then, only over the pixels of the sprite itself.
// Used for things that can be picked (Forageable). One shared material is enough: every prop has its own phase, so they do not all shine at once.
// The band is placed from the picture's UV (0..1), so it works for any frame of an animation and for both Tight and Full Rect sprites.
// The sprite must NOT be in a Sprite Atlas. No _MainTex_ST / _TexelSize in the property buffer (they switch off the 2D SRP Batcher).
Shader "DeepVain/Sprite Shine"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite", 2D) = "white" {}
        [MainColor] _Color ("Tint", Color) = (1,1,1,1)
        _ArtSize ("Picture size in art pixels", Float) = 50
        _ShinePeriod ("Seconds from one sweep to the next", Float) = 2.9
        _ShineRun ("Share of the period the band is moving", Float) = 0.4
        _ShinePhase ("Phase in seconds (set per prop)", Float) = 0
        _ShineStrength ("Strength 0..1 (0 = off)", Float) = 1
        _ShineLean ("How much the band leans", Float) = 0.6
        _ShineCore ("Half width of the bright core, art px", Float) = 2.5
        _ShineEdge ("Half width including the soft edge, art px", Float) = 5
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Shine"
            Tags { "LightMode"="Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _ArtSize;
                float _ShinePeriod;
                float _ShineRun;
                float _ShinePhase;
                float _ShineStrength;
                float _ShineLean;
                float _ShineCore;
                float _ShineEdge;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.color = v.color * _Color;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                if (c.a <= 0.0) return half4(0, 0, 0, 0);

                // where in its cycle this prop is: 0..1; the band moves during the first part and then it rests
                float t = frac((_Time.y + _ShinePhase) / max(_ShinePeriod, 0.01));
                if (_ShineStrength > 0.001 && t < _ShineRun)
                {
                    float k = t / max(_ShineRun, 0.01);
                    float pos = -14.0 + k * (_ArtSize + 28.0);                       // art pixels, sweeping left to right
                    float ux = i.uv.x * _ArtSize;
                    float h = i.uv.y * _ArtSize;                                      // height above the bottom of the picture
                    float d = (ux + h * _ShineLean) - pos;
                    float ad = abs(d);
                    float amt = ad < _ShineCore ? 0.85 : (ad < _ShineEdge ? 0.40 : 0.0);   // two hard steps: stays pixel art
                    c.rgb = lerp(c.rgb, half3(1, 1, 1), amt * _ShineStrength);
                }
                return c;
            }
            ENDHLSL
        }
    }
}

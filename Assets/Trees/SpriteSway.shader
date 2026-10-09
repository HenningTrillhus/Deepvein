// Sprite shader for URP 2D that bends a sprite sideways in the wind: nothing at the bottom, most at the top, like a tree in a breeze.
// The bend happens per pixel row, snapped to whole sprite pixels, so it stays crisp pixel art and needs no extra frames.
// Used by TreeSway.cs (which fills in the per-tree values). The sprite must NOT be in a Sprite Atlas, and should use Mesh Type = Full Rect.
Shader "DeepVain/Sprite Sway"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite", 2D) = "white" {}
        [MainColor] _Color ("Tint", Color) = (1,1,1,1)
        _SwayAmount ("Sway at the very top (units)", Float) = 0.08
        _SwaySpeed ("Speed", Float) = 1.0
        _SwayPhase ("Phase (set per tree)", Float) = 0
        _Wind ("Wind scale (1 = normal)", Float) = 1
        _BaseY ("Sprite bottom, object y", Float) = 0
        _Height ("Sprite height, units", Float) = 3
        _CenterX ("Sprite centre, object x", Float) = 0
        _UVPerUnit ("UV width per unit", Float) = 0.3
        _UVMinMaxX ("UV min/max x", Vector) = (0,1,0,0)
        _PPU ("Pixels per unit", Float) = 100
        _Snap ("Snap to pixels (0/1)", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Sway"
            Tags { "LightMode"="Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _SwayAmount;
                float _SwaySpeed;
                float _SwayPhase;
                float _Wind;
                float _BaseY;
                float _Height;
                float _CenterX;
                float _UVPerUnit;
                float4 _UVMinMaxX;
                float _PPU;
                float _Snap;
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
                float localY : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                // make the picture a little wider on both sides so the bent parts are not cut off by the edge of the sprite
                float pad = abs(_SwayAmount * _Wind) * 1.3 + 2.0 / _PPU;
                float side = v.positionOS.x >= _CenterX ? 1.0 : -1.0;
                v.positionOS.x += side * pad;
                v.uv.x += side * pad * _UVPerUnit;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.color = v.color * _Color;
                o.uv = v.uv;
                o.localY = v.positionOS.y;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // height up the sprite, 0 at the bottom and 1 at the top, in whole pixel rows
                float rows = max(_Height * _PPU, 1.0);
                float row = floor((i.localY - _BaseY) * _PPU);
                float h = saturate((row + 0.5) / rows);

                float t = _Time.y * _SwaySpeed + _SwayPhase;
                float sway = sin(t + h * 0.9) * 0.75 + sin(t * 2.17 + _SwayPhase * 1.7 + h * 2.0) * 0.25;
                float amount = _SwayAmount * _Wind;
                float dx = sway * amount * h * h;                                        // bends more and more towards the top
                dx += sin(_Time.y * 7.0 * _SwaySpeed + _SwayPhase * 3.0 + h * 9.0) * amount * 0.12 * h * h * h;   // the crown flutters a little
                if (_Snap > 0.5) dx = round(dx * _PPU) / _PPU;

                float2 uv = i.uv;
                uv.x -= dx * _UVPerUnit;
                if (uv.x < _UVMinMaxX.x || uv.x > _UVMinMaxX.y) return half4(0, 0, 0, 0);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * i.color;
            }
            ENDHLSL
        }
    }
}

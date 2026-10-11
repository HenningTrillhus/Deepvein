// Sprite shader for URP 2D that bends a sprite sideways in the wind (nothing at the bottom, most at the top), in whole sprite pixels,
// and can draw a "selected" look: a one-pixel white outline around the whole silhouette and a slightly brighter picture.
// Used by TreeSway.cs (which fills in the per-tree values). The sprite must NOT be in a Sprite Atlas, and should use Mesh Type = Full Rect.
// v3: no _MainTex_ST / _MainTex_TexelSize (they switch off the 2D SRP Batcher). v2: same shader name as before, so existing materials keep working. The outline needs room outside the picture, so the quad is padded on all sides.
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
        _UVPerUnitY ("UV height per unit", Float) = 0.3
        _UVMinMaxX ("UV min/max x", Vector) = (0,1,0,0)
        _UVMinMaxY ("UV min/max y", Vector) = (0,1,0,0)
        _PPU ("Pixels per unit", Float) = 100
        _Snap ("Snap to pixels (0/1)", Float) = 1
        _Outline ("Selected (0/1)", Float) = 0
        _OutlineColor ("Outline colour", Color) = (1,1,1,1)
        _OutlineWidth ("Outline width, sprite pixels", Float) = 1
        _Brighten ("Brighten when selected (0-1)", Float) = 0.22
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
                half4 _Color;
                half4 _OutlineColor;
                float _SwayAmount;
                float _SwaySpeed;
                float _SwayPhase;
                float _Wind;
                float _BaseY;
                float _Height;
                float _CenterX;
                float _UVPerUnit;
                float _UVPerUnitY;
                float4 _UVMinMaxX;
                float4 _UVMinMaxY;
                float _PPU;
                float _Snap;
                float _Outline;
                float _OutlineWidth;
                float _Brighten;
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
                // make the picture a little bigger on every side so neither the bent parts nor the outline are cut off by the edge of the sprite
                float pad = abs(_SwayAmount * _Wind) * 1.3 + 3.0 / _PPU;
                float padY = 3.0 / _PPU;
                float side = v.positionOS.x >= _CenterX ? 1.0 : -1.0;
                float sideY = v.positionOS.y >= _BaseY + _Height * 0.5 ? 1.0 : -1.0;
                v.positionOS.x += side * pad;
                v.uv.x += side * pad * _UVPerUnit;
                v.positionOS.y += sideY * padY;
                v.uv.y += sideY * padY * _UVPerUnitY;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.color = v.color * _Color;
                o.uv = v.uv;
                o.localY = v.positionOS.y;
                return o;
            }

            // the sprite's own pixels only: outside its rectangle on the texture counts as empty
            half4 Pic(float2 uv)
            {
                if (uv.x < _UVMinMaxX.x || uv.x > _UVMinMaxX.y || uv.y < _UVMinMaxY.x || uv.y > _UVMinMaxY.y) return half4(0, 0, 0, 0);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
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
                half4 c = Pic(uv);

                if (_Outline > 0.5)
                {
                    if (c.a < 0.5)
                    {
                        // an empty pixel next to a drawn one becomes outline (8 neighbours, so corners are closed too)
                        // one sprite pixel in uv units (from the values TreeSway sends), so no _TexelSize / _ST is needed
                        float2 step = float2(_UVPerUnit, _UVPerUnitY) / max(_PPU, 1.0) * max(_OutlineWidth, 1.0);
                        float n = 0;
                        n = max(n, Pic(uv + float2( step.x, 0)).a);
                        n = max(n, Pic(uv + float2(-step.x, 0)).a);
                        n = max(n, Pic(uv + float2(0,  step.y)).a);
                        n = max(n, Pic(uv + float2(0, -step.y)).a);
                        n = max(n, Pic(uv + float2( step.x,  step.y)).a);
                        n = max(n, Pic(uv + float2(-step.x,  step.y)).a);
                        n = max(n, Pic(uv + float2( step.x, -step.y)).a);
                        n = max(n, Pic(uv + float2(-step.x, -step.y)).a);
                        if (n > 0.5) return half4(_OutlineColor.rgb, 1) * half4(1, 1, 1, i.color.a);
                        return half4(0, 0, 0, 0);
                    }
                    c.rgb = lerp(c.rgb, half3(1, 1, 1), saturate(_Brighten));
                }
                else if (c.a <= 0.0) return half4(0, 0, 0, 0);

                return c * i.color;
            }
            ENDHLSL
        }
    }
}

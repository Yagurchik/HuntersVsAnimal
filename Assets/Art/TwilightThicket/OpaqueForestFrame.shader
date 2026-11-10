Shader "ForestAnimalWar/Opaque Forest Frame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        _Cutoff ("Silhouette Cutoff", Range(0,1)) = 0.18
        _Brightness ("Frame Brightness", Range(0,1)) = 0.72
        _Saturation ("Frame Saturation", Range(0,1)) = 0.58
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="TransparentCutout" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One Zero

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _AlphaTex;
            fixed4 _Color;
            fixed4 _RendererColor;
            float _EnableExternalAlpha;
            float _Cutoff;
            float _Brightness;
            float _Saturation;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color * _RendererColor;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, input.uv) * input.color;
                fixed externalAlpha = tex2D(_AlphaTex, input.uv).r;
                color.a = lerp(color.a, externalAlpha * input.color.a, _EnableExternalAlpha);
                clip(color.a - _Cutoff);
                fixed gray = dot(color.rgb, fixed3(0.299, 0.587, 0.114));
                // Desaturate toward warm ochre, not neutral gray, so the
                // pumpkin-orange carving stays earthy instead of turning pink.
                color.rgb = lerp(gray * fixed3(1.0, 0.88, 0.50), color.rgb,
                    _Saturation) * _Brightness;
                color.a = 1;
                return color;
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}

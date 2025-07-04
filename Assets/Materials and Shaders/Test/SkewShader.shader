Shader "Custom/SkewShaderURP"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _SkewAmount ("Skew Amount", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "LightMode"="UniversalForward" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _SkewAmount;

            Varyings vert(Attributes v)
            {
                Varyings o;
                // Apply skew based on Y position
                float skew = v.positionOS.y * _SkewAmount;
                v.positionOS.x += skew;
                o.positionHCS = TransformObjectToHClip(v.positionOS); // Corrected for URP
                o.texcoord = v.texcoord;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord);
            }
            ENDHLSL
        }
    }
}

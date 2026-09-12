Shader "Custom/DoorBlurOutline"
{
    Properties
    {
        _GlowColor ("Glow Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _OutlineWidth ("Outline Width", Range(0.01, 0.2)) = 0.04
        _EdgeFalloff ("Edge Falloff", Range(0.5, 4.0)) = 1.5
        _EdgeIntensity ("Edge Intensity", Range(0.5, 3.0)) = 1.6
        _SurfaceGlow ("Surface Glow", Range(0.0, 1.0)) = 0.15
        [Header(Pulse Settings)]
        _MinOpacity ("Min Opacity", Range(0.0, 1.0)) = 0.25
        _MaxOpacity ("Max Opacity", Range(0.0, 1.0)) = 0.50
        _PulseSpeed ("Pulse Speed", Float) = 3.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent+200" 
            "RenderPipeline"="UniversalPipeline" 
        }

        LOD 100

        Pass
        {
            Name "BlurOutline"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
                float _OutlineWidth;
                float _EdgeFalloff;
                float _EdgeIntensity;
                float _SurfaceGlow;
                float _MinOpacity;
                float _MaxOpacity;
                float _PulseSpeed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // Extrude along vertex normal
                float3 posOS = input.positionOS.xyz + normalize(input.normalOS) * _OutlineWidth;
                output.positionCS = TransformObjectToHClip(posOS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 posWS = TransformObjectToWorld(posOS);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(posWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                // Silhouette edge detection: grazing angles with view direction
                float NdotV = abs(dot(normalWS, viewDirWS));
                float edge = pow(1.0 - NdotV, _EdgeFalloff) * _EdgeIntensity;

                // Smooth sinusoidal 50% to 25% opacity pulse transition
                float pulseSine = 0.5f + 0.5f * sin(_Time.y * _PulseSpeed);
                float currentOpacity = lerp(_MinOpacity, _MaxOpacity, pulseSine);
                
                float alpha = saturate((edge + _SurfaceGlow) * currentOpacity) * _GlowColor.a;

                return half4(_GlowColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}

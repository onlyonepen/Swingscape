Shader "Custom/VolumetricSpotlightV2"
{
    Properties
    {
        [HDR] _Color ("Light Color", Color) = (1, 0.92, 0.65, 1)
        _EmissionIntensity ("Emission Intensity", Range(0, 10)) = 2.0
        _Opacity ("Overall Opacity", Range(0, 3)) = 1.0
        _CoreStrength ("Core Strength (visible from any angle)", Range(0, 2)) = 0.5
        _RimStrength ("Rim/Fresnel Strength (side-on glow)", Range(0, 4)) = 1.2
        _EdgeFalloff ("Rim Falloff Power", Range(0.1, 8)) = 2.5
        _LengthFalloff ("Length Falloff Power", Range(0.1, 8)) = 1.5
        [Toggle] _InvertLength ("Invert Length Direction", Float) = 0
        
        [Toggle] _UseNoise ("Use Noise", Float) = 1
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.5
        _NoiseTiling ("Noise Tiling (xy)", Vector) = (2, 2, 0, 0)
        _NoiseScrollSpeed ("Noise Scroll Speed (xy)", Vector) = (0.05, 0.1, 0, 0)

        _NearFadeStart ("Near Camera Fade Start", Float) = 0.05
        _NearFadeEnd ("Near Camera Fade End", Float) = 0.2
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Unlit"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _EmissionIntensity;
                float _Opacity;
                float _CoreStrength;
                float _RimStrength;
                float _EdgeFalloff;
                float _LengthFalloff;
                float _InvertLength;
                float _UseNoise;
                float4 _NoiseTex_ST;
                float _NoiseStrength;
                float4 _NoiseTiling;
                float4 _NoiseScrollSpeed;
                float _NearFadeStart;
                float _NearFadeEnd;
            CBUFFER_END

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                return OUT;
            }

            float4 frag (Varyings IN) : SV_Target
            {
                float3 viewDir = normalize(GetCameraPositionWS() - IN.positionWS);
                float3 normalWS = normalize(IN.normalWS);

                // abs() makes this symmetric: it behaves the same whether we're
                // looking at the outward-facing side or the inward-facing side of
                // the shell, so it never collapses to zero on backfaces (viewed
                // from the front, down the axis, or from inside the volume).
                float NdotV = abs(dot(normalWS, viewDir));
                float rim = pow(1.0 - saturate(NdotV), _EdgeFalloff) * _RimStrength;

                // Lengthwise fade along the beam, driven purely by mesh UV (not
                // view-dependent at all), so there is always a baseline amount of
                // visible density no matter which way you're looking.
                float uvY = _InvertLength > 0.5 ? 1.0 - IN.uv.y : IN.uv.y;
                float lengthFade = pow(1.0 - saturate(uvY), _LengthFalloff);

                float density = saturate(_CoreStrength * lengthFade + rim);

                if (_UseNoise > 0.5)
                {
                    float2 noiseUV = IN.uv * _NoiseTiling.xy + _Time.y * _NoiseScrollSpeed.xy;
                    float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;
                    density *= lerp(1.0, noise, _NoiseStrength);
                }

                // Small fade right at the camera lens only, to avoid a hard clip
                // pop when something gets extremely close - not a wide "near the
                // camera" zone like the old shader had, so it won't blank out the
                // whole interior when you're standing inside the beam.
                float camDist = distance(GetCameraPositionWS(), IN.positionWS);
                float nearFade = smoothstep(_NearFadeStart, _NearFadeEnd, camDist);
                density *= nearFade;

                // Emission: boost the rgb independently of alpha so this can push
                // past 1.0 and feed URP's Bloom post-process for a glow, without
                // making the surface itself any more opaque.
                float3 emissiveColor = _Color.rgb * _EmissionIntensity;

                float alpha = saturate(density * _Opacity);
                return float4(emissiveColor, alpha);
            }
            ENDHLSL
        }
    }
}

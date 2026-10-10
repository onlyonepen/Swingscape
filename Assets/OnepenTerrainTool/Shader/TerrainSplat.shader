Shader "Onepen/TerrainSplat"
{
    // Triplanar texture-splat shader for the marching-cubes terrain.
    //
    // The mesh has no UVs, so textures are projected triplanar-style from world
    // space. Each vertex's COLOR (RGBA) holds four blend weights, one per FIXED
    // Texture2DArray slice (0..3). Only weights are interpolated across a triangle
    // (never an index), so textures cross-fade smoothly. The fragment samples the
    // four slices triplanar and blends by the weights.
    //
    // An all-zero weight (an unpainted-but-solid sample) falls back to slice 0,
    // so sculpted terrain always shows a base texture.
    Properties
    {
        [NoScaleOffset] _TexArray ("Texture Array", 2DArray) = "" {}
        _TexScale ("Texture Scale (world units)", Float) = 0.2
        _BlendSharpness ("Triplanar Sharpness", Range(1, 16)) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D_ARRAY(_TexArray);
            SAMPLER(sampler_TexArray);

            CBUFFER_START(UnityPerMaterial)
                float _TexScale;
                float _BlendSharpness;
                // Per-slice tiling multiplier (.xy), one entry per Texture2DArray slice (0..3).
                // Set from TerrainTool.ApplyTextureTiling; an unset (0,0) entry falls back to (1,1).
                float4 _TexTiling[4];
            CBUFFER_END

            // Tiling for a slice, defaulting a 0 component to 1 so an unset array doesn't collapse the UVs.
            float2 SliceTiling(int i)
            {
                float2 t = _TexTiling[i].xy;
                if (t.x <= 0) t.x = 1;
                if (t.y <= 0) t.y = 1;
                return t;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;      // per-slice blend weights
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 weights     : TEXCOORD2;
                float  fogCoord    : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = pos.positionCS;
                OUT.positionWS  = pos.positionWS;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.weights     = IN.color;
                OUT.fogCoord    = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            // Triplanar-sample one array slice at a world position, blended by the axis weights,
            // with a per-slice tiling multiplier on top of the global world-space scale.
            float3 SampleTriplanar(int slice, float3 wpos, float3 blend)
            {
                float2 s = _TexScale * SliceTiling(slice);
                float2 uvX = wpos.zy * s;
                float2 uvY = wpos.xz * s;
                float2 uvZ = wpos.xy * s;
                float3 cx = SAMPLE_TEXTURE2D_ARRAY(_TexArray, sampler_TexArray, uvX, slice).rgb;
                float3 cy = SAMPLE_TEXTURE2D_ARRAY(_TexArray, sampler_TexArray, uvY, slice).rgb;
                float3 cz = SAMPLE_TEXTURE2D_ARRAY(_TexArray, sampler_TexArray, uvZ, slice).rgb;
                return cx * blend.x + cy * blend.y + cz * blend.z;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);

                // Triplanar axis weights from the surface normal.
                float3 blend = pow(abs(n), _BlendSharpness);
                blend /= (blend.x + blend.y + blend.z + 1e-5);

                // Per-slice weights; fall back to slice 0 for unpainted samples.
                float4 w = IN.weights;
                float wsum = w.x + w.y + w.z + w.w;
                if (wsum < 1e-4)
                {
                    w = float4(1, 0, 0, 0);
                    wsum = 1;
                }
                w /= wsum;

                float3 albedo =
                    SampleTriplanar(0, IN.positionWS, blend) * w.x +
                    SampleTriplanar(1, IN.positionWS, blend) * w.y +
                    SampleTriplanar(2, IN.positionWS, blend) * w.z +
                    SampleTriplanar(3, IN.positionWS, blend) * w.w;

                // Simple diffuse lighting: main light (+ shadow) and SH ambient.
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                float ndotl = saturate(dot(n, mainLight.direction));
                float3 lighting = mainLight.color * (ndotl * mainLight.shadowAttenuation);
                float3 ambient = SampleSH(n);

                float3 color = albedo * (lighting + ambient);
                color = MixFog(color, IN.fogCoord);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // Shadow casting so the terrain casts shadows.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionHCS : SV_POSITION; };

            V shadowVert(A IN)
            {
                V OUT;
                float3 wpos = TransformObjectToWorld(IN.positionOS.xyz);
                float3 wnrm = TransformObjectToWorldNormal(IN.normalOS);
                float4 pos = TransformWorldToHClip(ApplyShadowBias(wpos, wnrm, _LightDirection));
                #if UNITY_REVERSED_Z
                    pos.z = min(pos.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    pos.z = max(pos.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                OUT.positionHCS = pos;
                return OUT;
            }

            half4 shadowFrag(V IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // Depth prepass (used by URP depth texture / features that need it).
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionHCS : SV_POSITION; };

            V depthVert(A IN)
            {
                V OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 depthFrag(V IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}

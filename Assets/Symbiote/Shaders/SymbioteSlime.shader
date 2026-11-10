Shader "SymbioteRaid/Slime"
{
    Properties
    {
        [MainColor] _BaseColor("Petroleum color", Color) = (0.012,0.020,0.024,1)
        _Smoothness("Wet gloss", Range(0,1)) = 0.94
        _Metallic("Metallic", Range(0,1)) = 0.18
        _RimColor("Soft edge reflection", Color) = (0.045,0.11,0.10,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimColor;
                half _Smoothness;
                half _Metallic;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; half fog : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fog = ComputeFogFactor(p.positionCS.z);
                return output;
            }
            half3 WetLight(Light light, half3 n, half3 v, half3 base)
            {
                half attenuation = light.distanceAttenuation * light.shadowAttenuation;
                half diffuse = saturate(dot(n, light.direction));
                half3 h = SafeNormalize(light.direction + v);
                half exponent = lerp(24.0h, 160.0h, _Smoothness);
                half highlight = pow(saturate(dot(n, h)), exponent) * diffuse;
                return (base * diffuse + highlight * lerp(0.45h, 0.85h, _Smoothness)) * light.color * attenuation;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                half3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 base = _BaseColor.rgb;
                half3 result = base * (SampleSH(n) + 0.12h);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                result += WetLight(mainLight, n, v, base);
                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; i++) result += WetLight(GetAdditionalLight(i, input.positionWS), n, v, base);
                #endif
                half fresnel = pow(1.0h - saturate(dot(n, v)), 3.0h);
                result += _RimColor.rgb * fresnel * 0.75h;
                return half4(MixFog(result, input.fog), 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}

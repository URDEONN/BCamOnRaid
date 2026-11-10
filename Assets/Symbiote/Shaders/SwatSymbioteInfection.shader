Shader "SymbioteRaid/Infection"
{
    Properties
    {
        [MainTexture] _BaseMap("Original diffuse texture", 2D) = "white" {}
        [MainColor] _BaseColor("Original color", Color) = (0.12,0.18,0.23,1)
        _PossessionAmount("Possession", Range(0,1)) = 0
        _Smoothness("Infected gloss", Range(0,1)) = 0.91
        _Inflation("Biomass thickness", Range(0,0.1)) = 0.035
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
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _PossessionAmount;
                half _Smoothness;
                half _Inflation;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; float3 positionOS : TEXCOORD3; half fog : TEXCOORD4; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float pulse = sin(input.positionOS.x * 11 + input.positionOS.y * 8 + input.positionOS.z * 13) * .2 + .8;
                float3 inflated = input.positionOS.xyz + input.normalOS * _Inflation * _PossessionAmount * pulse;
                VertexPositionInputs p = GetVertexPositionInputs(inflated);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionOS = input.positionOS.xyz;
                output.fog = ComputeFogFactor(p.positionCS.z);
                return output;
            }
            half3 InfectionLight(Light light, half3 n, half3 v, half3 base, half wet)
            {
                half diffuse = saturate(dot(n, light.direction));
                half3 h = SafeNormalize(light.direction + v);
                half specular = pow(saturate(dot(n, h)), lerp(20.0h, 140.0h, wet)) * diffuse * lerp(.08h, .8h, wet);
                return (base * diffuse + specular) * light.color * light.distanceAttenuation * light.shadowAttenuation;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                half3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 p = input.positionOS;
                float veinField = abs(sin(p.y * 29 + sin(p.x * 18) + p.z * 9));
                half veins = 1.0h - smoothstep(.035, .15, veinField);
                half infection = saturate(_PossessionAmount * (.82h + veins * .65h));
                half3 uniformColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                half3 base = lerp(uniformColor, half3(.009, .017, .019), infection);
                half wet = lerp(.18h, _Smoothness, infection);
                half3 result = base * (SampleSH(n) + .10h);
                result += InfectionLight(GetMainLight(TransformWorldToShadowCoord(input.positionWS)), n, v, base, wet);
                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; i++) result += InfectionLight(GetAdditionalLight(i, input.positionWS), n, v, base, wet);
                #endif
                result += half3(.025, .065, .055) * pow(1.0h - saturate(dot(n, v)), 3.0h) * infection;
                return half4(MixFog(result, input.fog), 1);
            }
            ENDHLSL
        }
        // The stock shadow/depth silhouettes do not include the small infection inflation.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}

Shader "Custom/SwatSymbioteInfection"
{
    Properties
    {
        _MainTex ("SWAT Original Texture", 2D) = "white" {}
        _Color ("SWAT Tint", Color) = (1,1,1,1)
        _SymbioteColor ("Symbiote Vein Color", Color) = (0.03, 0.04, 0.06, 1)
        _SymbioteGlow ("Symbiote Vein Glow", Color) = (0.12, 0.35, 0.65, 1)
        _PossessionAmount ("Possession Progress (0 to 1)", Range(0.0, 1.0)) = 0.0
        _MuscleInflation ("Symbiotic Muscle Inflation", Range(0.0, 0.08)) = 0.025
        _VeinScale ("Organic Vein Scale", Range(2.0, 30.0)) = 12.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _SymbioteColor;
        fixed4 _SymbioteGlow;
        float _PossessionAmount;
        float _MuscleInflation;
        float _VeinScale;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 viewDir;
            floatLocalPosY : TEXCOORD4;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.floatLocalPosY = v.vertex.y;

            if (_PossessionAmount > 0.01)
            {
                // Infla la musculatura del SWAT poseído y añade pulsación orgánica en hombros/pecho
                float pulse = sin(_Time.y * 5.0 + v.vertex.y * _VeinScale) * cos(v.vertex.x * _VeinScale);
                float inflate = _PossessionAmount * _MuscleInflation * (1.0 + 0.35 * pulse);
                v.vertex.xyz += v.normal * inflate;
            }
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 swatCol = tex2D(_MainTex, IN.uv_MainTex) * _Color;

            // Máscara orgánica de propagación desde el pecho hacia las extremidades
            float organicNoise = 0.5 + 0.5 * sin(IN.worldPos.x * _VeinScale + _Time.y * 3.0)
                                           * cos(IN.worldPos.y * _VeinScale - _Time.y * 2.0)
                                           * sin(IN.worldPos.z * _VeinScale);

            float threshold = saturate(_PossessionAmount * 1.35 - organicNoise * 0.35);
            float isInfected = smoothstep(0.35, 0.55, threshold);
            float veinEdge = smoothstep(0.30, 0.45, threshold) - smoothstep(0.45, 0.60, threshold);

            o.Albedo = lerp(swatCol.rgb, _SymbioteColor.rgb, isInfected * 0.92);
            o.Metallic = lerp(0.1, 0.35, isInfected);
            o.Smoothness = lerp(0.35, 0.92, isInfected);

            half rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
            o.Emission = (_SymbioteGlow.rgb * veinEdge * 2.0) + (_SymbioteGlow.rgb * pow(rim, 3.0) * isInfected * 0.6);
            o.Alpha = swatCol.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

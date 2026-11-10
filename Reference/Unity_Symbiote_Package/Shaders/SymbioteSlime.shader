Shader "Custom/SymbioteSlime"
{
    Properties
    {
        _Color ("Symbiote Base Color", Color) = (0.03, 0.04, 0.06, 1)
        _RimColor ("Rim Highlight Color", Color) = (0.15, 0.28, 0.45, 1)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.0
        _Glossiness ("Wet Smoothness", Range(0,1)) = 0.92
        _Metallic ("Metallic", Range(0,1)) = 0.25
        _PulseSpeed ("Organic Pulse Speed", Range(0.5, 10.0)) = 4.2
        _PulseAmplitude ("Organic Pulse Amplitude", Range(0.0, 0.3)) = 0.06
        _NoiseScale ("Organic Noise Scale", Range(1.0, 15.0)) = 6.0
        _SquashAmount ("Surface Flatten Amount", Range(0.0, 1.0)) = 0.45
        _Velocity ("Current Velocity (XYZ)", Vector) = (0, 0, 0, 0)
        _SurfaceNormal ("Current Surface Normal", Vector) = (0, 1, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0

        struct Input
        {
            float3 viewDir;
            float3 worldPos;
        };

        fixed4 _Color;
        fixed4 _RimColor;
        half _RimPower;
        half _Glossiness;
        half _Metallic;
        float _PulseSpeed;
        float _PulseAmplitude;
        float _NoiseScale;
        float _SquashAmount;
        float4 _Velocity;
        float4 _SurfaceNormal;

        // Ruido pseudo-orgánico 3D rápido para hervir la superficie del simbionte
        float organicWave(float3 p, float t)
        {
            float w1 = sin(p.x * _NoiseScale + t) * cos(p.y * _NoiseScale - t * 0.8) * sin(p.z * _NoiseScale + t * 0.6);
            float w2 = cos((p.x + p.z) * (_NoiseScale * 1.7) - t * 1.4) * 0.5;
            return (w1 + w2);
        }

        void vert(inout appdata_full v)
        {
            float t = _Time.y * _PulseSpeed;
            float3 localPos = v.vertex.xyz;

            // 1. Ondulación orgánica tipo masa viviente (Venom / Slime)
            float wave = organicWave(localPos, t);
            localPos += v.normal * (wave * _PulseAmplitude);

            // 2. Aplastamiento contra la normal de la superficie (suelo, pared o techo)
            float3 surfN = normalize(_SurfaceNormal.xyz + float3(0.0001, 0.0001, 0.0001));
            float dotSurf = dot(normalize(localPos), -surfN);
            if (dotSurf > 0.0)
            {
                localPos += surfN * (dotSurf * _SquashAmount * 0.25);
            }

            // 3. Estiramiento en la dirección de la velocidad (Squash & Stretch)
            float speed = length(_Velocity.xyz);
            if (speed > 0.05)
            {
                float3 velDir = normalize(_Velocity.xyz);
                float align = dot(normalize(localPos), velDir);
                localPos += velDir * (align * clamp(speed * 0.04, 0.0, 0.35));
            }

            v.vertex.xyz = localPos;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = _Color.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;

            // Brillo Fresnel viscoso en los bordes para resaltar silueta en zonas oscuras
            half rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
            o.Emission = _RimColor.rgb * pow(rim, _RimPower) * 0.65;
            o.Alpha = _Color.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

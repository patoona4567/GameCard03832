// Leaves / grass clumps: wind sway, per-object colour variation, soft wrap lighting and rim light.
Shader "PangGolf/Foliage"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 0.6, 0.2, 1)
        _Sway ("Sway", Float) = 0.12
        _Variation ("Variation", Float) = 0.18
        _Rim ("Rim", Float) = 0.35
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull [_Cull]

        CGPROGRAM
        #pragma surface surf WrapLambert vertex:vert addshadow fullforwardshadows
        #pragma target 3.0

        fixed4 _Color;
        float _Sway;
        float _Variation;
        float _Rim;

        struct Input
        {
            float3 viewDir;
            float3 worldNormal;
            float variation;
            float height;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            float3 origin = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float phase = dot(origin.xz, float2(0.13, 0.17));
            float t = _Time.y;
            float gust = 0.6 + 0.4 * sin(t * 0.5 + phase);
            float lift = saturate((wp.y - origin.y) * 0.5 + 0.5);
            wp.x += sin(t * 1.6 + phase) * _Sway * gust * lift;
            wp.z += cos(t * 1.25 + phase * 1.3) * _Sway * 0.6 * gust * lift;
            wp += v.normal * sin(t * 5.0 + dot(wp, float3(2.1, 1.7, 2.3))) * _Sway * 0.12;
            v.vertex.xyz = mul(unity_WorldToObject, float4(wp, 1)).xyz;
            o.variation = frac(sin(dot(floor(origin.xz * 0.7), float2(12.9898, 78.233))) * 43758.5453);
            o.height = wp.y - origin.y;
        }

        half4 LightingWrapLambert(SurfaceOutput s, half3 lightDir, half atten)
        {
            half ndl = dot(s.Normal, lightDir) * 0.5 + 0.5;
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * (ndl * ndl * atten * 1.15);
            c.a = s.Alpha;
            return c;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            half3 c = _Color.rgb * (1 + (IN.variation - 0.5) * 2 * _Variation);
            c = lerp(c, c * half3(1.15, 1.08, 0.65), saturate(IN.variation * 2 - 1.2));
            c *= saturate(IN.height * 0.35 + 0.8);
            half rim = pow(1 - saturate(dot(normalize(IN.viewDir), normalize(IN.worldNormal))), 3) * _Rim;
            o.Albedo = c;
            o.Emission = c * rim;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}

// Bloom + ACES tone mapping + colour grading + vignette for the built-in pipeline.
Shader "Hidden/PangGolf/Post"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _BloomTex ("Bloom", 2D) = "black" {}
        _AOTex ("AO", 2D) = "white" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    float _Threshold;
    float _Knee;
    float _BloomIntensity;
    float _Exposure;
    float _Saturation;
    float _Contrast;
    float _Vignette;
    float4 _Tint;
    sampler2D _AOTex;
    float _AOStrength;
    float4 _AOParams;    // x radius (m), y intensity, z bias, w max distance
    float4 _ProjInfo;    // x = 1/P00, y = 1/P11, z = P00
    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
    float4 _CameraDepthTexture_TexelSize;

    float3 ViewPos(float2 uv)
    {
        float d = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));
        float2 ndc = uv * 2 - 1;
        return float3(ndc.x * _ProjInfo.x * d, ndc.y * _ProjInfo.y * d, d);
    }

    // Screen-space ambient occlusion (Alchemy style) on the half resolution buffer
    half4 FragAO(v2f_img i) : SV_Target
    {
        float2 uv = i.uv;
        #if UNITY_UV_STARTS_AT_TOP
        if (_MainTex_TexelSize.y < 0) uv.y = 1 - uv.y;
        #endif
        float3 p = ViewPos(uv);
        if (p.z > _AOParams.w) return 1;

        float2 tx = float2(_CameraDepthTexture_TexelSize.x, 0);
        float2 ty = float2(0, _CameraDepthTexture_TexelSize.y);
        float3 px1 = ViewPos(uv + tx), px0 = ViewPos(uv - tx);
        float3 py1 = ViewPos(uv + ty), py0 = ViewPos(uv - ty);
        float3 dx = abs(px1.z - p.z) < abs(p.z - px0.z) ? px1 - p : p - px0;
        float3 dy = abs(py1.z - p.z) < abs(p.z - py0.z) ? py1 - p : p - py0;
        float3 n = normalize(cross(dy, dx));

        float radius = _AOParams.x;
        float2 radiusUV = min(0.5 * radius * _ProjInfo.z / p.z, 0.08) * float2(1, _ScreenParams.x / _ScreenParams.y);
        float noise = frac(sin(dot(uv * _ScreenParams.xy, float2(12.9898, 78.233))) * 43758.5453);
        float sum = 0;
        const int N = 12;
        for (int k = 0; k < N; k++)
        {
            float a = (k + noise) * 2.39996;
            float r = (k + 0.5) / N;
            float2 off = float2(cos(a), sin(a)) * r * radiusUV;
            float3 v = ViewPos(uv + off) - p;
            float vv = dot(v, v);
            float range = saturate(1 - vv / (radius * radius * 4));
            sum += max(0, dot(v, n) - _AOParams.z * p.z) / (vv + 0.02) * range;
        }
        float ao = saturate(1 - _AOParams.y * radius * sum / N);
        ao = lerp(ao, 1, saturate((p.z - _AOParams.w * 0.6) / (_AOParams.w * 0.4)));
        return half4(ao, ao, ao, 1);
    }

    half4 FragAOBlur(v2f_img i) : SV_Target
    {
        float2 t = _MainTex_TexelSize.xy;
        half a = tex2D(_MainTex, i.uv).r * 2;
        a += tex2D(_MainTex, i.uv + float2(t.x, t.y)).r;
        a += tex2D(_MainTex, i.uv + float2(-t.x, t.y)).r;
        a += tex2D(_MainTex, i.uv + float2(t.x, -t.y)).r;
        a += tex2D(_MainTex, i.uv + float2(-t.x, -t.y)).r;
        a /= 6;
        return half4(a, a, a, 1);
    }

    half3 SampleBox(float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float2(-d, d).xxyy;
        half3 s = tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb +
                  tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb;
        return s * 0.25;
    }

    half3 Prefilter(half3 c)
    {
        half br = max(c.r, max(c.g, c.b));
        half rq = clamp(br - _Threshold + _Knee, 0, 2 * _Knee);
        rq = rq * rq / (4 * _Knee + 0.00001);
        half w = max(rq, br - _Threshold) / max(br, 0.00001);
        return c * w;
    }

    half3 ACESFilm(half3 x)
    {
        const half a = 2.51;
        const half b = 0.03;
        const half c = 2.43;
        const half d = 0.59;
        const half e = 0.14;
        return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
    }

    half4 FragPrefilter(v2f_img i) : SV_Target
    {
        return half4(Prefilter(min(SampleBox(i.uv, 1), 20)), 1);
    }

    half4 FragDown(v2f_img i) : SV_Target
    {
        return half4(SampleBox(i.uv, 1), 1);
    }

    half4 FragUp(v2f_img i) : SV_Target
    {
        return half4(SampleBox(i.uv, 0.5), 1);
    }

    half4 FragFinal(v2f_img i) : SV_Target
    {
        half3 c = tex2D(_MainTex, i.uv).rgb;
        c *= lerp(1, tex2D(_AOTex, i.uv).r, _AOStrength);
        c += tex2D(_BloomTex, i.uv).rgb * _BloomIntensity;
        c = ACESFilm(c * _Exposure);
        half l = dot(c, half3(0.2126, 0.7152, 0.0722));
        c = lerp(l.xxx, c, _Saturation);
        c = saturate((c - 0.35) * _Contrast + 0.35);
        c *= _Tint.rgb;
        float2 v = i.uv - 0.5;
        c *= saturate(1 - dot(v, v) * _Vignette);
        return half4(c, 1);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass // 0 prefilter
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragPrefilter
            ENDCG
        }
        Pass // 1 downsample
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragDown
            ENDCG
        }
        Pass // 2 upsample (additive)
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragUp
            ENDCG
        }
        Pass // 3 final
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragFinal
            ENDCG
        }
        Pass // 4 ambient occlusion
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragAO
            #pragma target 3.0
            ENDCG
        }
        Pass // 5 ambient occlusion blur
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragAOBlur
            ENDCG
        }
    }
    Fallback Off
}

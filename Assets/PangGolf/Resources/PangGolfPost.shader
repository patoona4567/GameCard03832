// Bloom + ACES tone mapping + colour grading + vignette for the built-in pipeline.
Shader "Hidden/PangGolf/Post"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _BloomTex ("Bloom", 2D) = "black" {}
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
    }
    Fallback Off
}

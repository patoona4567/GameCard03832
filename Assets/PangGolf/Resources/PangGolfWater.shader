// Stylised water: gentle waves, sky reflection (reflection probe), sun glints,
// depth based colour and shoreline foam.
Shader "PangGolf/Water"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.25, 0.75, 0.8, 1)
        _DeepColor ("Deep", Color) = (0.03, 0.25, 0.45, 1)
        _FoamColor ("Foam", Color) = (1, 1, 1, 1)
        _NormalTex ("Normal", 2D) = "bump" {}
        _Gloss ("Gloss", Float) = 220
        _DepthFade ("Depth Fade", Float) = 2.5
        _FoamDist ("Foam Distance", Float) = 0.6
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _NormalTex;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            fixed4 _ShallowColor;
            fixed4 _DeepColor;
            fixed4 _FoamColor;
            float _Gloss;
            float _DepthFade;
            float _FoamDist;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wpos : TEXCOORD0;
                float4 screen : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                float4 w = mul(unity_ObjectToWorld, v.vertex);
                w.y += sin(w.x * 0.35 + _Time.y * 1.3) * 0.04 + cos(w.z * 0.3 + _Time.y * 1.1) * 0.04;
                o.wpos = w.xyz;
                o.pos = mul(UNITY_MATRIX_VP, w);
                o.screen = ComputeScreenPos(o.pos);
                o.screen.z = -mul(UNITY_MATRIX_V, w).z;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 uv1 = i.wpos.xz * 0.08 + _Time.y * float2(0.02, 0.015);
                float2 uv2 = i.wpos.xz * 0.13 - _Time.y * float2(0.017, 0.024);
                float3 n1 = UnpackNormal(tex2D(_NormalTex, uv1));
                float3 n2 = UnpackNormal(tex2D(_NormalTex, uv2));
                float2 nxy = (n1.xy + n2.xy) * 0.35;
                float3 N = normalize(float3(nxy.x, 1, nxy.y));
                float3 V = normalize(_WorldSpaceCameraPos - i.wpos);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);

                float fres = pow(1 - saturate(dot(N, V)), 4) * 0.85 + 0.05;
                float3 R = reflect(-V, N);
                half4 skyData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, R);
                half3 refl = DecodeHDR(skyData, unity_SpecCube0_HDR);

                float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screen)));
                float depth = max(0, sceneZ - i.screen.z);
                float dfade = saturate(depth / _DepthFade);

                half3 water = lerp(_ShallowColor.rgb, _DeepColor.rgb, dfade);
                water *= _LightColor0.rgb * (0.6 + 0.4 * saturate(dot(N, L))) + ShadeSH9(float4(N, 1)) * 0.5;
                half3 col = lerp(water, refl, fres);

                float3 H = normalize(L + V);
                col += pow(saturate(dot(N, H)), _Gloss) * 3.0 * _LightColor0.rgb;

                float foam = 1 - saturate(depth / _FoamDist);
                foam *= 0.65 + 0.35 * sin(_Time.y * 2.2 + i.wpos.x * 0.7 + i.wpos.z * 0.5);
                foam = saturate(foam);
                col = lerp(col, _FoamColor.rgb, foam * 0.85);

                float alpha = saturate(lerp(0.45, 0.95, dfade) + fres * 0.3 + foam);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return half4(col, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}

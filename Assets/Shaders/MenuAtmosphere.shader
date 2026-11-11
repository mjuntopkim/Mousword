Shader "Mousword/MenuAtmosphere"
{
    Properties
    {
        [PerRendererData] _MainTex ("Background", 2D) = "white" {}
        _MenuTime ("Animation Time", Float) = 0
        _Strength ("Effect Strength", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Background" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            sampler2D _MainTex;
            float _MenuTime, _Strength;
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o;
            }
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f*f*(3-2*f);
                return lerp(lerp(hash(i), hash(i+float2(1,0)), f.x),
                    lerp(hash(i+float2(0,1)), hash(i+1), f.x), f.y);
            }
            float stars(float2 uv, float scale, float speed)
            {
                float2 p = uv * float2(1.77778,1) * scale + float2(_MenuTime*speed, -_MenuTime*speed*1.7);
                float2 cell = floor(p), f = frac(p);
                float seed = hash(cell);
                float2 center = float2(hash(cell+3.2), hash(cell+7.9)) * 0.7 + 0.15;
                float d = max(abs(f.x-center.x), abs(f.y-center.y));
                float sparkle = 0.45 + 0.55 * sin(_MenuTime*(0.5+seed) + seed*40);
                return (1-smoothstep(0.018,0.035,d)) * step(0.86, seed) * sparkle;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 pixelUV = floor(uv*float2(640,360))/float2(640,360);
                float t = _MenuTime;
                // The approved pixel art and wordmark stay sharp and stationary.
                float3 color = tex2D(_MainTex,uv).rgb;
                float n = noise(pixelUV*float2(5,9)+float2(-t*0.045,t*0.065));
                float ribbon = pow(saturate(0.5+0.5*sin(pixelUV.y*19-pixelUV.x*12+n*4-t*0.38)),9);
                float lower = (1-smoothstep(0.4,0.72,uv.y))*smoothstep(0.1,0.25,uv.y);
                float left = 1-smoothstep(0.4,0.8,uv.x);
                color += float3(0.035,0.13,0.12)*ribbon*lower*left*_Strength;
                float blade = (1-smoothstep(0.012,0.035,abs(uv.x-0.181)))
                    *smoothstep(0.11,0.18,uv.y)*(1-smoothstep(0.57,0.61,uv.y));
                float pulse = 0.5+0.5*sin(t*1.5-pixelUV.y*11+n*2);
                color += float3(0.01,0.16,0.18)*blade*pulse*_Strength;
                float quietUI = 1-smoothstep(0.52,0.65,uv.x);
                float cyan = stars(pixelUV,24,0.045);
                float amber = stars(pixelUV+float2(0.31,0.17),18,0.025);
                color += (cyan*float3(0.25,0.9,0.85)+amber*float3(0.7,0.45,0.12))*quietUI*_Strength;
                return float4(color,1)*i.color;
            }
            ENDCG
        }
    }
}

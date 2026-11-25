// 피격 플래시: 스프라이트의 모양(알파)만 남기고 단색으로 칠한다.
// HitFlash.cs 가 Shader.Find 로 찾으므로 빌드에 포함되도록 Resources 폴더에 둔다.
Shader "Mousword/SpriteHitFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            fixed4 _FlashColor;

            fixed4 frag(v2f IN) : SV_Target
            {
                // 스프라이트 색(틴트)은 무시하고 알파만 사용 → 투명한 히트박스 스프라이트는 그대로 투명
                fixed a = SampleSpriteTexture(IN.texcoord).a * IN.color.a * _FlashColor.a;
                return fixed4(_FlashColor.rgb * a, a);
            }
            ENDCG
        }
    }
}

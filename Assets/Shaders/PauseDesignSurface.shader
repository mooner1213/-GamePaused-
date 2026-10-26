// 성벽 타일·배경·도트 카운트의 원색과 정지 흑백을 같은 방식으로 표시합니다.
Shader "PauseClimb/DesignSurface"
{
    Properties
    {
        [PerRendererData] _MainTex ("원본 도트 그림", 2D) = "white" {}
        _Frozen ("시간 정지 흑백", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Frozen;
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct Output { float4 position : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            // 스프라이트와 타일맵의 정점색을 동일하게 전달합니다.
            Output Vert(Input input)
            {
                Output output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            // 원본 그림은 바꾸지 않고 정지 때 밝기만 흑백으로 바꿉니다.
            fixed4 Frag(Output input) : SV_Target
            {
                fixed4 source = tex2D(_MainTex, input.uv) * input.color;
                fixed gray = dot(source.rgb, fixed3(0.299, 0.587, 0.114));
                return fixed4(lerp(source.rgb, gray.xxx, saturate(_Frozen)), source.a);
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}

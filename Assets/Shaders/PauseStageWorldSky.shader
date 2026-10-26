// 비교 스테이지의 배경 원본을 유지하고 월드 높이에 따른 전체 색조와 정지 흑백만 적용합니다.
Shader "PauseClimb/StageWorldSky"
{
    Properties
    {
        [PerRendererData] _MainTex ("테마 배경 원본 그림", 2D) = "white" {}
        _DaySky ("하층 배경에 곱할 색", Color) = (1, 1, 1, 1)
        _SunsetSky ("상층 배경에 곱할 색", Color) = (1, 0.86, 0.75, 1)
        _SunsetCloud ("기존 배경 제어기 호환용 구름 색", Color) = (1, 1, 1, 1)
        _SunsetStart ("색조 전환 시작 높이", Float) = 70
        _SunsetEnd ("색조 전환 완성 높이", Float) = 175
        _EdgeFogColor ("배경 조각의 상하 연결 안개색", Color) = (0.55, 0.65, 0.75, 1)
        _EdgeFade ("상하 연결 안개 폭 · 그림 높이 비율", Float) = 0.07
        _Frozen ("시간 정지 흑백", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _DaySky, _SunsetSky, _SunsetCloud, _EdgeFogColor;
            float _SunsetStart, _SunsetEnd, _EdgeFade, _Frozen;

            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct Interpolator { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float height : TEXCOORD1; fixed4 color : COLOR; };

            // 그림과 오브젝트의 위치를 바꾸지 않고 현재 월드 높이만 색 계산에 전달합니다.
            Interpolator Vert(Input input)
            {
                Interpolator output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                output.height = mul(unity_ObjectToWorld, input.vertex).y;
                return output;
            }

            // [NOTE] 파란색 분류나 색 치환 없이 원본 픽셀과 SpriteRenderer 색에 공통 색조만 곱합니다.
            fixed4 Frag(Interpolator input) : SV_Target
            {
                fixed4 source = tex2D(_MainTex, input.uv);
                // [NOTE] 원경 건물을 뒤집지 않고 상하 가장자리만 공통 안개색으로 이어 붙입니다.
                float edgeDistance = min(input.uv.y, 1.0 - input.uv.y);
                float sourceWeight = smoothstep(0.0, max(0.001, _EdgeFade), edgeDistance);
                fixed3 joinedSource = lerp(_EdgeFogColor.rgb, source.rgb, sourceWeight);
                float transition = smoothstep(_SunsetStart, max(_SunsetStart + 0.001, _SunsetEnd), input.height);
                fixed3 tint = lerp(_DaySky.rgb, _SunsetSky.rgb, transition);
                fixed3 result = joinedSource * tint * input.color.rgb;
                float gray = dot(result, fixed3(0.299, 0.587, 0.114));
                return fixed4(lerp(result, gray.xxx, saturate(_Frozen)), source.a * input.color.a);
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}

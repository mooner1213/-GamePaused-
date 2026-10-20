// 고정 배경의 월드 높이에서만 석양 색을 계산합니다. 카메라 좌표와 시간은 사용하지 않습니다.
Shader "PauseClimb/WorldSky"
{
    Properties
    {
        [PerRendererData] _MainTex ("기존 구름 또는 들판 그림", 2D) = "white" {}
        _DaySky ("낮 하늘 색", Color) = (0.10, 0.56, 0.98, 1)
        _SunsetSky ("석양 하늘 색", Color) = (0.91, 0.37, 0.31, 1)
        _SunsetCloud ("석양 구름 색", Color) = (1, 0.78, 0.65, 1)
        _SunsetStart ("석양 시작 높이", Float) = 70
        _SunsetEnd ("석양 완성 높이", Float) = 175
        _IsMeadow ("초록 들판 보존", Float) = 0
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
            fixed4 _DaySky, _SunsetSky, _SunsetCloud;
            float _SunsetStart, _SunsetEnd, _IsMeadow, _Frozen;

            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct Interpolator { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float height : TEXCOORD1; fixed4 color : COLOR; };

            // 각 조각의 로컬 좌표를 월드 높이로 전달하므로 조각 사이에도 같은 색이 나옵니다.
            Interpolator Vert(Input input)
            {
                Interpolator output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv; output.color = input.color;
                output.height = mul(unity_ObjectToWorld, input.vertex).y;
                return output;
            }

            // 원본 도트 구름은 유지하고 파란 바탕만 공통 하늘색으로 연결하여 반복 경계를 없앱니다.
            fixed4 Frag(Interpolator input) : SV_Target
            {
                fixed4 source = tex2D(_MainTex, input.uv);
                float sunset = smoothstep(_SunsetStart, max(_SunsetStart + 0.001, _SunsetEnd), input.height);
                fixed3 sky = lerp(_DaySky.rgb, _SunsetSky.rgb, sunset);
                fixed3 cloud = source.rgb * lerp(fixed3(1, 1, 1), _SunsetCloud.rgb, sunset);
                // 밝은 파란 바탕을 구름으로 오인하지 않도록 밝기 대신 빨강/파랑 비율로 구분합니다.
                float cloudMask = smoothstep(0.60, 0.76, source.r / max(source.b, 0.001));
                fixed3 result = lerp(sky, cloud, cloudMask);
                float meadowMask = step(0.05, source.g - source.b) * step(0.05, source.g - source.r) * _IsMeadow;
                result = lerp(result, source.rgb, meadowMask) * input.color.rgb;
                float gray = dot(result, fixed3(0.299, 0.587, 0.114));
                return fixed4(lerp(result, gray.xxx, _Frozen), source.a * input.color.a);
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}

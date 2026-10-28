// 월드 좌표에 고정된 도트 별과 성운으로 기존 하늘에서 우주까지 이어집니다.
Shader "PauseClimb/SpaceSky"
{
    Properties
    {
        _FadeStart ("우주 전환 시작 높이", Float) = 210
        _FadeEnd ("우주 전환 완성 높이", Float) = 360
        _SpaceColor ("우주 바탕색", Color) = (0.008, 0.012, 0.045, 1)
        _Frozen ("시간 정지 흑백", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            float _FadeStart, _FadeEnd, _Frozen;
            float4 _SpaceColor;
            struct Input { float4 vertex : POSITION; };
            struct Output { float4 position : SV_POSITION; float2 world : TEXCOORD0; };
            // 카메라를 따라 움직이지 않는 월드 좌표를 사용합니다.
            Output Vert(Input v) { Output o; o.position=UnityObjectToClipPos(v.vertex); o.world=mul(unity_ObjectToWorld,v.vertex).xy; return o; }
            // 같은 도트 칸은 높이와 무관하게 같은 별을 유지합니다.
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float4 Frag(Output i) : SV_Target
            {
                float2 pixel=floor(i.world*16.0)/16.0;
                float t=smoothstep(_FadeStart,max(_FadeStart+1,_FadeEnd),pixel.y);
                float high=smoothstep(_FadeEnd,1200,pixel.y);
                float3 atmosphere=lerp(float3(.15,.10,.28),_SpaceColor.rgb,high);
                float haze=pow(saturate(.5+.25*sin(pixel.x*.12+pixel.y*.009)+.25*sin(pixel.x*.27-pixel.y*.015)),4);
                atmosphere+=float3(.035,.015,.07)*haze;
                float2 cell=floor(pixel/3.0), local=frac(pixel/3.0);
                float2 star=float2(.15+.7*Hash(cell+8),.15+.7*Hash(cell+19));
                float d=max(abs(local.x-star.x),abs(local.y-star.y));
                float stars=step(.78,Hash(cell))*step(d,.021);
                float3 color=lerp(atmosphere,float3(.78,.86,1),stars*smoothstep(_FadeStart+30,_FadeEnd+100,pixel.y));
                color=lerp(color,dot(color,float3(.299,.587,.114)).xxx,saturate(_Frozen));
                return float4(color,t);
            }
            ENDCG
        }
    }
}

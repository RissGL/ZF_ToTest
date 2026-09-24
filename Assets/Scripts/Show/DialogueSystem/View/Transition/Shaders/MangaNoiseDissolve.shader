// 漫画网点溶解擦除（NEO 那种）：**整页沿一个方向一起被擦掉**，擦过去的那条前沿是
// 「噪声打毛 + 网点 + 水平拉丝 + 墨边」的痕迹，旧画面碎掉多少就露出下面多少新画面。
//
// ★ 主次关系（这个搞反了就不像）：
//   主导 = **方向梯度**：整页一起走，前沿是一条横扫整屏的带
//   辅助 = 噪声：只用来把**前沿打毛**（_FrontJitter），或者在带内把边界打碎成网点
//   ✗ 错误做法：让噪声决定谁先消失 —— 那是"全屏随机溶解"，满屏零散碎点，不是擦除
//
// 五层：
//   ① 方向场    ：dot(screenUv - 0.5, 方向) —— 整页一起推进
//   ② 前沿打毛  ：把噪声按比例加到方向场上 —— 前沿不是笔直的，是毛的
//   ③ 网点（半调）：在前沿**带内**把存活的像素打碎成网点 —— 漫画印刷味
//   ④ 水平拉丝  ：前沿带内按随机条带横向拉 UV —— 撕裂/拖拽
//   ⑤ 墨边      ：前沿带内压深色 + 加强 alpha —— 墨水侵蚀的毛边
//
// 用法：材质贴在**旧画面**的部件上（Graphic.material），逐帧改 _Progress：0 = 原样，1 = 擦没了。
// 屏幕空间 UV 保证整屏是一张统一的擦除图（各部件不会各擦各的）。
//
// ★ 属性名和 MangaNoiseDissolveTransition 里的字段一一对应，改名要两边一起改。

Shader "ZF/Dialogue/MangaNoiseDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // ---- ①② 方向 + 前沿 ----
        _Angle ("方向（度，0 = 左→右）", Range(0, 360)) = 0
        _Progress ("擦除进度 0→1", Range(0, 1)) = 0
        _EdgeWidth ("前沿带宽度（网点/拉丝/墨边都在这条带里）", Range(0.005, 0.6)) = 0.18
        _EdgeSoftness ("前沿软度", Range(0.001, 0.5)) = 0.045
        _FrontJitter ("前沿毛糙度（0 = 笔直一条线，0.1~0.2 = 手绘撕裂感）", Range(0, 0.5)) = 0.12
        _NoiseTex ("噪波图（可平铺；空 = 纯灰）", 2D) = "gray" {}
        _NoiseScale ("噪波密度", Range(0.5, 40)) = 6
        _NoiseScroll ("噪波流动速度 XY", Vector) = (0.35, -0.12, 0, 0)
        _Scatter ("零散颗粒（0 = 整页一起走；调高会变成全屏随机溶解，慎用）", Range(0, 1)) = 0.12

        // ---- ③ 网点 ----
        _HalftoneAmount ("网点参与程度（越大前沿越像印刷网点）", Range(0, 1)) = 0.6
        _HalftoneScale ("网点大小（屏幕像素）", Range(1, 40)) = 7
        _HalftoneAngle ("网点角度（度）", Range(0, 180)) = 45
        _HalftoneSharpness ("网点锐度", Range(0.01, 1)) = 0.6

        // ---- ④ 水平拉丝 ----
        _Smear ("拉丝强度（屏幕像素）", Range(0, 300)) = 55
        _SmearBands ("拉丝条带密度", Range(1, 80)) = 22
        _SmearBandRandom ("条带随机度（1 = 完全随机条带）", Range(0, 1)) = 0.75

        // ---- ⑤ 墨边 ----
        _EdgeInk ("墨边强度", Range(0, 3)) = 1.3
        _EdgeColor ("墨边颜色", Color) = (0.04, 0.04, 0.05, 1)

        // ---- UGUI 标准（遮罩 / 裁剪要用，别删）----
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "MANGA_WIPE"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"

            // ★ 不用 UGUI 的 _ClipRect 路径：工程里没有 Mask / RectMask2D，
            //   而 RectMask2D 的矩形裁剪 UGUI 是在 CanvasRenderer 层做的（EnableRectClipping），
            //   不需要 shader 参与。留着一个"关键字被打开、_ClipRect 却是 0"的路径只会让画面莫名消失。

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;

            float _Angle;
            float _Progress;
            float _EdgeWidth;
            float _EdgeSoftness;
            float _FrontJitter;
            float _Scatter;

            sampler2D _NoiseTex;
            float _NoiseScale;
            float4 _NoiseScroll;

            float _HalftoneAmount;
            float _HalftoneScale;
            float _HalftoneAngle;
            float _HalftoneSharpness;

            float _Smear;
            float _SmearBands;
            float _SmearBandRandom;

            float _EdgeInk;
            fixed4 _EdgeColor;

            v2f vert(appdata_t v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            // 网点：把屏幕按像素切成格子，格子里画圆点，返回 1 = 点在格子外
            float Halftone(float2 screenUv, float scale, float angleDeg, float sharpness)
            {
                float2 p = screenUv * _ScreenParams.xy / max(scale, 1.0);
                float a = radians(angleDeg);
                float s = sin(a);
                float c = cos(a);
                float2 r = float2(p.x * c - p.y * s, p.x * s + p.y * c);

                float2 cell = frac(r) - 0.5;
                float d = length(cell) * 2.0;

                float lo = 0.5 - sharpness * 0.5;
                float hi = 0.5 + sharpness * 0.5;
                return smoothstep(lo, hi, d);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 screenUv = i.screenPos.xy / max(i.screenPos.w, 1e-5);

                // ① 方向场：整页沿 _Angle 一起推进（0 = 最后被擦掉，1 = 最先被擦掉）
                float2 dir = float2(cos(radians(_Angle)), sin(radians(_Angle)));
                float gradient = saturate(dot(screenUv - 0.5, dir) + 0.5);

                // 噪波：两组不同速度/尺度叠一层，免得看上去是死图
                float n1 = tex2D(_NoiseTex, screenUv * _NoiseScale + _NoiseScroll.xy * _Time.y).r;
                float n2 = tex2D(_NoiseTex, screenUv * _NoiseScale * 0.53 - _NoiseScroll.xy * 1.7 * _Time.y).r;
                float noise = saturate(n1 * 0.68 + n2 * 0.32);

                // ② 前沿打毛：噪声**只**扰动前沿，整页还是一起走
                float field = gradient + (noise - 0.5) * 2.0 * _FrontJitter;

                // 想要一点零散颗粒时才让噪声当主体（默认很低，保持"整页一起擦"）
                field = lerp(field, noise, _Scatter);

                float progress = _Progress;

                // 前沿带：0 = 带外，1 = 正好在擦除线上
                float band = 1.0 - saturate(abs(field - progress) / max(_EdgeWidth, 0.001));

                // ③ 网点只吃前沿带：整页是干净的，边缘才是网点化的
                float dots = Halftone(screenUv, _HalftoneScale, _HalftoneAngle, _HalftoneSharpness);
                float halftoneMask = lerp(1.0, dots, saturate(_HalftoneAmount * band));

                // 擦除判定：field < progress 的地方已经没了
                float softness = max(_EdgeSoftness, 0.001);
                float keep = smoothstep(progress - softness, progress + softness, field);
                keep *= halftoneMask;

                // ④ 水平拉丝：前沿带内按条带随机横向拉 UV
                float bandNoise = tex2D(_NoiseTex, float2(screenUv.y * _SmearBands, _Time.y * 0.07)).r;
                float bandMask = lerp(1.0, bandNoise, _SmearBandRandom);
                float smearPixels = _Smear * band * bandMask * (n2 - 0.5) * 2.0;

                float2 uv = i.texcoord;
                uv.x += smearPixels / max(_ScreenParams.x, 1.0);

                fixed4 col = tex2D(_MainTex, uv) * i.color;
                col.a *= keep;

                // ⑤ 墨边：前沿带内还活着的地方压深、加强 alpha（墨水侵蚀的毛边）
                float ink = saturate(band * bandMask * _EdgeInk);
                col.rgb = lerp(col.rgb, _EdgeColor.rgb, ink);
                col.a = saturate(col.a + ink * 0.7 * keep);

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}

Shader "ZF/Dialogue/MangaNoiseDissolveTMP" {

Properties {
	_FaceTex			("Face Texture", 2D) = "white" {}
	_FaceUVSpeedX		("Face UV Speed X", Range(-5, 5)) = 0.0
	_FaceUVSpeedY		("Face UV Speed Y", Range(-5, 5)) = 0.0
	[HDR]_FaceColor		("Face Color", Color) = (1,1,1,1)
	_FaceDilate			("Face Dilate", Range(-1,1)) = 0

	[HDR]_OutlineColor	("Outline Color", Color) = (0,0,0,1)
	_OutlineTex			("Outline Texture", 2D) = "white" {}
	_OutlineUVSpeedX	("Outline UV Speed X", Range(-5, 5)) = 0.0
	_OutlineUVSpeedY	("Outline UV Speed Y", Range(-5, 5)) = 0.0
	_OutlineWidth		("Outline Thickness", Range(0, 1)) = 0
	_OutlineSoftness	("Outline Softness", Range(0,1)) = 0

	_Bevel				("Bevel", Range(0,1)) = 0.5
	_BevelOffset		("Bevel Offset", Range(-0.5,0.5)) = 0
	_BevelWidth			("Bevel Width", Range(-.5,0.5)) = 0
	_BevelClamp			("Bevel Clamp", Range(0,1)) = 0
	_BevelRoundness		("Bevel Roundness", Range(0,1)) = 0

	_LightAngle			("Light Angle", Range(0.0, 6.2831853)) = 3.1416
	[HDR]_SpecularColor	("Specular", Color) = (1,1,1,1)
	_SpecularPower		("Specular", Range(0,4)) = 2.0
	_Reflectivity		("Reflectivity", Range(5.0,15.0)) = 10
	_Diffuse			("Diffuse", Range(0,1)) = 0.5
	_Ambient			("Ambient", Range(1,0)) = 0.5

	_BumpMap 			("Normal map", 2D) = "bump" {}
	_BumpOutline		("Bump Outline", Range(0,1)) = 0
	_BumpFace			("Bump Face", Range(0,1)) = 0

	_ReflectFaceColor	("Reflection Color", Color) = (0,0,0,1)
	_ReflectOutlineColor("Reflection Color", Color) = (0,0,0,1)
	_Cube 				("Reflection Cubemap", Cube) = "black" { /* TexGen CubeReflect */ }
	_EnvMatrixRotation	("Texture Rotation", vector) = (0, 0, 0, 0)


	[HDR]_UnderlayColor	("Border Color", Color) = (0,0,0, 0.5)
	_UnderlayOffsetX	("Border OffsetX", Range(-1,1)) = 0
	_UnderlayOffsetY	("Border OffsetY", Range(-1,1)) = 0
	_UnderlayDilate		("Border Dilate", Range(-1,1)) = 0
	_UnderlaySoftness	("Border Softness", Range(0,1)) = 0

	[HDR]_GlowColor			("Color", Color) = (0, 1, 0, 0.5)
	_GlowOffset			("Offset", Range(-1,1)) = 0
	_GlowInner			("Inner", Range(0,1)) = 0.05
	_GlowOuter			("Outer", Range(0,1)) = 0.05
	_GlowPower			("Falloff", Range(1, 0)) = 0.75

	_WeightNormal		("Weight Normal", float) = 0
	_WeightBold			("Weight Bold", float) = 0.5

	_ShaderFlags		("Flags", float) = 0
	_ScaleRatioA		("Scale RatioA", float) = 1
	_ScaleRatioB		("Scale RatioB", float) = 1
	_ScaleRatioC		("Scale RatioC", float) = 1

	_MainTex			("Font Atlas", 2D) = "white" {}
	_TextureWidth		("Texture Width", float) = 512
	_TextureHeight		("Texture Height", float) = 512
	_GradientScale		("Gradient Scale", float) = 5.0
	_ScaleX				("Scale X", float) = 1.0
	_ScaleY				("Scale Y", float) = 1.0
	_PerspectiveFilter	("Perspective Correction", Range(0, 1)) = 0.875
	_Sharpness			("Sharpness", Range(-1,1)) = 0

	_VertexOffsetX		("Vertex OffsetX", float) = 0
	_VertexOffsetY		("Vertex OffsetY", float) = 0

	_MaskCoord			("Mask Coordinates", vector) = (0, 0, 32767, 32767)
	_ClipRect			("Clip Rect", vector) = (-32767, -32767, 32767, 32767)
	_MaskSoftnessX		("Mask SoftnessX", float) = 0
	_MaskSoftnessY		("Mask SoftnessY", float) = 0

	_StencilComp		("Stencil Comparison", Float) = 8
	_Stencil			("Stencil ID", Float) = 0
	_StencilOp			("Stencil Operation", Float) = 0
	_StencilWriteMask	("Stencil Write Mask", Float) = 255
	_StencilReadMask	("Stencil Read Mask", Float) = 255

	_CullMode			("Cull Mode", Float) = 0
	_ColorMask			("Color Mask", Float) = 15

	// ===== 漫画网点擦除（追加；属性名和 MangaNoiseDissolveTransition 的字段一一对应）=====
	_Angle ("方向（度，0 = 左→右）", Range(0, 360)) = 0
	_Progress ("擦除进度 0→1", Range(0, 1)) = 0
	_EdgeWidth ("前沿带宽度", Range(0.005, 0.6)) = 0.18
	_EdgeSoftness ("前沿软度", Range(0.001, 0.5)) = 0.045
	_FrontJitter ("前沿毛糙度（0 = 笔直一条线）", Range(0, 0.5)) = 0.12
	_Scatter ("零散颗粒（0 = 整页一起擦）", Range(0, 1)) = 0.12
	_NoiseTex ("噪波图（可平铺）", 2D) = "gray" {}
	_NoiseScale ("噪波密度", Range(0.5, 40)) = 6
	_NoiseScroll ("噪波流动速度 XY", Vector) = (0.35, -0.12, 0, 0)
	_HalftoneAmount ("网点参与程度", Range(0, 1)) = 0.6
	_HalftoneScale ("网点大小（屏幕像素）", Range(1, 40)) = 7
	_HalftoneAngle ("网点角度（度）", Range(0, 180)) = 45
	_HalftoneSharpness ("网点锐度", Range(0.01, 1)) = 0.6
	_Smear ("拉丝强度（TMP 版不做像素级拉丝，属性保留只为参数统一）", Range(0, 300)) = 0
	_SmearBands ("拉丝条带密度", Range(1, 80)) = 22
	_SmearBandRandom ("条带随机度", Range(0, 1)) = 0.75
	_EdgeInk ("墨边强度", Range(0, 3)) = 1.3
	_EdgeColor ("墨边颜色", Color) = (0.04, 0.04, 0.05, 1)
}

SubShader {

	Tags
	{
		"Queue"="Transparent"
		"IgnoreProjector"="True"
		"RenderType"="Transparent"
	}

	Stencil
	{
		Ref [_Stencil]
		Comp [_StencilComp]
		Pass [_StencilOp]
		ReadMask [_StencilReadMask]
		WriteMask [_StencilWriteMask]
	}

	Cull [_CullMode]
	ZWrite Off
	Lighting Off
	Fog { Mode Off }
	ZTest [unity_GUIZTestMode]
	Blend One OneMinusSrcAlpha
	ColorMask [_ColorMask]

	Pass {
		CGPROGRAM
		#pragma target 3.0
		#pragma vertex VertShader
		#pragma fragment PixShader
		#pragma shader_feature __ BEVEL_ON
		#pragma shader_feature __ UNDERLAY_ON UNDERLAY_INNER
		#pragma shader_feature __ GLOW_ON

		#pragma multi_compile __ UNITY_UI_CLIP_RECT
		#pragma multi_compile __ UNITY_UI_ALPHACLIP

		#include "UnityCG.cginc"
		#include "UnityUI.cginc"
		// ★ TMP 的这两个 cginc 只存在于「导入到工程」的 TextMesh Pro 目录里（UPM 包里没有），
		//   本 shader 又不在那个目录下，所以必须写成工程根路径；写相对文件名会报
		//   "Couldn't open include file 'TMPro_Properties.cginc'"。
		#include "Assets/TextMesh Pro/Shaders/TMPro_Properties.cginc"
		#include "Assets/TextMesh Pro/Shaders/TMPro.cginc"


		// ===== 漫画网点擦除：uniform 声明 + 网点函数（TMP 自己那套 SDF 计算一个字没动）=====
		sampler2D _NoiseTex;
		float _NoiseScale;
		float4 _NoiseScroll;
		float _Angle;
		float _Progress;
		float _EdgeWidth;
		float _EdgeSoftness;
		float _FrontJitter;
		float _Scatter;
		float _HalftoneAmount;
		float _HalftoneScale;
		float _HalftoneAngle;
		float _HalftoneSharpness;
		float _Smear;
		float _SmearBands;
		float _SmearBandRandom;
		float _EdgeInk;
		fixed4 _EdgeColor;

		// 网点：把屏幕按像素切成格子，格子里画圆点，返回 1 = 点在格子外
		float WipeHalftone(float2 screenUv, float scale, float angleDeg, float sharpness)
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
		struct vertex_t {
			UNITY_VERTEX_INPUT_INSTANCE_ID
			float4	position		: POSITION;
			float3	normal			: NORMAL;
			fixed4	color			: COLOR;
			float2	texcoord0		: TEXCOORD0;
			float2	texcoord1		: TEXCOORD1;
		};


		struct pixel_t {
			UNITY_VERTEX_INPUT_INSTANCE_ID
			UNITY_VERTEX_OUTPUT_STEREO
			float4	position		: SV_POSITION;
			fixed4	color			: COLOR;
			float2	atlas			: TEXCOORD0;		// Atlas
			float4	param			: TEXCOORD1;		// alphaClip, scale, bias, weight
			float4	mask			: TEXCOORD2;		// Position in object space(xy), pixel Size(zw)
			float3	viewDir			: TEXCOORD3;

		#if (UNDERLAY_ON || UNDERLAY_INNER)
			float4	texcoord2		: TEXCOORD4;		// u,v, scale, bias
			fixed4	underlayColor	: COLOR1;
		#endif
			float4 textures			: TEXCOORD5;
			float4	screenPos		: TEXCOORD6;		// 溶解用：屏幕空间 UV
		};

		// Used by Unity internally to handle Texture Tiling and Offset.
		float4 _FaceTex_ST;
		float4 _OutlineTex_ST;

		pixel_t VertShader(vertex_t input)
		{
			pixel_t output;

			UNITY_INITIALIZE_OUTPUT(pixel_t, output);
			UNITY_SETUP_INSTANCE_ID(input);
			UNITY_TRANSFER_INSTANCE_ID(input,output);
			UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

			float bold = step(input.texcoord1.y, 0);

			float4 vert = input.position;
			vert.x += _VertexOffsetX;
			vert.y += _VertexOffsetY;

			float4 vPosition = UnityObjectToClipPos(vert);

			float2 pixelSize = vPosition.w;
			pixelSize /= float2(_ScaleX, _ScaleY) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
			float scale = rsqrt(dot(pixelSize, pixelSize));
			scale *= abs(input.texcoord1.y) * _GradientScale * (_Sharpness + 1);
			if (UNITY_MATRIX_P[3][3] == 0) scale = lerp(abs(scale) * (1 - _PerspectiveFilter), scale, abs(dot(UnityObjectToWorldNormal(input.normal.xyz), normalize(WorldSpaceViewDir(vert)))));

			float weight = lerp(_WeightNormal, _WeightBold, bold) / 4.0;
			weight = (weight + _FaceDilate) * _ScaleRatioA * 0.5;

			float bias =(.5 - weight) + (.5 / scale);

			float alphaClip = (1.0 - _OutlineWidth * _ScaleRatioA - _OutlineSoftness * _ScaleRatioA);

		#if GLOW_ON
			alphaClip = min(alphaClip, 1.0 - _GlowOffset * _ScaleRatioB - _GlowOuter * _ScaleRatioB);
		#endif

			alphaClip = alphaClip / 2.0 - ( .5 / scale) - weight;

		#if (UNDERLAY_ON || UNDERLAY_INNER)
			float4 underlayColor = _UnderlayColor;
			underlayColor.rgb *= underlayColor.a;

			float bScale = scale;
			bScale /= 1 + ((_UnderlaySoftness*_ScaleRatioC) * bScale);
			float bBias = (0.5 - weight) * bScale - 0.5 - ((_UnderlayDilate * _ScaleRatioC) * 0.5 * bScale);

			float x = -(_UnderlayOffsetX * _ScaleRatioC) * _GradientScale / _TextureWidth;
			float y = -(_UnderlayOffsetY * _ScaleRatioC) * _GradientScale / _TextureHeight;
			float2 bOffset = float2(x, y);
		#endif

			// Generate UV for the Masking Texture
			float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
			float2 maskUV = (vert.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);

			// Support for texture tiling and offset
			float2 textureUV = UnpackUV(input.texcoord1.x);
			float2 faceUV = TRANSFORM_TEX(textureUV, _FaceTex);
			float2 outlineUV = TRANSFORM_TEX(textureUV, _OutlineTex);


			output.position = vPosition;
			output.color = input.color;
			output.atlas =	input.texcoord0;
			output.param =	float4(alphaClip, scale, bias, weight);
			output.mask = half4(vert.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_MaskSoftnessX, _MaskSoftnessY) + pixelSize.xy));
			output.viewDir =	mul((float3x3)_EnvMatrix, _WorldSpaceCameraPos.xyz - mul(unity_ObjectToWorld, vert).xyz);
			#if (UNDERLAY_ON || UNDERLAY_INNER)
			output.texcoord2 = float4(input.texcoord0 + bOffset, bScale, bBias);
			output.underlayColor =	underlayColor;
			#endif
			output.textures = float4(faceUV, outlineUV);


			output.screenPos = ComputeScreenPos(vPosition);
			return output;
		}


		fixed4 PixShader(pixel_t input) : SV_Target
		{
			UNITY_SETUP_INSTANCE_ID(input);

			float c = tex2D(_MainTex, input.atlas).a;

		#ifndef UNDERLAY_ON
			clip(c - input.param.x);
		#endif

			float	scale	= input.param.y;
			float	bias	= input.param.z;
			float	weight	= input.param.w;
			float	sd = (bias - c) * scale;

			float outline = (_OutlineWidth * _ScaleRatioA) * scale;
			float softness = (_OutlineSoftness * _ScaleRatioA) * scale;

			half4 faceColor = _FaceColor;
			half4 outlineColor = _OutlineColor;

			faceColor.rgb *= input.color.rgb;

			faceColor *= tex2D(_FaceTex, input.textures.xy + float2(_FaceUVSpeedX, _FaceUVSpeedY) * _Time.y);
			outlineColor *= tex2D(_OutlineTex, input.textures.zw + float2(_OutlineUVSpeedX, _OutlineUVSpeedY) * _Time.y);

			faceColor = GetColor(sd, faceColor, outlineColor, outline, softness);

		#if BEVEL_ON
			float3 dxy = float3(0.5 / _TextureWidth, 0.5 / _TextureHeight, 0);
			float3 n = GetSurfaceNormal(input.atlas, weight, dxy);

			float3 bump = UnpackNormal(tex2D(_BumpMap, input.textures.xy + float2(_FaceUVSpeedX, _FaceUVSpeedY) * _Time.y)).xyz;
			bump *= lerp(_BumpFace, _BumpOutline, saturate(sd + outline * 0.5));
			n = normalize(n- bump);

			float3 light = normalize(float3(sin(_LightAngle), cos(_LightAngle), -1.0));

			float3 col = GetSpecular(n, light);
			faceColor.rgb += col*faceColor.a;
			faceColor.rgb *= 1-(dot(n, light)*_Diffuse);
			faceColor.rgb *= lerp(_Ambient, 1, n.z*n.z);

			fixed4 reflcol = texCUBE(_Cube, reflect(input.viewDir, -n));
			faceColor.rgb += reflcol.rgb * lerp(_ReflectFaceColor.rgb, _ReflectOutlineColor.rgb, saturate(sd + outline * 0.5)) * faceColor.a;
		#endif

		#if UNDERLAY_ON
			float d = tex2D(_MainTex, input.texcoord2.xy).a * input.texcoord2.z;
			faceColor += input.underlayColor * saturate(d - input.texcoord2.w) * (1 - faceColor.a);
		#endif

		#if UNDERLAY_INNER
			float d = tex2D(_MainTex, input.texcoord2.xy).a * input.texcoord2.z;
			faceColor += input.underlayColor * (1 - saturate(d - input.texcoord2.w)) * saturate(1 - sd) * (1 - faceColor.a);
		#endif

		#if GLOW_ON
			float4 glowColor = GetGlowColor(sd, scale);
			faceColor.rgb += glowColor.rgb * glowColor.a;
		#endif

		// Alternative implementation to UnityGet2DClipping with support for softness.
		#if UNITY_UI_CLIP_RECT
			half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
			faceColor *= m.x * m.y;
		#endif

		#if UNITY_UI_ALPHACLIP
			clip(faceColor.a - 0.001);
		#endif
		// ===================== 漫画网点擦除（追加；只削 alpha，不动 TMP 的 SDF）=====================
		// 方向主导（整页沿一个方向一起擦）+ 噪声只打毛前沿 + 网点只吃前沿带 + 墨边。
		// ★ 故意不做水平拉丝：TMP 的 UV 是字体图集坐标，按像素横向拉 UV 会把字形采花。
		{
			float2 screenUv = input.screenPos.xy / max(input.screenPos.w, 1e-5);

			float2 dir = float2(cos(radians(_Angle)), sin(radians(_Angle)));
			float gradient = saturate(dot(screenUv - 0.5, dir) + 0.5);

			float n1 = tex2D(_NoiseTex, screenUv * _NoiseScale + _NoiseScroll.xy * _Time.y).r;
			float n2 = tex2D(_NoiseTex, screenUv * _NoiseScale * 0.53 - _NoiseScroll.xy * 1.7 * _Time.y).r;
			float noise = saturate(n1 * 0.68 + n2 * 0.32);

			float field = gradient + (noise - 0.5) * 2.0 * _FrontJitter;
			field = lerp(field, noise, _Scatter);

			float band = 1.0 - saturate(abs(field - _Progress) / max(_EdgeWidth, 0.001));

			float dots = WipeHalftone(screenUv, _HalftoneScale, _HalftoneAngle, _HalftoneSharpness);
			float halftoneMask = lerp(1.0, dots, saturate(_HalftoneAmount * band));

			float softness = max(_EdgeSoftness, 0.001);
			float keep = smoothstep(_Progress - softness, _Progress + softness, field) * halftoneMask;

			faceColor.a *= keep;

			// 墨边：TMP 的 rgb 是**预乘过 alpha** 的，所以墨边色也要按预乘混
			float bandNoise = tex2D(_NoiseTex, float2(screenUv.y * _SmearBands, _Time.y * 0.07)).r;
			float bandMask = lerp(1.0, bandNoise, _SmearBandRandom);
			float ink = saturate(band * bandMask * _EdgeInk);

			faceColor.rgb = lerp(faceColor.rgb, _EdgeColor.rgb * _EdgeColor.a, ink);
			faceColor.a = saturate(faceColor.a + ink * 0.7 * keep);
		}

  		return faceColor * input.color.a;
		}

		ENDCG
	}
}

Fallback "TextMeshPro/Mobile/Distance Field"
CustomEditor "TMPro.EditorUtilities.TMP_SDFShaderGUI"
}

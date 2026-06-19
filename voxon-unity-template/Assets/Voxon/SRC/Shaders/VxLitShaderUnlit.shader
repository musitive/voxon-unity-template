Shader "Voxon/VxLitShader_UnLit"{

	/*
	* A Shader based on the VxLitShader that adds directly to the poltex (bypassing the depth map)
	* As an effect it allows it to be brighter by repeating... it could be used for a 'ghost' effect
	* Or for making something be persistent and bright such as text or a UI.
	* As it writes directly to the poltex values it can't cast shadows.
	* Only viewable by the standard render
	*/

	Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Texture", 2D) = "white" {}
		_LumosityCullThreshold("Lumosity Cull Threshold", Range(0,256)) = 1
		_MatBrightnessBoost("Brightness Adjust", Range(0,5)) = 1
		_RepeatCount("Repeats (int)", Float) = 0
		_RepeatOffSet("RepeatsOffset", Range(-1,1)) = 0
		_Id("Shadow Layer Id", Range(0,256)) = 0								// Materials that have the same Layer ID won't cast shadows onto eachother
		_CullMode("Cull Mode", Range(0,2)) = 2 // 0 = Off, 1 = Front, 2 = Back
		[HideInInspector] _OriginalCullMode("OriginalCullValue", Range(-1,2)) = -1 // A hidden variable to manage the material's original cull mode incase the Renderer is overiding it.

	}

	SubShader
	{
		Pass
		{
			Cull[_CullMode]
			CGPROGRAM
			

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			#include "UnityCG.cginc"
			 
			struct POLTEX
			{
				float3 vertex;
				float u;    
				float v;   
				uint col;
			};

			// Variables from properties
			uint _LumosityCullThreshold;
			float _MatBrightnessBoost;
			float4 _MainTex_ST;
			uint _RepeatCount;
			float _RepeatOffSet;
			float _Id;
			// Variables from C# script
			/* uniform */ RWStructuredBuffer<POLTEX> _PoltexData : register(u1);
			RWStructuredBuffer<int> _LastPoltexIndex : register(u3);

			float4x4 _VxCamera : register(u4);
			uint _Resolution : register(u5);

			float3 _CamPos : register(u6);
			float3 _ViewAspectRatio : register(u7);
			float3 _ViewOffset : register(u8);
			int   _ViewOccluding : register(u9);
			int   _ViewDepthPostProcessMode : register(u10);
			float   _ViewClipRadius : register(u11);
			int   _PoltexHeadRoom : register(u13);
			float _GlobalBrightnessValue : register(u14);


			// Variables
			float4 _Color;
			sampler2D _MainTex;

			struct v2f
			{
				float4 vertex : COLOR; // Vertex position input
				float2 uv : TEXCOORD0;
				float id : TEXCOORD1;
			};

			// VERTEX FUNCTION
			v2f vert(float4 vertex : POSITION, float2 uv : TEXCOORD0, out float4 outpos : SV_POSITION)
			{
				v2f o;
				o.uv = float2(uv.x * _MainTex_ST.x + _MainTex_ST.z, uv.y * _MainTex_ST.y + _MainTex_ST.w);
				outpos = UnityObjectToClipPos(vertex);
				o.vertex = mul(_VxCamera, mul(unity_ObjectToWorld, vertex));
				o.id = _Id;
				return o;
			}

			// FRAGMENT FUNCTION
			fixed4 frag(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
			{
				int debug = 0;

			// Get color from texture and multiply by vertex color
			fixed4 texColor = tex2D(_MainTex, i.uv) * _Color;

			// Early exit if camera isn't assigned Depth Camera 
			float3 cameraPos = _WorldSpaceCameraPos;
			bool isCameraMatched = all(abs(cameraPos - _CamPos) < 2);

			if (!isCameraMatched)
				return texColor;

			// Early exit if color is too dark
			float4 color = texColor * 255.0;
			uint red = uint(color.r);
			uint green = uint(color.g);
			uint blue = uint(color.b);

			if (red < _LumosityCullThreshold && green < _LumosityCullThreshold && blue < _LumosityCullThreshold)
				return texColor;

			float _BrightnessBoost = max((_MatBrightnessBoost * _GlobalBrightnessValue), 0);

			// Apply brightness boost
			if (_BrightnessBoost != 1.0)
			{
				red = min(red * _BrightnessBoost, 255.0);
				green = min(green * _BrightnessBoost, 255.0);
				blue = min(blue * _BrightnessBoost, 255.0);
			}

			uint col = (red << 16) | (green << 8) | blue;

			// Compute pixel coordinates
			uint x = uint(screenPos.x / _ScreenParams.x * _Resolution);
			uint y = uint(screenPos.y / _ScreenParams.y * _Resolution);

			// Compute depth index - using Cantor Pairing
			int depth_index = (x + y) * (x + y + 1) / 2 + y;

			// Get current depth
			float depth = screenPos.z / screenPos.w;

			// Ensure valid depth data range
			int max_rez = _Resolution * _Resolution * _PoltexHeadRoom;

			// Transform vertex position
			float3 trans = -i.vertex.xzy * _ViewAspectRatio;
			trans.x *= -1;

			// Frustum Culling... actually this is just for a cube... need to do it for 
			if (_ViewClipRadius <= -1) {

				if (trans.x > _ViewAspectRatio.x || trans.x < -_ViewAspectRatio.x ||
					trans.y > _ViewAspectRatio.y || trans.y < -_ViewAspectRatio.y ||
					trans.z > _ViewAspectRatio.z || trans.z < -_ViewAspectRatio.z) {
					return texColor; // Outside of view
				}

			}
			else if (_ViewClipRadius > 0) { // circle 

				float distance = trans.x * trans.x + trans.y * trans.y;
				float radius = ((_ViewAspectRatio.x + _ViewAspectRatio.y) / (_ViewClipRadius + 1));
				float rs = radius * radius;

				if (distance > rs)
				{
					return texColor;
				}

			}

			trans += _ViewOffset;


			int data_index = 0;
			InterlockedAdd(_LastPoltexIndex[0], 1, data_index);
			if (_LastPoltexIndex[0] >= max_rez)
				return texColor;

			// Assign poltex data
			_PoltexData[data_index].vertex = trans;
			_PoltexData[data_index].v = i.id;
			_PoltexData[data_index].u = depth;
			_PoltexData[data_index].col = col;

			if (_RepeatCount > 0) {
				float  repVar = (1 / (float)_RepeatCount);

				for (uint it = 0; it < _RepeatCount; it++) {


					InterlockedAdd(_LastPoltexIndex[0], 1, data_index);
					if (_LastPoltexIndex[0] >= max_rez)
						return texColor;

					trans *= (1 + _RepeatOffSet);

					// Assign poltex data
					_PoltexData[data_index].vertex = trans;
					_PoltexData[data_index].v = i.id;
					_PoltexData[data_index].u = depth;

					blue = uint(blue * (1 - repVar)) & 255;
					green = uint (green * (1 - repVar)) & 255;
					red = uint(red * (1 - repVar)) & 255;

					_PoltexData[data_index].col = (red << 16) | (green << 8) | blue;


				}
			}

			return texColor;

			}

		ENDCG
		}

	}
}

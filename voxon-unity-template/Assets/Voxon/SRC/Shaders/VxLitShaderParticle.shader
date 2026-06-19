Shader "Voxon/VxLitShaderParticle"{

	/// Particle version of the Lit Shader.
	/// Very similar to the Standard Lit v3 Shader but instead of getting the 
	/// colour value from the Shader's properties we use an input from the particle instead
	/// 			fixed4 texColor = tex2D(_MainTex, i.uv) * i.color; // from the particle / vert
	///			    fixed4 texColor = tex2D(_MainTex, i.uv) * (_Color);  // from the color property
	/// 
	/// We also use a shader property for ViewOccluding to determine weather the particles should occlude the view - off by default
	/// 
	/// No ID or shadows


	Properties
	{
		
		_MainTex("Texture", 2D) = "white" {}
		_LumosityCullThreshold("Luminosity Cull Threshold", Range(0,256)) = 1
		_MatBrightnessBoost("Brightness Adjust", Range(0,5)) = 1
		[Toggle(_ViewOccluding)] _ViewOccluding("Particles Occlude View", Range(0,1)) = 0 // Boolean-like property
	//	_MatShadowValue("Shadow Opacity", Range(-1, 1)) = 0
	//	_Id("Id", Range(0,256)) = 0
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
					float u;    // Depth
					float v;    // Depth index
					uint col;
				};

				struct DEPTH
				{
					float depth_value;		// Depth value at this point
					int pol_index;			// Index value to the poltex
					int depth_frame;        // Current rendering frame number 
				};

				// Variables from properties
				uint _LumosityCullThreshold;
				float _MatBrightnessBoost;
				float _MatShadowValue = 0;
				float4 _MainTex_ST;
				float _Id = 0;
				int _ViewOccluding;

				// Variables from C# script
				uniform RWStructuredBuffer<POLTEX> _RefinedPoltexData : register(u1);
				uniform RWStructuredBuffer<DEPTH> _DepthData : register(u2);
				uniform RWStructuredBuffer<int> _LastPoltexIndex : register(u3);
				uniform RWStructuredBuffer<POLTEX> _AllPoltexData : register(u4);

				float4x4 _VxCamera : register(u5);
				uint _Resolution : register(u6);

				float3 _CamPos : register(u7);
				float3 _ViewAspectRatio : register(u8);
				float3 _ViewOffset : register(u9);
				//int   _ViewOccluding : register(u10);
				int   _ViewDepthPostProcessMode : register(u11);
				float   _ViewClipRadius : register(u12);
				float  _GlobalShadowValue : register(u13);
				int   _PoltexHeadRoom : register(u14);
				float _GlobalBrightnessValue : register(u15);
				float _DepthThreshold : register(u16);
				float _CameraClipNear : register(u17);
				float _CameraClipFar : register(u18);
				int   _CaptureAllData : register(u19);


				// Variables
				float4 _Color;
				sampler2D _MainTex;


				sampler2D _CameraDepthTexture; // used for Depth Data
				//sampler2D _LastCameraDepthTexture;

				struct vertIn
				{
					float4 vertex : POSITION;
					float2 uv : TEXCOORD0;
					float4 color : COLOR;
				};

				struct v2f
				{
					float4 vertex : TEXCOORD3; // Voxon Vertex position 
					float2 uv : TEXCOORD0;
					float id : TEXCOORD1;
					float4 color : COLOR;
					float4 screenPos : SV_POSITION;
				};

				// VERTEX FUNCTION
				v2f vert(vertIn v)
				{
					v2f o;
					o.uv = float2(v.uv.x * _MainTex_ST.x + _MainTex_ST.z, v.uv.y * _MainTex_ST.y + _MainTex_ST.w);
					o.screenPos = UnityObjectToClipPos(v.vertex);

					o.vertex = mul(_VxCamera, mul(unity_ObjectToWorld, v.vertex));
					o.id = _Id;
					o.color = v.color;

					return o;
				}




				// FRAGMENT FUNCTION
				fixed4 frag(v2f i) : SV_Target
				{
				int debug = 0;

				// Get color from texture and multiply by vertex color
				fixed4 texColor = tex2D(_MainTex, i.uv) * i.color;

				// Early exit if camera isn't assigned Depth Camera 
				float3 cameraPos = _WorldSpaceCameraPos;
				bool isCameraMatched = all(abs(cameraPos - _CamPos) < 2);

				if (!isCameraMatched)
					return texColor;

				// Early exit if color is too dark
				half4 color = texColor * 255.0;
				uint red = uint(color.r);
				uint green = uint(color.g);
				uint blue = uint(color.b);

				if (red < _LumosityCullThreshold && green < _LumosityCullThreshold && blue < _LumosityCullThreshold)
					return texColor;

				half _BrightnessBoost = max((_MatBrightnessBoost * _GlobalBrightnessValue), 0);

				// Apply brightness boost
				if (_BrightnessBoost != 1.0)
				{
					red = min(red * _BrightnessBoost, 255.0);
					green = min(green * _BrightnessBoost, 255.0);
					blue = min(blue * _BrightnessBoost, 255.0);
				}

				uint col = (red << 16) | (green << 8) | blue;

				// Compute pixel coordinates
				uint x = uint(i.screenPos.x / _ScreenParams.x * _Resolution);
				uint y = uint(i.screenPos.y / _ScreenParams.y * _Resolution);
				// Compute depth index - using Cantor Pairing
				uint depth_index = (x + y) * (x + y + 1) / 2 + y;

				//float depth = screenPos.z / screenPos.w;
				float depth = 0;

				// Various DepthPostProcessModes for experimenting with

				if (_ViewDepthPostProcessMode == 0) { // Classic - most precise 
					depth = i.screenPos.z / i.screenPos.w;
				}
				else if (_ViewDepthPostProcessMode == 1) { // UNITY_SAMPLE_DEPTH uses sampler2D _CameraDepthTexture;

					depth = i.screenPos.z / UNITY_SAMPLE_DEPTH(i.uv);

				}
				else if (_ViewDepthPostProcessMode == 2) {

					depth = UNITY_SAMPLE_DEPTH(i.uv);

				}
				else if (_ViewDepthPostProcessMode == 3) { // Unity LinearEyeDepth 
				
					depth = i.screenPos.z / LinearEyeDepth(i.screenPos.w);

				}

				// Ensure valid depth data range
				int max_rez = _Resolution * _Resolution * _PoltexHeadRoom;

				// Transform vertex position
				half3 trans = -i.vertex.xzy * _ViewAspectRatio;
				trans.x *= -1;


				if (_ViewClipRadius <= -1) { // for Cube

					if (trans.x > _ViewAspectRatio.x || trans.x < -_ViewAspectRatio.x ||
						trans.y > _ViewAspectRatio.y || trans.y < -_ViewAspectRatio.y ||
						trans.z > _ViewAspectRatio.z || trans.z < -_ViewAspectRatio.z) {
						return texColor; // Outside of view
					}

				}
				else if (_ViewClipRadius > 0) { // for Circle 

					half distance = trans.x * trans.x + trans.y * trans.y;
					half radius = ((_ViewAspectRatio.x + _ViewAspectRatio.y) / (_ViewClipRadius + 1));
					half rs = radius * radius;

					if (distance > rs)
					{
						return texColor;
					}

				}

				trans += _ViewOffset;

				bool addToPoltex = false;

				uint data_index = 0;
				int debugCol = 0x00ff00;

				// Depth checks and occlusion logic		
				if (_CaptureAllData)
				{
					data_index = 0;
					InterlockedAdd(_LastPoltexIndex[2], 1, data_index);

					if (_LastPoltexIndex[2] < max_rez) {

						// Assign poltex data
						_AllPoltexData[data_index].vertex = trans;
						_AllPoltexData[data_index].v = i.id;
						_AllPoltexData[data_index].u = depth;
						_AllPoltexData[data_index].col = col;
					}
				}


				if (_DepthData[depth_index].depth_frame != _LastPoltexIndex[1] || _ViewOccluding == 0)
				{




					_DepthData[depth_index].depth_value = depth;
					_DepthData[depth_index].depth_frame = _LastPoltexIndex[1];
					//debugCol = 0xffff00; // Yellow new pixel
					addToPoltex = true;
				}
				else {

					// its not a new entry to the depth map. So let's check if its further or closer to the current entry.
					// if debug col : Blue & Cyan = block. Green and purple = good to pass 

					half shadowOpacityValue = min(max(((_MatShadowValue + _GlobalShadowValue)), 0), 1);

					// Occlusion checks

					// True if this poltex is further from the camera
					if (depth - _DepthThreshold <= _DepthData[depth_index].depth_value && _DepthData[depth_index].depth_frame == _LastPoltexIndex[1]) {

						if (_RefinedPoltexData[_DepthData[depth_index].pol_index].u != 0) {

							//	_PoltexData[_DepthData[depth_index].index].col = 0x000000; // don't turn it black its good! 
							//	_PoltexData[_DepthData[depth_index].index].v = -1; // flag it to be deleted 
							if (debug) _RefinedPoltexData[_DepthData[depth_index].pol_index].col = 0x00ff00; // turn to black so it will be a shadow
						}
						else {
							debugCol = 0xff0000;
						}

						// if Shadows is enabled allowed a certain amount of colour to pass through
						if (shadowOpacityValue > 0 && (i.id != _RefinedPoltexData[_DepthData[depth_index].pol_index].v || i.id == 0)) {

							// darken the colour coming in
							blue = uint(blue * shadowOpacityValue) & 255;
							green = uint (green * shadowOpacityValue) & 255;
							red = uint(red * shadowOpacityValue) & 255;

							col = (red << 16) | (green << 8) | blue;

							//col = 0xff0000;
							addToPoltex = true;

						}
						else {
							// keep the old depth data and don't add this one to the stack
							return texColor; // this one shouldn't fill the poltex as already have a closer poltex
						}

					}

					// True if this poltex is going to be closer
					if (depth >= _DepthData[depth_index].depth_value + _DepthThreshold && _DepthData[depth_index].depth_frame == _LastPoltexIndex[1])
					{
						if (_RefinedPoltexData[_DepthData[depth_index].pol_index].u != 0 && (i.id != _RefinedPoltexData[_DepthData[depth_index].pol_index].v || i.id == 0)) {

							// find the old value and apply a shadow if needed
							if (shadowOpacityValue > 0) {

								red = (_RefinedPoltexData[_DepthData[depth_index].pol_index].col >> 16) & 0xFF;
								green = (_RefinedPoltexData[_DepthData[depth_index].pol_index].col >> 8) & 0xFF;
								blue = (_RefinedPoltexData[_DepthData[depth_index].pol_index].col & 0xFF);

								blue = uint(blue * shadowOpacityValue) & 255;
								green = uint(green * shadowOpacityValue) & 255;
								red = uint(red * shadowOpacityValue) & 255;

								_RefinedPoltexData[_DepthData[depth_index].pol_index].col = (red << 16) | (green << 8) | (blue);

							}
							else {
								// or mark that poltex to be deleted.
								_RefinedPoltexData[_DepthData[depth_index].pol_index].v = -1; // flag it to be deleted 
								_RefinedPoltexData[_DepthData[depth_index].pol_index].col = 0x00000; // or whatever shadow amount
							}

							if (debug) _RefinedPoltexData[_DepthData[depth_index].pol_index].col = 0x0000ff; // turn to black so it will be a shadow

						}

						debugCol = 0xff00ff; // need this to pass though as this is the new second pixel
						//return c; // this one SHOULD flll the poltex
						addToPoltex = true;
					}

					if (_ViewOccluding == 0 || debug) addToPoltex = true;
				}

					if (addToPoltex)
					{
						data_index = 0;


						InterlockedAdd(_LastPoltexIndex[0], 1, data_index);

						if (_LastPoltexIndex[0] >= max_rez)
							return texColor;


						// Assign depth data
						// IMPORTANT to make sure the depth data has the correct poltex ref 
						_DepthData[depth_index].pol_index = data_index;
						_DepthData[depth_index].depth_value = depth;
						_DepthData[depth_index].depth_frame = _LastPoltexIndex[1];

						// Assign poltex data
						_RefinedPoltexData[data_index].vertex = trans;
						//_RefinedPoltexData[data_index].v = depth_index;
						_RefinedPoltexData[data_index].v = i.id;
						_RefinedPoltexData[data_index].u = depth;
						_RefinedPoltexData[data_index].col = col;
						if (debug) _RefinedPoltexData[data_index].col = debugCol;
					}

					return texColor;
				}

			ENDCG
			}
		}
}
Shader "Voxon/VxTextShader"
{
    Properties {
        _Color("Base Color", Color) = (1,1,1,1)
        _MainTex("Font Atlas", 2D) = "white" {}
        _Brightness("Brightness", Range(1,50)) = 15
        _AlphaCutoff("Alpha Cutoff", Range(0,1)) = 0.05
        _LumosityCullThreshold("Luminosity Cull Threshold", Range(0,256)) = 0
        _Id("Shadow Layer Id", Range(0,256)) = 0
    }

    SubShader {
        Tags {
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Pass {
            Blend SrcAlpha OneMinusSrcAlpha

            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0

            #include "UnityCG.cginc"

            struct POLTEX {
                float3 vertex;
                float u;    // Depth
                float v;    // Depth index
                uint col;
            };

            struct DEPTH {
                float depth_value;		// Depth value at this point
                int pol_index;			// Index value to the poltex
                int depth_frame;        // Current rendering frame number 
            };

            // Variables from properties
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _Brightness;
            float _AlphaCutoff;
            uint _LumosityCullThreshold;
            float _Id;

            // Variables from C# script
            uniform RWStructuredBuffer<POLTEX> _RefinedPoltexData : register(u1);
            uniform RWStructuredBuffer<DEPTH> _DepthData : register(u2);
            uniform RWStructuredBuffer<int> _LastPoltexIndex : register(u3);

            float4x4 _VxCamera : register(u5);
            uint _Resolution : register(u6);
            
            float3 _CamPos : register(u7);
            float3 _ViewAspectRatio : register(u8);
            float3 _ViewOffset : register(u9);
            
            int _PoltexHeadRoom : register(u14);
            
            float _DepthThreshold : register(u16);
            float _CamPosThreshold : register(u17);

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f {
                float4 vertex : TEXCOORD3; // Voxon Vertex position 
                float4 screenPos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float id : TEXCOORD1;
            };

            v2f vert(appdata v) {
                v2f o;
                o.screenPos = UnityObjectToClipPos(v.vertex);
                o.vertex = mul(_VxCamera, mul(unity_ObjectToWorld, v.vertex));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                o.id = _Id;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                fixed4 atlas = tex2D(_MainTex, i.uv);

                fixed4 tex_color;
                tex_color.rgb = saturate(i.color.rgb * _Color.rgb * _Brightness);
                tex_color.a = step(0.5, atlas.a) * i.color.a * _Color.a;

                if (tex_color.a < _AlphaCutoff)
                    discard; // do not output result of current pixel

                float3 cameraPos = _WorldSpaceCameraPos;
                bool isCameraMatched = all(abs(cameraPos - _CamPos) < _CamPosThreshold);

                if (!isCameraMatched)
                    return tex_color; 
                
                half4 color = tex_color * 255.0;

                uint red = uint(color.r);
                uint green = uint(color.g);
                uint blue = uint(color.b);

                if (red < _LumosityCullThreshold && green < _LumosityCullThreshold && blue < _LumosityCullThreshold)
                    return tex_color;

                uint col = (red << 16) | (green << 8) | blue;

                // SCREEN POSITION
                uint x = int(i.screenPos.x / _ScreenParams.x * _Resolution);
                uint y = int(i.screenPos.y / _ScreenParams.y * _Resolution);

                uint depth_index = (x + y) * (x + y + 1) / 2 + y;

                float depth = i.screenPos.z;

                // Random comment
                
                // VOXON SPACE
                half3 trans = -i.vertex.xzy * _ViewAspectRatio;
                trans.x *= -1;
                trans += _ViewOffset;
                uint data_index = 0;
                
                InterlockedAdd(_LastPoltexIndex[0], 1, data_index);

                int max_rez = _Resolution * _Resolution * _PoltexHeadRoom;

                if (_LastPoltexIndex[0] >= max_rez)
                    return tex_color;

                // DEPTH
                _DepthData[depth_index].pol_index = data_index;
                _DepthData[depth_index].depth_value = depth;
                _DepthData[depth_index].depth_frame = _LastPoltexIndex[1];

                // POLTEX
                _RefinedPoltexData[data_index].vertex = trans;
                _RefinedPoltexData[data_index].v = i.id;
                _RefinedPoltexData[data_index].u = depth;
                _RefinedPoltexData[data_index].col = col;
                
                return tex_color;
            }
            
            ENDCG
        }
    }
}
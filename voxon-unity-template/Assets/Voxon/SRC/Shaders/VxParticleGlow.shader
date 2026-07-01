Shader "Voxon/VxParticleGlow"
{
    Properties
    {
        _MainTex("Texture",2D)="white"{}

        _LumosityCullThreshold("Luminosity Cull Threshold",Range(0,256)) = 1

        _MatBrightnessBoost("Brightness Adjust",Range(0,10)) = 3

        [Toggle(_ViewOccluding)]
        _ViewOccluding("Particles Occlude View",Range(0,1)) = 0

        _CullMode("Cull Mode",Range(0,2)) = 0

        [HideInInspector]
        _OriginalCullMode("OriginalCullValue",Range(-1,2)) = -1

        _Softness("Softness",Range(0.001,1)) = 0.1

        _AlphaCutoff("Alpha Cutoff",Range(0,1)) = 0.02
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Cull[_CullMode]

            Blend SrcAlpha One

            ZWrite Off

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

            struct DEPTH
            {
                float depth_value;
                int pol_index;
                int depth_frame;
            };

            uint _LumosityCullThreshold;

            float _MatBrightnessBoost;

            float4 _MainTex_ST;

            float _Id = 0;

            int _ViewOccluding;

            float _Softness;

            float _AlphaCutoff;

            uniform RWStructuredBuffer<POLTEX> _RefinedPoltexData : register(u1);

            uniform RWStructuredBuffer<DEPTH> _DepthData : register(u2);

            uniform RWStructuredBuffer<int> _LastPoltexIndex : register(u3);

            uniform RWStructuredBuffer<POLTEX> _AllPoltexData : register(u4);

            float4x4 _VxCamera : register(u5);

            uint _Resolution : register(u6);

            float3 _CamPos : register(u7);

            float3 _ViewAspectRatio : register(u8);

            float3 _ViewOffset : register(u9);

            int _ViewDepthPostProcessMode : register(u11);

            float _ViewClipRadius : register(u12);

            float _GlobalShadowValue : register(u13);

            int _PoltexHeadRoom : register(u14);

            float _GlobalBrightnessValue : register(u15);

            float _DepthThreshold : register(u16);

            float _CameraClipNear : register(u17);

            float _CameraClipFar : register(u18);

            int _CaptureAllData : register(u19);

            sampler2D _MainTex;

            sampler2D _CameraDepthTexture;

            struct vertIn
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : TEXCOORD3;
                float2 uv : TEXCOORD0;
                float id : TEXCOORD1;
                float4 color : COLOR;
                float4 screenPos : SV_POSITION;
            };

            v2f vert(vertIn v)
            {
                v2f o;

                o.uv = TRANSFORM_TEX(v.uv,_MainTex);

                o.screenPos = UnityObjectToClipPos(v.vertex);

                o.vertex = mul(_VxCamera,mul(unity_ObjectToWorld,v.vertex));

                o.id = _Id;

                o.color = v.color;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex,i.uv);

                // REMOVE BLACK BACKGROUND
                float alpha = max(tex.r,max(tex.g,tex.b));

                alpha *= tex.a;

                alpha = smoothstep(_AlphaCutoff,_AlphaCutoff + _Softness,alpha);

                // FINAL PARTICLE COLOR
                fixed4 texColor = fixed4(tex.rgb * i.color.rgb,alpha * i.color.a);

                // EARLY EXIT IF NOT VOXON CAMERA
                float3 cameraPos = _WorldSpaceCameraPos;

                bool isCameraMatched = all(abs(cameraPos - _CamPos) < 2);

                if (!isCameraMatched)
                    return texColor;

                // BRIGHTNESS
                half4 color = texColor * 255.0;

                uint red = uint(color.r);

                uint green = uint(color.g);

                uint blue = uint(color.b);

                if (red < _LumosityCullThreshold &&
                    green < _LumosityCullThreshold &&
                    blue < _LumosityCullThreshold)
                    return texColor;

                half brightnessBoost = max((_MatBrightnessBoost * _GlobalBrightnessValue),0);

                if (brightnessBoost != 1.0)
                {
                    red = min(red * brightnessBoost,255.0);

                    green = min(green * brightnessBoost,255.0);

                    blue = min(blue * brightnessBoost,255.0);
                }

                uint col = (red << 16) | (green << 8) | blue;

                // SCREEN POSITION
                uint x = uint(i.screenPos.x / _ScreenParams.x * _Resolution);

                uint y = uint(i.screenPos.y / _ScreenParams.y * _Resolution);

                uint depth_index = (x + y) * (x + y + 1) / 2 + y;

                float depth = i.screenPos.z / i.screenPos.w;

                int max_rez = _Resolution * _Resolution * _PoltexHeadRoom;

                half3 trans = -i.vertex.xzy * _ViewAspectRatio;

                trans.x *= -1;

                trans += _ViewOffset;

                uint data_index = 0;

                InterlockedAdd(_LastPoltexIndex[0],1,data_index);

                if (_LastPoltexIndex[0] >= max_rez)
                    return texColor;

                _DepthData[depth_index].pol_index = data_index;

                _DepthData[depth_index].depth_value = depth;

                _DepthData[depth_index].depth_frame = _LastPoltexIndex[1];

                _RefinedPoltexData[data_index].vertex = trans;

                _RefinedPoltexData[data_index].v = i.id;

                _RefinedPoltexData[data_index].u = depth;

                _RefinedPoltexData[data_index].col = col;

                return texColor;
            }

            ENDCG
        }
    }
}
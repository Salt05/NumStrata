Shader "UI/BackgroundBlur"
{
    Properties
    {
        [HideInInspector] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BlurSize ("Blur Size", Range(0, 10)) = 1
        _Color ("Tint Color", Color) = (0,0,0,0.5)
        
        // Các thuộc tính bắt buộc cho UI Mask/Stencil
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "RenderType"="Transparent" 
            "IgnoreProjector"="True" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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

        GrabPass { "_BackgroundTexture" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                fixed4 color  : COLOR;
                float4 grabPos : TEXCOORD0;
                float2 uv     : TEXCOORD1;
            };

            sampler2D _BackgroundTexture;
            float4 _BackgroundTexture_TexelSize;
            sampler2D _MainTex;
            float _BlurSize;
            float4 _Color;

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.grabPos = ComputeGrabScreenPos(o.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                // 1. Tính toán UV đúng chuẩn (chia cho w) để offset không bị sai tỷ lệ
                float2 uv = i.grabPos.xy / i.grabPos.w;
                float2 texel = _BackgroundTexture_TexelSize.xy * _BlurSize;
                
                // 2. Thuật toán Blur chất lượng cao (25-tap)
                float4 color = 0;
                float sum = 0;
                for(int x = -2; x <= 2; x++) {
                    for(int y = -2; y <= 2; y++) {
                        float weight = 1.0 / (1.0 + abs(x) + abs(y));
                        color += tex2D(_BackgroundTexture, uv + float2(x, y) * texel) * weight;
                        sum += weight;
                    }
                }
                color /= sum;

                // 3. Kết hợp màu sắc: i.color.a là thông số được PauseManager chạy từ 0 -> maxAlpha
                color.rgb = lerp(color.rgb, _Color.rgb, i.color.a);
                
                // 4. BẮT BUỘC: Đặt Alpha = 1.0 để lớp nền này đè hoàn toàn lên hình ảnh sắc nét phía sau!
                color.a = 1.0;
                
                return color;
            }
            ENDCG
        }
    }
}
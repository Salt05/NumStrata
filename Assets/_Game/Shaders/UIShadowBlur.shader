Shader "Custom/UIShadowBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _BlurSize ("Blur Size", Range(0, 10)) = 2

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
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
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float _BlurSize;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            float SampleBlurAlpha(float2 uv, float2 texel, float blur)
            {
                float2 offset = texel * blur;

                float a = tex2D(_MainTex, uv).a * 0.227027;
                a += tex2D(_MainTex, uv + float2(offset.x, 0)).a * 0.1945946;
                a += tex2D(_MainTex, uv - float2(offset.x, 0)).a * 0.1945946;
                a += tex2D(_MainTex, uv + float2(0, offset.y)).a * 0.1945946;
                a += tex2D(_MainTex, uv - float2(0, offset.y)).a * 0.1945946;

                a += tex2D(_MainTex, uv + offset).a * 0.1216216;
                a += tex2D(_MainTex, uv + float2(offset.x, -offset.y)).a * 0.1216216;
                a += tex2D(_MainTex, uv + float2(-offset.x, offset.y)).a * 0.1216216;
                a += tex2D(_MainTex, uv - offset).a * 0.1216216;

                return a;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float blur = max(_BlurSize, 0.0);
                float a = SampleBlurAlpha(i.texcoord, _MainTex_TexelSize.xy, blur);
                fixed4 c = i.color;
                c.a *= a;

                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a - 0.001);
                #endif

                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}

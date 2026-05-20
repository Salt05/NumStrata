Shader "Custom/SpriteShadowBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _BlurSize ("Blur Size", Range(0, 10)) = 2
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Sprite"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

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
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float _BlurSize;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 SampleBlur(float2 uv, float2 texel, float blur)
            {
                float2 offset = texel * blur;
                fixed4 c = tex2D(_MainTex, uv) * 0.204164;
                c += tex2D(_MainTex, uv + float2(offset.x, 0)) * 0.304005;
                c += tex2D(_MainTex, uv - float2(offset.x, 0)) * 0.304005;
                c += tex2D(_MainTex, uv + float2(0, offset.y)) * 0.093913;
                c += tex2D(_MainTex, uv - float2(0, offset.y)) * 0.093913;
                return c;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float blur = max(_BlurSize, 0.0);
                fixed4 c = SampleBlur(i.texcoord, _MainTex_TexelSize.xy, blur);
                c *= i.color;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}

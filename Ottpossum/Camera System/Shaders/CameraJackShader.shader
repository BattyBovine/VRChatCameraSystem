Shader "Custom/ScreenUV"
{
    Properties
    {
        _MainTex ("Image", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass {
            Cull Off

            CGPROGRAM
            #include "UnityCG.cginc"
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _MainTex;

            struct Data
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct V2F
            {
                float4 position : SV_POSITION;
                float4 screenPosition : TEXCOORD0;
            };

            V2F vert(Data v)
            {
                V2F o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.screenPosition = ComputeScreenPos(o.position);
                return o;
            }

            fixed4 frag(V2F i) : SV_TARGET
            {
                float2 textureCoordinate = i.screenPosition.xy / i.screenPosition.w;
                fixed4 colour = tex2D(_MainTex, textureCoordinate);
                return colour;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}

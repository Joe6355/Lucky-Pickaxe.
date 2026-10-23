Shader "MineLoot/VoxelWorld"
{
    Properties { _MainTex ("Pixels", 2D) = "white" {} _Color ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float light:TEXCOORD1; };
            sampler2D _MainTex; fixed4 _Color;
            v2f vert(appdata v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv;
                float3 n=UnityObjectToWorldNormal(v.normal);
                o.light=.68 + .32 * max(0,dot(n,normalize(float3(-.45,1,-.5))));
                return o;
            }
            fixed4 frag(v2f i):SV_Target { fixed4 c=tex2D(_MainTex,i.uv)*_Color; return fixed4(c.rgb*i.light,1); }
            ENDCG
        }
    }
}

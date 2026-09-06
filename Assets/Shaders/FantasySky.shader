Shader "DarkBeforeDawn/FantasySky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.025,0.035,0.10,1)
        _Horizon ("Horizon", Color) = (0.24,0.17,0.31,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 position:SV_POSITION; float3 direction:TEXCOORD0; };
            float4 _Zenith, _Horizon;
            v2f vert(float4 vertex:POSITION) { v2f o; o.position=UnityObjectToClipPos(vertex); o.direction=vertex.xyz; return o; }
            fixed4 frag(v2f i):SV_Target
            {
                float y=normalize(i.direction).y;
                float t=pow(saturate(y),.45);
                return lerp(_Horizon,_Zenith,t);
            }
            ENDCG
        }
    }
}

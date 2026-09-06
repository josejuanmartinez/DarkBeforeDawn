Shader "DarkBeforeDawn/FantasySky"
{
 Properties{_Zenith("Zenith",Color)=(.25,.42,.53,1) _Horizon("Horizon",Color)=(.76,.77,.64,1)}
 SubShader
 {
  Tags{"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"} Cull Off ZWrite Off
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct V{float4 p:SV_POSITION;float3 d:TEXCOORD0;};float4 _Zenith,_Horizon;
   V vert(float4 p:POSITION){V o;o.p=UnityObjectToClipPos(p);o.d=p.xyz;return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
   fixed4 frag(V i):SV_Target
   {
    float3 d=normalize(i.d);float3 c=lerp(_Horizon.rgb,_Zenith.rgb,pow(saturate(d.y),.5));
    float2 p=d.xz/(max(.03,d.y)+.20)*2.8+float2(_Time.y*.003,0);
    float cloud=noise(p)*.58+noise(p*2.05)*.28+noise(p*4.1)*.14;
    float cover=smoothstep(.46,.69,cloud)*smoothstep(.0,.12,d.y);
    c=lerp(c,lerp(float3(.66,.67,.59),float3(.93,.89,.75),cloud),cover*.86);
    float sun=pow(saturate(dot(d,normalize(float3(-.55,.4,.8)))),160);
    c+=float3(1,.77,.42)*sun*.28;
    return fixed4(c,1);
   }
   ENDCG
  }
 }
}

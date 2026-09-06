Shader "DarkBeforeDawn/IllustratedRiver"
{
 Properties{_BaseColor("Jade water",Color)=(.08,.34,.32,1) _Foam("Golden reflections",Color)=(.7,.79,.48,1) _Fall("Waterfall",Float)=0}
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"} Cull Off
  Pass
  {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A{float4 p:POSITION;float2 uv:TEXCOORD0;};struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float2 uv:TEXCOORD1;float fog:TEXCOORD2;};
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseColor,_Foam;float _Fall;
   CBUFFER_END
   float _ValleyCycleEnabled;float4 _ValleySceneTint;
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);}
   V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.uv=a.uv;o.fog=ComputeFogFactor(o.p.z);return o;}
   half4 frag(V i):SV_Target
   {
    float2 p=lerp(i.w.xz,float2(i.w.x+i.w.z,i.w.y),_Fall);float t=_Time.y;
    float warp=noise(p*.7+float2(0,-t*.09));
    float wave=sin(p.y*10+warp*5+t*1.2);
    float streak=smoothstep(.75,.96,wave)*smoothstep(.4,.65,noise(float2(p.x*2,p.y*.8-t*.12)));
    float bank=pow(abs(i.uv.x*2-1),12);
    float3 c=lerp(_BaseColor.rgb*.68,_BaseColor.rgb*1.30,noise(p*.20));
    c=lerp(c,_Foam.rgb,saturate(streak*.68+bank*(.35+.25*wave)));
    if(_Fall>.5){float ribbons=noise(float2(p.x*6,p.y*.15+t*.4));c=lerp(_BaseColor.rgb,_Foam.rgb,.3+ribbons*.7);}
    c*=lerp(float3(1,1,1),_ValleySceneTint.rgb,_ValleyCycleEnabled);
    return half4(MixFog(c,i.fog),1);
   }
   ENDHLSL
  }
 }
}

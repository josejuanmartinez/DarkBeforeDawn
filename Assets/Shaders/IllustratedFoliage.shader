Shader "DarkBeforeDawn/IllustratedFoliage"
{
 Properties
 {
  _BaseMap("Illustrated oak spray",2D)="white"{}
  _BaseColor("Pigment tint",Color)=(1,1,1,1)
  _Cutoff("Leaf edge",Range(0,1))=.55
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest"} Cull Off
  Pass
  {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST,_BaseColor;float _Cutoff;
   CBUFFER_END
   float _ValleyCycleEnabled;float4 _ValleySceneTint;
   struct A{float4 p:POSITION;float2 uv:TEXCOORD0;float4 c:COLOR;};
   struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float4 c:COLOR;float fog:TEXCOORD1;};
   V vert(A a){V o;float3 w=TransformObjectToWorld(a.p.xyz);w.x+=sin(w.x*.6+w.z*.5+_Time.y*.65)*.045*a.uv.y;o.p=TransformWorldToHClip(w);o.uv=a.uv;o.c=a.c;o.fog=ComputeFogFactor(o.p.z);return o;}
   half4 frag(V i):SV_Target
   {
    half4 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);clip(tex.a-_Cutoff);
    float3 c=tex.rgb*_BaseColor.rgb*i.c.rgb;
    c=lerp(c*.85,c,saturate(i.uv.y*.6+.4));
    c*=lerp(float3(1,1,1),_ValleySceneTint.rgb,_ValleyCycleEnabled);
    return half4(MixFog(c,i.fog),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 }
}

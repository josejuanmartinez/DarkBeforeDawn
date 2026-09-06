Shader "DarkBeforeDawn/GreenwoodPollen"
{
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Pass
  {
   Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A{float4 p:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;o.color=i.color;return o;}
   half4 frag(V i):SV_Target{float a=pow(saturate(1-length(i.uv*2-1)),2);return half4(i.color.rgb,i.color.a*a);}
   ENDHLSL
  }
 }
}

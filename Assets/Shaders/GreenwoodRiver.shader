Shader "DarkBeforeDawn/GreenwoodRiver"
{
 Properties{_Deep("Deep jade",Color)=(.075,.20,.17,1) _Shallow("Shallows",Color)=(.35,.43,.28,1)}
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Pass
  {
   Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;float4 screen:TEXCOORD1;float fog:TEXCOORD2;};
   float4 _Deep,_Shallow;
   V vert(A i){V o;o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);o.screen=ComputeScreenPos(o.p);o.fog=ComputeFogFactor(o.p.z);return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 q=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(q),hash(q+float2(1,0)),f.x),lerp(hash(q+float2(0,1)),hash(q+1),f.x),f.y);}
   half4 frag(V i):SV_Target
   {
    float2 p=i.world.xz;float t=_Time.y;
    float warp=noise(p*.6+float2(t*.04,-t*.07))*5;
    float wave=sin(p.x*3.1+p.y*.7+warp+t*.5)+sin(p.y*4.3-p.x*.4+warp-t*.7)*.45;
    float3 n=normalize(float3(cos(p.x*3.1+p.y*.7+warp+t*.5)*.028,1,cos(p.y*4.3-p.x*.4+warp-t*.7)*.025));
    float3 view=normalize(_WorldSpaceCameraPos-i.world);
    float depth=LinearEyeDepth(SampleSceneDepth(i.screen.xy/i.screen.w),_ZBufferParams);
    float own=-TransformWorldToView(i.world).z;float thickness=max(0,depth-own);
    float fresnel=pow(1-saturate(dot(view,n)),3);
    float3 c=lerp(_Shallow.rgb,_Deep.rgb,saturate(thickness*.55));
    c=lerp(c,float3(.58,.65,.59),fresnel*.4);
    Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
    float glint=pow(saturate(dot(n,normalize(view+sun.direction))),96);
    c+=sun.color*glint*.24*sun.shadowAttenuation;
    float foam=(1-smoothstep(.05,.55,thickness))*smoothstep(.1,.65,sin(p.y*8+wave*2-t)*.5+.5);
    c=lerp(c,float3(.70,.73,.58),foam*.65);
    c+=pow(saturate(wave*.45),18)*.035;
    return half4(MixFog(c,i.fog),.94);
   }
   ENDHLSL
  }
 }
}

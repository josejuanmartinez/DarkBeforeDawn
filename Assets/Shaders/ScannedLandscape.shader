Shader "DarkBeforeDawn/ScannedLandscape"
{
 Properties
 {
  _BaseMap("Scanned albedo",2D)="white"{}
  _BumpMap("Scanned normal",2D)="bump"{}
  _RoughMap("Scanned roughness",2D)="white"{}
  _BaseColor("Tint",Color)=(1,1,1,1)
  _Illustrated("Illustrated palette",Range(0,1))=0
  _Ink("Shadow ink",Color)=(.045,.10,.14,1)
  _Paint("Pigment",Color)=(.65,.42,.13,1)
  _Meadow("Meadow palette blend",Range(0,1))=0
  _GrassDark("Meadow shadow",Color)=(.14,.23,.065,1)
  _GrassLight("Meadow sun",Color)=(.42,.49,.19,1)
  _WorldScale("Repeats per metre",Float)=.5
  _BumpScale("Normal strength",Range(0,2))=.7
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass
  {
   Tags{"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
   TEXTURE2D(_RoughMap);SAMPLER(sampler_RoughMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseColor,_GrassDark,_GrassLight,_Ink,_Paint;float _WorldScale,_BumpScale,_Meadow,_Illustrated;
   CBUFFER_END
   struct A{float4 p:POSITION;float3 n:NORMAL;};
   struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float fog:TEXCOORD2;};
   V vert(A i){V o;o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.n);o.fog=ComputeFogFactor(o.p.z);return o;}
   half4 frag(V i):SV_Target
   {
    float3 n=normalize(i.normal),weights=pow(abs(n),6);weights/=max(dot(weights,1),.0001);
    float3 p=i.world*_WorldScale;
    float3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.zy).rgb*weights.x+SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xz).rgb*weights.y+SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xy).rgb*weights.z;
    // Broad meadow colors retain scanned detail without the brown leaf litter.
    float detail=dot(albedo,float3(.2126,.7152,.0722));
    float patches=.5+.25*sin(i.world.x*.19+sin(i.world.z*.13))+.25*sin(i.world.z*.24+i.world.x*.08);
    float grassTone=saturate(.26+patches*.38+(detail-.3)*.24);
    albedo=lerp(albedo,lerp(_GrassDark.rgb,_GrassLight.rgb,grassTone),_Meadow);
    float rough=SAMPLE_TEXTURE2D(_RoughMap,sampler_RoughMap,p.zy).r*weights.x+SAMPLE_TEXTURE2D(_RoughMap,sampler_RoughMap,p.xz).r*weights.y+SAMPLE_TEXTURE2D(_RoughMap,sampler_RoughMap,p.xy).r*weights.z;
    float3 nx=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,p.zy),_BumpScale);
    float3 ny=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,p.xz),_BumpScale);
    float3 nz=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,p.xy),_BumpScale);
    float3 normal=normalize(float3(nx.z*sign(n.x),nx.y,nx.x)*weights.x+float3(ny.x,ny.z*sign(n.y),ny.y)*weights.y+float3(nz.x,nz.y,nz.z*sign(n.z))*weights.z);
    InputData input=(InputData)0;input.positionWS=i.world;input.normalWS=normal;input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.world);input.shadowCoord=TransformWorldToShadowCoord(i.world);input.bakedGI=SampleSH(normal);input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);input.shadowMask=half4(1,1,1,1);
    SurfaceData surface=(SurfaceData)0;surface.albedo=albedo*_BaseColor.rgb;surface.alpha=1;surface.smoothness=saturate(1-rough)*.65;surface.occlusion=1;surface.normalTS=float3(0,0,1);
    half4 color=UniversalFragmentPBR(input,surface);
    Light sun=GetMainLight(input.shadowCoord);
    float lightTone=saturate(dot(normal,sun.direction)*.5+.5)*lerp(.38,1,sun.shadowAttenuation);
    float textureTone=saturate(dot(albedo,float3(.3,.59,.11))*1.5);
    float tone=saturate(lightTone*.74+textureTone*.26);
    tone=lerp(tone,floor(tone*5)/5,.38);
    float3 pigment=lerp(_Ink.rgb,_Paint.rgb,smoothstep(.12,.93,tone));
    color.rgb=lerp(color.rgb,pigment,_Illustrated);
    color.rgb=MixFog(color.rgb,i.fog);return color;
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}

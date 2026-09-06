Shader "DarkBeforeDawn/PrintedFantasy"
{
 Properties
 {
  _BaseColor("Pigment",Color)=(.3,.45,.2,1)
  _ShadowColor("Shadow ink",Color)=(.025,.085,.10,1)
  _Surface("0 earth / 1 stone / 2 leaves / 3 bark / 4 roof / 5 rock",Float)=0
  _Wind("Breeze",Float)=0
  _BaseMap("Surface detail",2D)="white"{}
  _TextureStrength("Surface texture",Range(0,1))=0
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass
  {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   struct A{float4 p:POSITION;float3 n:NORMAL;float4 c:COLOR;};
   struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;float4 c:COLOR;float fog:TEXCOORD2;};
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseColor,_ShadowColor,_BaseMap_ST;float _Surface,_Wind,_TextureStrength;
   CBUFFER_END
   float _ValleyCycleEnabled;float4 _ValleySceneTint;
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);}
   V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.w.x+=sin(_Time.y*.55+o.w.z*.32+o.w.y*.27)*_Wind*.07;o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.c=a.c;o.fog=ComputeFogFactor(o.p.z);return o;}
   half4 frag(V i):SV_Target
   {
    float3 n=normalize(i.n);float2 uv=lerp(float2(abs(n.x)>abs(n.z)?i.w.z:i.w.x,i.w.y),i.w.xz,step(.72,abs(n.y)));
    float broad=noise(uv*.65)*.65+noise(uv*1.8)*.35;
    float detail=noise(uv*13),stipple=hash(floor(uv*95));
    Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));
    float3 lightDirection=normalize(lerp(float3(-.6,.85,-.4),sun.direction,_ValleyCycleEnabled));
    float ndl=dot(n,lightDirection);
    // Broad sky fill keeps illustrated details legible when the sun is behind the castle.
    float tone=saturate(lerp(ndl*.5+.48,ndl*.28+.66,_ValleyCycleEnabled));
    tone=saturate(tone+(broad-.5)*.23+(detail-.5)*.14);
    tone=lerp(tone,floor(tone*6)/6,.18);
    float3 pigment=_BaseColor.rgb*i.c.rgb;
    float textureValue=dot(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv*.28).rgb,float3(.3,.59,.11));
    pigment*=lerp(1,.58+textureValue*1.7,_TextureStrength);
    if(_Surface>.5&&_Surface<1.5){float row=floor(uv.y*1.3);float2 f=frac(float2(uv.x*.9+fmod(row,2)*.5,uv.y*1.3));float joint=min(min(f.x,1-f.x),min(f.y,1-f.y));tone*=lerp(.66,1,smoothstep(.025,.06,joint));}
    if(_Surface>1.5&&_Surface<2.5){tone+=smoothstep(.57,.72,detail)*.15; pigment*=lerp(float3(.8,.96,1),float3(1.13,1.06,.74),broad);}
    if(_Surface>2.5&&_Surface<3.5){tone*=.70+noise(float2(uv.x*9,uv.y*.45))*.42;}
    if(_Surface>3.5&&_Surface<4.5){float2 tile=uv*2;tile.x+=fmod(floor(tile.y),2)*.5;tone*=lerp(.63,1,smoothstep(.025,.10,frac(tile.y)));}
    if(_Surface>4.5){tone*=.75+noise(float2(uv.x*3,uv.y*.4))*.35;pigment=lerp(pigment,pigment*float3(.9,1.17,.65),smoothstep(.35,.85,n.y));}
    float3 c=lerp(_ShadowColor.rgb*i.c.rgb,pigment,smoothstep(.12,.98,tone));
    c+=pigment*.09*_ValleyCycleEnabled;
    c*=lerp(lerp(.62,.76,_ValleyCycleEnabled),1,sun.shadowAttenuation);
    c*=lerp(float3(1,1,1),_ValleySceneTint.rgb,_ValleyCycleEnabled);
    // Fine pigment irregularities stay attached to geometry as the camera drifts.
    c*=.95+stipple*.10;
    return half4(MixFog(c,i.fog),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 }
}

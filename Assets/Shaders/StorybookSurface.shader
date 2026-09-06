Shader "DarkBeforeDawn/StorybookSurface"
{
 Properties
 {
  _BaseColor("Pigment",Color)=(.3,.4,.2,1)
  _Stone("Stone courses",Range(0,1))=0
  [Enum(Ground,0,Stone,1,Foliage,2,Wood,3,Roof,4,Path,5)] _Surface("Surface",Float)=0
  _DetailScale("Detail scale",Float)=1
  _BumpStrength("Relief",Range(0,1))=.3
  _Wind("Wind",Range(0,1))=0
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
   struct A{float4 p:POSITION;float3 n:NORMAL;};
   struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float fog:TEXCOORD2;};
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseColor;float _Stone,_Surface,_DetailScale,_BumpStrength,_Wind;
   CBUFFER_END
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
   float fbm(float2 p){return noise(p)*.57+noise(p*2.03+17.2)*.28+noise(p*4.09+3.1)*.15;}
   V vert(A i)
   {
    V o;o.world=TransformObjectToWorld(i.p.xyz);
    float bend=saturate(i.p.y*.7+.4)*_Wind;
    o.world.x+=sin(_Time.y*.8+o.world.z*.43+o.world.x*.23)*bend*.12;
    o.world.z+=cos(_Time.y*.63+o.world.x*.35)*bend*.08;
    o.p=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.n);o.fog=ComputeFogFactor(o.p.z);return o;
   }
   half4 frag(V i):SV_Target
   {
    float3 n=normalize(i.normal);float2 uv=lerp(float2(abs(n.x)>abs(n.z)?i.world.z:i.world.x,i.world.y),i.world.xz,step(.65,abs(n.y)))*_DetailScale;
    float broad=fbm(uv*.43),grain=fbm(uv*11),height=grain*.12;
    float3 pigment=_BaseColor.rgb*(.78+broad*.30+grain*.13);
    if(_Surface<.5)
    {
     float patch=fbm(i.world.xz*.13),slope=1-saturate(n.y);
     pigment=lerp(_BaseColor.rgb*float3(.76,.88,.78),_BaseColor.rgb*float3(1.15,1.06,.78),smoothstep(.3,.72,patch));
     pigment*=.82+grain*.28;
     pigment=lerp(pigment,float3(.30,.28,.20),saturate(slope*2.5)*.5);
     height=grain*.045;
    }
    else if(_Surface<1.5 || _Stone>.5)
    {
     float row=floor(uv.y*1.85);float2 cell=float2(uv.x*1.25+fmod(row,2)*.5,uv.y*1.85);
     float2 f=frac(cell);float random=hash(floor(cell));
     float chip=noise(uv*23)*.016;
     float joint=min(min(f.x,1-f.x),min(f.y,1-f.y));
     float aa=max(fwidth(joint),.006);float block=smoothstep(.028+chip,.028+chip+aa,joint);
     float bevel=smoothstep(.02,.105,joint);
     pigment*=.78+random*.38;
     pigment=lerp(float3(.19,.18,.135),pigment,block);
     pigment*=.80+bevel*.20;
     float moss=smoothstep(.55,.76,fbm(uv*.82+31))*saturate(.7+n.y*.55);
     pigment=lerp(pigment,float3(.20,.28,.115),moss*.73);
     height=bevel*.22+grain*.055;
    }
    else if(_Surface<2.5)
    {
     float clumps=fbm(uv*2.7);
     pigment*=.67+clumps*.68;
     pigment=lerp(pigment,pigment*float3(1.19,1.10,.72),smoothstep(.6,.82,clumps));
     height=clumps*.16;
    }
    else if(_Surface<3.5)
    {
     float streak=fbm(float2(uv.x*15,uv.y*.6));
     pigment*=.65+streak*.65;height=streak*.12;
    }
    else if(_Surface<4.5)
    {
     float2 tile=uv*float2(3.1,3.6);tile.x+=fmod(floor(tile.y),2)*.5;
     float2 f=frac(tile);float seam=smoothstep(.035,.09,min(f.x,1-f.x))*smoothstep(.045,.1,f.y);
     pigment*=.72+hash(floor(tile))*.42;
     pigment*=.58+seam*.42;height=seam*.12;
    }
    else {pigment*=.8+noise(uv*29)*.30;height=grain*.045;}
    float3 dpdx=ddx(i.world),dpdy=ddy(i.world);float3 r1=cross(dpdy,n),r2=cross(n,dpdx);float det=dot(dpdx,r1);
    float3 grad=sign(det)*(ddx(height)*r1+ddy(height)*r2);
    n=normalize(abs(det)*n-_BumpStrength*grad+float3(0,.000001,0));
    Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
    float diffuse=saturate(dot(n,sun.direction));float shade=sun.shadowAttenuation;
    float3 color=pigment*(float3(.31,.36,.29)+sun.color*diffuse*shade*.92);
    if(_Surface>1.5&&_Surface<2.5)
    {
     float back=pow(saturate(dot(normalize(i.world-_WorldSpaceCameraPos),sun.direction)),3);
     color+=pigment*sun.color*back*.24;
    }
    return half4(MixFog(color,i.fog),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 }
}

Shader "DarkBeforeDawn/GoldenDawn"
{
 Properties { _Top("Upper sky",Color)=(.12,.30,.36,1) _Horizon("Parchment light",Color)=(.95,.69,.32,1) }
 SubShader
 {
  Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"} Cull Off ZWrite Off
  Pass
  {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct V{float4 p:SV_POSITION;float3 d:TEXCOORD0;};float4 _Top,_Horizon;
   float _ValleyCycleEnabled,_ValleyDaylight,_ValleyTwilight,_ValleyNight;
   float4 _ValleySunDirection,_ValleyMoonDirection,_ValleySkyZenith,_ValleySkyHorizon;
   V vert(float4 p:POSITION){V o;o.p=TransformObjectToHClip(p.xyz);o.d=p.xyz;return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);}
   half4 frag(V i):SV_Target
   {
    float3 d=normalize(i.d);float elevation=saturate(d.y);
    float day=lerp(1,_ValleyDaylight,_ValleyCycleEnabled);
    float night=_ValleyNight*_ValleyCycleEnabled;
    float3 zenith=lerp(float3(.07,.24,.34),_ValleySkyZenith.rgb,_ValleyCycleEnabled);
    float3 horizon=lerp(float3(.73,.70,.49),_ValleySkyHorizon.rgb,_ValleyCycleEnabled);
    float3 c=lerp(horizon,zenith,saturate(elevation*2.1));
    float2 p=d.xz/(max(.03,d.y)+.4)*float2(3,8)+float2(_Time.y*.0015,0);
    float cloud=noise(p)*.55+noise(p*2.1)*.28+noise(p*4.2)*.17;
    float cover=smoothstep(.54,.74,cloud)*smoothstep(0,.12,elevation);
    float3 cloudColor=lerp(lerp(float3(.012,.025,.055),float3(.34,.47,.51),day),lerp(float3(.045,.075,.135),float3(.87,.81,.61),day),smoothstep(.54,.73,cloud));
    c=lerp(c,cloudColor,cover*.75);
    float3 sunDirection=normalize(lerp(float3(-.30,.20,1),_ValleySunDirection.xyz,_ValleyCycleEnabled));
    float sunDot=dot(d,sunDirection);float sunVisible=smoothstep(-.055,.025,sunDirection.y);
    c+=float3(.42,.25,.07)*pow(saturate(sunDot),58)*.45*sunVisible;
    c=lerp(c,float3(1,.88,.53),smoothstep(.9989,.9993,sunDot)*sunVisible);
    if(_ValleyCycleEnabled>.5)
    {
     float3 moon=normalize(_ValleyMoonDirection.xyz);float moonDot=dot(d,moon);
     float moonVisible=smoothstep(-.035,.035,moon.y)*night;
     float crater=noise(d.xz*310)*.5+noise(d.xy*720)*.5;
     float moonDisc=smoothstep(.99905,.99932,moonDot)*moonVisible;
     c+=float3(.035,.06,.12)*pow(saturate(moonDot),75)*moonVisible;
     c=lerp(c,float3(.68,.79,.96)*(.74+crater*.26),moonDisc);
     float2 stars=float2(atan2(d.z,d.x)*.159155+.5,asin(clamp(d.y,-1,1))*.31831+.5)*float2(620,310);
     float2 cell=floor(stars),f=frac(stars)-.5;float radius=length(f);
     float star=smoothstep(.12,.025,radius)*step(.996,hash(cell));
     float twinkle=.76+.24*sin(_Time.y*.7+hash(cell+18)*30);
     c+=float3(.58,.71,.92)*star*night*twinkle*smoothstep(.02,.14,d.y)*(1-cover)*(1-moonDisc);
    }
    return half4(c,1);
   }
   ENDHLSL
  }
 }
}

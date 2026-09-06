Shader "DarkBeforeDawn/FantasyWater"
{
    Properties { _BaseColor ("Deep water", Color) = (0.025,0.075,0.11,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A {float4 positionOS:POSITION;};
            struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float fog:TEXCOORD1;};
            float4 _BaseColor;
            V vert(A i) {V o;o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.fog=ComputeFogFactor(o.positionCS.z);return o;}
            half4 frag(V i):SV_Target
            {
                float2 p=i.world.xz;float t=_Time.y*.13;
                float wave=sin(p.x*2.1+sin(p.y*.63+t)*1.4+t)*sin(p.y*3.7-p.x*.23-t);
                float threads=pow(saturate(wave),12);
                float drift=sin(p.y*.4+p.x*.15+t)*.5+.5;
                float3 color=_BaseColor.rgb+float3(.015,.04,.05)*drift+threads*float3(.045,.10,.12);
                float moon=exp(-pow((p.x+10+sin(p.y*.25)*2)/7,2));
                color+=threads*moon*float3(.10,.17,.21);
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
}

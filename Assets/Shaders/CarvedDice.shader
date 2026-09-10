Shader "DarkBeforeDawn/CarvedDice"
{
    Properties
    {
        _BaseColor("Stone pigment", Color) = (.84,.74,.55,1)
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Polish", Range(0,1)) = .48
        _GrainStrength("Fine stone grain", Range(0,.15)) = .035
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Metallic, _Smoothness, _GrainStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float3 positionOS:TEXCOORD2; };
            float Hash(float3 p) { p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z); }
            float Grain(float3 p)
            {
                float3 a=floor(p), f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(lerp(Hash(a),Hash(a+float3(1,0,0)),f.x),lerp(Hash(a+float3(0,1,0)),Hash(a+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(Hash(a+float3(0,0,1)),Hash(a+float3(1,0,1)),f.x),lerp(Hash(a+float3(0,1,1)),Hash(a+1),f.x),f.y),f.z);
            }
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                o.positionOS=v.positionOS.xyz;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                // Object-space grain stays attached during the roll; no masonry seams or magnified wall texture.
                float grain=(Grain(i.positionOS*85)-.5)*_GrainStrength;
                grain+=(Grain(i.positionOS*9)-.5)*_GrainStrength*.35;
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=_BaseColor.rgb*(1+grain);
                surface.metallic=_Metallic;
                surface.smoothness=saturate(_Smoothness+grain);
                surface.normalTS=half3(0,0,1);
                surface.occlusion=1;
                surface.alpha=1;
                InputData lighting=(InputData)0;
                lighting.positionWS=i.positionWS;
                lighting.normalWS=normalize(i.normalWS);
                lighting.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
                lighting.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                lighting.bakedGI=half3(.16,.18,.18);
                lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                lighting.shadowMask=half4(1,1,1,1);
                return UniversalFragmentPBR(lighting,surface);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}

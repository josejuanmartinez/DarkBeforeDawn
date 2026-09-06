Shader "DarkBeforeDawn/RetroLandscape"
{
 Properties
 {
  _VerticalResolution("Virtual screen height",Float)=432
  _ColorSteps("Palette steps",Range(8,64))=32
  _Dither("Paper grain strength",Range(0,1))=.35
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" }
  Pass
  {
   ZWrite Off ZTest Always Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
   float _VerticalResolution, _ColorSteps, _Dither;
   half4 Frag(Varyings input):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float2 grid=float2(_VerticalResolution*_ScreenParams.x/_ScreenParams.y,_VerticalResolution);
    float2 cell=floor(input.texcoord*grid);
    float2 uv=(cell+.5)/grid;
    half4 color=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv);
    // Quantize in display space so dark scenery retains readable detail.
    float3 c=LinearToSRGB(max(color.rgb,0));
    c=lerp(dot(c,float3(.299,.587,.114)).xxx,c,.90);
    c=c*float3(1.025,1.01,.965);
    // Stationary fine paper grain, without a repeating pixel/dither lattice.
    float grain=frac(sin(dot(floor(input.texcoord*_ScreenParams.xy),float2(12.9898,78.233)))*43758.5453)-.5;
    c=saturate(c+grain*_Dither*.10);
    return half4(SRGBToLinear(c),color.a);
   }
   ENDHLSL
  }
 }
}

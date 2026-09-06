using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Illustrated sun/moon orbits and coordinated scene lighting. No shared materials are edited.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class ValleyDayNightCycle : MonoBehaviour
{
    [Tooltip("Seconds for one complete sunrise, day, sunset and night.")]
    [Min(20)] public float cycleSeconds = 360;
    [Tooltip("0 midnight; .25 sunrise; .5 noon; .75 sunset. Also previews in Edit mode.")]
    [Range(0,1)] public float timeOfDay = .62f;
    public bool animate = true;
    public Light sun;
    public Light moon;
    [Range(.1f,2)] public float daylightIntensity = 1.1f;
    [Range(0,1)] public float moonlightIntensity = .24f;
    private float lastApplied = -1;
    private bool captured;
    private Color oldFog,oldSky,oldEquator,oldGround,oldSunColor;
    private float oldFogDensity,oldSunIntensity;
    private bool oldSunEnabled;
    private Quaternion oldSunRotation;
    private Light oldMainLight;
    private AmbientMode oldAmbientMode;

    private void OnEnable()
    {
        Capture();
        ApplyTime(timeOfDay);
    }
    private void Update()
    {
        if(Application.isPlaying && animate) timeOfDay=Mathf.Repeat(timeOfDay+Time.deltaTime/Mathf.Max(20,cycleSeconds),1);
        if(Application.isPlaying || !Mathf.Approximately(lastApplied,timeOfDay)) ApplyTime(timeOfDay);
    }
    private void Capture()
    {
        if(captured || sun==null || moon==null)return;
        oldFog=RenderSettings.fogColor;oldFogDensity=RenderSettings.fogDensity;oldAmbientMode=RenderSettings.ambientMode;
        oldSky=RenderSettings.ambientSkyColor;oldEquator=RenderSettings.ambientEquatorColor;oldGround=RenderSettings.ambientGroundColor;oldMainLight=RenderSettings.sun;
        if(sun!=null){oldSunColor=sun.color;oldSunIntensity=sun.intensity;oldSunRotation=sun.transform.rotation;oldSunEnabled=sun.enabled;}
        captured=true;
    }
    public void RebindSun(Light replacement)
    {
        if(replacement==null)return;
        sun=replacement;captured=false;Capture();ApplyTime(timeOfDay);
    }
    public void ApplyTime(float normalizedTime)
    {
        if(sun==null || moon==null)return;
        Capture();timeOfDay=Mathf.Repeat(normalizedTime,1);lastApplied=timeOfDay;
        float angle=(timeOfDay-.25f)*Mathf.PI*2;
        // An illustrated orbit facing the valley: both bodies rise and set behind its horizon.
        Vector3 sunDirection=new Vector3(Mathf.Cos(angle)*.72f,Mathf.Sin(angle)*.55f,1).normalized;
        Vector3 moonDirection=new Vector3(-Mathf.Cos(angle)*.72f,-Mathf.Sin(angle)*.55f,1).normalized;
        float day=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.09f,.24f,sunDirection.y));
        float twilight=1-Mathf.SmoothStep(0,1,Mathf.Abs(sunDirection.y)/.22f);
        float sunPower=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.025f,.30f,sunDirection.y));
        float moonPower=(1-day)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.25f,moonDirection.y));
        sun.transform.rotation=Quaternion.LookRotation(-sunDirection);sun.enabled=sunDirection.y>-.04f;
        sun.intensity=daylightIntensity*sunPower;sun.color=Color.Lerp(new Color(1,.46f,.18f),new Color(1,.92f,.77f),sunPower);
        moon.transform.rotation=Quaternion.LookRotation(-moonDirection);moon.enabled=moonPower>.001f;
        moon.intensity=moonlightIntensity*moonPower;moon.color=new Color(.47f,.64f,1);
        RenderSettings.sun=sun.enabled?sun:moon;
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=Color.Lerp(new Color(.06f,.10f,.22f),new Color(.56f,.64f,.61f),day);
        RenderSettings.ambientEquatorColor=Color.Lerp(new Color(.035f,.06f,.13f),new Color(.27f,.37f,.32f),day);
        RenderSettings.ambientGroundColor=Color.Lerp(new Color(.012f,.025f,.06f),new Color(.12f,.18f,.16f),day);
        RenderSettings.fogColor=Color.Lerp(new Color(.030f,.050f,.095f),new Color(.43f,.56f,.53f),day);
        RenderSettings.fogColor=Color.Lerp(RenderSettings.fogColor,new Color(.51f,.32f,.23f),twilight*.4f);
        RenderSettings.fogDensity=Mathf.Lerp(.006f,.0045f,day);
        Color tint=Color.Lerp(new Color(.16f,.24f,.42f),Color.white,day);
        tint=Color.Lerp(tint,new Color(.77f,.48f,.31f),twilight*.43f);
        Color zenith=Color.Lerp(new Color(.002f,.005f,.018f),new Color(.07f,.24f,.34f),day);
        Color horizon=Color.Lerp(new Color(.020f,.035f,.080f),new Color(.73f,.70f,.49f),day);
        horizon=Color.Lerp(horizon,new Color(.72f,.30f,.12f),twilight*.73f);
        Shader.SetGlobalFloat("_ValleyCycleEnabled",1);
        Shader.SetGlobalVector("_ValleySceneTint",new Vector4(tint.r,tint.g,tint.b,1));
        Shader.SetGlobalVector("_ValleySunDirection",sunDirection);
        Shader.SetGlobalVector("_ValleyMoonDirection",moonDirection);
        Shader.SetGlobalVector("_ValleySkyZenith",new Vector4(zenith.r,zenith.g,zenith.b,1));
        Shader.SetGlobalVector("_ValleySkyHorizon",new Vector4(horizon.r,horizon.g,horizon.b,1));
        Shader.SetGlobalFloat("_ValleyDaylight",day);Shader.SetGlobalFloat("_ValleyTwilight",twilight);Shader.SetGlobalFloat("_ValleyNight",1-day);
    }
    private void OnDisable()
    {
        Shader.SetGlobalFloat("_ValleyCycleEnabled",0);
        if(!captured)return;
        RenderSettings.fogColor=oldFog;RenderSettings.fogDensity=oldFogDensity;RenderSettings.ambientMode=oldAmbientMode;
        RenderSettings.ambientSkyColor=oldSky;RenderSettings.ambientEquatorColor=oldEquator;RenderSettings.ambientGroundColor=oldGround;RenderSettings.sun=oldMainLight;
        if(sun!=null){sun.color=oldSunColor;sun.intensity=oldSunIntensity;sun.transform.rotation=oldSunRotation;sun.enabled=oldSunEnabled;}
        if(moon!=null)moon.enabled=false;
        captured=false;lastApplied=-1;
    }
}

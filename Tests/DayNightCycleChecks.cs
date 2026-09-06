// Run through Unity CLI eval_file; restores the selected time after sampling a full cycle.
var cycle=UnityEngine.Object.FindAnyObjectByType<ValleyDayNightCycle>();
if(cycle==null)throw new System.Exception("Day/night controller is missing.");
float original=cycle.timeOfDay;
var elevations=new System.Collections.Generic.List<float>();
try
{
 cycle.ApplyTime(.25f);elevations.Add(UnityEngine.Shader.GetGlobalVector("_ValleySunDirection").y);
 cycle.ApplyTime(.5f);elevations.Add(UnityEngine.Shader.GetGlobalVector("_ValleySunDirection").y);
 if(!cycle.sun.enabled||cycle.sun.intensity<1||cycle.moon.enabled)throw new System.Exception("Invalid noon lighting.");
 if(UnityEngine.RenderSettings.sun!=cycle.sun)throw new System.Exception("Sun is not the daytime key light.");
 var dayFog=UnityEngine.RenderSettings.fogColor;var dayAmbient=UnityEngine.RenderSettings.ambientSkyColor;var dayTint=UnityEngine.Shader.GetGlobalVector("_ValleySceneTint");
 cycle.ApplyTime(.75f);elevations.Add(UnityEngine.Shader.GetGlobalVector("_ValleySunDirection").y);
 cycle.ApplyTime(0);elevations.Add(UnityEngine.Shader.GetGlobalVector("_ValleySunDirection").y);
 if(cycle.sun.enabled||!cycle.moon.enabled||cycle.moon.intensity<.1f)throw new System.Exception("Invalid moonlight.");
 if(UnityEngine.RenderSettings.sun!=cycle.moon)throw new System.Exception("Moon is not the nighttime key light.");
 if(UnityEngine.Shader.GetGlobalVector("_ValleyMoonDirection").y<.4f)throw new System.Exception("Moon is not above the horizon at midnight.");
 if(UnityEngine.RenderSettings.ambientSkyColor.grayscale>=dayAmbient.grayscale*.5f)throw new System.Exception("Ambient light did not darken.");
 if(UnityEngine.RenderSettings.fogColor.grayscale>=dayFog.grayscale*.5f)throw new System.Exception("Fog did not change for night.");
 if(UnityEngine.Shader.GetGlobalVector("_ValleySceneTint").x>=dayTint.x*.5f)throw new System.Exception("Illustrated materials did not receive the night tint.");
 if(UnityEngine.Shader.GetGlobalFloat("_ValleyNight")<.95f)throw new System.Exception("Stars are not enabled at night.");
 if(UnityEngine.Mathf.Abs(elevations[0])>.001f||elevations[1]<.4f||UnityEngine.Mathf.Abs(elevations[2])>.001f||elevations[3]>-.4f)throw new System.Exception("Sun does not rise and set.");
 cycle.ApplyTime(1);if(UnityEngine.Mathf.Abs(cycle.timeOfDay)>.001f)throw new System.Exception("Day cycle does not wrap at midnight.");
 foreach(var name in new[]{"GoldenDawn","PrintedFantasy","IllustratedFoliage","IllustratedRiver","RetroLandscape"})
  foreach(var message in UnityEditor.ShaderUtil.GetShaderMessages(UnityEngine.Shader.Find("DarkBeforeDawn/"+name)))
   if(message.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)throw new System.Exception(name+": "+message.message);
}
finally{cycle.ApplyTime(original);}
return new {sunElevations=elevations,cycleSeconds=cycle.cycleSeconds,result="Passed: sunrise/noon/sunset/midnight, moon orbit, sun/moon key-light swap, ambient/fog/material lighting, stars, cycle wrap, shader compilation."};

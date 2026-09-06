// Run with Unity CLI eval_file outside Play mode; samples the actual rig and renderer bounds.
var d=UnityEngine.Object.FindAnyObjectByType<ValleyDragonFlight>();
if(d==null)throw new System.Exception("Dragon is missing.");
var camera=UnityEngine.Camera.main;
var positions=new System.Collections.Generic.List<float>();
var wingRotations=new System.Collections.Generic.List<UnityEngine.Quaternion>();
try
{
 foreach(float time in new[]{1f,6f,11f,16f,21f})
 {
  d.Sample(time);var viewport=camera.WorldToViewportPoint(d.transform.position);
  positions.Add(viewport.x);wingRotations.Add(d.leftWing.localRotation);
  if(!d.model.gameObject.activeSelf||viewport.y<.65f||viewport.y>.96f)throw new System.Exception("Dragon left the sky corridor.");
 }
 for(int i=1;i<positions.Count;i++)if(positions[i]>=positions[i-1])throw new System.Exception("Dragon is not moving right to left.");
 d.Sample(8);var firstWing=d.leftWing.localRotation;var firstTail=d.tailJoints[3].localRotation;
 d.Sample(8+.25f/d.wingbeatsPerSecond);
 if(UnityEngine.Quaternion.Angle(firstWing,d.leftWing.localRotation)<5)throw new System.Exception("Wings are not flapping.");
 if(UnityEngine.Quaternion.Angle(firstTail,d.tailJoints[3].localRotation)<1)throw new System.Exception("Tail is not moving.");
 d.Sample(d.crossingSeconds*(1-d.initialProgress)-.001f);
 foreach(var filter in d.GetComponentsInChildren<UnityEngine.MeshFilter>())
  foreach(var vertex in filter.sharedMesh.vertices)
   if(camera.WorldToViewportPoint(filter.transform.TransformPoint(vertex)).x>=0)throw new System.Exception("Dragon resets before fully leaving the left edge.");
 d.Sample(d.crossingSeconds*(1-d.initialProgress)+.1f);
 if(d.model.gameObject.activeSelf)throw new System.Exception("Dragon is visible during the return pause.");
 d.Sample(d.crossingSeconds+d.pauseSeconds-d.crossingSeconds*d.initialProgress+.001f);
 foreach(var filter in d.GetComponentsInChildren<UnityEngine.MeshFilter>())
  foreach(var vertex in filter.sharedMesh.vertices)
   if(camera.WorldToViewportPoint(filter.transform.TransformPoint(vertex)).x<=1)throw new System.Exception("Dragon reappears inside the screen.");
}
finally{d.Sample(11);}
return "Passed: right-to-left travel, sky height, wingbeats, tail motion, fully off-screen exit and re-entry, hidden return pause.";

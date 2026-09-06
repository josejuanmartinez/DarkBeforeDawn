// Run through Unity CLI eval_file immediately after rebuilding Golden Vale in Edit mode.
// Raycasts use the rendered terrain mesh, not the builder's height function.
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
var type=typeof(RetroValleyBuilder);
var tops=(System.Collections.Generic.List<UnityEngine.Vector3>)type.GetField("stairTops",flags).GetValue(null);
var bases=(System.Collections.Generic.List<UnityEngine.Vector3>)type.GetField("stairBases",flags).GetValue(null);
if(tops.Count!=64)throw new System.Exception("Expected the complete 64-step approach.");
var terrain=UnityEngine.GameObject.Find("The Golden Vale/Moss and fern carpet").GetComponent<UnityEngine.MeshFilter>().sharedMesh;
var probe=new UnityEngine.GameObject("Temporary approach verification");
int checkedSupports=0;float landingGap=0;
try
{
 var collider=probe.AddComponent<UnityEngine.MeshCollider>();collider.sharedMesh=terrain;
 UnityEngine.Physics.SyncTransforms();
 for(int i=0;i<tops.Count;i++)
 {
  var ray=new UnityEngine.Ray(new UnityEngine.Vector3(tops[i].x,200,tops[i].z),UnityEngine.Vector3.down);
  if(!collider.Raycast(ray,out var hit,400))throw new System.Exception("Stair has no terrain below it: "+i);
  if(bases[i].y>hit.point.y+.01f)throw new System.Exception("Floating support at stair "+i);
  if(tops[i].y<hit.point.y-.1f)throw new System.Exception("Buried stair surface: "+i);
  if(i==tops.Count-1)landingGap=tops[i].y-hit.point.y;
  checkedSupports++;
 }
 if(landingGap>.28f||landingGap<-.1f)throw new System.Exception("Approach does not meet the terrain: "+landingGap);
}
finally{UnityEngine.Object.DestroyImmediate(probe);}
return new {checkedSupports,landingGap,description="All stair foundations intersect the actual terrain mesh; the last tread meets the ground."};

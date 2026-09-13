using System.Collections.Generic;
using UnityEngine;

/// <summary>A rounded solid die, with continuous bevel normals instead of corner spheres.</summary>
public static class CarvedDieMesh
{
    public static Mesh Build()
    {
        var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var triangles = new List<int>();
        Vector3[] faces = { Vector3.forward, Vector3.back, Vector3.up, Vector3.down, Vector3.right, Vector3.left };
        const int steps = 12;
        foreach (var face in faces)
        {
            var rotation = Quaternion.FromToRotation(Vector3.forward, face);
            int start = vertices.Count;
            for (int y = 0; y <= steps; y++) for (int x = 0; x <= steps; x++)
            {
                var p = rotation * new Vector3((float)x / steps - .5f, (float)y / steps - .5f, .5f);
                var inner = new Vector3(Mathf.Clamp(p.x,-.40f,.40f),Mathf.Clamp(p.y,-.40f,.40f),Mathf.Clamp(p.z,-.40f,.40f));
                var normal = (p-inner).normalized;
                vertices.Add(inner + normal * .10f); normals.Add(normal);
            }
            for (int y=0;y<steps;y++) for (int x=0;x<steps;x++)
            {
                int a=start+y*(steps+1)+x, b=a+1, c=a+steps+1, d=c+1;
                triangles.Add(a); triangles.Add(b); triangles.Add(d);
                triangles.Add(a); triangles.Add(d); triangles.Add(c);
            }
        }
        var mesh = new Mesh { name = "Rounded carved die" };
        mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles,0); mesh.RecalculateBounds();
        return mesh;
    }
}

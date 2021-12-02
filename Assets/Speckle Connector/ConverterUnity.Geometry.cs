﻿using Objects.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Objects.Other;
using Speckle.ConnectorUnity;
using Speckle.Core.Models;
using UnityEditor;
using UnityEngine;

using Mesh = Objects.Geometry.Mesh;
using SColor = System.Drawing.Color;

namespace Objects.Converter.Unity
{
  public partial class ConverterUnity
  {
    #region helper methods
    /// <summary>
    /// 
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public Vector3 VectorByCoordinates(double x, double y, double z, string units)
    {
      // switch y and z
      return new Vector3((float)ScaleToNative(x, units), (float)ScaleToNative(z, units),
          (float)ScaleToNative(y, units));
    }

    public Vector3 VectorFromPoint(Point p)
    {
      // switch y and z
      return new Vector3((float)ScaleToNative(p.x, p.units), (float)ScaleToNative(p.z, p.units),
          (float)ScaleToNative(p.y, p.units));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ptValues"></param>
    /// <returns></returns>
    // public Vector3 ArrayToPoint(double[] ptValues, string units)
    // {
    //   double x = ptValues[0];
    //   double y = ptValues[1];
    //   double z = ptValues[2];
    //
    //   return PointByCoordinates(x, y, z, units);
    // }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="arr"></param>
    /// <returns></returns>
    public Vector3[] ArrayToPoints(IList<double> arr, string units)
    {
      if (arr.Count % 3 != 0) throw new Exception("Array malformed: length%3 != 0.");

      Vector3[] points = new Vector3[arr.Count / 3];

      for (int i = 2, k = 0; i < arr.Count; i += 3)
        points[k++] = VectorByCoordinates(arr[i - 2], arr[i - 1], arr[i], units);


      return points;
    }

    public Vector3[] ArrayToPoints(IEnumerable<double> arr, string units, out Vector2[] uv)
    {
      uv = null;
      if (arr.Count() % 3 != 0) throw new Exception("Array malformed: length%3 != 0.");

      Vector3[] points = new Vector3[arr.Count() / 3];
      uv = new Vector2[points.Length];

      var asArray = arr.ToArray();
      for (int i = 2, k = 0; i < arr.Count(); i += 3)
      {

        points[k++] = VectorByCoordinates(asArray[i - 2], asArray[i - 1], asArray[i], units);
      }


      // get size of mesh
      for (int i = 0; i < points.Length; i++) { }

      return points;
    }
    #endregion

    #region ToSpeckle
    //TODO: more of these

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public Point PointToSpeckle(Vector3 p)
    {
      //switch y and z
      return new Point(p.x, p.z, p.y);
    }


    /// <summary>
    /// Converts the <see cref="MeshFilter"/> component on <paramref name="go"/> into a Speckle <see cref="Mesh"/>
    /// </summary>
    /// <param name="go">The Unity <see cref="GameObject"/> to convert</param>
    /// <returns>The converted <see cref="Mesh"/>, <see langword="null"/> if no <see cref="MeshFilter"/> on <paramref name="go"/> exists</returns>
    public Mesh MeshToSpeckle(GameObject go)
    {
      //TODO: support multiple filters?
      var filter = go.GetComponent<MeshFilter>();
      if (filter == null) return null;

      var nativeMesh = filter.mesh;
      
      var nTriangles = nativeMesh.triangles;
      List<int> sFaces = new List<int>(nTriangles.Length * 4);
      for (int i = 2; i < nTriangles.Length; i += 3)
      {
        sFaces.Add(0); //Triangle cardinality indicator

        sFaces.Add(nTriangles[i]);
        sFaces.Add(nTriangles[i - 1]);
        sFaces.Add(nTriangles[i - 2]);
      }

      var nVertices = nativeMesh.vertices;
      List<double> sVertices = new List<double>(nVertices.Length * 3);
      foreach (var vertex in nVertices)
      {
        var p = go.transform.TransformPoint(vertex);
        sVertices.Add(p.x);
        sVertices.Add(p.y);
        sVertices.Add(p.z);
      }
      
      var nColors = nativeMesh.colors;
      List<int> sColors = new List<int>(nColors.Length);
      sColors.AddRange(nColors.Select(c => c.ToIntColor()));

      var nTexCoords = nativeMesh.uv;
      List<double> sTexCoords = new List<double>(nTexCoords.Length * 2);
      foreach (var uv in nTexCoords)
      {
        sTexCoords.Add(uv.x);
        sTexCoords.Add(uv.y);
      }

      var mesh = new Mesh();
      // get the speckle data from the go here
      // so that if the go comes from speckle, typed props will get overridden below
      AttachUnityProperties(mesh, go);
      
      mesh.vertices = sVertices;
      mesh.faces = sFaces;
      mesh.colors = sColors;
      mesh.textureCoordinates = sTexCoords;
      mesh.units = ModelUnits;

      return mesh;
    }
    #endregion

    #region ToNative
    private GameObject NewPointBasedGameObject(Vector3[] points, string name)
    {
      if (points.Length == 0) return null;

      float pointDiameter = 1; //TODO: figure out how best to change this?

      var go = new GameObject();
      go.name = name;

      var lineRenderer = go.AddComponent<LineRenderer>();

      lineRenderer.positionCount = points.Length;
      lineRenderer.SetPositions(points);
      lineRenderer.numCornerVertices = lineRenderer.numCapVertices = 8;
      lineRenderer.startWidth = lineRenderer.endWidth = pointDiameter;

      return go;
    }

    /// <summary>
    /// Converts a Speckle <paramref name="point"/> to a <see cref="GameObject"/> with a <see cref="LineRenderer"/>
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public GameObject PointToNative(Point point)
    {
      Vector3 newPt = VectorByCoordinates(point.x, point.y, point.z, point.units);

      var go = NewPointBasedGameObject(new Vector3[] { newPt, newPt }, point.speckle_type);
      return go;
    }


    /// <summary>
    /// Converts a Speckle <paramref name="line"/> to a <see cref="GameObject"/> with a <see cref="LineRenderer"/>
    /// </summary>
    /// <param name="line"></param>
    /// <returns></returns>
    public GameObject LineToNative(Line line)
    {
      var points = new List<Vector3> { VectorFromPoint(line.start), VectorFromPoint(line.end) };

      var go = NewPointBasedGameObject(points.ToArray(), line.speckle_type);
      return go;
    }

    /// <summary>
    /// Converts a Speckle <paramref name="polyline"/> to a <see cref="GameObject"/> with a <see cref="LineRenderer"/>
    /// </summary>
    /// <param name="polyline"></param>
    /// <returns></returns>
    public GameObject PolylineToNative(Polyline polyline)
    {
      var points = polyline.GetPoints().Select(VectorFromPoint);

      var go = NewPointBasedGameObject(points.ToArray(), polyline.speckle_type);
      return go;
    }

    /// <summary>
    /// Converts a Speckle <paramref name="curve"/> to a <see cref="GameObject"/> with a <see cref="LineRenderer"/>
    /// </summary>
    /// <param name="curve"></param>
    /// <returns></returns>
    public GameObject CurveToNative(Curve curve)
    {
      var points = ArrayToPoints(curve.points, curve.units);
      var go = NewPointBasedGameObject(points, curve.speckle_type);
      return go;
    }

    
    public GameObject MeshToNative(Base speckleMeshObject)
    {
      if (!(speckleMeshObject["displayMesh"] is Mesh))
        return null;

      return MeshToNative(speckleMeshObject["displayMesh"] as Mesh,
          speckleMeshObject["renderMaterial"] as RenderMaterial, speckleMeshObject.GetMembers());
    }
    /// <summary>
    /// Converts <paramref name="speckleMesh"/> to a <see cref="GameObject"/> with a <see cref="MeshRenderer"/>
    /// </summary>
    /// <param name="speckleMesh">Mesh to convert</param>
    /// <param name="renderMaterial">If provided, will override the renderMaterial on the mesh itself</param>
    /// <param name="properties">If provided, will override the properties on the mesh itself</param>
    /// <returns></returns>
    public GameObject MeshToNative(
        Mesh speckleMesh, RenderMaterial renderMaterial = null,
        Dictionary<string, object> properties = null
    )
    {
      if (speckleMesh.vertices.Count == 0 || speckleMesh.faces.Count == 0)
      {
        return null;
      }
      
      var recenterMeshTransforms = true; //TODO: figure out how best to change this?

      speckleMesh.AlignVerticesWithTexCoordsByIndex();
      var verts = ArrayToPoints(speckleMesh.vertices, speckleMesh.units);


      //convert speckleMesh.faces into triangle array           
      List<int> tris = new List<int>();
      int i = 0;
      // TODO: Check if this is causing issues with normals for mesh 
      while (i < speckleMesh.faces.Count)
      {
        int n = speckleMesh.faces[i];
        if (n < 3) n += 3; // 0 -> 3, 1 - > 4
        
        if (n == 3)
        {
          //Triangles
          tris.Add(speckleMesh.faces[i + 1]);
          tris.Add(speckleMesh.faces[i + 3]);
          tris.Add(speckleMesh.faces[i + 2]);
        }
        else if (n == 4)
        {
          //Quads to triangles
          tris.Add(speckleMesh.faces[i + 1]);
          tris.Add(speckleMesh.faces[i + 3]);
          tris.Add(speckleMesh.faces[i + 2]);

          tris.Add(speckleMesh.faces[i + 1]);
          tris.Add(speckleMesh.faces[i + 4]);
          tris.Add(speckleMesh.faces[i + 3]);
        }
        else
        {
          //TODO n-gon triangulation, for now n-gon faces will be ignored
          
        }
        
        i += n + 1;
      }

      var go = new GameObject { name = speckleMesh.speckle_type };
      var mesh = new UnityEngine.Mesh { name = speckleMesh.speckle_type };

      if (verts.Length >= 65535)
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;


      // center transform pivot according to the bounds of the model
      if (recenterMeshTransforms)
      {
        Bounds meshBounds = new Bounds
        {
          center = verts[0]
        };

        foreach (var vert in verts)
        {
          meshBounds.Encapsulate(vert);
        }

        go.transform.position = meshBounds.center;

        // offset mesh vertices
        for (int l = 0; l < verts.Length; l++)
        {
          verts[l] -= meshBounds.center;
        }
      }
      
      mesh.SetVertices(verts);
      mesh.SetTriangles(tris, 0);

      //Set texture coordinates
      bool hasValidUVs = speckleMesh.TextureCoordinatesCount == speckleMesh.VerticesCount;
      if(speckleMesh.textureCoordinates.Count > 0 && !hasValidUVs) Debug.LogWarning($"Expected number of UV coordinates to equal vertices. Got {speckleMesh.TextureCoordinatesCount} expected {speckleMesh.VerticesCount}. \nID = {speckleMesh.id}", mesh);
      
      if (hasValidUVs)
      {
        var uv = new List<Vector2>(speckleMesh.TextureCoordinatesCount);
        for (int j = 0; j < speckleMesh.TextureCoordinatesCount; j++)
        {
          var (u, v) = speckleMesh.GetTextureCoordinate(j);
          uv.Add(new Vector2((float)u,(float)v));
        }
        mesh.SetUVs(0, uv);
      }
      else if (speckleMesh.bbox != null)
      {
        //Attempt to generate some crude UV coordinates using bbox
        var uv = GenerateUV(verts, (float)speckleMesh.bbox.xSize.Length, (float)speckleMesh.bbox.ySize.Length).ToList();
        mesh.SetUVs(0, uv);
      }

      //Set vertex colors
      if (speckleMesh.colors.Count == speckleMesh.VerticesCount)
      {
        static Color ToUnityColor(SColor color) => new Color(color.R, color.G, color.B, color.A);
        var colors = speckleMesh.colors.Select(c => ToUnityColor(SColor.FromArgb(c))).ToList();
        mesh.SetColors(colors);
      }

      // BUG: causing some funky issues with meshes
      // mesh.RecalculateNormals( );
      mesh.Optimize();
      // Setting mesh to filter once all mesh modifying is done
      go.SafeMeshSet(mesh, true);


      var meshRenderer = go.AddComponent<MeshRenderer>();
      var speckleMaterial = renderMaterial ?? (RenderMaterial)speckleMesh["renderMaterial"];
      meshRenderer.sharedMaterial = GetMaterial(speckleMaterial);

      //Add mesh collider
      // MeshCollider mc = go.AddComponent<MeshCollider>( );
      // mc.sharedMesh = mesh;
      //mc.convex = true;


      //attach properties on this very mesh
      //means the mesh originated in Rhino or similar
      if (properties == null)
      {
        var meshprops = typeof(Mesh).GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(x => x.Name)
            .ToList();
        properties = speckleMesh.GetMembers()
            .Where(x => !meshprops.Contains(x.Key))
            .ToDictionary(x => x.Key, x => x.Value);
      }

      AttachSpeckleProperties(go, properties);
      return go;
    }

    private static IEnumerable<Vector2> GenerateUV(IReadOnlyList<Vector3> verts, float xSize, float ySize)
    {
      var uv = new Vector2[verts.Count];
      for (int i = 0; i < verts.Count; i++)
      {

        var vert = verts[i];
        uv[i] = new Vector2(vert.x / xSize, vert.y / ySize);
      }
      return uv;
    }
    #endregion





    private Material GetMaterial(RenderMaterial renderMaterial)
    {
      //todo support more complex materials
      var shader = Shader.Find("Standard");
      Material mat = new Material(shader);

      //if a renderMaterial is passed use that, otherwise try get it from the mesh itself

      if (renderMaterial != null)
      {
        // 1. match material by name, if any
        Material matByName = null;
        
        foreach (var _mat in ContextObjects)
        {
          if (((Material)_mat.NativeObject).name == renderMaterial.name)
          {
            if (matByName == null) matByName = (Material)_mat.NativeObject;
            else Debug.LogWarning("There is more than one Material with the name \'" + renderMaterial.name + "\'!", (Material)_mat.NativeObject);
          }
        }
        if (matByName != null) return matByName;

        // 2. re-create material by setting diffuse color and transparency on standard shaders
        if (renderMaterial.opacity < 1)
        {
          shader = Shader.Find("Transparent/Diffuse");
          mat = new Material(shader);
        }

        var c = renderMaterial.diffuse.ToUnityColor();
        mat.color = new Color(c.r, c.g, c.b, Convert.ToSingle(renderMaterial.opacity));
        mat.name = renderMaterial.name == null ? "material-"+ Guid.NewGuid().ToString().Substring(0,8) : renderMaterial.name;


#if UNITY_EDITOR
        if (StreamManager.GenerateMaterials)
        {
          if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
          if (!AssetDatabase.IsValidFolder("Assets/Resources/Materials")) AssetDatabase.CreateFolder("Assets/Resources", "Materials");
          if (!AssetDatabase.IsValidFolder("Assets/Resources/Materials/Speckle Generated")) AssetDatabase.CreateFolder("Assets/Resources/Materials", "Speckle Generated");
          if (AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/Materials/Speckle Generated/" + mat.name + ".mat").Length == 0) AssetDatabase.CreateAsset(mat, "Assets/Resources/Materials/Speckle Generated/" + mat.name + ".mat");
        }
#endif


        return mat;
      }
      // 3. if not renderMaterial was passed, the default shader will be used 
      return mat;
    }

    private void AttachSpeckleProperties(GameObject go, Dictionary<string, object> properties)
    {
      var sd = go.AddComponent<SpeckleProperties>();
      sd.Data = properties;
    }


    private void AttachUnityProperties(Base @base, GameObject go)
    {
      var sd = go.GetComponent<SpeckleProperties>();
      if (sd == null || sd.Data == null)
        return;

      foreach (var key in sd.Data.Keys)
      {
        @base[key] = sd.Data[key];
      }
    }
  }
}
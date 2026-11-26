using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using Zenject;
using ZombieCity.City;
using ZombieCity.City.Core;
using Debug = UnityEngine.Debug;

namespace ZombieCity.City.Scene
{
    /// <summary>One building mesh, built once at startup (ADR-004 §4).</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class BuildingsView : MonoBehaviour, IBuildingsView
    {
        [SerializeField] Material material;

        CityModelAsset _model;
        CityBakeAsset _bake;
        bool _visible = true;

        public bool Visible
        {
            get => _visible;
            set
            {
                if (_visible == value)
                {
                    return;
                }

                _visible = value;
                MeshRenderer renderer = GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.enabled = value;
                }

                Changed?.Invoke();
            }
        }

        public event Action Changed;

        [Inject]
        public void Construct(CityModelAsset model, CityBakeAsset bake)
        {
            _model = model;
            _bake = bake;
        }

        [ContextMenu("Buildings/Show")]
        void ShowBuildings()
        {
            Visible = true;
        }

        [ContextMenu("Buildings/Hide")]
        void HideBuildings()
        {
            Visible = false;
        }

        void Start()
        {
            if (_model == null || _model.model == null)
            {
                Debug.LogWarning("[City] BuildingsView has no CityModelAsset.");
                return;
            }

            var watch = Stopwatch.StartNew();
            BuildingMeshData data = _bake != null
                ? BuildingMeshBuilder.Build(_model.model, _bake.Grid)
                : BuildingMeshBuilder.Build(_model.model);
            watch.Stop();

            int count = data.vertexCount;
            var vertices = new Vector3[count];
            var colors = new Color32[count];
            for (int i = 0; i < count; i++)
            {
                int p = i * 3;
                vertices[i] = new Vector3(data.positions[p], data.positions[p + 1], data.positions[p + 2]);
                uint c = data.colors[i];
                colors[i] = new Color32((byte)c, (byte)(c >> 8), (byte)(c >> 16), (byte)(c >> 24));
            }

            var mesh = new Mesh
            {
                name = "CityBuildings",
                indexFormat = IndexFormat.UInt32
            };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetIndices(data.indices, MeshTopology.Triangles, 0);
            mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            if (material != null)
            {
                GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            Debug.Log(
                "[City] buildings " + data.buildingCount
                + " skipped " + data.skippedBuildings
                + " rings " + data.skippedRings
                + " outsideGrid " + data.outsideGrid
                + " verts " + data.vertexCount
                + " tris " + data.triangleCount
                + " ms " + watch.Elapsed.TotalMilliseconds.ToString("0.0"));
        }
    }
}

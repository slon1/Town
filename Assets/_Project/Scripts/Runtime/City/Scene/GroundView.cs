using UnityEngine;
using Zenject;
using ZombieCity.City;
using ZombieCity.City.Core;

namespace ZombieCity.City.Scene
{
    /// <summary>
    /// XZ quad over <see cref="CityGrid"/> with a point-filtered RGBA32 texture.
    /// Row 0 of the texture is minimum j, so north (+Z) is the top of the texture.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class GroundView : MonoBehaviour, ILayerView
    {
        [SerializeField] Material material;
        [SerializeField] CityOverlay initialOverlays = CityOverlay.Tram;

        CityBakeAsset _bake;
        CityLayer _layer = CityLayer.Access;
        CityOverlay _overlays;
        Texture2D _texture;
        uint[] _pixels;
        Material _runtime;
        bool _built;

        public CityLayer Layer => _layer;

        public event System.Action Changed;

        [Inject]
        public void Construct(CityBakeAsset bake)
        {
            _bake = bake;
        }

        void Awake()
        {
            _overlays = initialOverlays;
        }

        void Start()
        {
            Rebuild();
            Changed?.Invoke();
        }

        public void SetLayer(CityLayer layer)
        {
            if (_built && _layer == layer)
            {
                return;
            }

            _layer = layer;
            Rebuild();
            Changed?.Invoke();
        }

        public void SetOverlay(CityOverlay overlay, bool enabled)
        {
            CityOverlay next = enabled ? _overlays | overlay : _overlays & ~overlay;
            if (_built && next == _overlays)
            {
                return;
            }

            _overlays = next;
            Rebuild();
            Changed?.Invoke();
        }

        public bool GetOverlay(CityOverlay overlay)
        {
            return (_overlays & overlay) != 0;
        }

        [ContextMenu("Layer/Access")]
        void MenuAccess() => SetLayer(CityLayer.Access);

        [ContextMenu("Layer/Flags")]
        void MenuFlags() => SetLayer(CityLayer.Flags);

        [ContextMenu("Layer/BuildingId")]
        void MenuBuildingId() => SetLayer(CityLayer.BuildingId);

        [ContextMenu("Overlay/Toggle Rails")]
        void MenuRails() => SetOverlay(CityOverlay.RailOnRoad, !GetOverlay(CityOverlay.RailOnRoad));

        void Rebuild()
        {
            if (_bake == null)
            {
                return;
            }

            CityGrid grid = _bake.Grid;
            int w = grid.w;
            int h = grid.h;
            int count = w * h;
            if (_pixels == null || _pixels.Length != count)
            {
                _pixels = new uint[count];
            }

            if (_texture == null || _texture.width != w || _texture.height != h)
            {
                if (_texture != null)
                {
                    Destroy(_texture);
                }

                // linear: false — sRGB texture so palette hex matches the Game view (ADR-004 §3).
                _texture = new Texture2D(w, h, TextureFormat.RGBA32, false, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "CityGround"
                };
            }

            LayerComposer.Compose(
                w, h,
                _bake.GetFlagsArray(),
                _bake.GetBuildingIdArray(),
                _bake.GetAccessArray(),
                _layer,
                _overlays,
                _pixels);
            _texture.SetPixelData(_pixels, 0);
            _texture.Apply(false, false);
            EnsureQuad(grid);
            if (_runtime == null && material != null)
            {
                _runtime = new Material(material);
                GetComponent<MeshRenderer>().sharedMaterial = _runtime;
            }

            if (_runtime != null)
            {
                _runtime.SetTexture("_BaseMap", _texture);
            }

            _built = true;
        }

        void EnsureQuad(CityGrid grid)
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter.sharedMesh != null && filter.sharedMesh.name == "CityGroundQuad")
            {
                return;
            }

            float x0 = (float)grid.origin.x;
            float z0 = (float)grid.origin.z;
            float x1 = x0 + grid.w * grid.cell;
            float z1 = z0 + grid.h * grid.cell;
            var mesh = new Mesh { name = "CityGroundQuad" };
            mesh.vertices = new[]
            {
                new Vector3(x0, 0f, z0),
                new Vector3(x1, 0f, z0),
                new Vector3(x1, 0f, z1),
                new Vector3(x0, 0f, z1)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 3, 1, 1, 3, 2 };
            mesh.RecalculateBounds();
            Bounds bounds = mesh.bounds;
            bounds.Expand(new Vector3(0f, 1f, 0f));
            mesh.bounds = bounds;
            filter.sharedMesh = mesh;
        }
    }
}

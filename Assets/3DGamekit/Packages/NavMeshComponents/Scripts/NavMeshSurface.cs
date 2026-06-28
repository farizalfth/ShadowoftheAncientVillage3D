using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace UnityEngine.AI
{
    public enum CollectObjects
    {
        All = 0,
        Volume = 1,
        Children = 2,
    }

    [ExecuteAlways]
    [DefaultExecutionOrder(-102)]
    [AddComponentMenu("Navigation/NavMeshSurface", 30)]
    [HelpURL("https://github.com/Unity-Technologies/NavMeshComponents#documentation-draft")]
    public class NavMeshSurface : MonoBehaviour
    {
        [SerializeField] int m_AgentTypeID;
        public int agentTypeID { get { return m_AgentTypeID; } set { m_AgentTypeID = value; } }

        [SerializeField] CollectObjects m_CollectObjects = CollectObjects.All;
        public CollectObjects collectObjects { get { return m_CollectObjects; } set { m_CollectObjects = value; } }

        [SerializeField] Vector3 m_Size = new Vector3(10.0f, 10.0f, 10.0f);
        public Vector3 size { get { return m_Size; } set { m_Size = value; } }

        [SerializeField] Vector3 m_Center = new Vector3(0, 2.0f, 0);
        public Vector3 center { get { return m_Center; } set { m_Center = value; } }

        [SerializeField] LayerMask m_LayerMask = ~0;
        public LayerMask layerMask { get { return m_LayerMask; } set { m_LayerMask = value; } }

        [SerializeField] NavMeshCollectGeometry m_UseGeometry = NavMeshCollectGeometry.RenderMeshes;
        public NavMeshCollectGeometry useGeometry { get { return m_UseGeometry; } set { m_UseGeometry = value; } }

        [SerializeField] int m_DefaultArea;
        public int defaultArea { get { return m_DefaultArea; } set { m_DefaultArea = value; } }

        [SerializeField] bool m_IgnoreNavMeshAgent = true;
        public bool ignoreNavMeshAgent { get { return m_IgnoreNavMeshAgent; } set { m_IgnoreNavMeshAgent = value; } }

        [SerializeField] bool m_IgnoreNavMeshObstacle = true;
        public bool ignoreNavMeshObstacle { get { return m_IgnoreNavMeshObstacle; } set { m_IgnoreNavMeshObstacle = value; } }

        [SerializeField] bool m_OverrideTileSize;
        public bool overrideTileSize { get { return m_OverrideTileSize; } set { m_OverrideTileSize = value; } }
        [SerializeField] int m_TileSize = 256;
        public int tileSize { get { return m_TileSize; } set { m_TileSize = value; } }
        [SerializeField] bool m_OverrideVoxelSize;
        public bool overrideVoxelSize { get { return m_OverrideVoxelSize; } set { m_OverrideVoxelSize = value; } }
        [SerializeField] float m_VoxelSize;
        public float voxelSize { get { return m_VoxelSize; } set { m_VoxelSize = value; } }

        [SerializeField] bool m_BuildHeightMesh;
        public bool buildHeightMesh { get { return m_BuildHeightMesh; } set { m_BuildHeightMesh = value; } }

        [UnityEngine.Serialization.FormerlySerializedAs("m_BakedNavMeshData")]
        [SerializeField] NavMeshData m_NavMeshData;
        public NavMeshData navMeshData { get { return m_NavMeshData; } set { m_NavMeshData = value; } }

        NavMeshDataInstance m_NavMeshDataInstance;
        Vector3 m_LastPosition = Vector3.zero;
        Quaternion m_LastRotation = Quaternion.identity;

        static readonly List<NavMeshSurface> s_NavMeshSurfaces = new List<NavMeshSurface>();
        public static List<NavMeshSurface> activeSurfaces => s_NavMeshSurfaces;

        void OnEnable()
        {
            Register(this);
            AddData();
        }

        void OnDisable()
        {
            RemoveData();
            Unregister(this);
        }

        public void AddData()
        {
#if UNITY_EDITOR
            if (EditorSceneManager.IsPreviewSceneObject(this) || EditorUtility.IsPersistent(this)) return;
#endif
            if (m_NavMeshDataInstance.valid) return;

            if (m_NavMeshData != null)
            {
                m_NavMeshDataInstance = NavMesh.AddNavMeshData(m_NavMeshData, transform.position, transform.rotation);
                m_NavMeshDataInstance.owner = this;
            }

            m_LastPosition = transform.position;
            m_LastRotation = transform.rotation;
        }

        public void RemoveData()
        {
            m_NavMeshDataInstance.Remove();
            m_NavMeshDataInstance = new NavMeshDataInstance();
        }

        public NavMeshBuildSettings GetBuildSettings()
        {
            var buildSettings = NavMesh.GetSettingsByID(m_AgentTypeID);
            if (buildSettings.agentTypeID == -1)
            {
                Debug.LogWarning("No build settings for agent type ID " + agentTypeID, this);
                buildSettings.agentTypeID = m_AgentTypeID;
            }

            if (overrideTileSize) { buildSettings.overrideTileSize = true; buildSettings.tileSize = tileSize; }
            if (overrideVoxelSize) { buildSettings.overrideVoxelSize = true; buildSettings.voxelSize = voxelSize; }
            return buildSettings;
        }

        public void BuildNavMesh()
        {
            var sources = CollectSources();
            var sourcesBounds = new Bounds(m_Center, Abs(m_Size));
            if (m_CollectObjects == CollectObjects.All || m_CollectObjects == CollectObjects.Children)
                sourcesBounds = CalculateWorldBounds(sources);

            var data = NavMeshBuilder.BuildNavMeshData(GetBuildSettings(), sources, sourcesBounds, transform.position, transform.rotation);

            if (data != null)
            {
                data.name = gameObject.name;
                RemoveData();
                m_NavMeshData = data;
                if (isActiveAndEnabled) AddData();
            }
        }

        public AsyncOperation UpdateNavMesh(NavMeshData data)
        {
            var sources = CollectSources();
            var sourcesBounds = new Bounds(m_Center, Abs(m_Size));
            if (m_CollectObjects == CollectObjects.All || m_CollectObjects == CollectObjects.Children)
                sourcesBounds = CalculateWorldBounds(sources);

            return NavMeshBuilder.UpdateNavMeshDataAsync(data, GetBuildSettings(), sources, sourcesBounds);
        }

        static void Register(NavMeshSurface surface)
        {
#if UNITY_EDITOR
            if (EditorSceneManager.IsPreviewSceneObject(surface) || EditorUtility.IsPersistent(surface)) return;
#endif
            if (s_NavMeshSurfaces.Count == 0) NavMesh.onPreUpdate += UpdateActive;
            if (!s_NavMeshSurfaces.Contains(surface)) s_NavMeshSurfaces.Add(surface);
        }

        static void Unregister(NavMeshSurface surface)
        {
            s_NavMeshSurfaces.Remove(surface);
            if (s_NavMeshSurfaces.Count == 0) NavMesh.onPreUpdate -= UpdateActive;
        }

        static void UpdateActive()
        {
            for (var i = 0; i < s_NavMeshSurfaces.Count; ++i)
                s_NavMeshSurfaces[i].UpdateDataIfTransformChanged();
        }

        void AppendModifierVolumes(ref List<NavMeshBuildSource> sources)
        {
            List<NavMeshModifierVolume> modifiers;
            if (m_CollectObjects == CollectObjects.Children)
            {
                modifiers = new List<NavMeshModifierVolume>(GetComponentsInChildren<NavMeshModifierVolume>());
                modifiers.RemoveAll(x => !x.isActiveAndEnabled);
            }
            else
            {
                modifiers = NavMeshModifierVolume.activeModifiers;
            }

            foreach (var m in modifiers)
            {
                if ((m_LayerMask & (1 << m.gameObject.layer)) == 0 || !m.AffectsAgentType(m_AgentTypeID)) continue;
                
                var mcenter = m.transform.TransformPoint(m.center);
                var scale = m.transform.lossyScale;
                var msize = new Vector3(m.size.x * Mathf.Abs(scale.x), m.size.y * Mathf.Abs(scale.y), m.size.z * Mathf.Abs(scale.z));

                var src = new NavMeshBuildSource();
                src.shape = NavMeshBuildSourceShape.ModifierBox;
                src.transform = Matrix4x4.TRS(mcenter, m.transform.rotation, Vector3.one);
                src.size = msize;
                src.area = m.area;
                sources.Add(src);
            }
        }

        List<NavMeshBuildSource> CollectSources()
        {
            var sources = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>();

            List<NavMeshModifier> modifiers = (m_CollectObjects == CollectObjects.Children) 
                ? new List<NavMeshModifier>(GetComponentsInChildren<NavMeshModifier>()) 
                : NavMeshModifier.activeModifiers;

            if (m_CollectObjects == CollectObjects.Children) modifiers.RemoveAll(x => !x.isActiveAndEnabled);

            foreach (var m in modifiers)
            {
                if ((m_LayerMask & (1 << m.gameObject.layer)) == 0 || !m.AffectsAgentType(m_AgentTypeID)) continue;
                markups.Add(new NavMeshBuildMarkup { root = m.transform, overrideArea = m.overrideArea, area = m.area, ignoreFromBuild = m.ignoreFromBuild });
            }

            // PERBAIKAN: Menggunakan NavMeshBuilder standar (UnityEngine.AI) yang bekerja di semua versi Unity
            if (m_CollectObjects == CollectObjects.All)
            {
                // Passing null sebagai root akan mengumpulkan semua objek di scene secara otomatis
                NavMeshBuilder.CollectSources(null, m_LayerMask, m_UseGeometry, m_DefaultArea, markups, sources);
            }
            else if (m_CollectObjects == CollectObjects.Children)
            {
                NavMeshBuilder.CollectSources(transform, m_LayerMask, m_UseGeometry, m_DefaultArea, markups, sources);
            }
            else if (m_CollectObjects == CollectObjects.Volume)
            {
                Matrix4x4 localToWorld = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                var worldBounds = GetWorldBounds(localToWorld, new Bounds(m_Center, m_Size));
                NavMeshBuilder.CollectSources(worldBounds, m_LayerMask, m_UseGeometry, m_DefaultArea, markups, sources);
            }

            if (m_IgnoreNavMeshAgent) sources.RemoveAll((x) => (x.component != null && x.component.gameObject.GetComponent<NavMeshAgent>() != null));
            if (m_IgnoreNavMeshObstacle) sources.RemoveAll((x) => (x.component != null && x.component.gameObject.GetComponent<NavMeshObstacle>() != null));

            AppendModifierVolumes(ref sources);
            return sources;
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        static Bounds GetWorldBounds(Matrix4x4 mat, Bounds bounds)
        {
            var absAxisX = Abs(mat.MultiplyVector(Vector3.right));
            var absAxisY = Abs(mat.MultiplyVector(Vector3.up));
            var absAxisZ = Abs(mat.MultiplyVector(Vector3.forward));
            var worldPosition = mat.MultiplyPoint(bounds.center);
            var worldSize = absAxisX * bounds.size.x + absAxisY * bounds.size.y + absAxisZ * bounds.size.z;
            return new Bounds(worldPosition, worldSize);
        }

        Bounds CalculateWorldBounds(List<NavMeshBuildSource> sources)
        {
            Matrix4x4 worldToLocal = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one).inverse;
            var result = new Bounds();
            foreach (var src in sources)
            {
                switch (src.shape)
                {
                    case NavMeshBuildSourceShape.Mesh: result.Encapsulate(GetWorldBounds(worldToLocal * src.transform, ((Mesh)src.sourceObject).bounds)); break;
                    case NavMeshBuildSourceShape.Terrain: 
                        var t = (TerrainData)src.sourceObject;
                        result.Encapsulate(GetWorldBounds(worldToLocal * src.transform, new Bounds(0.5f * t.size, t.size))); break;
                    default: result.Encapsulate(GetWorldBounds(worldToLocal * src.transform, new Bounds(Vector3.zero, src.size))); break;
                }
            }
            result.Expand(0.1f);
            return result;
        }

        void UpdateDataIfTransformChanged() { if (m_LastPosition != transform.position || m_LastRotation != transform.rotation) { RemoveData(); AddData(); } }

#if UNITY_EDITOR
        void OnValidate()
        {
            var settings = NavMesh.GetSettingsByID(m_AgentTypeID);
            if (settings.agentTypeID != -1)
            {
                if (!m_OverrideVoxelSize) m_VoxelSize = Mathf.Max(0.01f, settings.agentRadius / 3.0f);
                if (!m_OverrideTileSize) m_TileSize = 256;
            }
        }
#endif
    }
}
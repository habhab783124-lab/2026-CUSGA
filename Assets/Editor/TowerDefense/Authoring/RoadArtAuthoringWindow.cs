using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TowerDefense.Editor
{
    /// <summary>
    /// Builds a dedicated decorative road-art layer on top of authored gameplay paths.
    ///
    /// Why this tool exists:
    /// 1. The existing path-segment generator is intentionally gameplay-first.
    /// 2. Later maps now need a separate visual-paving pass instead of reusing blocker strips as
    ///    final art forever.
    /// 3. The author needs to iterate on road appearance without risking route colliders and
    ///    placement blockers.
    ///
    /// The generated layer is therefore decorative only:
    /// - straight strips
    /// - optional corner markers
    /// - optional start/end caps
    ///
    /// It does not touch gameplay collisions, gates, or wave wiring.
    /// </summary>
    public sealed class RoadArtAuthoringWindow : EditorWindow
    {
        private const string DefaultRoadArtRootName = "RoadArt";
        private const string DefaultRoadArtPrefix = "RoadArt";

        [SerializeField] private EnemyPath targetPath;
        [SerializeField] private Transform roadArtRootOverride;
        [SerializeField] private GameObject straightRoadArtTemplate;
        [SerializeField] private GameObject cornerRoadArtTemplate;
        [SerializeField] private GameObject endCapRoadArtTemplate;
        [SerializeField] private bool clearExistingRoadArt = true;
        [SerializeField] private bool groupRoadArtByPath = true;
        [SerializeField] private bool createCornerMarkers = true;
        [SerializeField] private bool createEndCaps = true;
        [SerializeField] private float defaultRoadArtThickness = 1.8f;
        [SerializeField] private string roadArtRootName = "RoadArt";
        [SerializeField] private string roadArtSegmentPrefix = "RoadArt";

        [MenuItem("Tools/Tower Defense/Authoring/道路美术铺设工具")]
        public static void OpenWindow()
        {
            RoadArtAuthoringWindow window = GetWindow<RoadArtAuthoringWindow>("道路美术");
            window.minSize = new Vector2(520f, 360f);
            window.Show();
        }

        /// <summary>
        /// Batch entry used by automated level-polish passes.
        /// </summary>
        public static void GenerateLevel03AndLevel04RoadArtBatch()
        {
            GenerateRoadArtForScene(TowerDefenseMapToolkitUtility.Level03ScenePath);
            GenerateRoadArtForScene(TowerDefenseMapToolkitUtility.Level04ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("道路美术铺设工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "在正式路径上铺设独立的道路美术层。这个工具只生成视觉对象，不会改动功能性 PathSegment、碰撞或禁建逻辑。",
                MessageType.Info);

            targetPath = (EnemyPath)EditorGUILayout.ObjectField("目标 EnemyPath", targetPath, typeof(EnemyPath), true);
            roadArtRootOverride = (Transform)EditorGUILayout.ObjectField("道路美术根覆盖", roadArtRootOverride, typeof(Transform), true);
            straightRoadArtTemplate = (GameObject)EditorGUILayout.ObjectField("直线路面模板", straightRoadArtTemplate, typeof(GameObject), false);
            cornerRoadArtTemplate = (GameObject)EditorGUILayout.ObjectField("拐角模板", cornerRoadArtTemplate, typeof(GameObject), false);
            endCapRoadArtTemplate = (GameObject)EditorGUILayout.ObjectField("端点模板", endCapRoadArtTemplate, typeof(GameObject), false);
            clearExistingRoadArt = EditorGUILayout.Toggle("生成前清空旧美术层", clearExistingRoadArt);
            groupRoadArtByPath = EditorGUILayout.Toggle("按路径分组", groupRoadArtByPath);
            createCornerMarkers = EditorGUILayout.Toggle("生成拐角标记", createCornerMarkers);
            createEndCaps = EditorGUILayout.Toggle("生成起终点盖板", createEndCaps);
            defaultRoadArtThickness = EditorGUILayout.FloatField("默认路面厚度", defaultRoadArtThickness);
            roadArtRootName = EditorGUILayout.TextField("道路美术根名称", roadArtRootName);
            roadArtSegmentPrefix = EditorGUILayout.TextField("对象名前缀", roadArtSegmentPrefix);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(targetPath == null))
                {
                    if (GUILayout.Button("为当前路径生成"))
                    {
                        GenerateForPath(targetPath);
                    }
                }

                if (GUILayout.Button("为当前场景全部路径生成"))
                {
                    GenerateForAllPathsInActiveScene();
                }
            }

            if (GUILayout.Button("清空当前场景已生成道路美术"))
            {
                ClearGeneratedRoadArtInActiveScene();
            }
        }

        private void GenerateForAllPathsInActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            List<EnemyPath> allPaths = CollectAuthorablePaths(activeScene);

            for (int index = 0; index < allPaths.Count; index++)
            {
                GenerateForPath(allPaths[index]);
            }
        }

        private void GenerateForPath(EnemyPath enemyPath)
        {
            if (enemyPath == null)
            {
                return;
            }

            Scene scene = enemyPath.gameObject.scene;
            List<Transform> waypoints = EnemyPathAuthoringUtility.GetWaypointChildren(enemyPath);
            if (waypoints.Count < 2)
            {
                return;
            }

            List<PathSurfaceSegment> referenceRoadSegments = TowerDefenseMapToolkitUtility.CollectPathSurfaceSegments(scene);
            float roadThickness = TowerDefenseMapToolkitUtility.EstimatePreferredRoadThickness(referenceRoadSegments, defaultRoadArtThickness);
            Transform roadArtRoot = ResolveRoadArtRoot(scene);
            Transform parent = roadArtRoot;
            if (groupRoadArtByPath)
            {
                parent = TowerDefenseMapToolkitUtility.EnsureGeneratedRoadGroupRoot(roadArtRoot, $"{roadArtSegmentPrefix}_{enemyPath.name}");
            }

            if (clearExistingRoadArt)
            {
                ClearGeneratedChildren(parent);
            }

            int stripIndex = 1;
            int cornerIndex = 1;

            for (int waypointIndex = 0; waypointIndex < waypoints.Count - 1; waypointIndex++)
            {
                Transform start = waypoints[waypointIndex];
                Transform end = waypoints[waypointIndex + 1];
                if (start == null || end == null)
                {
                    continue;
                }

                CreateStraightStrip(parent, enemyPath.name, stripIndex, start.position, end.position, roadThickness);
                stripIndex++;

                if (!createCornerMarkers || waypointIndex <= 0 || waypointIndex >= waypoints.Count - 2)
                {
                    continue;
                }

                Vector3 previousDelta = start.position - waypoints[waypointIndex - 1].position;
                Vector3 nextDelta = waypoints[waypointIndex + 1].position - start.position;
                if (Mathf.Abs(Vector3.Angle(previousDelta, nextDelta)) < 1f)
                {
                    continue;
                }

                CreateCornerMarker(parent, enemyPath.name, cornerIndex, start.position, roadThickness);
                cornerIndex++;
            }

            if (createEndCaps)
            {
                CreateEndCap(parent, enemyPath.name, "Start", waypoints[0].position, waypoints[1].position, roadThickness);
                CreateEndCap(parent, enemyPath.name, "End", waypoints[waypoints.Count - 1].position, waypoints[waypoints.Count - 2].position, roadThickness);
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>
        /// Rebuilds decorative road art for one scene path without requiring an editor window
        /// instance. This is what lets batch tooling polish authored levels end-to-end.
        /// </summary>
        internal static void GenerateRoadArtForScene(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Transform roadArtRoot = TowerDefenseMapToolkitUtility.EnsureNamedRoot(scene, DefaultRoadArtRootName);
            ClearGeneratedChildren(roadArtRoot);

            List<EnemyPath> paths = CollectAuthorablePaths(scene);
            List<PathSurfaceSegment> referenceRoadSegments = TowerDefenseMapToolkitUtility.CollectPathSurfaceSegments(scene);
            float thickness = TowerDefenseMapToolkitUtility.EstimatePreferredRoadThickness(referenceRoadSegments, 1.8f);

            for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
            {
                EnemyPath enemyPath = paths[pathIndex];
                List<Transform> waypoints = EnemyPathAuthoringUtility.GetWaypointChildren(enemyPath);
                if (waypoints.Count < 2)
                {
                    continue;
                }

                Transform groupRoot = TowerDefenseMapToolkitUtility.EnsureGeneratedRoadGroupRoot(roadArtRoot, $"{DefaultRoadArtPrefix}_{enemyPath.name}");
                ClearGeneratedChildren(groupRoot);

                for (int waypointIndex = 0; waypointIndex < waypoints.Count - 1; waypointIndex++)
                {
                    Transform start = waypoints[waypointIndex];
                    Transform end = waypoints[waypointIndex + 1];
                    if (start == null || end == null)
                    {
                        continue;
                    }

                    GameObject strip = BuildFallbackDecorativeTemplate(scene);
                    Undo.RegisterCreatedObjectUndo(strip, "Generate Road Art");
                    strip.name = $"{DefaultRoadArtPrefix}_{enemyPath.name}_Strip_{waypointIndex + 1:D2}";
                    strip.transform.SetParent(groupRoot, false);

                    bool horizontal = Mathf.Abs(start.position.y - end.position.y) <= 0.001f;
                    float length = horizontal ? Mathf.Abs(end.position.x - start.position.x) : Mathf.Abs(end.position.y - start.position.y);
                    strip.transform.position = horizontal
                        ? new Vector3((start.position.x + end.position.x) * 0.5f, start.position.y, 0f)
                        : new Vector3(start.position.x, (start.position.y + end.position.y) * 0.5f, 0f);
                    strip.transform.rotation = Quaternion.identity;
                    strip.transform.localScale = horizontal
                        ? new Vector3(length + thickness, thickness, 1f)
                        : new Vector3(thickness, length + thickness, 1f);
                }
            }

            EditorSceneManager.SaveScene(scene);
        }

        private Transform ResolveRoadArtRoot(Scene scene)
        {
            if (roadArtRootOverride != null)
            {
                return roadArtRootOverride;
            }

            string safeRootName = string.IsNullOrWhiteSpace(roadArtRootName) ? "RoadArt" : roadArtRootName;
            return TowerDefenseMapToolkitUtility.EnsureNamedRoot(scene, safeRootName);
        }

        private void CreateStraightStrip(Transform parent, string pathName, int stripIndex, Vector3 start, Vector3 end, float thickness)
        {
            GameObject stripObject = InstantiateDecorativeObject(
                straightRoadArtTemplate,
                parent,
                $"{roadArtSegmentPrefix}_{pathName}_Strip_{stripIndex:D2}");

            bool horizontal = Mathf.Abs(start.y - end.y) <= 0.001f;
            float length = horizontal ? Mathf.Abs(end.x - start.x) : Mathf.Abs(end.y - start.y);
            stripObject.transform.position = horizontal
                ? new Vector3((start.x + end.x) * 0.5f, start.y, 0f)
                : new Vector3(start.x, (start.y + end.y) * 0.5f, 0f);
            stripObject.transform.rotation = Quaternion.identity;
            stripObject.transform.localScale = horizontal
                ? new Vector3(length + thickness, thickness, 1f)
                : new Vector3(thickness, length + thickness, 1f);
        }

        private void CreateCornerMarker(Transform parent, string pathName, int cornerIndex, Vector3 position, float thickness)
        {
            GameObject cornerObject = InstantiateDecorativeObject(
                cornerRoadArtTemplate,
                parent,
                $"{roadArtSegmentPrefix}_{pathName}_Corner_{cornerIndex:D2}");
            cornerObject.transform.position = position;
            cornerObject.transform.rotation = Quaternion.identity;
            cornerObject.transform.localScale = new Vector3(thickness, thickness, 1f);
        }

        private void CreateEndCap(Transform parent, string pathName, string capLabel, Vector3 position, Vector3 lookBackPosition, float thickness)
        {
            GameObject capObject = InstantiateDecorativeObject(
                endCapRoadArtTemplate,
                parent,
                $"{roadArtSegmentPrefix}_{pathName}_{capLabel}");
            Vector3 forward = (position - lookBackPosition).sqrMagnitude > 0.0001f
                ? (position - lookBackPosition).normalized
                : Vector3.right;
            capObject.transform.position = position;
            capObject.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg);
            capObject.transform.localScale = new Vector3(thickness, thickness, 1f);
        }

        private GameObject InstantiateDecorativeObject(GameObject explicitTemplate, Transform parent, string objectName)
        {
            GameObject createdObject;
            if (explicitTemplate != null)
            {
                createdObject = (GameObject)PrefabUtility.InstantiatePrefab(explicitTemplate);
                if (createdObject == null)
                {
                    createdObject = Instantiate(explicitTemplate);
                }
            }
            else
            {
                createdObject = BuildFallbackDecorativeTemplate(parent.gameObject.scene);
            }

            Undo.RegisterCreatedObjectUndo(createdObject, "Generate Road Art");
            createdObject.name = objectName;
            createdObject.transform.SetParent(parent, false);
            StripGameplayOnlyComponents(createdObject);
            return createdObject;
        }

        private static GameObject BuildFallbackDecorativeTemplate(Scene scene)
        {
            GameObject sceneTemplate = TowerDefenseMapToolkitUtility.EnumerateSceneObjects(scene)
                .FirstOrDefault(candidate => candidate.name.StartsWith("PathSegment_", StringComparison.Ordinal));
            if (sceneTemplate != null)
            {
                GameObject cloned = Instantiate(sceneTemplate);
                StripGameplayOnlyComponents(cloned);
                return cloned;
            }

            GameObject fallback = new GameObject("RoadArtTemplate");
            SpriteRenderer renderer = fallback.AddComponent<SpriteRenderer>();
            renderer.color = new Color(1f, 1f, 1f, 0.45f);
            return fallback;
        }

        private void ClearGeneratedRoadArtInActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            Transform root = ResolveRoadArtRoot(activeScene);
            ClearGeneratedChildren(root);
            EditorSceneManager.MarkSceneDirty(activeScene);
        }

        private static void ClearGeneratedChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            List<GameObject> children = parent.Cast<Transform>()
                .Where(child => child != null)
                .Select(child => child.gameObject)
                .ToList();
            for (int index = 0; index < children.Count; index++)
            {
                Undo.DestroyObjectImmediate(children[index]);
            }
        }

        /// <summary>
        /// Removes only components that can accidentally affect gameplay from generated road-art
        /// instances, while keeping presentation components intact.
        ///
        /// This is intentionally more permissive than the first draft of the tool:
        /// when the user starts supplying his own road-art prefabs, those prefabs may legitimately
        /// contain animators, particles, audio, custom visual scripts, and other presentation
        /// helpers. Destroying everything except a tiny whitelist would make the art workflow feel
        /// hostile and force unnecessary prefab duplication.
        ///
        /// The real risk here is not "too many visual components" but "components that start
        /// participating in gameplay". So we only strip colliders, rigidbodies, blockers, and
        /// known map-runtime components.
        /// </summary>
        private static void StripGameplayOnlyComponents(GameObject targetObject)
        {
            Component[] components = targetObject.GetComponents<Component>();
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null ||
                    component is Transform)
                {
                    continue;
                }

                bool shouldStrip =
                    component is Collider ||
                    component is Collider2D ||
                    component is Rigidbody ||
                    component is Rigidbody2D ||
                    component is PlacementBlocker ||
                    component is EnemyPath ||
                    component is EnemySpawnGate ||
                    component is DefensePointFlag ||
                    component is BuildZone ||
                    component is BattlefieldMapDefinition ||
                    component is TowerDefenseGame ||
                    component is WaveSpawner;

                if (shouldStrip)
                {
                    Undo.DestroyObjectImmediate(component);
                }
            }
        }

        private static List<EnemyPath> CollectAuthorablePaths(Scene scene)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<EnemyPath>(true))
                .Where(path => path != null && !path.name.StartsWith("Legacy_", StringComparison.Ordinal))
                .Distinct()
                .OrderBy(path => path.name, StringComparer.Ordinal)
                .ToList();
        }
    }
}

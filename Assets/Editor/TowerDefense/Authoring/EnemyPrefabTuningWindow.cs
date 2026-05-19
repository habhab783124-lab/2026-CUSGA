using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// 这个工作台专门服务“在 Scene 视图里微调怪物 prefab，再把局部改动安全回写到源 prefab”。
    ///
    /// 为什么要单独做一套场景和工具，而不是继续在普通预览场景里直接 Apply All：
    /// 1. 普通预览场景里往往还混着相机、布局、命名、父节点等场景专用 override。
    /// 2. 如果直接对整只怪物实例 Apply All，很容易把这些场景专用信息一起写回 prefab。
    /// 3. 绝大多数真正想回写的内容，其实只是某个子节点的局部 Transform。
    ///
    /// 所以这套工具明确拆成两部分：
    /// - 一个干净的怪物 prefab 调整场景
    /// - 一个“小范围 Apply”按钮，只回写当前选中子对象的局部 Transform
    ///
    /// 这样你后续就能稳定地在 Scene 里边看边调，而不用担心误把整只实例的场景状态写回源 prefab。
    /// </summary>
    public sealed class EnemyPrefabTuningWindow : EditorWindow
    {
        private const string ScenePath = "Assets/Scenes/EnemyPrefabTuning.unity";
        private const string SceneFolder = "Assets/Scenes";
        private const string PreviewRootName = "EnemyPrefabTuningRoot";
        private const string CameraName = "PrefabTuningCamera";

        private static readonly PreviewEntry[] PreviewEntries =
        {
            new PreviewEntry("Assets/Prefabs/TowerDefense/Runtime/Enemies/ScavengerEnemy.prefab", -7.5f, 4.5f),
            new PreviewEntry("Assets/Prefabs/TowerDefense/Runtime/Enemies/WolfEnemy.prefab", -2.5f, 4.5f),
            new PreviewEntry("Assets/Prefabs/TowerDefense/Runtime/Enemies/BannerScavengerEnemy.prefab", 2.5f, 4.5f),
            new PreviewEntry("Assets/Prefabs/TowerDefense/Runtime/Enemies/MechanicEnemy.prefab", 7.5f, 4.5f),
            new PreviewEntry("Assets/Prefabs/TowerDefense/Runtime/Enemies/HeavyArmoredMachineEnemy.prefab", -7.5f, 1.2f),
            new PreviewEntry("Assets/Prefabs/TowerDefense/Runtime/Enemies/StealthStalkerEnemy.prefab", -2.5f, 1.2f),
            new PreviewEntry("Assets/Prefabs/TowerDefense/Runtime/Enemies/AbominationEnemy.prefab", 2.5f, 1.2f),
            new PreviewEntry("Assets/Prefabs/TowerDefense/Runtime/Enemies/SmallScavengerEnemy.prefab", 7.5f, 1.2f)
        };

        [MenuItem("Tools/Tower Defense/Authoring/怪物 Prefab 调整工作台")]
        public static void OpenWindow()
        {
            EnemyPrefabTuningWindow window = GetWindow<EnemyPrefabTuningWindow>();
            window.titleContent = new GUIContent("怪物 Prefab 调整");
            window.minSize = new Vector2(420f, 280f);
            window.Show();
        }

        [MenuItem("Tools/Tower Defense/Authoring/重建怪物 Prefab 调整场景")]
        public static void RebuildTuningSceneMenu()
        {
            RebuildTuningScene();
        }

        [MenuItem("Tools/Tower Defense/Authoring/怪物 Prefab 小范围回写/应用选中对象局部位置")]
        public static void ApplySelectedLocalPositionMenu()
        {
            ApplySelectedTransformOverrides(applyPosition: true, applyRotation: false, applyScale: false);
        }

        [MenuItem("Tools/Tower Defense/Authoring/怪物 Prefab 小范围回写/应用选中对象局部旋转")]
        public static void ApplySelectedLocalRotationMenu()
        {
            ApplySelectedTransformOverrides(applyPosition: false, applyRotation: true, applyScale: false);
        }

        [MenuItem("Tools/Tower Defense/Authoring/怪物 Prefab 小范围回写/应用选中对象局部缩放")]
        public static void ApplySelectedLocalScaleMenu()
        {
            ApplySelectedTransformOverrides(applyPosition: false, applyRotation: false, applyScale: true);
        }

        [MenuItem("Tools/Tower Defense/Authoring/怪物 Prefab 小范围回写/应用选中对象全部局部 Transform")]
        public static void ApplySelectedLocalTransformMenu()
        {
            ApplySelectedTransformOverrides(applyPosition: true, applyRotation: true, applyScale: true);
        }

        private Vector2 _scrollPosition;

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "这个工作台用于“在专用场景里微调怪物 prefab 实例，再把选中子对象的局部 Transform 安全回写到源 prefab”。\n" +
                "推荐操作：先打开/重建调整场景，选中例如 HealthBarRoot、VisualScaleRoot 这样的子节点，再点击下面的小范围 Apply 按钮。",
                MessageType.Info);

            DrawSceneSection();
            EditorGUILayout.Space();
            DrawSelectionSection();

            EditorGUILayout.EndScrollView();
        }

        private static void DrawHeader(string title)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private void DrawSceneSection()
        {
            DrawHeader("调整场景");
            EditorGUILayout.LabelField("场景路径", ScenePath);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开/定位调整场景"))
                {
                    OpenOrCreateScene();
                }

                if (GUILayout.Button("重建调整场景"))
                {
                    RebuildTuningScene();
                }
            }

            EditorGUILayout.HelpBox(
                "重建场景会重新生成一份干净的怪物 prefab 调整布局。\n" +
                "这一步适合在预览场景被改乱、或者你想重新开始一轮微调时使用。",
                MessageType.None);
        }

        private void DrawSelectionSection()
        {
            DrawHeader("小范围回写");

            if (!TryBuildSelectionContext(out SelectionContext context, out string validationMessage))
            {
                EditorGUILayout.HelpBox(validationMessage, MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox(
                $"当前选中：{context.SelectedPath}\n" +
                $"源 Prefab：{context.PrefabAssetPath}\n" +
                $"相对路径：{context.RelativePath}",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("应用局部位置"))
                {
                    ApplySelectedTransformOverrides(applyPosition: true, applyRotation: false, applyScale: false);
                }

                if (GUILayout.Button("应用局部旋转"))
                {
                    ApplySelectedTransformOverrides(applyPosition: false, applyRotation: true, applyScale: false);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("应用局部缩放"))
                {
                    ApplySelectedTransformOverrides(applyPosition: false, applyRotation: false, applyScale: true);
                }

                if (GUILayout.Button("应用全部局部 Transform"))
                {
                    ApplySelectedTransformOverrides(applyPosition: true, applyRotation: true, applyScale: true);
                }
            }

            EditorGUILayout.HelpBox(
                "这里故意不提供 Apply All。\n" +
                "因为我们只想把你真正修改的子节点局部 Transform 回写到源 prefab，" +
                "而不是把场景布局、实例激活状态、父节点关系等场景专用 override 一起带回去。",
                MessageType.None);
        }

        private static void OpenOrCreateScene()
        {
            if (!File.Exists(ScenePath))
            {
                RebuildTuningScene();
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            FocusPreviewRootIfPresent();
        }

        private static void RebuildTuningScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureFolder(SceneFolder);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();

            GameObject previewRootObject = new GameObject(PreviewRootName);
            Transform previewRoot = previewRootObject.transform;

            for (int index = 0; index < PreviewEntries.Length; index++)
            {
                PreviewEntry entry = PreviewEntries[index];
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(entry.PrefabPath);
                if (prefabAsset == null)
                {
                    throw new InvalidOperationException($"Missing prefab asset: {entry.PrefabPath}");
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
                if (instance == null)
                {
                    throw new InvalidOperationException($"Failed to instantiate prefab: {entry.PrefabPath}");
                }

                instance.transform.SetParent(previewRoot, false);
                instance.transform.position = new Vector3(entry.WorldX, entry.WorldY, 0f);
                instance.SetActive(true);
            }

            Selection.activeGameObject = previewRootObject;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new GameObject(CameraName);
            cameraObject.transform.position = new Vector3(0f, 2.8f, -10f);

            Camera cameraComponent = cameraObject.AddComponent<Camera>();
            cameraComponent.orthographic = true;
            cameraComponent.orthographicSize = 6f;
            cameraComponent.clearFlags = CameraClearFlags.SolidColor;
            cameraComponent.backgroundColor = new Color(0.12f, 0.12f, 0.13f, 1f);

            cameraObject.AddComponent<AudioListener>();
        }

        private static void ApplySelectedTransformOverrides(bool applyPosition, bool applyRotation, bool applyScale)
        {
            if (!TryBuildSelectionContext(out SelectionContext context, out string validationMessage))
            {
                EditorUtility.DisplayDialog("怪物 Prefab 小范围回写", validationMessage, "知道了");
                return;
            }

            SerializedObject serializedTransform = new SerializedObject(context.SelectedTransform);

            if (applyPosition)
            {
                ApplyProperty(serializedTransform.FindProperty("m_LocalPosition"), context.PrefabAssetPath, "局部位置");
            }

            if (applyRotation)
            {
                ApplyProperty(serializedTransform.FindProperty("m_LocalRotation"), context.PrefabAssetPath, "局部旋转");

                // `m_LocalEulerAnglesHint` 只是 Inspector 显示辅助值，但一起应用后，
                // 你下次再选中 prefab 时看到的角度会更接近自己刚刚在场景里调的值。
                SerializedProperty eulerHint = serializedTransform.FindProperty("m_LocalEulerAnglesHint");
                if (eulerHint != null)
                {
                    ApplyProperty(eulerHint, context.PrefabAssetPath, "局部旋转提示值");
                }
            }

            if (applyScale)
            {
                ApplyProperty(serializedTransform.FindProperty("m_LocalScale"), context.PrefabAssetPath, "局部缩放");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "怪物 Prefab 小范围回写",
                $"已把 {context.RelativePath} 的所选 Transform 改动回写到：\n{context.PrefabAssetPath}",
                "好的");
        }

        private static void ApplyProperty(SerializedProperty property, string prefabAssetPath, string propertyLabel)
        {
            if (property == null)
            {
                throw new InvalidOperationException($"无法找到需要回写的属性：{propertyLabel}");
            }

            PrefabUtility.ApplyPropertyOverride(property, prefabAssetPath, InteractionMode.UserAction);
        }

        private static bool TryBuildSelectionContext(out SelectionContext context, out string validationMessage)
        {
            context = default;

            Transform selectedTransform = Selection.activeTransform;
            if (selectedTransform == null)
            {
                validationMessage = "请先在场景里选中一个怪物 prefab 实例的子节点，例如 `HealthBarRoot` 或 `VisualScaleRoot`。";
                return false;
            }

            GameObject prefabInstanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(selectedTransform.gameObject);
            if (prefabInstanceRoot == null)
            {
                validationMessage = "当前选中的对象不在 prefab 实例里，无法回写到源 prefab。";
                return false;
            }

            if (selectedTransform == prefabInstanceRoot.transform)
            {
                validationMessage =
                    "当前选中的是 prefab 实例根节点。\n" +
                    "根节点位置通常代表“场景布局”，不是 prefab 内部结构。\n" +
                    "请改选你真正想回写的子节点，例如 `HealthBarRoot`、`VisualScaleRoot`。";
                return false;
            }

            string prefabAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabInstanceRoot);
            if (string.IsNullOrWhiteSpace(prefabAssetPath))
            {
                validationMessage = "无法解析当前实例对应的 prefab 资产路径。";
                return false;
            }

            string relativePath = AnimationUtility.CalculateTransformPath(selectedTransform, prefabInstanceRoot.transform);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                validationMessage = "无法计算选中子节点相对于 prefab 根的路径。";
                return false;
            }

            context = new SelectionContext(
                selectedTransform,
                prefabAssetPath,
                relativePath,
                GetHierarchyPath(selectedTransform));

            validationMessage = string.Empty;
            return true;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = $"{current.name}/{path}";
                current = current.parent;
            }

            return path;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parentPath = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folderName = Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parentPath) && !AssetDatabase.IsValidFolder(parentPath))
            {
                EnsureFolder(parentPath);
            }

            AssetDatabase.CreateFolder(parentPath, folderName);
        }

        private static void FocusPreviewRootIfPresent()
        {
            GameObject previewRoot = GameObject.Find(PreviewRootName);
            if (previewRoot != null)
            {
                Selection.activeGameObject = previewRoot;
            }
        }

        private readonly struct PreviewEntry
        {
            public PreviewEntry(string prefabPath, float worldX, float worldY)
            {
                PrefabPath = prefabPath;
                WorldX = worldX;
                WorldY = worldY;
            }

            public string PrefabPath { get; }
            public float WorldX { get; }
            public float WorldY { get; }
        }

        private readonly struct SelectionContext
        {
            public SelectionContext(Transform selectedTransform, string prefabAssetPath, string relativePath, string selectedPath)
            {
                SelectedTransform = selectedTransform;
                PrefabAssetPath = prefabAssetPath;
                RelativePath = relativePath;
                SelectedPath = selectedPath;
            }

            public Transform SelectedTransform { get; }
            public string PrefabAssetPath { get; }
            public string RelativePath { get; }
            public string SelectedPath { get; }
        }
    }
}

using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// 塔 Prefab 视觉调整工作台。
    ///
    /// 参考 EnemyPrefabTuningWindow 的设计：
    /// - 一个干净的专用调整场景，陈列三种战斗塔和继电器
    /// - 在 Scene 视图中直接拖拽调整子节点的局部 Transform
    /// - "小范围回写"按钮只把选中子节点的局部 Transform 回写到源 prefab
    ///
    /// 塔的层级比怪物更复杂（FeedbackRoot / TypeSignatureRoot / LevelMarkerRoot），
    /// 所以这个独立工作台让作者能在统一的场景里对比三种塔的外观差异，
    /// 并在调完后精确回写。
    /// </summary>
    public sealed class TowerPrefabTuningWindow : EditorWindow
    {
        private const string ScenePath = "Assets/Scenes/TowerPrefabTuning.unity";
        private const string SceneFolder = "Assets/Scenes";
        private const string PreviewRootName = "TowerPrefabTuningRoot";
        private const string CameraName = "PrefabTuningCamera";

        private static readonly PreviewEntry[] PreviewEntries =
        {
            new PreviewEntry("Assets/Prefabs/TowerDefense/Runtime/SingleTargetTowerPrototype.prefab", -6f, 0f),
            new PreviewEntry("Assets/Prefabs/TowerDefense/Runtime/SlowFieldTowerPrototype.prefab",    -2f, 0f),
            new PreviewEntry("Assets/Prefabs/TowerDefense/Runtime/BombardTowerPrototype.prefab",       2f, 0f),
            new PreviewEntry("Assets/Prefabs/TowerDefense/Runtime/RelayTowerPrototype.prefab",         6f, 0f)
        };

        [MenuItem("Tools/Tower Defense/Authoring/塔 Prefab 调整工作台")]
        public static void OpenWindow()
        {
            TowerPrefabTuningWindow window = GetWindow<TowerPrefabTuningWindow>();
            window.titleContent = new GUIContent("塔 Prefab 调整");
            window.minSize = new Vector2(420f, 280f);
            window.Show();
        }

        [MenuItem("Tools/Tower Defense/Authoring/重建塔 Prefab 调整场景")]
        public static void RebuildTuningSceneMenu()
        {
            RebuildTuningScene();
        }

        private Vector2 _scrollPosition;

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "这个工作台用于在专用场景里微调三种战斗塔和继电器的 prefab 外观。\n" +
                "推荐操作：先打开/重建调整场景，选中例如 FeedbackRoot、TypeSignatureRoot、LevelMarkerRoot 这样的子节点，" +
                "在 Scene 视图中拖动调整，再点击下面的小范围 Apply 按钮回写到源 prefab。",
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
                "四种塔在场景中排成一行，可以在 Scene 视图里同时对比它们的视觉差异。\n" +
                "例如：比较三种战斗塔的 TypeSignatureRoot 大小、调整 LevelMarkerRoot 的位置、微调 Relay 的 bodyRenderer。" +
                "重建场景会丢弃所有临时的场景内修改，生成一份干净的布局。",
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
                "目的是只把你真正修改的子节点局部 Transform 回写到源 prefab，" +
                "而不是把场景布局、相机位置、实例激活状态等场景专用信息一起带回去。",
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
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            Camera cameraComponent = cameraObject.AddComponent<Camera>();
            cameraComponent.orthographic = true;
            cameraComponent.orthographicSize = 5f;
            cameraComponent.clearFlags = CameraClearFlags.SolidColor;
            cameraComponent.backgroundColor = new Color(0.12f, 0.12f, 0.13f, 1f);

            cameraObject.AddComponent<AudioListener>();
        }

        private static void ApplySelectedTransformOverrides(bool applyPosition, bool applyRotation, bool applyScale)
        {
            if (!TryBuildSelectionContext(out SelectionContext context, out string validationMessage))
            {
                EditorUtility.DisplayDialog("塔 Prefab 小范围回写", validationMessage, "知道了");
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
                "塔 Prefab 小范围回写",
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
                validationMessage =
                    "请先在场景里选中一个塔 prefab 实例的子节点。\n" +
                    "例如：FeedbackRoot（反馈挂点）、TypeSignatureRoot（塔型签名）、LevelMarkerRoot（等级标记）、" +
                    "或是 Relay 实例下方的自定义子节点。";
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
                    "根节点位置通常代表场景布局，不是 prefab 内部结构。\n" +
                    "请改选真正的视觉子节点，例如 FeedbackRoot、TypeSignatureRoot、LevelMarkerRoot。";
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

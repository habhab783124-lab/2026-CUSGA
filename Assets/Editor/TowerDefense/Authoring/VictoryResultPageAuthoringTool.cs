using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense.Editor
{
    /// <summary>
    /// Shared constants for the victory-result authoring workflow.
    ///
    /// We keep these paths in one place because the preview scene, the shared prefab, and the
    /// validation pass all need to agree on the exact same assets. This keeps the tool readable
    /// and makes future path changes much less error-prone.
    /// </summary>
    internal static class VictoryResultPageAuthoringPaths
    {
        public const string PreviewScenePath = "Assets/Scenes/VictoryResultPreview.unity";
        public const string SharedPrefabPath = "Assets/Resources/TowerDefense/UI/VictoryResultPage.prefab";

        public static readonly string[] FormalLevelScenePaths =
        {
            "Assets/Scenes/level 1.unity",
            "Assets/Scenes/Level 2.unity",
            "Assets/Scenes/Level 3.unity",
            "Assets/Scenes/level 4.unity"
        };
    }

    /// <summary>
    /// Utility methods behind the preview-to-prefab authoring workflow.
    ///
    /// The user's day-to-day need is simple:
    /// 1. Open the dedicated preview scene
    /// 2. Adjust the real victory page there
    /// 3. Click one button to push the result back to the shared prefab
    /// 4. Trust that every formal level now shows that updated shared page
    ///
    /// This helper centralizes those steps so both the editor window and the inspector button can
    /// share exactly the same implementation.
    /// </summary>
    internal static class VictoryResultPageAuthoringUtility
    {
        public static bool OpenPreviewScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return false;
            }

            EditorSceneManager.OpenScene(VictoryResultPageAuthoringPaths.PreviewScenePath, OpenSceneMode.Single);
            return true;
        }

        public static bool ApplyPreviewToSharedPrefab(out string summary)
        {
            summary = string.Empty;

            if (EditorApplication.isPlaying)
            {
                summary = "当前处于 Play Mode，不能在运行时回写胜利页 prefab。";
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!string.Equals(activeScene.path, VictoryResultPageAuthoringPaths.PreviewScenePath, StringComparison.OrdinalIgnoreCase))
            {
                summary = "请先打开 VictoryResultPreview 场景，再执行一键应用。";
                return false;
            }

            VictoryResultPageView previewView = UnityEngine.Object.FindFirstObjectByType<VictoryResultPageView>(FindObjectsInactive.Include);
            if (previewView == null)
            {
                summary = "当前预览场景里没有找到 VictoryResultPageView，无法回写共享 prefab。";
                return false;
            }

            GameObject previewRoot = previewView.gameObject;
            string currentSourcePath = GetConnectedPrefabAssetPath(previewRoot);

            if (string.Equals(currentSourcePath, VictoryResultPageAuthoringPaths.SharedPrefabPath, StringComparison.OrdinalIgnoreCase))
            {
                PrefabUtility.ApplyPrefabInstance(previewRoot, InteractionMode.UserAction);
            }
            else
            {
                // If the scene root somehow lost its prefab connection, we still want one click to
                // recover the workflow by rebuilding the shared prefab from the current preview root.
                PrefabUtility.SaveAsPrefabAssetAndConnect(
                    previewRoot,
                    VictoryResultPageAuthoringPaths.SharedPrefabPath,
                    InteractionMode.UserAction);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            summary =
                "已把当前 VictoryResultPreview 里的 VictoryResultPage 回写到共享 prefab。\n" +
                $"Prefab: {VictoryResultPageAuthoringPaths.SharedPrefabPath}";
            return true;
        }

        public static bool ApplyPreviewToSharedPrefabWithValidation(out string summary, out ValidationResult validationResult)
        {
            validationResult = default;
            if (!ApplyPreviewToSharedPrefab(out string applySummary))
            {
                summary = applySummary;
                return false;
            }

            validationResult = ValidateFormalLevelIntegration();
            summary = BuildApplyAndValidationSummary(applySummary, validationResult);
            return !validationResult.HasFailures;
        }

        public static ValidationResult ValidateFormalLevelIntegration()
        {
            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            List<SceneValidationItem> items = new List<SceneValidationItem>(VictoryResultPageAuthoringPaths.FormalLevelScenePaths.Length);

            try
            {
                for (int index = 0; index < VictoryResultPageAuthoringPaths.FormalLevelScenePaths.Length; index++)
                {
                    string scenePath = VictoryResultPageAuthoringPaths.FormalLevelScenePaths[index];
                    items.Add(ValidateSingleFormalScene(scenePath));
                }
            }
            finally
            {
                if (originalSetup != null && originalSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                }
            }

            return new ValidationResult(items);
        }

        private static SceneValidationItem ValidateSingleFormalScene(string scenePath)
        {
            if (!System.IO.File.Exists(scenePath))
            {
                return new SceneValidationItem(scenePath, false, "场景文件不存在。");
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                TowerDefenseGame game = FindFirstComponentInScene<TowerDefenseGame>(scene);
                if (game == null)
                {
                    return new SceneValidationItem(scenePath, false, "场景里没有 TowerDefenseGame。");
                }

                SerializedObject serializedGame = new SerializedObject(game);
                SerializedProperty pageReferenceProperty = serializedGame.FindProperty("victoryResultPageViewReference");
                bool hasSceneLocalVictoryPageReference = pageReferenceProperty != null &&
                                                        pageReferenceProperty.objectReferenceValue != null;

                VictoryResultPageView[] scenePages = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<VictoryResultPageView>(true))
                    .ToArray();

                if (hasSceneLocalVictoryPageReference)
                {
                    return new SceneValidationItem(
                        scenePath,
                        false,
                        "TowerDefenseGame 上仍然挂着场景内的 VictoryResultPageView，本关没有纯共享化。");
                }

                if (scenePages.Length > 0)
                {
                    return new SceneValidationItem(
                        scenePath,
                        false,
                        "场景里存在静态 VictoryResultPageView，正式关卡不应该保留本地副本。");
                }

                return new SceneValidationItem(
                    scenePath,
                    true,
                    $"通过：当前关卡会在运行时走共享 prefab {VictoryResultPageAuthoringPaths.SharedPrefabPath}。");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static string GetConnectedPrefabAssetPath(GameObject sceneRoot)
        {
            if (sceneRoot == null)
            {
                return string.Empty;
            }

            GameObject sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(sceneRoot);
            return sourcePrefab != null ? AssetDatabase.GetAssetPath(sourcePrefab) : string.Empty;
        }

        private static T FindFirstComponentInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                T component = roots[index] != null ? roots[index].GetComponentInChildren<T>(true) : null;
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static string BuildApplyAndValidationSummary(string applySummary, ValidationResult validationResult)
        {
            StringBuilder builder = new StringBuilder(512);
            builder.AppendLine(applySummary);
            builder.AppendLine();
            builder.AppendLine("正式关卡共享接入检查：");
            builder.AppendLine(validationResult.OverallSummary);

            if (validationResult.Items.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine(validationResult.BuildDetailedReport());
            }

            return builder.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// Simple editor window for the victory-result authoring workflow.
    ///
    /// This window intentionally stays small and task-focused:
    /// - open preview scene
    /// - apply preview page back to shared prefab
    /// - verify the formal level scenes still use the shared runtime page
    ///
    /// That keeps it fast to use while still giving the user confidence that one click really does
    /// reach every level through the shared prefab path.
    /// </summary>
    public sealed class VictoryResultPageAuthoringWindow : EditorWindow
    {
        private Vector2 _validationScroll;
        private string _latestSummary = "还没有执行过胜利页同步或检查。";
        private MessageType _latestSummaryType = MessageType.Info;
        private string _latestValidationReport = string.Empty;

        [MenuItem("Tools/Tower Defense/Authoring/胜利页同步工具")]
        public static void OpenWindow()
        {
            VictoryResultPageAuthoringWindow window = GetWindow<VictoryResultPageAuthoringWindow>("胜利页同步");
            window.minSize = new Vector2(560f, 360f);
            window.Show();
        }

        [MenuItem("Tools/Tower Defense/Authoring/胜利页/打开预览场景")]
        public static void OpenPreviewSceneFromMenu()
        {
            VictoryResultPageAuthoringUtility.OpenPreviewScene();
        }

        [MenuItem("Tools/Tower Defense/Authoring/胜利页/应用预览到共享页面")]
        public static void ApplyPreviewToSharedPrefabFromMenu()
        {
            if (VictoryResultPageAuthoringUtility.ApplyPreviewToSharedPrefabWithValidation(
                    out string summary,
                    out ValidationResult validationResult))
            {
                EditorUtility.DisplayDialog("胜利页同步完成", summary, "确定");
            }
            else
            {
                string title = validationResult.Items.Count > 0 ? "胜利页同步完成，但检查发现问题" : "胜利页同步失败";
                EditorUtility.DisplayDialog(title, summary, "确定");
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("胜利页同步工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "推荐工作流：\n" +
                "1. 打开 VictoryResultPreview\n" +
                "2. 直接在场景里调整正式 VictoryResultPage\n" +
                "3. 点击“一键应用到所有关卡”\n" +
                "4. 如有需要，再点击“检查正式关卡共享接入”确认所有关卡仍然走共享页面",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开预览场景", GUILayout.Height(28f)))
                {
                    VictoryResultPageAuthoringUtility.OpenPreviewScene();
                }

                if (GUILayout.Button("一键应用到所有关卡", GUILayout.Height(28f)))
                {
                    if (VictoryResultPageAuthoringUtility.ApplyPreviewToSharedPrefabWithValidation(
                            out string summary,
                            out ValidationResult validationResult))
                    {
                        _latestSummary = summary;
                        _latestSummaryType = MessageType.Info;
                        _latestValidationReport = validationResult.BuildDetailedReport();
                    }
                    else
                    {
                        _latestSummary = summary;
                        _latestSummaryType = validationResult.Items.Count > 0 ? MessageType.Warning : MessageType.Error;
                        _latestValidationReport = validationResult.BuildDetailedReport();
                    }
                }
            }

            if (GUILayout.Button("检查正式关卡共享接入", GUILayout.Height(24f)))
            {
                ValidationResult validationResult = VictoryResultPageAuthoringUtility.ValidateFormalLevelIntegration();
                _latestSummary = validationResult.OverallSummary;
                _latestSummaryType = validationResult.HasFailures ? MessageType.Warning : MessageType.Info;
                _latestValidationReport = validationResult.BuildDetailedReport();
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(_latestSummary, _latestSummaryType);

            EditorGUILayout.LabelField("正式关卡检查报告", EditorStyles.miniBoldLabel);
            using (EditorGUILayout.ScrollViewScope scrollScope = new EditorGUILayout.ScrollViewScope(_validationScroll, GUILayout.MinHeight(180f)))
            {
                _validationScroll = scrollScope.scrollPosition;
                EditorGUILayout.TextArea(
                    string.IsNullOrWhiteSpace(_latestValidationReport)
                        ? "还没有执行正式关卡共享接入检查。"
                        : _latestValidationReport,
                    GUILayout.ExpandHeight(true));
            }
        }
    }

    /// <summary>
    /// Convenience inspector so the user can stay inside the preview scene and click one button.
    ///
    /// The window remains the richer workflow surface, but this inspector shortcut removes the
    /// mental overhead of having to re-open another tool while adjusting the page.
    /// </summary>
    [CustomEditor(typeof(VictoryResultPreviewController))]
    public sealed class VictoryResultPreviewControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10f);
            EditorGUILayout.HelpBox(
                "这个预览场景现在直接复用正式 VictoryResultPage.prefab。\n" +
                "你在这里调完后，点击下面这个按钮就会把当前页面一键应用回共享 prefab。",
                MessageType.Info);

            if (GUILayout.Button("应用当前预览到所有关卡", GUILayout.Height(28f)))
            {
                if (VictoryResultPageAuthoringUtility.ApplyPreviewToSharedPrefabWithValidation(
                        out string summary,
                        out ValidationResult validationResult))
                {
                    EditorUtility.DisplayDialog("胜利页同步完成", summary, "确定");
                }
                else
                {
                    string title = validationResult.Items.Count > 0 ? "胜利页同步完成，但检查发现问题" : "胜利页同步失败";
                    EditorUtility.DisplayDialog(title, summary, "确定");
                }
            }

            if (GUILayout.Button("打开胜利页同步工具", GUILayout.Height(22f)))
            {
                VictoryResultPageAuthoringWindow.OpenWindow();
            }
        }
    }

    internal readonly struct SceneValidationItem
    {
        public SceneValidationItem(string scenePath, bool isValid, string message)
        {
            ScenePath = scenePath ?? string.Empty;
            IsValid = isValid;
            Message = message ?? string.Empty;
        }

        public string ScenePath { get; }
        public bool IsValid { get; }
        public string Message { get; }
    }

    internal readonly struct ValidationResult
    {
        public ValidationResult(IReadOnlyList<SceneValidationItem> items)
        {
            Items = items ?? Array.Empty<SceneValidationItem>();
        }

        public IReadOnlyList<SceneValidationItem> Items { get; }
        public bool HasFailures => Items.Any(item => !item.IsValid);

        public string OverallSummary
        {
            get
            {
                if (Items.Count == 0)
                {
                    return "没有拿到正式关卡检查结果。";
                }

                if (!HasFailures)
                {
                    return "正式塔防关卡共享接入检查通过：当前正式关卡都会通过共享 VictoryResultPage prefab 吃到更新。";
                }

                return "正式塔防关卡共享接入检查发现问题，请查看下面的逐关报告。";
            }
        }

        public string BuildDetailedReport()
        {
            if (Items.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(256);
            for (int index = 0; index < Items.Count; index++)
            {
                SceneValidationItem item = Items[index];
                builder.Append(item.IsValid ? "[OK] " : "[WARN] ");
                builder.Append(item.ScenePath);
                builder.AppendLine();
                builder.AppendLine(item.Message);

                if (index < Items.Count - 1)
                {
                    builder.AppendLine();
                }
            }

            return builder.ToString();
        }
    }
}

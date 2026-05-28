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
    /// Shared paths for the result-page authoring workflow.
    ///
    /// Victory and failure currently share the same runtime prefab, but use different preview scenes.
    /// Keeping the path table in one place lets the menu tools, the editor window, and the scene
    /// inspector shortcuts all agree on the same assets.
    /// </summary>
    internal static class ResultPageAuthoringPaths
    {
        public const string VictoryPreviewScenePath = "Assets/Scenes/VictoryResultPreview.unity";
        public const string FailurePreviewScenePath = "Assets/Scenes/FailureResultPreview.unity";
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
    /// A tiny profile that says "which preview scene are we authoring right now?"
    ///
    /// We intentionally keep this lightweight:
    /// - victory and failure share the same real prefab
    /// - their authoring difference is mainly which preview scene the user wants to open
    /// - the shared integration validation stays exactly the same
    /// </summary>
    internal readonly struct ResultPageAuthoringProfile
    {
        public ResultPageAuthoringProfile(
            string previewScenePath,
            string previewSceneName,
            string pageDisplayName,
            string windowTitle)
        {
            PreviewScenePath = previewScenePath ?? string.Empty;
            PreviewSceneName = previewSceneName ?? string.Empty;
            PageDisplayName = pageDisplayName ?? string.Empty;
            WindowTitle = windowTitle ?? string.Empty;
        }

        public string PreviewScenePath { get; }
        public string PreviewSceneName { get; }
        public string PageDisplayName { get; }
        public string WindowTitle { get; }
    }

    internal static class ResultPageAuthoringProfiles
    {
        public static readonly ResultPageAuthoringProfile Victory = new ResultPageAuthoringProfile(
            ResultPageAuthoringPaths.VictoryPreviewScenePath,
            "VictoryResultPreview",
            "胜利页",
            "胜利页同步");

        public static readonly ResultPageAuthoringProfile Failure = new ResultPageAuthoringProfile(
            ResultPageAuthoringPaths.FailurePreviewScenePath,
            "FailureResultPreview",
            "失败页",
            "失败页同步");

        public static ResultPageAuthoringProfile ResolveFromScenePath(string scenePath)
        {
            if (string.Equals(scenePath, ResultPageAuthoringPaths.FailurePreviewScenePath, StringComparison.OrdinalIgnoreCase))
            {
                return Failure;
            }

            return Victory;
        }
    }

    /// <summary>
    /// Utility methods behind the preview-to-prefab authoring workflow.
    ///
    /// The authoring loop is intentionally kept the same for victory and failure:
    /// 1. Open the dedicated preview scene
    /// 2. Adjust the real result page there
    /// 3. Push the scene copy back into the shared prefab
    /// 4. Verify that formal levels still consume the shared runtime page
    ///
    /// Using one shared implementation here avoids the common "two tools slowly drift apart" trap.
    /// </summary>
    internal static class ResultPageAuthoringUtility
    {
        public static bool OpenPreviewScene(ResultPageAuthoringProfile profile)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return false;
            }

            if (!System.IO.File.Exists(profile.PreviewScenePath))
            {
                Debug.LogWarning($"找不到 {profile.PageDisplayName} 预览场景：{profile.PreviewScenePath}");
                return false;
            }

            EditorSceneManager.OpenScene(profile.PreviewScenePath, OpenSceneMode.Single);
            return true;
        }

        public static bool ApplyPreviewToSharedPrefab(ResultPageAuthoringProfile profile, out string summary)
        {
            summary = string.Empty;

            if (EditorApplication.isPlaying)
            {
                summary = $"当前处于 Play Mode，不能在运行时回写{profile.PageDisplayName}共享页面。";
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!string.Equals(activeScene.path, profile.PreviewScenePath, StringComparison.OrdinalIgnoreCase))
            {
                summary = $"请先打开 {profile.PreviewSceneName} 场景，再执行一键应用。";
                return false;
            }

            VictoryResultPageView previewView = UnityEngine.Object.FindFirstObjectByType<VictoryResultPageView>(FindObjectsInactive.Include);
            if (previewView == null)
            {
                summary = $"当前 {profile.PageDisplayName} 预览场景里没有找到 VictoryResultPageView，无法回写共享 prefab。";
                return false;
            }

            GameObject previewRoot = previewView.gameObject;
            string currentSourcePath = GetConnectedPrefabAssetPath(previewRoot);

            if (string.Equals(currentSourcePath, ResultPageAuthoringPaths.SharedPrefabPath, StringComparison.OrdinalIgnoreCase))
            {
                PrefabUtility.ApplyPrefabInstance(previewRoot, InteractionMode.UserAction);
            }
            else
            {
                // If the preview root somehow lost its prefab connection, we still keep the tool
                // one-click friendly by rebuilding the shared prefab from the current preview state.
                PrefabUtility.SaveAsPrefabAssetAndConnect(
                    previewRoot,
                    ResultPageAuthoringPaths.SharedPrefabPath,
                    InteractionMode.UserAction);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            summary =
                $"已把当前 {profile.PreviewSceneName} 里的 VictoryResultPage 回写到共享 prefab。\n" +
                $"Prefab: {ResultPageAuthoringPaths.SharedPrefabPath}";
            return true;
        }

        public static bool ApplyPreviewToSharedPrefabWithValidation(
            ResultPageAuthoringProfile profile,
            out string summary,
            out ValidationResult validationResult)
        {
            validationResult = default;
            if (!ApplyPreviewToSharedPrefab(profile, out string applySummary))
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
            List<SceneValidationItem> items = new List<SceneValidationItem>(ResultPageAuthoringPaths.FormalLevelScenePaths.Length);

            try
            {
                for (int index = 0; index < ResultPageAuthoringPaths.FormalLevelScenePaths.Length; index++)
                {
                    string scenePath = ResultPageAuthoringPaths.FormalLevelScenePaths[index];
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
                bool hasSceneLocalResultPageReference = pageReferenceProperty != null &&
                                                        pageReferenceProperty.objectReferenceValue != null;

                VictoryResultPageView[] scenePages = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<VictoryResultPageView>(true))
                    .ToArray();

                if (hasSceneLocalResultPageReference)
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
                    $"通过：当前关卡会在运行时走共享 prefab {ResultPageAuthoringPaths.SharedPrefabPath}。");
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
    /// Editor window shared by both victory and failure result-page authoring flows.
    ///
    /// The surface area intentionally stays small:
    /// - open the chosen preview scene
    /// - apply current preview back to the shared prefab
    /// - check whether formal levels still consume the shared runtime page
    ///
    /// We keep the workflow identical between victory and failure so the user does not need to
    /// learn two different tools for two moods of the same result-page system.
    /// </summary>
    public sealed class ResultPageAuthoringWindow : EditorWindow
    {
        private Vector2 _validationScroll;
        private string _latestSummary = "还没有执行过结果页同步或检查。";
        private MessageType _latestSummaryType = MessageType.Info;
        private string _latestValidationReport = string.Empty;
        private ResultPageAuthoringProfile _profile = ResultPageAuthoringProfiles.Victory;

        [MenuItem("Tools/Tower Defense/Authoring/胜利页同步工具")]
        public static void OpenVictoryWindow()
        {
            OpenWindow(ResultPageAuthoringProfiles.Victory);
        }

        [MenuItem("Tools/Tower Defense/Authoring/失败页同步工具")]
        public static void OpenFailureWindow()
        {
            OpenWindow(ResultPageAuthoringProfiles.Failure);
        }

        [MenuItem("Tools/Tower Defense/Authoring/胜利页/打开预览场景")]
        public static void OpenVictoryPreviewSceneFromMenu()
        {
            ResultPageAuthoringUtility.OpenPreviewScene(ResultPageAuthoringProfiles.Victory);
        }

        [MenuItem("Tools/Tower Defense/Authoring/失败页/打开预览场景")]
        public static void OpenFailurePreviewSceneFromMenu()
        {
            ResultPageAuthoringUtility.OpenPreviewScene(ResultPageAuthoringProfiles.Failure);
        }

        [MenuItem("Tools/Tower Defense/Authoring/胜利页/应用预览到共享页面")]
        public static void ApplyVictoryPreviewToSharedPrefabFromMenu()
        {
            ApplyFromMenu(ResultPageAuthoringProfiles.Victory);
        }

        [MenuItem("Tools/Tower Defense/Authoring/失败页/应用预览到共享页面")]
        public static void ApplyFailurePreviewToSharedPrefabFromMenu()
        {
            ApplyFromMenu(ResultPageAuthoringProfiles.Failure);
        }

        private static void OpenWindow(ResultPageAuthoringProfile profile)
        {
            ResultPageAuthoringWindow window = GetWindow<ResultPageAuthoringWindow>(profile.WindowTitle);
            window._profile = profile;
            window.minSize = new Vector2(560f, 360f);
            window.Show();
        }

        private static void ApplyFromMenu(ResultPageAuthoringProfile profile)
        {
            if (ResultPageAuthoringUtility.ApplyPreviewToSharedPrefabWithValidation(
                    profile,
                    out string summary,
                    out ValidationResult validationResult))
            {
                EditorUtility.DisplayDialog($"{profile.PageDisplayName}同步完成", summary, "确定");
            }
            else
            {
                string title = validationResult.Items.Count > 0
                    ? $"{profile.PageDisplayName}同步完成，但检查发现问题"
                    : $"{profile.PageDisplayName}同步失败";
                EditorUtility.DisplayDialog(title, summary, "确定");
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField($"{_profile.PageDisplayName}同步工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "推荐工作流：\n" +
                $"1. 打开 {_profile.PreviewSceneName}\n" +
                "2. 直接在场景里调整正式 VictoryResultPage\n" +
                "3. 点击“一键应用到所有关卡”\n" +
                "4. 如有需要，再点击“检查正式关卡共享接入”确认所有关卡仍然走共享页面",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开预览场景", GUILayout.Height(28f)))
                {
                    ResultPageAuthoringUtility.OpenPreviewScene(_profile);
                }

                if (GUILayout.Button("一键应用到所有关卡", GUILayout.Height(28f)))
                {
                    if (ResultPageAuthoringUtility.ApplyPreviewToSharedPrefabWithValidation(
                            _profile,
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
                ValidationResult validationResult = ResultPageAuthoringUtility.ValidateFormalLevelIntegration();
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
    /// Inspector shortcut so the user can stay inside either preview scene and still get a one-click
    /// "apply current preview" experience.
    ///
    /// The same preview controller component is reused in both scenes, so the inspector resolves
    /// which workflow the user wants based on the active scene path.
    /// </summary>
    [CustomEditor(typeof(VictoryResultPreviewController))]
    public sealed class VictoryResultPreviewControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ResultPageAuthoringProfile profile = ResultPageAuthoringProfiles.ResolveFromScenePath(SceneManager.GetActiveScene().path);

            EditorGUILayout.Space(10f);
            EditorGUILayout.HelpBox(
                $"这个预览场景现在直接复用正式 {ResultPageAuthoringPaths.SharedPrefabPath}。\n" +
                $"你在这里调完 {_profileLabel(profile)} 后，点击下面这个按钮就会把当前页面一键应用回共享 prefab。",
                MessageType.Info);

            if (GUILayout.Button("应用当前预览到所有关卡", GUILayout.Height(28f)))
            {
                if (ResultPageAuthoringUtility.ApplyPreviewToSharedPrefabWithValidation(
                        profile,
                        out string summary,
                        out ValidationResult validationResult))
                {
                    EditorUtility.DisplayDialog($"{profile.PageDisplayName}同步完成", summary, "确定");
                }
                else
                {
                    string title = validationResult.Items.Count > 0
                        ? $"{profile.PageDisplayName}同步完成，但检查发现问题"
                        : $"{profile.PageDisplayName}同步失败";
                    EditorUtility.DisplayDialog(title, summary, "确定");
                }
            }

            if (GUILayout.Button($"打开{profile.PageDisplayName}同步工具", GUILayout.Height(22f)))
            {
                if (profile.PageDisplayName == ResultPageAuthoringProfiles.Failure.PageDisplayName)
                {
                    ResultPageAuthoringWindow.OpenFailureWindow();
                }
                else
                {
                    ResultPageAuthoringWindow.OpenVictoryWindow();
                }
            }
        }

        private static string _profileLabel(ResultPageAuthoringProfile profile)
        {
            return string.IsNullOrWhiteSpace(profile.PageDisplayName) ? "结果页" : profile.PageDisplayName;
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

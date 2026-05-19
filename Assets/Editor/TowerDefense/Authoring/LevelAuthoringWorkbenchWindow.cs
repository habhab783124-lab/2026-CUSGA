using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense.Editor
{
    /// <summary>
    /// One-stop Chinese-friendly workbench for day-to-day level authoring.
    ///
    /// Why this window exists:
    /// 1. The project already has several specialized tools, but authors still need a stable
    ///    "home base" that exposes the most common scene parameters directly.
    /// 2. The user's goal is to finish most map work by combining Scene view with tools, without
    ///    repeatedly jumping into scattered assets and inspectors.
    /// 3. Specialized windows are still valuable, so this workbench does not replace them.
    ///    Instead, it gathers:
    ///    - current scene context
    ///    - core level parameters
    ///    - wave asset parameters
    ///    - quick entry points to the specialized tools
    ///
    /// Design boundary:
    /// - scene structure still belongs to Scene view
    /// - specialized tasks still belong to specialized windows
    /// - but the most common authoring parameters should be visible and editable here
    /// </summary>
    public sealed class LevelAuthoringWorkbenchWindow : EditorWindow
    {
        [SerializeField] private TowerDefenseGame currentGame;
        [SerializeField] private WaveSpawner currentWaveSpawner;
        [SerializeField] private BattlefieldMapDefinition currentMap;
        [SerializeField] private BuildZone currentBuildZone;
        [SerializeField] private Camera currentCamera;
        [SerializeField] private Vector2 scrollPosition;
        [SerializeField] private bool showSceneBootstrap = true;
        [SerializeField] private bool showSceneContext = true;
        [SerializeField] private bool showMapShell = true;
        [SerializeField] private bool showEconomyAndStarterZone = true;
        [SerializeField] private bool showWaveAuthoring = true;
        [SerializeField] private bool showEnemyCatalog = false;
        [SerializeField] private bool showSmokeTest = true;
        [SerializeField] private bool showAuthoringRecovery = true;
        [SerializeField] private bool showToolShortcuts = true;
        [SerializeField] private bool replaceExistingShellRoots = true;
        [SerializeField] private string latestSmokeSummary = "暂无烟测结果。";
        [SerializeField] private string latestSmokeReport = string.Empty;
        [SerializeField] private MessageType latestSmokeMessageType = MessageType.Info;
        [SerializeField] private Vector2 smokeReportScrollPosition;

        [MenuItem("Tools/Tower Defense/Authoring/关卡开发工作台")]
        public static void OpenWindow()
        {
            LevelAuthoringWorkbenchWindow window = GetWindow<LevelAuthoringWorkbenchWindow>("关卡开发工作台");
            window.minSize = new Vector2(760f, 540f);
            window.AdoptCurrentSceneContext();
            window.Show();
        }

        private void OnEnable()
        {
            AdoptCurrentSceneContext();
            RefreshSmokeTestReport();
        }

        private void OnHierarchyChange()
        {
            Repaint();
        }

        private void OnSelectionChange()
        {
            Repaint();
        }

        private void OnGUI()
        {
            DrawHeader();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            try
            {
                showSceneBootstrap = EditorGUILayout.Foldout(showSceneBootstrap, "关卡壳层创建器", true);
                if (showSceneBootstrap)
                {
                    DrawSceneBootstrapSection();
                }
                showSceneContext = EditorGUILayout.Foldout(showSceneContext, "当前场景上下文", true);
                if (showSceneContext)
                {
                    DrawSceneContextSection();
                }

                showMapShell = EditorGUILayout.Foldout(showMapShell, "地图骨架与场景参数", true);
                if (showMapShell)
                {
                    DrawMapShellSection();
                }

                showEconomyAndStarterZone = EditorGUILayout.Foldout(showEconomyAndStarterZone, "开局资源与放置规则", true);
                if (showEconomyAndStarterZone)
                {
                    DrawEconomyAndStarterZoneSection();
                }

                showWaveAuthoring = EditorGUILayout.Foldout(showWaveAuthoring, "波次与刷怪参数", true);
                if (showWaveAuthoring)
                {
                    DrawWaveAuthoringSection();
                }

                showEnemyCatalog = EditorGUILayout.Foldout(showEnemyCatalog, "敌人目录参数", true);
                if (showEnemyCatalog)
                {
                    DrawEnemyCatalogSection();
                }

                showSmokeTest = EditorGUILayout.Foldout(showSmokeTest, "运行态烟测", true);
                if (showSmokeTest)
                {
                    DrawSmokeTestSection();
                }

                showAuthoringRecovery = EditorGUILayout.Foldout(showAuthoringRecovery, "作者快照与回退", true);
                if (showAuthoringRecovery)
                {
                    DrawAuthoringRecoverySection();
                }

                showToolShortcuts = EditorGUILayout.Foldout(showToolShortcuts, "专项工具快捷入口", true);
                if (showToolShortcuts)
                {
                    DrawToolShortcutsSection();
                }
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        /// <summary>
        /// Keeps the window practical from the very first click.
        ///
        /// Authors frequently switch scenes and branches, so the header exposes:
        /// - adopt current scene
        /// - save all
        /// - collect references
        /// - quick scene summary
        /// </summary>
        private void DrawHeader()
        {
            EditorGUILayout.LabelField("关卡开发工作台", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("接管当前场景", GUILayout.Width(150f)))
                {
                    AdoptCurrentSceneContext();
                }

                if (GUILayout.Button("保存场景与资源", GUILayout.Width(150f)))
                {
                    AssetDatabase.SaveAssets();
                    EditorSceneManager.SaveOpenScenes();
                }

                using (new EditorGUI.DisabledScope(currentMap == null))
                {
                    if (GUILayout.Button("收集地图引用", GUILayout.Width(150f)))
                    {
                        Undo.RecordObject(currentMap, "收集地图引用");
                        currentMap.CollectSceneReferences();
                        EditorUtility.SetDirty(currentMap);
                        EditorSceneManager.MarkSceneDirty(currentMap.gameObject.scene);
                    }
                }
            }

            Scene activeScene = SceneManager.GetActiveScene();
            string sceneName = string.IsNullOrWhiteSpace(activeScene.name) ? "(无场景)" : activeScene.name;
            EditorGUILayout.HelpBox(
                $"当前场景：{sceneName}\n" +
                $"当前地图：{(currentMap != null ? currentMap.name : "(未找到)")}\n" +
                $"当前刷怪器：{(currentWaveSpawner != null ? currentWaveSpawner.name : "(未找到)")}",
                MessageType.Info);
        }

        private void DrawSceneBootstrapSection()
        {
            EditorGUILayout.HelpBox(
                "这个分区负责补上当前工具链里最缺的第一步：把一张空白 Scene 初始化成可继续编辑的塔防关卡壳层。初始化完成后，你就可以继续用拓扑、路径点、道路、波次和数值工具往下做。",
                MessageType.Info);

            replaceExistingShellRoots = EditorGUILayout.Toggle("重建同名共享壳层对象", replaceExistingShellRoots);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("把当前场景初始化成关卡壳层", GUILayout.Height(28f)))
                {
                    BootstrapCurrentSceneFromSample();
                }

                if (GUILayout.Button("新建空白关卡场景并注入壳层", GUILayout.Height(28f)))
                {
                    CreateNewSceneAndBootstrap();
                }
            }
        }

        private void DrawSceneContextSection()
        {
            EditorGUILayout.HelpBox(TowerDefenseAuthoringSceneContext.GetOrCreate().BuildSummary(), MessageType.None);
            currentGame = (TowerDefenseGame)EditorGUILayout.ObjectField("总控", currentGame, typeof(TowerDefenseGame), true);
            currentWaveSpawner = (WaveSpawner)EditorGUILayout.ObjectField("刷怪器", currentWaveSpawner, typeof(WaveSpawner), true);
            currentMap = (BattlefieldMapDefinition)EditorGUILayout.ObjectField("地图入口", currentMap, typeof(BattlefieldMapDefinition), true);
            currentBuildZone = (BuildZone)EditorGUILayout.ObjectField("可建造区", currentBuildZone, typeof(BuildZone), true);
            currentCamera = (Camera)EditorGUILayout.ObjectField("主相机", currentCamera, typeof(Camera), true);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPingButton(currentGame, "定位总控");
                DrawPingButton(currentWaveSpawner, "定位刷怪器");
                DrawPingButton(currentMap, "定位地图");
                DrawPingButton(currentBuildZone, "定位建造区");
                DrawPingButton(currentCamera, "定位相机");
            }
        }

        private void DrawMapShellSection()
        {
            if (currentMap == null && currentBuildZone == null && currentCamera == null)
            {
                EditorGUILayout.HelpBox("当前场景还没有完整接入地图骨架。先点“接管当前场景”。", MessageType.Warning);
                return;
            }

            if (currentMap != null)
            {
                SerializedObject serializedMap = new SerializedObject(currentMap);
                serializedMap.Update();
                EditorGUILayout.LabelField("地图入口参数", EditorStyles.miniBoldLabel);
                DrawPropertyField(serializedMap, "relayLimit", "继电器上限");
                DrawPropertyField(serializedMap, "buildZoneReference", "BuildZone 引用");
                DrawPropertyField(serializedMap, "spawnGates", "出怪口数组", includeChildren: true);
                DrawPropertyField(serializedMap, "defensePoints", "防御点数组", includeChildren: true);
                serializedMap.ApplyModifiedProperties();
                EditorUtility.SetDirty(currentMap);
            }

            if (currentBuildZone != null)
            {
                SerializedObject serializedBuildZone = new SerializedObject(currentBuildZone);
                serializedBuildZone.Update();
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("建造区参数", EditorStyles.miniBoldLabel);
                DrawTransformFields(currentBuildZone.transform, "建造区位置", "建造区缩放");
                DrawPropertyField(serializedBuildZone, "zoneShapeRootReference", "ZoneShapes 根");
                DrawPropertyField(serializedBuildZone, "zoneShapeColliders", "ZoneShapes 碰撞体", includeChildren: true);
                DrawPropertyField(serializedBuildZone, "autoCollectZoneShapes", "自动收集 ZoneShapes");
                DrawPropertyField(serializedBuildZone, "includeInactiveShapes", "包含未激活 Shape");
                serializedBuildZone.ApplyModifiedProperties();
                EditorUtility.SetDirty(currentBuildZone);
            }

            if (currentCamera != null)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("相机参数", EditorStyles.miniBoldLabel);
                DrawTransformFields(currentCamera.transform, "相机位置", null);
                if (currentCamera.orthographic)
                {
                    EditorGUI.BeginChangeCheck();
                    float orthographicSize = EditorGUILayout.FloatField("正交尺寸", currentCamera.orthographicSize);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(currentCamera, "修改相机正交尺寸");
                        currentCamera.orthographicSize = Mathf.Max(0.1f, orthographicSize);
                        EditorUtility.SetDirty(currentCamera);
                    }
                }
            }
        }

        private void DrawEconomyAndStarterZoneSection()
        {
            if (currentGame == null)
            {
                EditorGUILayout.HelpBox("当前场景没有找到 TowerDefenseGame，无法编辑这些参数。", MessageType.Warning);
                return;
            }

            SerializedObject serializedGame = new SerializedObject(currentGame);
            serializedGame.Update();

            EditorGUILayout.LabelField("开局资源", EditorStyles.miniBoldLabel);
            DrawPropertyField(serializedGame, "startingScrap", "开局废料");
            DrawPropertyField(serializedGame, "startingBaseHealth", "基地生命");

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("建造成本", EditorStyles.miniBoldLabel);
            DrawPropertyField(serializedGame, "relayTowerCost", "继电器成本");
            DrawPropertyField(serializedGame, "singleTargetTowerCost", "单体塔成本");
            DrawPropertyField(serializedGame, "slowFieldTowerCost", "减速塔成本");
            DrawPropertyField(serializedGame, "bombardTowerCost", "炸弹塔成本");

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("放置扩张规则", EditorStyles.miniBoldLabel);
            DrawPropertyField(serializedGame, "relayExpansionSquareSize", "继电器扩张方格边长");
            DrawPropertyField(serializedGame, "defenseExpansionSquareSize", "战斗塔扩张方格边长");
            DrawPropertyField(serializedGame, "enablePlacementDiagnostics", "启用放置诊断日志");
            EditorGUILayout.HelpBox("当前版本里，塔和继电器的占地 / 不可放置半径以各自 prefab 上的 CircleCollider2D 为准。请直接在 prefab 上调整 Collider2D 的 Radius 与 Offset。", MessageType.Info);

            serializedGame.ApplyModifiedProperties();
            EditorUtility.SetDirty(currentGame);
        }

        private void DrawWaveAuthoringSection()
        {
            if (currentWaveSpawner == null)
            {
                EditorGUILayout.HelpBox("当前场景没有找到 WaveSpawner。", MessageType.Warning);
                return;
            }

            SerializedObject serializedSpawner = new SerializedObject(currentWaveSpawner);
            serializedSpawner.Update();

            EditorGUILayout.LabelField("刷怪时序", EditorStyles.miniBoldLabel);
            DrawPropertyField(serializedSpawner, "initialDelay", "首波延迟");
            DrawPropertyField(serializedSpawner, "delayBetweenWaves", "波间隔");
            DrawPropertyField(serializedSpawner, "routePreviewLeadTime", "路线预告提前量");
            DrawPropertyField(serializedSpawner, "continueCampaignAfterClear", "通关后推进战役");

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("波次来源", EditorStyles.miniBoldLabel);
            DrawPropertyField(serializedSpawner, "waveCatalogAsset", "波次目录");
            DrawPropertyField(serializedSpawner, "enemyCatalogAsset", "敌人目录");
            DrawPropertyField(serializedSpawner, "enemyPrototypeReference", "兜底敌人 Prefab");
            DrawPropertyField(serializedSpawner, "enemyRootReference", "敌人根节点");
            DrawPropertyField(serializedSpawner, "battlefieldMapReference", "地图引用");
            serializedSpawner.ApplyModifiedProperties();
            EditorUtility.SetDirty(currentWaveSpawner);

            WaveCatalogAsset waveCatalog = ResolveWaveCatalog(serializedSpawner);
            if (waveCatalog != null)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("波次目录内容", EditorStyles.miniBoldLabel);
                SerializedObject serializedCatalog = new SerializedObject(waveCatalog);
                serializedCatalog.Update();
                DrawPropertyField(serializedCatalog, "waves", "波次列表", includeChildren: true);
                serializedCatalog.ApplyModifiedProperties();
                EditorUtility.SetDirty(waveCatalog);
            }
        }

        private void DrawEnemyCatalogSection()
        {
            if (currentWaveSpawner == null)
            {
                EditorGUILayout.HelpBox("当前场景没有找到 WaveSpawner。", MessageType.Warning);
                return;
            }

            SerializedObject serializedSpawner = new SerializedObject(currentWaveSpawner);
            EnemyCatalogAsset enemyCatalog = ResolveEnemyCatalog(serializedSpawner);
            if (enemyCatalog == null)
            {
                EditorGUILayout.HelpBox("当前刷怪器没有接 EnemyCatalogAsset。", MessageType.Warning);
                return;
            }

            SerializedObject serializedEnemyCatalog = new SerializedObject(enemyCatalog);
            serializedEnemyCatalog.Update();
            DrawPropertyField(serializedEnemyCatalog, "definitions", "敌人定义列表", includeChildren: true);
            serializedEnemyCatalog.ApplyModifiedProperties();
            EditorUtility.SetDirty(enemyCatalog);
        }

        private void DrawSmokeTestSection()
        {
            EditorGUILayout.HelpBox(
                "这一步专门验证当前关卡能不能真正开始玩。它会直接检查三件事：\n1. 任意可建造区里能不能放下第一座继电器。\n2. 放下继电器后，继电器覆盖区里能不能放下首座战斗塔。\n3. 第一波敌人能不能真正刷出来。\n建议每次新建关卡壳层、改完路径、改完 BuildZone 或波次后都点一次。",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("运行当前关卡烟测", GUILayout.Height(28f)))
                {
                    LevelSceneSanityProbe.ProbeCurrentScene();
                    RefreshSmokeTestReport();
                }

                if (GUILayout.Button("刷新烟测结果", GUILayout.Height(28f)))
                {
                    RefreshSmokeTestReport();
                }

                if (GUILayout.Button("复制报告路径", GUILayout.Height(28f)))
                {
                    EditorGUIUtility.systemCopyBuffer = LevelSceneSanityProbe.ReportFilePath;
                }
            }

            EditorGUILayout.HelpBox(latestSmokeSummary, latestSmokeMessageType);

            EditorGUILayout.LabelField("烟测报告路径", EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel(LevelSceneSanityProbe.ReportFilePath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight + 6f));

            EditorGUILayout.LabelField("详细报告", EditorStyles.miniBoldLabel);
            using (var scroll = new EditorGUILayout.ScrollViewScope(smokeReportScrollPosition, GUILayout.MinHeight(180f)))
            {
                smokeReportScrollPosition = scroll.scrollPosition;
                EditorGUILayout.TextArea(
                    string.IsNullOrWhiteSpace(latestSmokeReport)
                        ? "当前还没有烟测报告。先点击“运行当前关卡烟测”。"
                        : latestSmokeReport,
                    GUILayout.ExpandHeight(true));
            }
        }

        private void DrawToolShortcutsSection()
        {
            EditorGUILayout.HelpBox(
                "建议工作顺序：先用“拓扑编辑器”和“路径点工具”搭结构，再用“地图工具箱”补功能层，最后用“道路美术工具”和“数值调参台”收尾。",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开路径点工具"))
                {
                    EnemyPathAuthoringTool.OpenWindow();
                }

                if (GUILayout.Button("打开地图工具箱"))
                {
                    TowerDefenseMapToolkitWindow.OpenWindow();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开关卡拓扑编辑器"))
                {
                    LevelTopologyEditorWindow.OpenWindow();
                }

                if (GUILayout.Button("打开道路美术工具"))
                {
                    RoadArtAuthoringWindow.OpenWindow();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开关卡数值调参台"))
                {
                    LevelBalanceTuningWindow.OpenWindow();
                }
            }
        }

        private void DrawAuthoringRecoverySection()
        {
            EditorGUILayout.HelpBox(
                "在执行批量生成、批量清理、拓扑重构或大规模路段重建前，建议先留一个作者快照。恢复最近快照会直接把当前场景文件替换回最近一次快照版本，所以这是高风险动作。",
                MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("创建当前场景快照", GUILayout.Height(28f)))
                {
                    if (TowerDefenseAuthoringSnapshotUtility.TryCreateSnapshotForActiveScene(out string snapshotPath, out string errorMessage))
                    {
                        EditorUtility.DisplayDialog("快照已创建", $"已创建作者快照：\n{snapshotPath}", "确定");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("快照创建失败", errorMessage, "确定");
                    }
                }

                if (GUILayout.Button("恢复最近快照", GUILayout.Height(28f)))
                {
                    bool confirmed = EditorUtility.DisplayDialog(
                        "恢复最近快照",
                        "这个操作会用最近的作者快照直接覆盖当前场景文件。是否继续？",
                        "继续",
                        "取消");
                    if (!confirmed)
                    {
                        return;
                    }

                    if (TowerDefenseAuthoringSnapshotUtility.TryRestoreLatestSnapshotForActiveScene(out string restoredFromPath, out string errorMessage))
                    {
                        AdoptCurrentSceneContext();
                        EditorUtility.DisplayDialog("已恢复最近快照", $"已从以下快照恢复场景：\n{restoredFromPath}", "确定");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("恢复失败", errorMessage, "确定");
                    }
                }
            }
        }

        private void RefreshSmokeTestReport()
        {
            if (!LevelSceneSanityProbe.TryBuildLatestSummary(out latestSmokeSummary, out latestSmokeMessageType))
            {
                latestSmokeReport = string.Empty;
                return;
            }

            if (!LevelSceneSanityProbe.TryReadLatestReport(out latestSmokeReport))
            {
                latestSmokeReport = string.Empty;
            }
        }

        private void BootstrapCurrentSceneFromSample()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return;
            }

            Scene sampleScene = EditorSceneManager.OpenScene(TowerDefenseMapToolkitUtility.SampleScenePath, OpenSceneMode.Additive);
            try
            {
                TowerDefenseMapToolkitUtility.BootstrapCombatSceneShellFromSample(sampleScene, activeScene, replaceExistingShellRoots);
                EditorSceneManager.SaveScene(activeScene);
            }
            finally
            {
                EditorSceneManager.CloseScene(sampleScene, true);
            }

            if (!string.IsNullOrWhiteSpace(activeScene.path))
            {
                EditorSceneManager.OpenScene(activeScene.path, OpenSceneMode.Single);
            }

            AdoptCurrentSceneContext();
        }

        private void CreateNewSceneAndBootstrap()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            string scenePath = EditorUtility.SaveFilePanelInProject(
                "创建新关卡场景",
                "NewTowerDefenseLevel",
                "unity",
                "请选择新关卡场景的保存位置。");
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return;
            }

            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(newScene, scenePath);

            Scene sampleScene = EditorSceneManager.OpenScene(TowerDefenseMapToolkitUtility.SampleScenePath, OpenSceneMode.Additive);
            try
            {
                TowerDefenseMapToolkitUtility.BootstrapCombatSceneShellFromSample(sampleScene, newScene, replaceExistingShellRoots);
                EditorSceneManager.SaveScene(newScene);
            }
            finally
            {
                EditorSceneManager.CloseScene(sampleScene, true);
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            AdoptCurrentSceneContext();
        }

        private void AdoptCurrentSceneContext()
        {
            TowerDefenseAuthoringSceneContext context = TowerDefenseAuthoringSceneContext.CaptureActiveSceneContext();
            currentGame = context.CurrentGame;
            currentWaveSpawner = context.CurrentWaveSpawner;
            currentMap = context.CurrentMap;
            currentBuildZone = context.CurrentBuildZone;
            currentCamera = context.CurrentCamera;
        }

        private static T FindFirstComponentInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .FirstOrDefault(component => component != null);
        }

        private static void DrawPropertyField(SerializedObject serializedObject, string propertyPath, string label, bool includeChildren = false)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label), includeChildren);
            }
        }

        private static void DrawTransformFields(Transform targetTransform, string positionLabel, string scaleLabel)
        {
            if (targetTransform == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(positionLabel))
            {
                EditorGUI.BeginChangeCheck();
                Vector3 position = EditorGUILayout.Vector3Field(positionLabel, targetTransform.position);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(targetTransform, $"修改 {positionLabel}");
                    targetTransform.position = position;
                    EditorUtility.SetDirty(targetTransform);
                }
            }

            if (!string.IsNullOrWhiteSpace(scaleLabel))
            {
                EditorGUI.BeginChangeCheck();
                Vector3 scale = EditorGUILayout.Vector3Field(scaleLabel, targetTransform.localScale);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(targetTransform, $"修改 {scaleLabel}");
                    targetTransform.localScale = scale;
                    EditorUtility.SetDirty(targetTransform);
                }
            }
        }

        private static void DrawPingButton(UnityEngine.Object targetObject, string label)
        {
            using (new EditorGUI.DisabledScope(targetObject == null))
            {
                if (GUILayout.Button(label))
                {
                    Selection.activeObject = targetObject;
                    EditorGUIUtility.PingObject(targetObject);
                }
            }
        }

        private static WaveCatalogAsset ResolveWaveCatalog(SerializedObject serializedSpawner)
        {
            SerializedProperty property = serializedSpawner.FindProperty("waveCatalogAsset");
            return property != null ? property.objectReferenceValue as WaveCatalogAsset : null;
        }

        private static EnemyCatalogAsset ResolveEnemyCatalog(SerializedObject serializedSpawner)
        {
            SerializedProperty property = serializedSpawner.FindProperty("enemyCatalogAsset");
            return property != null ? property.objectReferenceValue as EnemyCatalogAsset : null;
        }
    }
}

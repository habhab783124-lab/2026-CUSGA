using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense.Editor
{
    /// <summary>
    /// 关卡运行态烟测工具。
    ///
    /// 设计目标很明确：
    /// - 不去做“完整通关测试”
    /// - 只回答地图作者最关心的最小可玩性问题
    ///
    /// 当前烟测固定检查三件事：
    /// 1. 任意可建造区里能不能放下第一座继电器
    /// 2. 放下继电器后，继电器覆盖区里能不能放下第一座战斗塔
    /// 3. 第一波敌人能不能真正刷出来
    ///
    /// 为什么要做成编辑器内烟测：
    /// - 比起依赖批处理时机，编辑器里手动点一下更稳定
    /// - 比起纯日志，这里会输出结构化中文报告，方便策划和地图作者直接看
    /// </summary>
    public static class LevelSceneSanityProbe
    {
        private const string ReportFileName = "level-runtime-smoke-report.txt";
        private static readonly StringBuilder ProbeLog = new StringBuilder();

        /// <summary>
        /// 统一的报告落盘位置。
        /// 放在项目根目录下的 `Temp/`，方便：
        /// - 不污染 Assets
        /// - 被工具窗口稳定读取
        /// - 需要时也能手工打开
        /// </summary>
        public static string ReportFilePath =>
            Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "Temp", ReportFileName);

        [MenuItem("Tools/Tower Defense/Validation/运行当前关卡烟测")]
        public static void ProbeCurrentScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                Debug.LogWarning("[关卡烟测] 当前没有可用场景，无法执行烟测。");
                return;
            }

            ProbeLoadedScene(activeScene, string.IsNullOrWhiteSpace(activeScene.path) ? activeScene.name : activeScene.path, saveAfterProbe: false);
        }

        public static void ProbeLevel02()
        {
            ProbeScene("Assets/Scenes/Level02.unity");
        }

        public static void ProbeLevel03()
        {
            ProbeScene("Assets/Scenes/Level03.unity");
        }

        public static void ProbeLevel04()
        {
            ProbeScene("Assets/Scenes/Level04.unity");
        }

        /// <summary>
        /// 让工作台可以直接读到最近一次报告正文。
        /// </summary>
        public static bool TryReadLatestReport(out string reportText)
        {
            if (!File.Exists(ReportFilePath))
            {
                reportText = string.Empty;
                return false;
            }

            reportText = File.ReadAllText(ReportFilePath, Encoding.UTF8);
            return true;
        }

        /// <summary>
        /// 让工作台拿到一个适合 HelpBox 显示的摘要。
        /// </summary>
        public static bool TryBuildLatestSummary(out string summary, out MessageType messageType)
        {
            if (!TryReadLatestReport(out string reportText) || string.IsNullOrWhiteSpace(reportText))
            {
                summary = "当前还没有烟测结果。先点击“运行当前关卡烟测”。";
                messageType = MessageType.Info;
                return false;
            }

            string[] lines = reportText
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();

            string sceneLine = lines.FirstOrDefault(line => line.StartsWith("[关卡烟测] 场景：", StringComparison.Ordinal)) ?? "[关卡烟测] 场景：未知";
            string relayLine = lines.FirstOrDefault(line => line.StartsWith("[关卡烟测] 初始继电器放置：", StringComparison.Ordinal)) ?? "[关卡烟测] 初始继电器放置：未记录";
            string towerLine = lines.FirstOrDefault(line => line.StartsWith("[关卡烟测] 首座战斗塔放置：", StringComparison.Ordinal)) ?? "[关卡烟测] 首座战斗塔放置：未记录";
            string waveLine = lines.FirstOrDefault(line => line.StartsWith("[关卡烟测] 运行时波次数：", StringComparison.Ordinal)) ?? "[关卡烟测] 运行时波次数：未记录";
            string spawnLine = lines.FirstOrDefault(line => line.StartsWith("[关卡烟测] 首只敌人刷出：", StringComparison.Ordinal)) ?? "[关卡烟测] 首只敌人刷出：未记录";
            string verdictLine = lines.FirstOrDefault(line => line.StartsWith("[关卡烟测] 综合结论：", StringComparison.Ordinal)) ?? "[关卡烟测] 综合结论：未记录";

            summary = string.Join("\n", new[]
            {
                sceneLine,
                relayLine,
                towerLine,
                waveLine,
                spawnLine,
                verdictLine
            });

            messageType = verdictLine.Contains("通过", StringComparison.Ordinal)
                ? MessageType.Info
                : MessageType.Warning;
            return true;
        }

        private static void ProbeScene(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            ProbeLoadedScene(scene, scenePath, saveAfterProbe: false);
        }

        /// <summary>
        /// 真正执行烟测的入口。
        /// </summary>
        private static void ProbeLoadedScene(Scene scene, string sceneLabel, bool saveAfterProbe)
        {
            ProbeLog.Clear();
            AppendLine($"[关卡烟测] 场景：{sceneLabel}");

            TowerDefenseGame game = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TowerDefenseGame>(true))
                .FirstOrDefault(component => component != null);
            WaveSpawner waveSpawner = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WaveSpawner>(true))
                .FirstOrDefault(component => component != null);

            if (game != null)
            {
                InvokePrivateInstanceMethod(game, "Awake");
                InvokePrivateInstanceMethod(game, "Start");
                ProbePlacement(game);
            }
            else
            {
                AppendLine("[关卡烟测] 缺少 TowerDefenseGame。");
            }

            if (waveSpawner != null)
            {
                InvokePrivateInstanceMethod(waveSpawner, "Start");
                ProbeSpawner(waveSpawner);
            }
            else
            {
                AppendLine("[关卡烟测] 缺少 WaveSpawner。");
            }

            AppendVerdict();
            WriteReport();
            Debug.Log(ProbeLog.ToString());

            if (saveAfterProbe)
            {
                EditorSceneManager.SaveScene(scene);
            }
        }

        /// <summary>
        /// 使用关卡真实规则检查“新规则下的最小可玩性”。
        ///
        /// 检查顺序改成：
        /// 1. 先在任意可建造区里找到一个能放继电器的位置
        /// 2. 临时放下一座继电器
        /// 3. 再在它的供电范围里检查第一座战斗塔是否能放
        /// </summary>
        private static void ProbePlacement(TowerDefenseGame game)
        {
            MethodInfo validatePlacementMethod = typeof(TowerDefenseGame)
                .GetMethod("ValidatePlacementPosition", BindingFlags.Instance | BindingFlags.NonPublic);
            if (validatePlacementMethod == null)
            {
                AppendLine("[关卡烟测] 无法找到放置校验入口。");
                return;
            }

            BuildZone buildZone = UnityEngine.Object.FindFirstObjectByType<BuildZone>();
            if (buildZone == null)
            {
                AppendLine("[关卡烟测] 初始继电器放置：失败（缺少 BuildZone）");
                AppendLine("[关卡烟测] 首座战斗塔放置：失败（缺少 BuildZone）");
                return;
            }

            if (!TryFindValidPlacementPoint(validatePlacementMethod, game, buildZone.WorldBounds, TowerType.Relay, out Vector3 relayPoint, out string relayFailureReason))
            {
                AppendLine($"[关卡烟测] 初始继电器放置：失败（{relayFailureReason}）");
                AppendLine("[关卡烟测] 首座战斗塔放置：失败（因为第一座继电器都放不下）");
                return;
            }

            AppendLine($"[关卡烟测] 初始继电器放置：成功（{relayPoint}）");

            Transform placedTowerRoot = ResolvePlacedTowerRoot(game);
            int originalChildCount = placedTowerRoot != null ? placedTowerRoot.childCount : 0;
            int originalScrap = ResolveCurrentScrap(game);

            if (!TryPlaceTower(game, relayPoint, TowerType.Relay))
            {
                AppendLine("[关卡烟测] 首座战斗塔放置：失败（烟测临时继电器落地失败）");
                return;
            }

            Vector3 towerProbeOrigin = relayPoint + new Vector3(1.5f, 0f, 0f);
            Bounds localBounds = new Bounds(towerProbeOrigin, new Vector3(6f, 6f, 0f));
            if (!TryFindValidPlacementPoint(validatePlacementMethod, game, localBounds, TowerType.SingleTarget, out Vector3 towerPoint, out string towerFailureReason))
            {
                AppendLine($"[关卡烟测] 首座战斗塔放置：失败（{towerFailureReason}）");
            }
            else
            {
                AppendLine($"[关卡烟测] 首座战斗塔放置：成功（{towerPoint}）");
            }

            CleanupPlacedStructures(placedTowerRoot, originalChildCount);
            RestoreCurrentScrap(game, originalScrap);
        }

        /// <summary>
        /// 使用运行时已构建好的 `_runtimeWaves` 检查刷怪链。
        /// </summary>
        private static void ProbeSpawner(WaveSpawner waveSpawner)
        {
            FieldInfo runtimeWavesField = typeof(WaveSpawner).GetField("_runtimeWaves", BindingFlags.Instance | BindingFlags.NonPublic);
            IList runtimeWaves = runtimeWavesField != null ? runtimeWavesField.GetValue(waveSpawner) as IList : null;
            AppendLine($"[关卡烟测] 运行时波次数：{(runtimeWaves != null ? runtimeWaves.Count : -1)}");

            if (runtimeWaves == null || runtimeWaves.Count == 0)
            {
                AppendLine("[关卡烟测] 首只敌人刷出：失败（没有可用运行时波次）");
                return;
            }

            object firstWave = runtimeWaves[0];
            FieldInfo groupsField = firstWave.GetType().GetField("Groups", BindingFlags.Instance | BindingFlags.Public);
            IList groups = groupsField != null ? groupsField.GetValue(firstWave) as IList : null;
            AppendLine($"[关卡烟测] 首波刷怪组数：{(groups != null ? groups.Count : -1)}");
            if (groups == null || groups.Count == 0)
            {
                AppendLine("[关卡烟测] 首只敌人刷出：失败（首波没有可用刷怪组）");
                return;
            }

            MethodInfo spawnEnemyMethod = typeof(WaveSpawner).GetMethod("SpawnEnemy", BindingFlags.Instance | BindingFlags.NonPublic);
            object firstGroup = groups[0];
            Transform enemyRoot = ResolveEnemyRoot(waveSpawner);
            var existingChildren = enemyRoot != null
                ? enemyRoot.Cast<Transform>().Select(child => child != null ? child.gameObject : null).Where(child => child != null).ToList()
                : new System.Collections.Generic.List<GameObject>();

            object[] spawnArguments = { firstGroup, 1, 1 };
            bool spawned = spawnEnemyMethod != null && (bool)spawnEnemyMethod.Invoke(waveSpawner, spawnArguments);
            AppendLine(spawned
                ? "[关卡烟测] 首只敌人刷出：成功"
                : "[关卡烟测] 首只敌人刷出：失败");

            if (spawned && enemyRoot != null)
            {
                foreach (Transform child in enemyRoot)
                {
                    if (child == null)
                    {
                        continue;
                    }

                    if (!existingChildren.Contains(child.gameObject))
                    {
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                    }
                }
            }
        }

        private static bool TryFindValidPlacementPoint(
            MethodInfo validatePlacementMethod,
            TowerDefenseGame game,
            Bounds searchBounds,
            TowerType towerType,
            out Vector3 validPoint,
            out string failureReason)
        {
            validPoint = Vector3.zero;
            failureReason = "没有找到合法落点";

            const int sampleCountPerAxis = 8;
            for (int xIndex = 0; xIndex < sampleCountPerAxis; xIndex++)
            {
                for (int yIndex = 0; yIndex < sampleCountPerAxis; yIndex++)
                {
                    float x = Mathf.Lerp(searchBounds.min.x, searchBounds.max.x, xIndex / (float)(sampleCountPerAxis - 1));
                    float y = Mathf.Lerp(searchBounds.min.y, searchBounds.max.y, yIndex / (float)(sampleCountPerAxis - 1));
                    Vector3 samplePoint = new Vector3(x, y, 0f);

                    object[] arguments = { samplePoint, towerType, null };
                    bool valid = (bool)validatePlacementMethod.Invoke(game, arguments);
                    if (valid)
                    {
                        validPoint = samplePoint;
                        return true;
                    }

                    if (arguments[2] is string invalidReason && !string.IsNullOrWhiteSpace(invalidReason))
                    {
                        failureReason = invalidReason;
                    }
                }
            }

            return false;
        }

        private static void AppendVerdict()
        {
            bool relayOk = ProbeLog.ToString().Contains("[关卡烟测] 初始继电器放置：成功", StringComparison.Ordinal);
            bool towerOk = ProbeLog.ToString().Contains("[关卡烟测] 首座战斗塔放置：成功", StringComparison.Ordinal);
            bool spawnOk = ProbeLog.ToString().Contains("[关卡烟测] 首只敌人刷出：成功", StringComparison.Ordinal);

            AppendLine(relayOk && towerOk && spawnOk
                ? "[关卡烟测] 综合结论：通过"
                : "[关卡烟测] 综合结论：未通过");
        }

        private static void WriteReport()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportFilePath) ?? Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "Temp"));
            File.WriteAllText(ReportFilePath, ProbeLog.ToString(), Encoding.UTF8);
        }

        private static Transform ResolveEnemyRoot(WaveSpawner waveSpawner)
        {
            FieldInfo enemyRootField = typeof(WaveSpawner).GetField("_enemyRoot", BindingFlags.Instance | BindingFlags.NonPublic);
            return enemyRootField != null ? enemyRootField.GetValue(waveSpawner) as Transform : null;
        }

        private static Transform ResolvePlacedTowerRoot(TowerDefenseGame game)
        {
            FieldInfo placedTowerRootField = typeof(TowerDefenseGame).GetField("_placedTowerRoot", BindingFlags.Instance | BindingFlags.NonPublic);
            return placedTowerRootField != null ? placedTowerRootField.GetValue(game) as Transform : null;
        }

        private static int ResolveCurrentScrap(TowerDefenseGame game)
        {
            FieldInfo sessionStateField = typeof(TowerDefenseGame).GetField("_sessionState", BindingFlags.Instance | BindingFlags.NonPublic);
            TowerDefenseSessionState sessionState = sessionStateField != null ? sessionStateField.GetValue(game) as TowerDefenseSessionState : null;
            return sessionState != null ? sessionState.CurrentScrap : 0;
        }

        private static void RestoreCurrentScrap(TowerDefenseGame game, int scrap)
        {
            FieldInfo sessionStateField = typeof(TowerDefenseGame).GetField("_sessionState", BindingFlags.Instance | BindingFlags.NonPublic);
            TowerDefenseSessionState sessionState = sessionStateField != null ? sessionStateField.GetValue(game) as TowerDefenseSessionState : null;
            sessionState?.SetCurrentScrap(scrap);
        }

        private static bool TryPlaceTower(TowerDefenseGame game, Vector3 worldPosition, TowerType towerType)
        {
            MethodInfo tryPlaceMethod = typeof(TowerDefenseGame).GetMethod("TryPlaceTowerAt", BindingFlags.Instance | BindingFlags.NonPublic);
            if (tryPlaceMethod == null)
            {
                return false;
            }

            object[] arguments = { worldPosition, towerType, null };
            return (bool)tryPlaceMethod.Invoke(game, arguments);
        }

        private static void CleanupPlacedStructures(Transform placedTowerRoot, int keepChildCount)
        {
            if (placedTowerRoot == null)
            {
                return;
            }

            for (int childIndex = placedTowerRoot.childCount - 1; childIndex >= keepChildCount; childIndex--)
            {
                Transform child = placedTowerRoot.GetChild(childIndex);
                if (child != null)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static void InvokePrivateInstanceMethod(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, null);
        }

        private static void AppendLine(string message)
        {
            ProbeLog.AppendLine(message);
        }
    }
}

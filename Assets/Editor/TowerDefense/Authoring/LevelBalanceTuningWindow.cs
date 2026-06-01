using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TowerDefense.Editor
{
    internal enum BalancePresetKind
    {
        Simple,
        Standard,
        Hard
    }

    /// <summary>
    /// One-stop tuning console for level designers and gameplay designers.
    ///
    /// Design intent:
    /// - The user asked for a planner-facing tool, not another scattered set of Inspectors.
    /// - Current balance knobs live in several places:
    ///   - scene objects (`TowerDefenseGame`, `WaveSpawner`, `BattlefieldMapDefinition`)
    ///   - referenced prefabs (`RelayTower`, `DefenseTower`)
    /// - Designers should be able to open one window, inspect the current level, and adjust
    ///   the major balance parameters without hunting through the hierarchy and asset folders.
    ///
    /// Scope of this first version:
    /// - current level economy and placement rules
    /// - route / relay scene limits
    /// - wave timing and wave-by-wave enemy values
    /// - relay prefab tuning
    /// - three combat tower prefab tunings
    /// - quick batch helpers for common balance passes
    ///
    /// Non-goals:
    /// - replacing every Inspector in the project
    /// - hiding scene ownership; the scene and prefab assets still remain the source of truth
    /// - inventing a new runtime data model just for tooling
    /// </summary>
    public sealed class LevelBalanceTuningWindow : EditorWindow
    {
        /// <summary>
        /// One preset bundle used by the planner-facing preset buttons.
        ///
        /// These presets intentionally operate on top of the current authored numbers instead of
        /// introducing a second hidden balance database. That keeps the workflow transparent:
        /// the scene and prefab assets still remain the single source of truth.
        /// </summary>
        private sealed class BalancePresetDefinition
        {
            public BalancePresetKind Kind;
            public string Label;
            public string Description;
            public float StartingScrapMultiplier = 1f;
            public float StartingBaseHealthMultiplier = 1f;
            public float BuildCostMultiplier = 1f;
            public float UpgradeCostMultiplier = 1f;
            public int RelayLimitDelta;
            public float WaveCountMultiplier = 1f;
            public float WaveHealthMultiplier = 1f;
            public float WaveSpeedMultiplier = 1f;
            public float WaveRewardMultiplier = 1f;
            public float WaveIntervalMultiplier = 1f;
            public float RelayRangeMultiplier = 1f;
            public int RelayCapacityDelta;
            public float TowerRangeMultiplier = 1f;
            public float TowerAttackIntervalMultiplier = 1f;
            public float TowerDamageMultiplier = 1f;
            public float TowerPowerMultiplier = 1f;
            public float BombRadiusMultiplier = 1f;
            public float SlowStrengthMultiplier = 1f;
        }

        [SerializeField] private TowerDefenseGame currentGame;
        [SerializeField] private WaveSpawner currentWaveSpawner;
        [SerializeField] private BattlefieldMapDefinition currentMap;

        [SerializeField] private bool showPresets = true;
        [SerializeField] private bool showCoreEconomy = true;
        [SerializeField] private bool showWaveTuning = true;
        [SerializeField] private bool showFallbackSceneWaveArray = false;
        [SerializeField] private bool showRelayTuning = true;
        [SerializeField] private bool showSingleTargetTuning = true;
        [SerializeField] private bool showSlowFieldTuning = true;
        [SerializeField] private bool showBombardTuning = true;
        [SerializeField] private bool showQuickBatchTools = true;
        [SerializeField] private bool showAdvancedRawEditors = false;

        [SerializeField] private float waveCountMultiplier = 1.1f;
        [SerializeField] private float waveHealthMultiplier = 1.15f;
        [SerializeField] private float waveSpeedMultiplier = 1.05f;
        [SerializeField] private float waveRewardMultiplier = 1f;
        [SerializeField] private float waveIntervalMultiplier = 0.95f;
        [SerializeField] private float buildCostMultiplier = 1.1f;
        [SerializeField] private float upgradeCostMultiplier = 1.1f;
        [SerializeField] private Vector2 scrollPosition;

        [MenuItem("Tools/Tower Defense/Authoring/关卡数值调参台")]
        public static void OpenWindow()
        {
            LevelBalanceTuningWindow window = GetWindow<LevelBalanceTuningWindow>("关卡数值");
            window.minSize = new Vector2(720f, 520f);
            window.AdoptCurrentSceneContext();
        }

        private void OnEnable()
        {
            AdoptCurrentSceneContext();
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            try
            {
                DrawHeader();
                EditorGUILayout.Space(8f);

                if (currentGame == null && currentWaveSpawner == null && currentMap == null)
                {
                    EditorGUILayout.HelpBox(
                        "当前还没有接管关卡上下文。请先打开一个战斗关卡场景，再点“接管当前场景”。",
                        MessageType.Warning);
                    return;
                }

                DrawSceneSummary();
                EditorGUILayout.Space(8f);

                showPresets = EditorGUILayout.Foldout(showPresets, "难度预设", true);
                if (showPresets)
                {
                    DrawPresetSection();
                }

                showCoreEconomy = EditorGUILayout.Foldout(showCoreEconomy, "核心经济与部署规则", true);
                if (showCoreEconomy)
                {
                    DrawCoreEconomySection();
                }

                showWaveTuning = EditorGUILayout.Foldout(showWaveTuning, "波次调参", true);
                if (showWaveTuning)
                {
                    DrawWaveSection();
                }

                showRelayTuning = EditorGUILayout.Foldout(showRelayTuning, "继电器原型调参", true);
                if (showRelayTuning)
                {
                    DrawRelaySection();
                }

                showSingleTargetTuning = EditorGUILayout.Foldout(showSingleTargetTuning, "单体塔调参", true);
                if (showSingleTargetTuning)
                {
                    DrawDefenseTowerSection(
                        "singleTargetTowerPrototypeReference",
                        "singleTargetTuning",
                        "单体塔原型资源");
                }

                showSlowFieldTuning = EditorGUILayout.Foldout(showSlowFieldTuning, "减速塔调参", true);
                if (showSlowFieldTuning)
                {
                    DrawDefenseTowerSection(
                        "slowFieldTowerPrototypeReference",
                        "slowFieldTuning",
                        "减速塔原型资源");
                }

                showBombardTuning = EditorGUILayout.Foldout(showBombardTuning, "炸弹塔调参", true);
                if (showBombardTuning)
                {
                    DrawDefenseTowerSection(
                        "bombardTowerPrototypeReference",
                        "bombardTuning",
                        "炸弹塔原型资源");
                }

                showQuickBatchTools = EditorGUILayout.Foldout(showQuickBatchTools, "批量调参快捷工具", true);
                if (showQuickBatchTools)
                {
                    DrawQuickBatchSection();
                }

                showAdvancedRawEditors = EditorGUILayout.Foldout(showAdvancedRawEditors, "高级原始参数面板", true);
                if (showAdvancedRawEditors)
                {
                    DrawAdvancedRawEditors();
                }
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        /// <summary>
        /// Keeps the top bar practical and safe.
        ///
        /// Designers often bounce between scenes while tuning, so the window always exposes:
        /// - a refresh button for current scene adoption
        /// - a save button for the active scene and dirty assets
        /// - explicit object fields in case the user wants to pin a different context manually
        /// </summary>
        private void DrawHeader()
        {
            EditorGUILayout.LabelField("关卡数值调参台", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("接管当前场景", GUILayout.Width(160f)))
            {
                AdoptCurrentSceneContext();
            }

            if (GUILayout.Button("保存场景与资源", GUILayout.Width(160f)))
            {
                SaveCurrentWork();
            }
            EditorGUILayout.EndHorizontal();

            currentGame = (TowerDefenseGame)EditorGUILayout.ObjectField("总控", currentGame, typeof(TowerDefenseGame), true);
            currentWaveSpawner = (WaveSpawner)EditorGUILayout.ObjectField("刷怪器", currentWaveSpawner, typeof(WaveSpawner), true);
            currentMap = (BattlefieldMapDefinition)EditorGUILayout.ObjectField("地图入口", currentMap, typeof(BattlefieldMapDefinition), true);
        }

        private void DrawSceneSummary()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            string sceneName = string.IsNullOrWhiteSpace(activeScene.name) ? "(无场景)" : activeScene.name;
            TowerDefenseAuthoringSceneContext sharedContext = TowerDefenseAuthoringSceneContext.GetOrCreate();

            int gateCount = currentMap != null ? currentMap.SpawnGateCount : 0;
            int defenseCount = currentMap != null ? currentMap.DefensePointCount : 0;
            int relayLimit = currentMap != null ? currentMap.RelayLimit : 0;

            EditorGUILayout.HelpBox(
                $"当前场景：{sceneName}\n" +
                $"出怪口：{gateCount} | 防御点：{defenseCount} | 继电器上限：{relayLimit}\n" +
                $"这个窗口会直接修改当前关卡场景，以及该场景引用到的原型 Prefab / 资产。",
                MessageType.Info);

            EditorGUILayout.HelpBox(sharedContext.BuildSummary(), MessageType.None);
        }

        /// <summary>
        /// Draws one-click preset actions for planners.
        ///
        /// The presets are intentionally opinionated:
        /// - Easy gives the player more economy and slightly gentler waves
        /// - Standard is the neutral reference point
        /// - Hard squeezes economy and makes waves scale up faster
        ///
        /// The section also mirrors the preset multipliers into the existing batch-helper fields
        /// so designers can see the numbers they just applied.
        /// </summary>
        private void DrawPresetSection()
        {
            EditorGUILayout.HelpBox(
                "预设会直接作用在当前关卡现有数值之上。建议先用它快速打一个难度基线，再在下方做细调。",
                MessageType.Warning);

            DrawPresetCard(BalancePresetKind.Simple);
            DrawPresetCard(BalancePresetKind.Standard);
            DrawPresetCard(BalancePresetKind.Hard);
        }

        private void DrawPresetCard(BalancePresetKind presetKind)
        {
            BalancePresetDefinition preset = GetPresetDefinition(presetKind);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(preset.Label, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(preset.Description, EditorStyles.wordWrappedMiniLabel);

            if (GUILayout.Button($"应用 {preset.Label} 预设"))
            {
                ApplyPreset(preset);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawCoreEconomySection()
        {
            if (currentGame == null)
            {
                EditorGUILayout.HelpBox("当前必须先找到 TowerDefenseGame，才能调核心经济参数。", MessageType.Warning);
                return;
            }

            SerializedObject serializedGame = new SerializedObject(currentGame);
            serializedGame.Update();

            EditorGUILayout.LabelField("开局资源", EditorStyles.miniBoldLabel);
            DrawPropertyField(serializedGame, "startingScrap");
            DrawPropertyField(serializedGame, "startingBaseHealth");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("建造成本", EditorStyles.miniBoldLabel);
            DrawPropertyField(serializedGame, "relayTowerCost");
            DrawPropertyField(serializedGame, "singleTargetTowerCost");
            DrawPropertyField(serializedGame, "slowFieldTowerCost");
            DrawPropertyField(serializedGame, "bombardTowerCost");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("部署规则", EditorStyles.miniBoldLabel);
            DrawPropertyField(serializedGame, "relayExpansionSquareSize");
            DrawPropertyField(serializedGame, "defenseExpansionSquareSize");
            EditorGUILayout.HelpBox("占地 / 不可放置半径现在改为从各 tower prefab 的 CircleCollider2D 读取。这里不再把 relayPlacementRadius / defensePlacementRadius 作为权威来源。", MessageType.Info);

            serializedGame.ApplyModifiedProperties();
            EditorUtility.SetDirty(currentGame);

            if (currentMap != null)
            {
                SerializedObject serializedMap = new SerializedObject(currentMap);
                serializedMap.Update();
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("地图限制", EditorStyles.miniBoldLabel);
                DrawPropertyField(serializedMap, "relayLimit");
                serializedMap.ApplyModifiedProperties();
                EditorUtility.SetDirty(currentMap);
            }
        }

        private void DrawWaveSection()
        {
            if (currentWaveSpawner == null)
            {
                EditorGUILayout.HelpBox("当前必须先找到 WaveSpawner，才能调波次。", MessageType.Warning);
                return;
            }

            SerializedObject serializedSpawner = new SerializedObject(currentWaveSpawner);
            serializedSpawner.Update();

            EditorGUILayout.LabelField("波次时序", EditorStyles.miniBoldLabel);
            DrawPropertyField(serializedSpawner, "initialDelay");
            DrawPropertyField(serializedSpawner, "delayBetweenWaves");
            DrawPropertyField(serializedSpawner, "routePreviewLeadTime");
            DrawPropertyField(serializedSpawner, "continueCampaignAfterClear");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("波次数据来源", EditorStyles.miniBoldLabel);
            DrawPropertyField(serializedSpawner, "waveCatalogAsset");
            DrawPropertyField(serializedSpawner, "enemyCatalogAsset");

            EditorGUILayout.Space(4f);
            WaveCatalogAsset resolvedWaveCatalog = ResolveWaveCatalogAsset();
            if (resolvedWaveCatalog != null)
            {
                EditorGUILayout.HelpBox(
                    "当前场景已经切到 WaveCatalogAsset 主工作流。建议直接在这里改波次组，场景里的 fallback waves 只保留给兼容兜底。",
                    MessageType.Info);

                SerializedObject serializedCatalog = new SerializedObject(resolvedWaveCatalog);
                serializedCatalog.Update();
                DrawPropertyField(serializedCatalog, "waves", includeChildren: true);
                serializedCatalog.ApplyModifiedProperties();
                EditorUtility.SetDirty(resolvedWaveCatalog);
                DrawPingButton(resolvedWaveCatalog, "定位波次目录");

                showFallbackSceneWaveArray = EditorGUILayout.Foldout(showFallbackSceneWaveArray, "兼容兜底场景波次数组", true);
                if (showFallbackSceneWaveArray)
                {
                    DrawPropertyField(serializedSpawner, "waves", includeChildren: true);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "当前还没有接 WaveCatalogAsset，所以这个场景仍在使用旧的场景波次数组。",
                    MessageType.Warning);
                EditorGUILayout.LabelField("场景波次数组", EditorStyles.miniBoldLabel);
                DrawPropertyField(serializedSpawner, "waves", includeChildren: true);
            }

            serializedSpawner.ApplyModifiedProperties();
            EditorUtility.SetDirty(currentWaveSpawner);
            DrawWavePathReferenceEditor();
        }

        private void DrawRelaySection()
        {
            RelayTower relayPrototype = ResolveRelayPrototype();
            if (relayPrototype == null)
            {
                EditorGUILayout.HelpBox("当前无法从 TowerDefenseGame 解析出继电器原型。", MessageType.Warning);
                return;
            }

            SerializedObject serializedRelay = new SerializedObject(relayPrototype);
            serializedRelay.Update();

            DrawPropertyField(serializedRelay, "supplyRange");
            DrawPropertyField(serializedRelay, "baseSupplyCapacity");
            DrawPropertyField(serializedRelay, "supplyCapacityPerUpgrade");
            DrawPropertyField(serializedRelay, "currentLevel");
            DrawPropertyField(serializedRelay, "maxLevel");
            DrawPropertyField(serializedRelay, "upgradeCostBase");
            DrawPropertyField(serializedRelay, "upgradeCostPerLevel");

            serializedRelay.ApplyModifiedProperties();
            EditorUtility.SetDirty(relayPrototype);

            DrawPingButton(relayPrototype, "定位继电器原型");
        }

        private void DrawDefenseTowerSection(string prefabPropertyName, string tuningPropertyName, string missingMessage)
        {
            DefenseTower towerPrototype = ResolveDefensePrototype(prefabPropertyName);
            if (towerPrototype == null)
            {
                EditorGUILayout.HelpBox($"当前无法解析：{missingMessage}", MessageType.Warning);
                return;
            }

            SerializedObject serializedTower = new SerializedObject(towerPrototype);
            serializedTower.Update();

            DrawPropertyField(serializedTower, "buildType");
            DrawPropertyField(serializedTower, "currentLevel");
            DrawPropertyField(serializedTower, "maxLevel");

            EditorGUILayout.Space(4f);
            DrawTuningSubset(serializedTower, tuningPropertyName);

            serializedTower.ApplyModifiedProperties();
            EditorUtility.SetDirty(towerPrototype);

            DrawPingButton(towerPrototype, "定位塔原型");
        }

        /// <summary>
        /// Exposes only the fields that genuinely influence balance.
        ///
        /// The nested tuning blocks also contain visual and feedback settings.
        /// For a planner-facing balance console, we keep the first layer focused on gameplay math:
        /// range, interval, damage, power, and upgrade costs.
        /// </summary>
        private void DrawTuningSubset(SerializedObject serializedTower, string tuningPropertyName)
        {
            string[] propertyPaths =
            {
                $"{tuningPropertyName}.attackRange",
                $"{tuningPropertyName}.attackRangePerUpgrade",
                $"{tuningPropertyName}.attackInterval",
                $"{tuningPropertyName}.attackIntervalPerUpgradeDelta",
                $"{tuningPropertyName}.baseDamage",
                $"{tuningPropertyName}.damagePerUpgrade",
                $"{tuningPropertyName}.basePowerRequired",
                $"{tuningPropertyName}.powerRequiredPerUpgrade",
                $"{tuningPropertyName}.upgradeCostBase",
                $"{tuningPropertyName}.upgradeCostPerLevel",
                $"{tuningPropertyName}.slowMultiplier",
                $"{tuningPropertyName}.slowMultiplierPerUpgradeDelta",
                $"{tuningPropertyName}.slowDuration",
                $"{tuningPropertyName}.slowDurationPerUpgrade",
                $"{tuningPropertyName}.bombFlightTime",
                $"{tuningPropertyName}.bombFlightTimePerUpgradeDelta",
                $"{tuningPropertyName}.bombRadius",
                $"{tuningPropertyName}.bombRadiusPerUpgrade"
            };

            foreach (string propertyPath in propertyPaths)
            {
                DrawPropertyField(serializedTower, propertyPath);
            }
        }

        private void DrawQuickBatchSection()
        {
            EditorGUILayout.HelpBox(
                "这些快捷工具适合快速做一轮平衡性调节。它们只改数值，不会去动引用和视觉资源。",
                MessageType.None);

            waveCountMultiplier = EditorGUILayout.FloatField("敌人数倍率", waveCountMultiplier);
            waveHealthMultiplier = EditorGUILayout.FloatField("敌人生命倍率", waveHealthMultiplier);
            waveSpeedMultiplier = EditorGUILayout.FloatField("敌人速度倍率", waveSpeedMultiplier);
            waveRewardMultiplier = EditorGUILayout.FloatField("废料奖励倍率", waveRewardMultiplier);
            waveIntervalMultiplier = EditorGUILayout.FloatField("刷怪间隔倍率", waveIntervalMultiplier);
            buildCostMultiplier = EditorGUILayout.FloatField("建造成本倍率", buildCostMultiplier);
            upgradeCostMultiplier = EditorGUILayout.FloatField("升级成本倍率", upgradeCostMultiplier);

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(currentWaveSpawner == null))
            {
                if (GUILayout.Button("按倍率缩放当前波次"))
                {
                    ApplyWaveMultipliers();
                }
            }

            using (new EditorGUI.DisabledScope(currentGame == null))
            {
                if (GUILayout.Button("按倍率缩放建造成本"))
                {
                    ApplyBuildCostMultiplier();
                }
            }

            using (new EditorGUI.DisabledScope(currentGame == null))
            {
                if (GUILayout.Button("按倍率缩放升级成本"))
                {
                    ApplyUpgradeCostMultiplier();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private static BalancePresetDefinition GetPresetDefinition(BalancePresetKind presetKind)
        {
            return presetKind switch
            {
                BalancePresetKind.Simple => new BalancePresetDefinition
                {
                    Kind = presetKind,
                    Label = "简单",
                    Description = "开局更宽松，成长更便宜，波次压力更轻。",
                    StartingScrapMultiplier = 1.25f,
                    StartingBaseHealthMultiplier = 1.25f,
                    BuildCostMultiplier = 0.9f,
                    UpgradeCostMultiplier = 0.9f,
                    RelayLimitDelta = 1,
                    WaveCountMultiplier = 0.85f,
                    WaveHealthMultiplier = 0.85f,
                    WaveSpeedMultiplier = 0.9f,
                    WaveRewardMultiplier = 1.05f,
                    WaveIntervalMultiplier = 1.1f,
                    RelayRangeMultiplier = 1.05f,
                    RelayCapacityDelta = 1,
                    TowerRangeMultiplier = 1.05f,
                    TowerAttackIntervalMultiplier = 0.95f,
                    TowerDamageMultiplier = 1.1f,
                    TowerPowerMultiplier = 0.9f,
                    BombRadiusMultiplier = 1.08f,
                    SlowStrengthMultiplier = 0.92f
                },
                BalancePresetKind.Hard => new BalancePresetDefinition
                {
                    Kind = presetKind,
                    Label = "困难",
                    Description = "经济更紧，波次更密，战斗压迫更高。",
                    StartingScrapMultiplier = 0.85f,
                    StartingBaseHealthMultiplier = 0.85f,
                    BuildCostMultiplier = 1.12f,
                    UpgradeCostMultiplier = 1.15f,
                    RelayLimitDelta = -1,
                    WaveCountMultiplier = 1.2f,
                    WaveHealthMultiplier = 1.2f,
                    WaveSpeedMultiplier = 1.1f,
                    WaveRewardMultiplier = 1.12f,
                    WaveIntervalMultiplier = 0.9f,
                    RelayRangeMultiplier = 0.96f,
                    RelayCapacityDelta = -1,
                    TowerRangeMultiplier = 0.96f,
                    TowerAttackIntervalMultiplier = 1.06f,
                    TowerDamageMultiplier = 0.95f,
                    TowerPowerMultiplier = 1.1f,
                    BombRadiusMultiplier = 0.94f,
                    SlowStrengthMultiplier = 1.08f
                },
                _ => new BalancePresetDefinition
                {
                    Kind = presetKind,
                    Label = "标准",
                    Description = "中性基准档，不额外偏向任何方向。",
                    StartingScrapMultiplier = 1f,
                    StartingBaseHealthMultiplier = 1f,
                    BuildCostMultiplier = 1f,
                    UpgradeCostMultiplier = 1f,
                    RelayLimitDelta = 0,
                    WaveCountMultiplier = 1f,
                    WaveHealthMultiplier = 1f,
                    WaveSpeedMultiplier = 1f,
                    WaveRewardMultiplier = 1f,
                    WaveIntervalMultiplier = 1f,
                    RelayRangeMultiplier = 1f,
                    RelayCapacityDelta = 0,
                    TowerRangeMultiplier = 1f,
                    TowerAttackIntervalMultiplier = 1f,
                    TowerDamageMultiplier = 1f,
                    TowerPowerMultiplier = 1f,
                    BombRadiusMultiplier = 1f,
                    SlowStrengthMultiplier = 1f
                }
            };
        }

        /// <summary>
        /// Applies one preset into the current level context.
        ///
        /// The method intentionally reuses the same scene/prefab sources already used by the
        /// tuning window itself. That means a preset never edits hidden duplicate data; it only
        /// changes the exact scene objects and prototype prefabs the planner is already looking at.
        /// </summary>
        private void ApplyPreset(BalancePresetDefinition preset)
        {
            if (preset == null)
            {
                return;
            }

            // Mirror the preset into the batch helper fields so the UI reflects the new pass.
            waveCountMultiplier = preset.WaveCountMultiplier;
            waveHealthMultiplier = preset.WaveHealthMultiplier;
            waveSpeedMultiplier = preset.WaveSpeedMultiplier;
            waveRewardMultiplier = preset.WaveRewardMultiplier;
            waveIntervalMultiplier = preset.WaveIntervalMultiplier;
            buildCostMultiplier = preset.BuildCostMultiplier;
            upgradeCostMultiplier = preset.UpgradeCostMultiplier;

            ApplyPresetToCoreEconomy(preset);
            ApplyPresetToMapLimit(preset);
            ApplyPresetToWaves(preset);
            ApplyPresetToRelay(preset);
            ApplyPresetToCombatTower("singleTargetTowerPrototypeReference", "singleTargetTuning", preset);
            ApplyPresetToCombatTower("slowFieldTowerPrototypeReference", "slowFieldTuning", preset);
            ApplyPresetToCombatTower("bombardTowerPrototypeReference", "bombardTuning", preset);

            SaveCurrentWork();
        }

        private void ApplyPresetToCoreEconomy(BalancePresetDefinition preset)
        {
            if (currentGame == null)
            {
                return;
            }

            SerializedObject serializedGame = new SerializedObject(currentGame);
            ScaleIntProperty(serializedGame.FindProperty("startingScrap"), preset.StartingScrapMultiplier);
            ScaleIntProperty(serializedGame.FindProperty("startingBaseHealth"), preset.StartingBaseHealthMultiplier);
            ScaleIntProperty(serializedGame.FindProperty("relayTowerCost"), preset.BuildCostMultiplier);
            ScaleIntProperty(serializedGame.FindProperty("singleTargetTowerCost"), preset.BuildCostMultiplier);
            ScaleIntProperty(serializedGame.FindProperty("slowFieldTowerCost"), preset.BuildCostMultiplier);
            ScaleIntProperty(serializedGame.FindProperty("bombardTowerCost"), preset.BuildCostMultiplier);
            serializedGame.ApplyModifiedProperties();
            EditorUtility.SetDirty(currentGame);
            EditorSceneManager.MarkSceneDirty(currentGame.gameObject.scene);
        }

        private void ApplyPresetToMapLimit(BalancePresetDefinition preset)
        {
            if (currentMap == null)
            {
                return;
            }

            SerializedObject serializedMap = new SerializedObject(currentMap);
            SerializedProperty relayLimitProperty = serializedMap.FindProperty("relayLimit");
            if (relayLimitProperty != null)
            {
                relayLimitProperty.intValue = Mathf.Max(0, relayLimitProperty.intValue + preset.RelayLimitDelta);
            }

            serializedMap.ApplyModifiedProperties();
            EditorUtility.SetDirty(currentMap);
            EditorSceneManager.MarkSceneDirty(currentMap.gameObject.scene);
        }

        private void ApplyPresetToWaves(BalancePresetDefinition preset)
        {
            if (currentWaveSpawner == null)
            {
                return;
            }

            SerializedObject serializedSpawner = new SerializedObject(currentWaveSpawner);
            ScaleFloatProperty(serializedSpawner.FindProperty("initialDelay"), preset.WaveIntervalMultiplier, minimum: 0f);
            ScaleFloatProperty(serializedSpawner.FindProperty("delayBetweenWaves"), preset.WaveIntervalMultiplier, minimum: 0f);
            ScaleFloatProperty(serializedSpawner.FindProperty("routePreviewLeadTime"), preset.WaveIntervalMultiplier, minimum: 0f);

            WaveCatalogAsset resolvedWaveCatalog = ResolveWaveCatalogAsset();
            EnemyCatalogAsset resolvedEnemyCatalog = ResolveEnemyCatalogAsset();
            if (resolvedWaveCatalog != null)
            {
                ApplyCatalogWaveMultipliers(resolvedWaveCatalog, resolvedEnemyCatalog, preset.WaveCountMultiplier, preset.WaveIntervalMultiplier, preset.WaveSpeedMultiplier, preset.WaveHealthMultiplier, preset.WaveRewardMultiplier);
            }
            else
            {
                SerializedProperty wavesProperty = serializedSpawner.FindProperty("waves");
                if (wavesProperty != null && wavesProperty.isArray)
                {
                    for (int waveIndex = 0; waveIndex < wavesProperty.arraySize; waveIndex++)
                    {
                        SerializedProperty waveProperty = wavesProperty.GetArrayElementAtIndex(waveIndex);
                        ScaleIntProperty(waveProperty.FindPropertyRelative("enemyCount"), preset.WaveCountMultiplier);
                        ScaleFloatProperty(waveProperty.FindPropertyRelative("spawnInterval"), preset.WaveIntervalMultiplier, minimum: 0.05f);
                        ScaleFloatProperty(waveProperty.FindPropertyRelative("moveSpeed"), preset.WaveSpeedMultiplier, minimum: 0.05f);
                        ScaleIntProperty(waveProperty.FindPropertyRelative("enemyHealth"), preset.WaveHealthMultiplier);
                        ScaleIntProperty(waveProperty.FindPropertyRelative("enemyScrapReward"), preset.WaveRewardMultiplier);
                    }
                }
            }

            serializedSpawner.ApplyModifiedProperties();
            EditorUtility.SetDirty(currentWaveSpawner);
            EditorSceneManager.MarkSceneDirty(currentWaveSpawner.gameObject.scene);
        }

        private void ApplyPresetToRelay(BalancePresetDefinition preset)
        {
            RelayTower relayPrototype = ResolveRelayPrototype();
            if (relayPrototype == null)
            {
                return;
            }

            SerializedObject relaySerialized = new SerializedObject(relayPrototype);
            ScaleFloatProperty(relaySerialized.FindProperty("supplyRange"), preset.RelayRangeMultiplier, minimum: 0.1f);

            SerializedProperty baseCapacityProperty = relaySerialized.FindProperty("baseSupplyCapacity");
            if (baseCapacityProperty != null)
            {
                baseCapacityProperty.intValue = Mathf.Max(0, baseCapacityProperty.intValue + preset.RelayCapacityDelta);
            }

            ScaleIntProperty(relaySerialized.FindProperty("upgradeCostBase"), preset.UpgradeCostMultiplier);
            ScaleIntProperty(relaySerialized.FindProperty("upgradeCostPerLevel"), preset.UpgradeCostMultiplier);
            relaySerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(relayPrototype);
        }

        private void ApplyPresetToCombatTower(string prefabPropertyName, string tuningPropertyName, BalancePresetDefinition preset)
        {
            DefenseTower towerPrototype = ResolveDefensePrototype(prefabPropertyName);
            if (towerPrototype == null)
            {
                return;
            }

            SerializedObject serializedTower = new SerializedObject(towerPrototype);
            ScaleFloatProperty(serializedTower.FindProperty($"{tuningPropertyName}.attackRange"), preset.TowerRangeMultiplier, minimum: 0.1f);
            ScaleFloatProperty(serializedTower.FindProperty($"{tuningPropertyName}.attackInterval"), preset.TowerAttackIntervalMultiplier, minimum: 0.05f);
            ScaleIntProperty(serializedTower.FindProperty($"{tuningPropertyName}.baseDamage"), preset.TowerDamageMultiplier);
            ScaleIntProperty(serializedTower.FindProperty($"{tuningPropertyName}.damagePerUpgrade"), preset.TowerDamageMultiplier);
            ScaleIntProperty(serializedTower.FindProperty($"{tuningPropertyName}.basePowerRequired"), preset.TowerPowerMultiplier);
            ScaleIntProperty(serializedTower.FindProperty($"{tuningPropertyName}.powerRequiredPerUpgrade"), preset.TowerPowerMultiplier);
            ScaleIntProperty(serializedTower.FindProperty($"{tuningPropertyName}.upgradeCostBase"), preset.UpgradeCostMultiplier);
            ScaleIntProperty(serializedTower.FindProperty($"{tuningPropertyName}.upgradeCostPerLevel"), preset.UpgradeCostMultiplier);

            // Only the relevant tuning families meaningfully use these fields.
            ScaleFloatProperty(serializedTower.FindProperty($"{tuningPropertyName}.bombRadius"), preset.BombRadiusMultiplier, minimum: 0.1f);
            ScaleFloatProperty(serializedTower.FindProperty($"{tuningPropertyName}.slowMultiplier"), preset.SlowStrengthMultiplier, minimum: 0.15f);
            ScaleFloatProperty(serializedTower.FindProperty($"{tuningPropertyName}.slowDuration"), preset.TowerRangeMultiplier, minimum: 0f);

            serializedTower.ApplyModifiedProperties();
            EditorUtility.SetDirty(towerPrototype);
        }

        private void DrawAdvancedRawEditors()
        {
            DrawRawObjectEditor("总控原始面板", currentGame);
            DrawRawObjectEditor("刷怪器原始面板", currentWaveSpawner);
            DrawRawObjectEditor("地图入口原始面板", currentMap);
            DrawRawObjectEditor("继电器原型原始面板", ResolveRelayPrototype());
            DrawRawObjectEditor("单体塔原型原始面板", ResolveDefensePrototype("singleTargetTowerPrototypeReference"));
            DrawRawObjectEditor("减速塔原型原始面板", ResolveDefensePrototype("slowFieldTowerPrototypeReference"));
            DrawRawObjectEditor("炸弹塔原型原始面板", ResolveDefensePrototype("bombardTowerPrototypeReference"));
        }

        private void DrawRawObjectEditor(string title, Object targetObject)
        {
            if (targetObject == null)
            {
                return;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);

            SerializedObject serializedObject = new SerializedObject(targetObject);
            serializedObject.Update();
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                if (iterator.name == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(iterator, true);
                    }
                }
                else
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }

                enterChildren = false;
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetObject);
        }

        private void ApplyWaveMultipliers()
        {
            if (currentWaveSpawner == null)
            {
                return;
            }

            SerializedObject serializedSpawner = new SerializedObject(currentWaveSpawner);
            WaveCatalogAsset resolvedWaveCatalog = ResolveWaveCatalogAsset();
            EnemyCatalogAsset resolvedEnemyCatalog = ResolveEnemyCatalogAsset();
            if (resolvedWaveCatalog != null)
            {
                ApplyCatalogWaveMultipliers(resolvedWaveCatalog, resolvedEnemyCatalog, waveCountMultiplier, waveIntervalMultiplier, waveSpeedMultiplier, waveHealthMultiplier, waveRewardMultiplier);
            }
            else
            {
                SerializedProperty wavesProperty = serializedSpawner.FindProperty("waves");
                if (wavesProperty == null || !wavesProperty.isArray)
                {
                    return;
                }

                for (int waveIndex = 0; waveIndex < wavesProperty.arraySize; waveIndex++)
                {
                    SerializedProperty waveProperty = wavesProperty.GetArrayElementAtIndex(waveIndex);
                    ScaleIntProperty(waveProperty.FindPropertyRelative("enemyCount"), waveCountMultiplier);
                    ScaleFloatProperty(waveProperty.FindPropertyRelative("spawnInterval"), waveIntervalMultiplier, minimum: 0.05f);
                    ScaleFloatProperty(waveProperty.FindPropertyRelative("moveSpeed"), waveSpeedMultiplier, minimum: 0.05f);
                    ScaleIntProperty(waveProperty.FindPropertyRelative("enemyHealth"), waveHealthMultiplier);
                    ScaleIntProperty(waveProperty.FindPropertyRelative("enemyScrapReward"), waveRewardMultiplier);
                }
            }

            serializedSpawner.ApplyModifiedProperties();
            EditorUtility.SetDirty(currentWaveSpawner);
            EditorSceneManager.MarkSceneDirty(currentWaveSpawner.gameObject.scene);
        }

        private void ApplyBuildCostMultiplier()
        {
            if (currentGame == null)
            {
                return;
            }

            SerializedObject serializedGame = new SerializedObject(currentGame);
            ScaleIntProperty(serializedGame.FindProperty("relayTowerCost"), buildCostMultiplier);
            ScaleIntProperty(serializedGame.FindProperty("singleTargetTowerCost"), buildCostMultiplier);
            ScaleIntProperty(serializedGame.FindProperty("slowFieldTowerCost"), buildCostMultiplier);
            ScaleIntProperty(serializedGame.FindProperty("bombardTowerCost"), buildCostMultiplier);
            serializedGame.ApplyModifiedProperties();
            EditorUtility.SetDirty(currentGame);
            EditorSceneManager.MarkSceneDirty(currentGame.gameObject.scene);
        }

        private void ApplyUpgradeCostMultiplier()
        {
            RelayTower relayPrototype = ResolveRelayPrototype();
            if (relayPrototype != null)
            {
                SerializedObject relaySerialized = new SerializedObject(relayPrototype);
                ScaleIntProperty(relaySerialized.FindProperty("upgradeCostBase"), upgradeCostMultiplier);
                ScaleIntProperty(relaySerialized.FindProperty("upgradeCostPerLevel"), upgradeCostMultiplier);
                relaySerialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(relayPrototype);
            }

            ApplyTowerUpgradeCostMultiplier("singleTargetTowerPrototypeReference", "singleTargetTuning");
            ApplyTowerUpgradeCostMultiplier("slowFieldTowerPrototypeReference", "slowFieldTuning");
            ApplyTowerUpgradeCostMultiplier("bombardTowerPrototypeReference", "bombardTuning");

            AssetDatabase.SaveAssets();
        }

        private void ApplyTowerUpgradeCostMultiplier(string prefabPropertyName, string tuningPropertyName)
        {
            DefenseTower towerPrototype = ResolveDefensePrototype(prefabPropertyName);
            if (towerPrototype == null)
            {
                return;
            }

            SerializedObject serializedTower = new SerializedObject(towerPrototype);
            ScaleIntProperty(serializedTower.FindProperty($"{tuningPropertyName}.upgradeCostBase"), upgradeCostMultiplier);
            ScaleIntProperty(serializedTower.FindProperty($"{tuningPropertyName}.upgradeCostPerLevel"), upgradeCostMultiplier);
            serializedTower.ApplyModifiedProperties();
            EditorUtility.SetDirty(towerPrototype);
        }

        private void AdoptCurrentSceneContext()
        {
            TowerDefenseAuthoringSceneContext context = TowerDefenseAuthoringSceneContext.CaptureActiveSceneContext();
            currentGame = context.CurrentGame;
            currentWaveSpawner = context.CurrentWaveSpawner;
            currentMap = context.CurrentMap;
        }

        private RelayTower ResolveRelayPrototype()
        {
            GameObject relayPrototypeObject = ResolvePrototypeObject("relayTowerPrototypeReference");
            return relayPrototypeObject != null ? relayPrototypeObject.GetComponent<RelayTower>() : null;
        }

        private DefenseTower ResolveDefensePrototype(string propertyName)
        {
            GameObject prototypeObject = ResolvePrototypeObject(propertyName);
            return prototypeObject != null ? prototypeObject.GetComponent<DefenseTower>() : null;
        }

        private GameObject ResolvePrototypeObject(string propertyName)
        {
            if (currentGame == null)
            {
                return null;
            }

            SerializedObject serializedGame = new SerializedObject(currentGame);
            SerializedProperty property = serializedGame.FindProperty(propertyName);
            return property != null ? property.objectReferenceValue as GameObject : null;
        }

        private WaveCatalogAsset ResolveWaveCatalogAsset()
        {
            if (currentWaveSpawner == null)
            {
                return null;
            }

            SerializedObject serializedSpawner = new SerializedObject(currentWaveSpawner);
            SerializedProperty property = serializedSpawner.FindProperty("waveCatalogAsset");
            return property != null ? property.objectReferenceValue as WaveCatalogAsset : null;
        }

        private EnemyCatalogAsset ResolveEnemyCatalogAsset()
        {
            if (currentWaveSpawner == null)
            {
                return null;
            }

            SerializedObject serializedSpawner = new SerializedObject(currentWaveSpawner);
            SerializedProperty property = serializedSpawner.FindProperty("enemyCatalogAsset");
            return property != null ? property.objectReferenceValue as EnemyCatalogAsset : null;
        }

        private static T FindFirstComponentInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .FirstOrDefault(component => component != null);
        }

        private static void DrawPropertyField(SerializedObject serializedObject, string propertyPath, bool includeChildren = false)
        {
            if (serializedObject == null)
            {
                return;
            }

            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, includeChildren);
            }
        }

        private static void ScaleIntProperty(SerializedProperty property, float multiplier)
        {
            if (property == null)
            {
                return;
            }

            int currentValue = property.intValue;
            property.intValue = Mathf.Max(0, Mathf.RoundToInt(currentValue * multiplier));
        }

        private static void ScaleFloatProperty(SerializedProperty property, float multiplier, float minimum)
        {
            if (property == null)
            {
                return;
            }

            float currentValue = property.floatValue;
            property.floatValue = Mathf.Max(minimum, currentValue * multiplier);
        }

        private void DrawWavePathReferenceEditor()
        {
            if (currentWaveSpawner == null)
            {
                return;
            }

            EnemyPath[] availablePaths = CollectSceneEnemyPaths();
            if (availablePaths.Length == 0)
            {
                EditorGUILayout.HelpBox("当前场景里还没有可供波次绑定的 EnemyPath。", MessageType.Warning);
                return;
            }

            WaveCatalogAsset resolvedWaveCatalog = ResolveWaveCatalogAsset();
            if (resolvedWaveCatalog != null)
            {
                DrawWavePathReferenceEditorV2();
                return;

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("刷怪组路径引用", EditorStyles.boldLabel);
                WaveCatalogAsset.WaveEntry[] waves = resolvedWaveCatalog.Waves;
                for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
                {
                    WaveCatalogAsset.WaveEntry wave = waves[waveIndex];
                    if (wave == null)
                    {
                        continue;
                    }

                    EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(wave.DisplayName) ? $"Wave {waveIndex + 1:D2}" : wave.DisplayName, EditorStyles.miniBoldLabel);
                    WaveCatalogAsset.SpawnGroup[] groups = wave.SpawnGroups;
                    for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
                    {
                        WaveCatalogAsset.SpawnGroup group = groups[groupIndex];
                        if (group == null)
                        {
                            continue;
                        }

                        EditorGUILayout.ObjectField($"组 {groupIndex + 1:D2} / {group.EnemyType}", group.EnemyPathReference, typeof(EnemyPath), true);
                    }
                }

                EditorGUILayout.HelpBox("如果这里还是不能直接改，说明这一版 `WaveCatalogAsset` 仍然只靠原始序列化块暴露字段。运行时已经支持 `EnemyPath` 直接引用，但更顺手的定制编辑入口还需要再补一层。", MessageType.Info);
                return;
            }

            SerializedObject serializedSpawner = new SerializedObject(currentWaveSpawner);
            serializedSpawner.Update();
            SerializedProperty wavesProperty = serializedSpawner.FindProperty("waves");
            if (wavesProperty == null || !wavesProperty.isArray)
            {
                return;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("fallback 波次路径引用", EditorStyles.boldLabel);
            for (int waveIndex = 0; waveIndex < wavesProperty.arraySize; waveIndex++)
            {
                SerializedProperty waveProperty = wavesProperty.GetArrayElementAtIndex(waveIndex);
                SerializedProperty pathProperty = waveProperty.FindPropertyRelative("enemyPathReference");
                if (pathProperty == null)
                {
                    continue;
                }

                pathProperty.objectReferenceValue = (EnemyPath)EditorGUILayout.ObjectField(
                    $"Wave {waveIndex + 1:D2} 路径",
                    pathProperty.objectReferenceValue,
                    typeof(EnemyPath),
                    true);
            }

            serializedSpawner.ApplyModifiedProperties();
            EditorUtility.SetDirty(currentWaveSpawner);
            DrawWavePathReferenceEditorV2();
        }

        private EnemyPath[] CollectSceneEnemyPaths()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            return activeScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<EnemyPath>(true))
                .Where(path => path != null)
                .ToArray();
        }

        private void DrawWavePathReferenceEditorV2()
        {
            if (currentWaveSpawner == null)
            {
                return;
            }

            EnemyPath[] availablePaths = CollectSceneEnemyPaths();
            if (availablePaths.Length == 0)
            {
                return;
            }

            WaveCatalogAsset resolvedWaveCatalog = ResolveWaveCatalogAsset();
            if (resolvedWaveCatalog != null)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("刷怪组路径引用（对象选择版）", EditorStyles.boldLabel);
                WaveCatalogAsset.WaveEntry[] waves = resolvedWaveCatalog.Waves;
                for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
                {
                    WaveCatalogAsset.WaveEntry wave = waves[waveIndex];
                    if (wave == null)
                    {
                        continue;
                    }

                    EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(wave.DisplayName) ? $"Wave {waveIndex + 1:D2}" : wave.DisplayName, EditorStyles.miniBoldLabel);
                    WaveCatalogAsset.SpawnGroup[] groups = wave.SpawnGroups;
                    for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
                    {
                        WaveCatalogAsset.SpawnGroup group = groups[groupIndex];
                        if (group == null)
                        {
                            continue;
                        }

                        EnemyPath currentPath = currentWaveSpawner.ResolveCatalogBinding(waveIndex, groupIndex);
                        EnemyPath selectedPath = (EnemyPath)EditorGUILayout.ObjectField(
                            $"组 {groupIndex + 1:D2} / {group.EnemyType}",
                            currentPath,
                            typeof(EnemyPath),
                            true);
                        if (selectedPath != currentPath)
                        {
                            Undo.RecordObject(currentWaveSpawner, "修改刷怪组路径引用");
                            currentWaveSpawner.AssignCatalogBinding(waveIndex, groupIndex, selectedPath);
                            EditorUtility.SetDirty(currentWaveSpawner);
                            EditorSceneManager.MarkSceneDirty(currentWaveSpawner.gameObject.scene);
                            GUI.changed = true;
                            Repaint();
                        }
                    }

                    EditorGUILayout.Space(2f);
                }

                EditorGUILayout.HelpBox("这里会把每个刷怪组对应的 EnemyPath 绑定保存到当前场景的 WaveSpawner 上，用于多路径关卡。", MessageType.None);
                return;
            }

            SerializedObject serializedSpawner = new SerializedObject(currentWaveSpawner);
            serializedSpawner.Update();
            SerializedProperty wavesProperty = serializedSpawner.FindProperty("waves");
            if (wavesProperty == null || !wavesProperty.isArray)
            {
                return;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("fallback 波次路径引用（对象选择版）", EditorStyles.boldLabel);
            for (int waveIndex = 0; waveIndex < wavesProperty.arraySize; waveIndex++)
            {
                SerializedProperty waveProperty = wavesProperty.GetArrayElementAtIndex(waveIndex);
                SerializedProperty pathProperty = waveProperty.FindPropertyRelative("enemyPathReference");
                if (pathProperty == null)
                {
                    continue;
                }

                pathProperty.objectReferenceValue = (EnemyPath)EditorGUILayout.ObjectField(
                    $"Wave {waveIndex + 1:D2} 路径",
                    pathProperty.objectReferenceValue,
                    typeof(EnemyPath),
                    true);
            }

            serializedSpawner.ApplyModifiedProperties();
            EditorUtility.SetDirty(currentWaveSpawner);
        }

        private static void ApplyCatalogWaveMultipliers(
            WaveCatalogAsset waveCatalogAsset,
            EnemyCatalogAsset enemyCatalogAsset,
            float countMultiplier,
            float intervalMultiplier,
            float speedMultiplier,
            float healthMultiplier,
            float rewardMultiplier)
        {
            if (waveCatalogAsset == null)
            {
                return;
            }

            SerializedObject serializedCatalog = new SerializedObject(waveCatalogAsset);
            SerializedProperty wavesProperty = serializedCatalog.FindProperty("waves");
            HashSet<EnemyArchetypeId> usedArchetypes = new HashSet<EnemyArchetypeId>();

            if (wavesProperty != null && wavesProperty.isArray)
            {
                for (int waveIndex = 0; waveIndex < wavesProperty.arraySize; waveIndex++)
                {
                    SerializedProperty waveProperty = wavesProperty.GetArrayElementAtIndex(waveIndex);
                    SerializedProperty groupsProperty = waveProperty.FindPropertyRelative("spawnGroups");
                    if (groupsProperty == null || !groupsProperty.isArray)
                    {
                        continue;
                    }

                    for (int groupIndex = 0; groupIndex < groupsProperty.arraySize; groupIndex++)
                    {
                        SerializedProperty groupProperty = groupsProperty.GetArrayElementAtIndex(groupIndex);
                        ScaleIntProperty(groupProperty.FindPropertyRelative("enemyCount"), countMultiplier);
                        ScaleFloatProperty(groupProperty.FindPropertyRelative("spawnInterval"), intervalMultiplier, minimum: 0.05f);

                        SerializedProperty enemyTypeProperty = groupProperty.FindPropertyRelative("enemyType");
                        if (enemyTypeProperty != null)
                        {
                            usedArchetypes.Add((EnemyArchetypeId)enemyTypeProperty.enumValueIndex);
                        }
                    }
                }
            }

            serializedCatalog.ApplyModifiedProperties();
            EditorUtility.SetDirty(waveCatalogAsset);

            if (enemyCatalogAsset == null)
            {
                return;
            }

            SerializedObject serializedEnemyCatalog = new SerializedObject(enemyCatalogAsset);
            SerializedProperty definitionsProperty = serializedEnemyCatalog.FindProperty("definitions");
            if (definitionsProperty != null && definitionsProperty.isArray)
            {
                for (int definitionIndex = 0; definitionIndex < definitionsProperty.arraySize; definitionIndex++)
                {
                    SerializedProperty definitionProperty = definitionsProperty.GetArrayElementAtIndex(definitionIndex);
                    SerializedProperty archetypeProperty = definitionProperty.FindPropertyRelative("archetypeId");
                    if (archetypeProperty == null)
                    {
                        continue;
                    }

                    EnemyArchetypeId archetypeId = (EnemyArchetypeId)archetypeProperty.enumValueIndex;
                    if (!usedArchetypes.Contains(archetypeId))
                    {
                        continue;
                    }

                    ScaleFloatProperty(definitionProperty.FindPropertyRelative("moveSpeed"), speedMultiplier, minimum: 0.05f);
                    ScaleIntProperty(definitionProperty.FindPropertyRelative("maxHealth"), healthMultiplier);
                    ScaleIntProperty(definitionProperty.FindPropertyRelative("scrapReward"), rewardMultiplier);
                }
            }

            serializedEnemyCatalog.ApplyModifiedProperties();
            EditorUtility.SetDirty(enemyCatalogAsset);
        }

        private static void DrawPingButton(Object targetObject, string buttonLabel)
        {
            if (targetObject == null)
            {
                return;
            }

            if (GUILayout.Button(buttonLabel, GUILayout.Width(180f)))
            {
                EditorGUIUtility.PingObject(targetObject);
                Selection.activeObject = targetObject;
            }
        }

        private static void SaveCurrentWork()
        {
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.Refresh();
        }
    }
}

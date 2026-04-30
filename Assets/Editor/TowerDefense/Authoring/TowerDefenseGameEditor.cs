using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

namespace TowerDefense.Editor
{
    /// <summary>
    /// `TowerDefenseGame` 的自定义检查器。
    ///
    /// 这里不试图重做整个数据模型，
    /// 而是先把“作者最常看的几组内容”更明确地分层展示出来：
    /// - 当前场景关键引用是否齐全
    /// - 当前地图骨架摘要
    /// - 核心数值、放置规则、主题和表现配置分别在哪一块
    /// </summary>
    [CustomEditor(typeof(TowerDefenseGame))]
    public sealed class TowerDefenseGameEditor : UnityEditor.Editor
    {
        private static readonly string[] CoreRuleFields = // 中文：核心RuleFields
        {
            "startingScrap",
            "startingBaseHealth",
            "relayTowerCost",
            "singleTargetTowerCost",
            "slowFieldTowerCost",
            "bombardTowerCost"
        };

        private static readonly string[] PlacementRuleFields = // 中文：放置RuleFields
        {
            "relayPlacementRadius",
            "defensePlacementRadius",
            "relayExpansionSquareSize",
            "defenseExpansionSquareSize",
            "initialPlacementSquareCenter",
            "initialPlacementSquareSize"
        };

        private static readonly string[] PlacementVisualFields = // 中文：放置视觉Fields
        {
            "validPreviewColor",
            "invalidPreviewColor",
            "placementRingSpriteReference",
            "placementRingResourcePath",
            "placementAreaOverlayPixelsPerUnit",
            "placementAreaOverlayFillColor",
            "placementAreaOverlayEdgeColor",
            "placementAreaOverlaySortingOrder",
            "starterZoneMarkerFillColor",
            "starterZoneMarkerEdgeColor",
            "starterZoneMarkerSortingOrder"
        };

        private static readonly string[] SharedPresentationAssetFields = // 中文：Shared展示资产Fields
        {
            "towerPresentationCatalogAsset",
            "hudThemeAsset",
            "hudCopyAsset",
            "placementVisualThemeAsset"
        };

        private static readonly string[] SceneReferenceFields = // 中文：场景引用Fields
        {
            "mainCameraReference",
            "relayTowerPrototypeReference",
            "singleTargetTowerPrototypeReference",
            "slowFieldTowerPrototypeReference",
            "bombardTowerPrototypeReference",
            "placedTowerRootReference",
            "placementPreviewRootReference",
            "buildZoneReference",
            "battlefieldMapReference"
        };

        private static readonly string[] HudReferenceFields = // 中文：HUD引用Fields
        {
            "scrapTextReference",
            "baseHealthTextReference",
            "waveTextReference",
            "selectionTextReference",
            "operationTextReference",
            "liveStatusTextReference",
            "powerGridTextReference",
            "latestEventTextReference",
            "recentLogTextReference",
            "relayTowerButtonReference",
            "defenseTowerButtonReference",
            "slowFieldTowerButtonReference",
            "bombardTowerButtonReference",
            "clearSelectionButtonReference",
            "gameOverPanelReference",
            "gameOverTitleReference",
            "gameOverHintReference",
            "dragPreviewPanelReference",
            "dragPreviewLabelReference"
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawSceneValidationSummary();
            DrawMapSummary();
            DrawHudStructureSummary();
            DrawPropertySection("共享表现资产", SharedPresentationAssetFields);
            DrawAuthoringActions();
            DrawPropertySection("核心规则", CoreRuleFields);
            DrawPropertySection("放置规则", PlacementRuleFields);
            DrawPropertySection("放置视觉", PlacementVisualFields);
            DrawFallbackPresentationSections();
            DrawPropertySection("场景引用", SceneReferenceFields);
            DrawPropertySection("HUD 引用", HudReferenceFields);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSceneValidationSummary()
        {
            string missing = string.Empty;
            AppendMissingRef(ref missing, "mainCameraReference", "主相机");
            AppendMissingRef(ref missing, "battlefieldMapReference", "战场地图定义");
            AppendMissingRef(ref missing, "buildZoneReference", "建造区");
            AppendMissingRef(ref missing, "placedTowerRootReference", "已放置塔根节点");
            AppendMissingRef(ref missing, "placementPreviewRootReference", "放置预览根节点");
            AppendMissingRef(ref missing, "relayTowerPrototypeReference", "继电器原型 Prefab");
            AppendMissingRef(ref missing, "singleTargetTowerPrototypeReference", "单体塔原型 Prefab");
            AppendMissingRef(ref missing, "slowFieldTowerPrototypeReference", "减速塔原型 Prefab");
            AppendMissingRef(ref missing, "bombardTowerPrototypeReference", "炸弹塔原型 Prefab");

            if (string.IsNullOrWhiteSpace(missing))
            {
                EditorGUILayout.HelpBox("当前场景关键引用已接齐。这个版本不再依赖运行时兜底创建，所以这里保持干净是非常重要的。", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox($"当前场景还有这些关键缺项：\n{missing}".TrimEnd(), MessageType.Warning);
        }

        private void DrawMapSummary()
        {
            SerializedProperty mapProperty = serializedObject.FindProperty("battlefieldMapReference");
            BattlefieldMapDefinition mapDefinition = mapProperty != null ? mapProperty.objectReferenceValue as BattlefieldMapDefinition : null;
            if (mapDefinition == null)
            {
                return;
            }

            EditorGUILayout.HelpBox($"地图摘要\n{mapDefinition.BuildAuthoringSummary()}", MessageType.None);
        }

        private void DrawHudStructureSummary()
        {
            bool hasSplitOperationBlock =
                HasObjectReference("operationTextReference") ||
                HasObjectReference("liveStatusTextReference") ||
                HasObjectReference("powerGridTextReference") ||
                HasObjectReference("latestEventTextReference") ||
                HasObjectReference("recentLogTextReference");

            if (hasSplitOperationBlock)
            {
                EditorGUILayout.HelpBox("当前 HUD 已经开始走拆分文本块结构。你可以直接在 Scene 里分别调整操作区、状态区、供电区和事件区。", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox("当前 HUD 仍主要依赖旧的 SelectionText 单块文本。若你想把操作区彻底改成 Scene 主导，建议点击下方的 `Materialize HUD Split Texts`。", MessageType.Warning);
        }

        private void DrawFallbackPresentationSections()
        {
            SerializedProperty towerPresentationAssetProperty = serializedObject.FindProperty("towerPresentationCatalogAsset");
            SerializedProperty hudThemeAssetProperty = serializedObject.FindProperty("hudThemeAsset");
            SerializedProperty hudCopyAssetProperty = serializedObject.FindProperty("hudCopyAsset");
            SerializedProperty placementVisualThemeAssetProperty = serializedObject.FindProperty("placementVisualThemeAsset");

            bool usingFallbackTowerPresentation = towerPresentationAssetProperty == null || towerPresentationAssetProperty.objectReferenceValue == null;
            bool usingFallbackHudTheme = hudThemeAssetProperty == null || hudThemeAssetProperty.objectReferenceValue == null;
            bool usingFallbackHudCopy = hudCopyAssetProperty == null || hudCopyAssetProperty.objectReferenceValue == null;
            bool usingFallbackPlacementVisualTheme = placementVisualThemeAssetProperty == null || placementVisualThemeAssetProperty.objectReferenceValue == null;

            if (usingFallbackTowerPresentation)
            {
                EditorGUILayout.HelpBox("当前没有接塔展示目录资产，所以仍会回退到场景里的旧展示配置。", MessageType.Warning);
                DrawSinglePropertySection("塔展示回退配置", "relayPresentation", "singleTargetPresentation", "slowFieldPresentation", "bombardPresentation");
            }

            if (usingFallbackHudTheme)
            {
                EditorGUILayout.HelpBox("当前没有接 HUD 主题资产，所以仍会回退到场景里的旧 HUD 主题配置。", MessageType.Warning);
                DrawSinglePropertySection("HUD 主题回退配置", "hudTheme");
            }

            if (usingFallbackHudCopy)
            {
                EditorGUILayout.HelpBox("当前没有接 HUD 文案资产，所以部分 HUD 固定文案仍会回退到代码内默认值。", MessageType.Warning);
            }

            if (usingFallbackPlacementVisualTheme)
            {
                EditorGUILayout.HelpBox("当前没有接放置可视化主题资产，所以放置可视化仍会回退到场景里的旧配色配置。", MessageType.Warning);
                DrawSinglePropertySection(
                    "放置视觉回退配置",
                    "validPreviewColor",
                    "invalidPreviewColor",
                    "placementRingSpriteReference",
                    "placementAreaOverlayPixelsPerUnit",
                    "placementAreaOverlayFillColor",
                    "placementAreaOverlayEdgeColor",
                    "placementAreaOverlaySortingOrder",
                    "starterZoneMarkerFillColor",
                    "starterZoneMarkerEdgeColor",
                    "starterZoneMarkerSortingOrder");
            }
        }

        private void DrawAuthoringActions()
        {
            TowerDefenseGame game = (TowerDefenseGame)target;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("作者操作", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("补齐默认共享资产"))
                {
                    AssignDefaultSharedAssets();
                }

                if (GUILayout.Button("生成 HUD 拆分文本"))
                {
                    MaterializeHudSplitTexts(game);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void AssignDefaultSharedAssets()
        {
            AssignAssetIfMissing("towerPresentationCatalogAsset", "Assets/Resources/TowerDefense/Configs/TowerPresentationCatalog.asset");
            AssignAssetIfMissing("hudThemeAsset", "Assets/Resources/TowerDefense/Configs/TowerDefenseHudTheme.asset");
            AssignAssetIfMissing("hudCopyAsset", "Assets/Resources/TowerDefense/Configs/TowerDefenseHudCopy.asset");
            AssignAssetIfMissing("placementVisualThemeAsset", "Assets/Resources/TowerDefense/Configs/TowerPlacementVisualTheme.asset");
        }

        private void AssignAssetIfMissing(string propertyName, string assetPath)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue != null)
            {
                return;
            }

            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (asset == null)
            {
                return;
            }

            property.objectReferenceValue = asset;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private void MaterializeHudSplitTexts(TowerDefenseGame game)
        {
            SerializedProperty selectionProperty = serializedObject.FindProperty("selectionTextReference");
            TMP_Text legacySelectionText = selectionProperty != null ? selectionProperty.objectReferenceValue as TMP_Text : null;
            if (legacySelectionText == null)
            {
                EditorUtility.DisplayDialog("生成 HUD 拆分文本", "请先在 TowerDefenseGame 上接好 SelectionText 引用，再执行拆分。", "确定");
                return;
            }

            RectTransform parent = legacySelectionText.transform.parent as RectTransform;
            if (parent == null)
            {
                EditorUtility.DisplayDialog("生成 HUD 拆分文本", "SelectionText 没有可用的父 RectTransform。", "确定");
                return;
            }

            TMP_FontAsset fontAsset = legacySelectionText.font;
            Material sharedMaterial = legacySelectionText.fontSharedMaterial;

            SerializedProperty operationProperty = serializedObject.FindProperty("operationTextReference");
            SerializedProperty liveStatusProperty = serializedObject.FindProperty("liveStatusTextReference");
            SerializedProperty powerGridProperty = serializedObject.FindProperty("powerGridTextReference");
            SerializedProperty latestEventProperty = serializedObject.FindProperty("latestEventTextReference");
            SerializedProperty recentLogProperty = serializedObject.FindProperty("recentLogTextReference");

            operationProperty.objectReferenceValue = EnsureHudBlock(parent, "OperationText", new Vector2(16f, -16f), new Vector2(-16f, -196f), 30f, fontAsset, sharedMaterial);
            liveStatusProperty.objectReferenceValue = EnsureHudBlock(parent, "LiveStatusText", new Vector2(16f, -212f), new Vector2(-16f, -300f), 18f, fontAsset, sharedMaterial);
            powerGridProperty.objectReferenceValue = EnsureHudBlock(parent, "PowerGridText", new Vector2(16f, -316f), new Vector2(-16f, -420f), 18f, fontAsset, sharedMaterial);
            latestEventProperty.objectReferenceValue = EnsureHudBlock(parent, "LatestEventText", new Vector2(16f, -436f), new Vector2(-16f, -500f), 18f, fontAsset, sharedMaterial);
            recentLogProperty.objectReferenceValue = EnsureHudBlock(parent, "RecentLogText", new Vector2(16f, -516f), new Vector2(-16f, -640f), 16f, fontAsset, sharedMaterial);

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        }

        private TMP_Text EnsureHudBlock(
            RectTransform parent,
            string objectName,
            Vector2 topLeft,
            Vector2 bottomRight,
            float fontSize,
            TMP_FontAsset fontAsset,
            Material sharedMaterial)
        {
            Transform existingChild = parent.Find(objectName);
            GameObject targetObject;
            RectTransform rectTransform;
            TextMeshProUGUI label;

            if (existingChild != null)
            {
                targetObject = existingChild.gameObject;
                rectTransform = targetObject.GetComponent<RectTransform>();
                label = targetObject.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                targetObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                Undo.RegisterCreatedObjectUndo(targetObject, $"Create {objectName}");
                rectTransform = targetObject.GetComponent<RectTransform>();
                rectTransform.SetParent(parent, false);
                label = targetObject.GetComponent<TextMeshProUGUI>();
            }

            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.offsetMin = new Vector2(topLeft.x, bottomRight.y);
            rectTransform.offsetMax = new Vector2(bottomRight.x, topLeft.y);
            rectTransform.localScale = Vector3.one;

            label.raycastTarget = false;
            label.enableWordWrapping = true;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.text = string.Empty;
            if (fontAsset != null)
            {
                label.font = fontAsset;
            }

            if (sharedMaterial != null)
            {
                label.fontSharedMaterial = sharedMaterial;
            }

            return label;
        }

        private void DrawPropertySection(string label, params string[] propertyNames)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                for (int index = 0; index < propertyNames.Length; index++)
                {
                    SerializedProperty property = serializedObject.FindProperty(propertyNames[index]);
                    if (property != null)
                    {
                        EditorGUILayout.PropertyField(property, includeChildren: true);
                    }
                }
            }
        }

        private void DrawSinglePropertySection(string label, params string[] propertyNames)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                for (int index = 0; index < propertyNames.Length; index++)
                {
                    SerializedProperty property = serializedObject.FindProperty(propertyNames[index]);
                    if (property != null)
                    {
                        EditorGUILayout.PropertyField(property, includeChildren: true);
                    }
                }
            }
        }

        private bool HasObjectReference(string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null && property.objectReferenceValue != null;
        }

        private void AppendMissingRef(ref string currentMessage, string propertyName, string displayName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue != null)
            {
                return;
            }

            currentMessage += $"- {displayName}\n";
        }
    }
}

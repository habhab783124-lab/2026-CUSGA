using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// 自定义 `DefenseTower` 检查器。
    ///
    /// 目标不是把运行时逻辑拆碎，而是先把作者看到的 Inspector 收得更清楚：
    /// - 默认只显示当前塔型真正会用到的那一组 tuning
    /// - 共享视觉、引用和升级设置独立成块
    /// - 需要时仍然可以展开查看其他塔型 tuning，方便批量对照
    /// </summary>
    [CustomEditor(typeof(DefenseTower))]
    public sealed class DefenseTowerEditor : UnityEditor.Editor
    {
        private SerializedProperty _buildTypeProperty;
        private SerializedProperty _singleTargetTuningProperty;
        private SerializedProperty _slowFieldTuningProperty;
        private SerializedProperty _bombardTuningProperty;
        private SerializedProperty _currentLevelProperty;
        private SerializedProperty _maxLevelProperty;
        private SerializedProperty _bodyRendererReferenceProperty;
        private SerializedProperty _feedbackRootReferenceProperty;
        private SerializedProperty _singleTargetFeedbackRootReferenceProperty;
        private SerializedProperty _slowFieldFeedbackRootReferenceProperty;
        private SerializedProperty _bombardFeedbackRootReferenceProperty;
        private SerializedProperty _typeSignatureRootReferenceProperty;
        private SerializedProperty _levelMarkerRootReferenceProperty;
        private SerializedProperty _flashColorProperty;
        private SerializedProperty _offlineColorProperty;
        private SerializedProperty _flashDurationProperty;
        private SerializedProperty _upgradeFlashColorProperty;
        private SerializedProperty _upgradePulseDurationProperty;
        private SerializedProperty _upgradeScaleMultiplierProperty;
        private SerializedProperty _feedbackMaterialProperty;
        private SerializedProperty _levelPipSpriteProperty;
        private SerializedProperty _levelPipColorProperty;
        private SerializedProperty _levelPipOffsetProperty;
        private SerializedProperty _levelPipSpacingProperty;
        private SerializedProperty _levelPipScaleProperty;
        private SerializedProperty _levelPipSortingOffsetProperty;

        private bool _showAllTuningBlocks;

        private void OnEnable()
        {
            _buildTypeProperty = serializedObject.FindProperty("buildType");
            _singleTargetTuningProperty = serializedObject.FindProperty("singleTargetTuning");
            _slowFieldTuningProperty = serializedObject.FindProperty("slowFieldTuning");
            _bombardTuningProperty = serializedObject.FindProperty("bombardTuning");
            _currentLevelProperty = serializedObject.FindProperty("currentLevel");
            _maxLevelProperty = serializedObject.FindProperty("maxLevel");
            _bodyRendererReferenceProperty = serializedObject.FindProperty("bodyRendererReference");
            _feedbackRootReferenceProperty = serializedObject.FindProperty("feedbackRootReference");
            _singleTargetFeedbackRootReferenceProperty = serializedObject.FindProperty("singleTargetFeedbackRootReference");
            _slowFieldFeedbackRootReferenceProperty = serializedObject.FindProperty("slowFieldFeedbackRootReference");
            _bombardFeedbackRootReferenceProperty = serializedObject.FindProperty("bombardFeedbackRootReference");
            _typeSignatureRootReferenceProperty = serializedObject.FindProperty("typeSignatureRootReference");
            _levelMarkerRootReferenceProperty = serializedObject.FindProperty("levelMarkerRootReference");
            _flashColorProperty = serializedObject.FindProperty("flashColor");
            _offlineColorProperty = serializedObject.FindProperty("offlineColor");
            _flashDurationProperty = serializedObject.FindProperty("flashDuration");
            _upgradeFlashColorProperty = serializedObject.FindProperty("upgradeFlashColor");
            _upgradePulseDurationProperty = serializedObject.FindProperty("upgradePulseDuration");
            _upgradeScaleMultiplierProperty = serializedObject.FindProperty("upgradeScaleMultiplier");
            _feedbackMaterialProperty = serializedObject.FindProperty("feedbackMaterial");
            _levelPipSpriteProperty = serializedObject.FindProperty("levelPipSprite");
            _levelPipColorProperty = serializedObject.FindProperty("levelPipColor");
            _levelPipOffsetProperty = serializedObject.FindProperty("levelPipOffset");
            _levelPipSpacingProperty = serializedObject.FindProperty("levelPipSpacing");
            _levelPipScaleProperty = serializedObject.FindProperty("levelPipScale");
            _levelPipSortingOffsetProperty = serializedObject.FindProperty("levelPipSortingOffset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DefenseTower tower = (DefenseTower)target;

            EditorGUILayout.HelpBox(
                "这份 Prefab 现在默认只展示当前塔型会用到的 tuning。这样作者打开单体塔、减速塔或炸弹塔时，不会再先看到一大块无关参数。",
                MessageType.Info);

            EditorGUILayout.PropertyField(_buildTypeProperty);
            EditorGUILayout.Space(4f);

            DrawAuthoringActions(tower);

            DrawActiveTuningBlock(tower.BuildType);
            DrawOptionalOtherTuningBlocks(tower.BuildType);
            DrawProgressionBlock();
            DrawVisualReferenceBlock();
            DrawSharedVisualBlock();
            DrawLevelMarkerBlock();
            DrawRuntimeReadOnlyBlock(tower);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawAuthoringActions(DefenseTower tower)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Authoring Actions", EditorStyles.boldLabel);
                if (GUILayout.Button("Materialize Visual Roots"))
                {
                    MaterializeVisualRoots(tower);
                }
            }
        }

        private void MaterializeVisualRoots(DefenseTower tower)
        {
            if (tower == null)
            {
                return;
            }

            Transform root = tower.transform;

            Transform feedbackRoot = EnsureChild(root, "FeedbackRoot");
            Transform singleTargetRoot = EnsureChild(feedbackRoot, "SingleTargetFeedbackRoot");
            Transform slowFieldRoot = EnsureChild(feedbackRoot, "SlowFieldFeedbackRoot");
            Transform bombardRoot = EnsureChild(feedbackRoot, "BombardFeedbackRoot");
            Transform signatureRoot = EnsureChild(root, "TypeSignatureRoot");
            Transform levelMarkerRoot = EnsureChild(root, "LevelMarkerRoot");

            _feedbackRootReferenceProperty.objectReferenceValue = feedbackRoot;
            _singleTargetFeedbackRootReferenceProperty.objectReferenceValue = singleTargetRoot;
            _slowFieldFeedbackRootReferenceProperty.objectReferenceValue = slowFieldRoot;
            _bombardFeedbackRootReferenceProperty.objectReferenceValue = bombardRoot;
            _typeSignatureRootReferenceProperty.objectReferenceValue = signatureRoot;
            _levelMarkerRootReferenceProperty.objectReferenceValue = levelMarkerRoot;

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tower);
            EditorSceneManager.MarkSceneDirty(tower.gameObject.scene);
        }

        private static Transform EnsureChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                return existing;
            }

            GameObject childObject = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(childObject, $"Create {childName}");
            Transform child = childObject.transform;
            child.SetParent(parent, false);
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            return child;
        }

        private void DrawActiveTuningBlock(TowerType buildType)
        {
            SerializedProperty activeProperty = GetTuningProperty(buildType);
            string label = GetTuningLabel(buildType);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                if (activeProperty != null)
                {
                    EditorGUILayout.PropertyField(activeProperty, includeChildren: true);
                }
            }
        }

        private void DrawOptionalOtherTuningBlocks(TowerType buildType)
        {
            _showAllTuningBlocks = EditorGUILayout.Foldout(_showAllTuningBlocks, "查看其余塔型 Tuning", true);
            if (!_showAllTuningBlocks)
            {
                return;
            }

            DrawTuningIfNotActive(TowerType.SingleTarget, buildType);
            DrawTuningIfNotActive(TowerType.SlowField, buildType);
            DrawTuningIfNotActive(TowerType.Bombard, buildType);
        }

        private void DrawTuningIfNotActive(TowerType candidateType, TowerType activeType)
        {
            if (candidateType == activeType)
            {
                return;
            }

            SerializedProperty property = GetTuningProperty(candidateType);
            if (property == null)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(GetTuningLabel(candidateType), EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(property, includeChildren: true);
            }
        }

        private void DrawProgressionBlock()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Progression", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_currentLevelProperty);
                EditorGUILayout.PropertyField(_maxLevelProperty);
            }
        }

        private void DrawVisualReferenceBlock()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Visual References", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_bodyRendererReferenceProperty);
                EditorGUILayout.PropertyField(_feedbackRootReferenceProperty);
                EditorGUILayout.PropertyField(_singleTargetFeedbackRootReferenceProperty);
                EditorGUILayout.PropertyField(_slowFieldFeedbackRootReferenceProperty);
                EditorGUILayout.PropertyField(_bombardFeedbackRootReferenceProperty);
                EditorGUILayout.PropertyField(_typeSignatureRootReferenceProperty);
                EditorGUILayout.PropertyField(_levelMarkerRootReferenceProperty);
            }
        }

        private void DrawSharedVisualBlock()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Shared Visuals", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_flashColorProperty);
                EditorGUILayout.PropertyField(_offlineColorProperty);
                EditorGUILayout.PropertyField(_flashDurationProperty);
                EditorGUILayout.PropertyField(_upgradeFlashColorProperty);
                EditorGUILayout.PropertyField(_upgradePulseDurationProperty);
                EditorGUILayout.PropertyField(_upgradeScaleMultiplierProperty);
                EditorGUILayout.PropertyField(_feedbackMaterialProperty);
            }
        }

        private void DrawLevelMarkerBlock()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Level Marker", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_levelPipSpriteProperty);
                EditorGUILayout.PropertyField(_levelPipColorProperty);
                EditorGUILayout.PropertyField(_levelPipOffsetProperty);
                EditorGUILayout.PropertyField(_levelPipSpacingProperty);
                EditorGUILayout.PropertyField(_levelPipScaleProperty);
                EditorGUILayout.PropertyField(_levelPipSortingOffsetProperty);
            }
        }

        private static void DrawRuntimeReadOnlyBlock(DefenseTower tower)
        {
            if (!Application.isPlaying || tower == null)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Runtime Snapshot", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Tower Number", tower.TowerNumber.ToString());
                EditorGUILayout.LabelField("Current Level", tower.CurrentLevel.ToString());
                EditorGUILayout.LabelField("Damage / Shot", tower.DamagePerShot.ToString());
                EditorGUILayout.LabelField("Power Required", tower.PowerRequired.ToString());
                EditorGUILayout.LabelField("Attack Range", tower.AttackRange.ToString("0.00"));
                EditorGUILayout.LabelField("Powered", tower.IsPowered ? "Yes" : "No");
                EditorGUILayout.LabelField("Power Status", tower.PowerStatusMessage);
            }
        }

        private SerializedProperty GetTuningProperty(TowerType buildType)
        {
            switch (buildType)
            {
                case TowerType.SlowField:
                    return _slowFieldTuningProperty;

                case TowerType.Bombard:
                    return _bombardTuningProperty;

                default:
                    return _singleTargetTuningProperty;
            }
        }

        private static string GetTuningLabel(TowerType buildType)
        {
            switch (buildType)
            {
                case TowerType.SlowField:
                    return "Slow Field Tuning";

                case TowerType.Bombard:
                    return "Bombard Tuning";

                default:
                    return "Single Target Tuning";
            }
        }
    }
}

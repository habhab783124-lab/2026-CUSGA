using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// 这个作者工具把“已经切好的怪物移动帧”批量整理成真正可用的动画资产。
    ///
    /// 当前正确来源是 `NoBG/` 下已经在 Unity 里导入成多切片 SpriteSheet 的资源。
    /// 工具负责的事情只有三件：
    /// 1. 把每组帧生成循环播放的 `AnimationClip`
    /// 2. 给每只敌人生成一个最小可用的 `AnimatorController`
    /// 3. 把控制器挂到 prefab 里真正显示怪物贴图的 `VisualScaleRoot` 上
    ///
    /// 之所以把 Animator 挂在子节点，而不是敌人 prefab 根节点，是因为当前敌人的实际
    /// SpriteRenderer 在 `VisualScaleRoot`，根节点自己的 SpriteRenderer 还是空的。
    /// 如果误绑到根节点，就会出现“控制器存在，但画面不动”的假接入结果。
    /// </summary>
    public static class EnemyMoveAnimationAuthoringTool
    {
        private const string SpriteSheetRootFolder = "Assets/Art/Enemy/移动/NoBG";
        private const string AnimationRootFolder = "Assets/Animations/TowerDefense/Enemies";
        private const string ClipsFolder = AnimationRootFolder + "/Clips";
        private const string ControllersFolder = AnimationRootFolder + "/Controllers";
        private const string VisualRootName = "VisualScaleRoot";
        private const string HealthBarRootName = "HealthBarRoot";
        private const float ClipFrameRate = 12f;
        private const float HealthBarGapFromVisualTop = 0.18f;
        private const int EnemyBodySortingOrder = 10;
        private const int HealthBarSortingOrder = 12;

        private static readonly EnemyAnimationBinding[] Bindings =
        {
            new EnemyAnimationBinding("拾荒者", "ScavengerEnemy", "ScavengerMove"),
            new EnemyAnimationBinding("狼", "WolfEnemy", "WolfMove"),
            new EnemyAnimationBinding("旗帜拾荒者", "BannerScavengerEnemy", "BannerScavengerMove"),
            new EnemyAnimationBinding("机械师", "MechanicEnemy", "MechanicMove"),
            new EnemyAnimationBinding("重甲机械兵", "HeavyArmoredMachineEnemy", "HeavyArmoredMachineMove"),
            new EnemyAnimationBinding("隐身人", "StealthStalkerEnemy", "StealthStalkerMove"),
            new EnemyAnimationBinding("憎恶", "AbominationEnemy", "AbominationMove"),
            new EnemyAnimationBinding("幼年拾荒者", "SmallScavengerEnemy", "SmallScavengerMove")
        };

        [MenuItem("Tools/Tower Defense/Authoring/重建敌人移动动画")]
        public static void RebuildEnemyMoveAnimations()
        {
            try
            {
                EnsureFolder(AnimationRootFolder);
                EnsureFolder(ClipsFolder);
                EnsureFolder(ControllersFolder);

                List<string> updatedPrefabs = new List<string>(Bindings.Length);
                List<string> createdClips = new List<string>(Bindings.Length);
                List<string> createdControllers = new List<string>(Bindings.Length);

                for (int index = 0; index < Bindings.Length; index++)
                {
                    EnemyAnimationBinding binding = Bindings[index];

                    Sprite[] frames = LoadFrames(binding.FrameFolderName);
                    AnimationClip clip = CreateOrUpdateMoveClip(binding.AnimationAssetBaseName, frames);
                    AnimatorController controller = CreateOrUpdateController(binding.AnimationAssetBaseName, clip);
                    ApplyControllerToPrefab(binding.PrefabName, controller, frames[0]);

                    if (frames.Length > 0 && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(frames[0], out string guid, out long localId))
                    {
                        Debug.Log(
                            $"[EnemyMoveAnimationAuthoringTool] {binding.AnimationAssetBaseName} first frame -> " +
                            $"{AssetDatabase.GetAssetPath(frames[0])} | sprite={frames[0].name} | guid={guid} | localId={localId}");
                    }

                    createdClips.Add(AssetDatabase.GetAssetPath(clip));
                    createdControllers.Add(AssetDatabase.GetAssetPath(controller));
                    updatedPrefabs.Add(GetPrefabPath(binding.PrefabName));
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(
                    "EnemyMoveAnimationAuthoringTool rebuilt enemy move animations successfully.\n" +
                    $"Clips:\n- {string.Join("\n- ", createdClips)}\n" +
                    $"Controllers:\n- {string.Join("\n- ", createdControllers)}\n" +
                    $"Prefabs:\n- {string.Join("\n- ", updatedPrefabs)}");
            }
            catch (Exception exception)
            {
                Debug.LogError($"EnemyMoveAnimationAuthoringTool failed: {exception}");
                throw;
            }
        }

        /// <summary>
        /// 提供给 batchmode 的入口。
        /// 保持和菜单项分开，是为了以后命令行执行时日志更直接，也更方便失败时定位。
        /// </summary>
        public static void RebuildEnemyMoveAnimationsBatch()
        {
            RebuildEnemyMoveAnimations();
        }

        private static Sprite[] LoadFrames(string frameFolderName)
        {
            string spriteSheetPath = $"{SpriteSheetRootFolder}/{frameFolderName}.png";
            if (!System.IO.File.Exists(spriteSheetPath))
            {
                throw new DirectoryNotFoundException($"Missing sprite sheet: {spriteSheetPath}");
            }

            UnityEngine.Object[] assetsAtPath = AssetDatabase.LoadAllAssetsAtPath(spriteSheetPath);
            List<Sprite> frames = new List<Sprite>(10);
            for (int index = 0; index < assetsAtPath.Length; index++)
            {
                if (assetsAtPath[index] is Sprite sprite && !string.IsNullOrWhiteSpace(sprite.name))
                {
                    frames.Add(sprite);
                }
            }

            frames.Sort((left, right) => CompareSpriteFrameNames(left.name, right.name));

            if (frames.Count == 0)
            {
                throw new InvalidOperationException($"No sliced sprites found in sprite sheet: {spriteSheetPath}");
            }

            return frames.ToArray();
        }

        private static int CompareSpriteFrameNames(string left, string right)
        {
            int leftFrame = ExtractTrailingFrameIndex(left);
            int rightFrame = ExtractTrailingFrameIndex(right);
            if (leftFrame != rightFrame)
            {
                return leftFrame.CompareTo(rightFrame);
            }

            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static int ExtractTrailingFrameIndex(string spriteName)
        {
            if (string.IsNullOrWhiteSpace(spriteName))
            {
                return int.MaxValue;
            }

            int underscoreIndex = spriteName.LastIndexOf('_');
            if (underscoreIndex >= 0 &&
                underscoreIndex < spriteName.Length - 1 &&
                int.TryParse(spriteName.Substring(underscoreIndex + 1), out int frameIndex))
            {
                return frameIndex;
            }

            return int.MaxValue;
        }

        private static AnimationClip CreateOrUpdateMoveClip(string animationAssetBaseName, Sprite[] frames)
        {
            string clipPath = $"{ClipsFolder}/{animationAssetBaseName}.anim";
            AnimationClip existingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existingClip != null)
            {
                AssetDatabase.DeleteAsset(clipPath);
            }

            AnimationClip clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, clipPath);

            clip.frameRate = ClipFrameRate;

            EditorCurveBinding spriteBinding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = string.Empty,
                propertyName = "m_Sprite"
            };

            ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[frames.Length];
            for (int index = 0; index < frames.Length; index++)
            {
                keyframes[index] = new ObjectReferenceKeyframe
                {
                    time = index / ClipFrameRate,
                    value = frames[index]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyframes);

            SerializedObject serializedClip = new SerializedObject(clip);
            SerializedProperty clipSettings = serializedClip.FindProperty("m_AnimationClipSettings");
            if (clipSettings != null)
            {
                SerializedProperty loopTime = clipSettings.FindPropertyRelative("m_LoopTime");
                if (loopTime != null)
                {
                    loopTime.boolValue = true;
                }
            }

            serializedClip.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimatorController CreateOrUpdateController(string animationAssetBaseName, AnimationClip clip)
        {
            string controllerPath = $"{ControllersFolder}/{animationAssetBaseName}.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            }

            AnimatorControllerLayer layer = controller.layers[0];
            AnimatorStateMachine stateMachine = layer.stateMachine;
            AnimatorState moveState = FindState(stateMachine, "Move") ?? stateMachine.AddState("Move");
            moveState.motion = clip;
            moveState.speed = 1f;
            stateMachine.defaultState = moveState;

            RemoveOtherStates(stateMachine, moveState);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
        {
            ChildAnimatorState[] states = stateMachine.states;
            for (int index = 0; index < states.Length; index++)
            {
                if (states[index].state != null && states[index].state.name == stateName)
                {
                    return states[index].state;
                }
            }

            return null;
        }

        private static void RemoveOtherStates(AnimatorStateMachine stateMachine, AnimatorState keepState)
        {
            ChildAnimatorState[] states = stateMachine.states;
            for (int index = states.Length - 1; index >= 0; index--)
            {
                AnimatorState state = states[index].state;
                if (state != null && state != keepState)
                {
                    stateMachine.RemoveState(state);
                }
            }
        }

        private static void ApplyControllerToPrefab(string prefabName, RuntimeAnimatorController controller, Sprite defaultSprite)
        {
            string prefabPath = GetPrefabPath(prefabName);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform visualRoot = prefabRoot.transform.Find(VisualRootName);
                if (visualRoot == null)
                {
                    throw new InvalidOperationException($"Prefab '{prefabPath}' is missing child '{VisualRootName}'.");
                }

                SpriteRenderer spriteRenderer = visualRoot.GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    throw new InvalidOperationException($"Visual root '{VisualRootName}' in prefab '{prefabPath}' is missing SpriteRenderer.");
                }

                // 让场景静止显示时也能直接看到正确怪物外观，而不是继续保留空白/占位图。
                // 这里把默认 Sprite 显式设成移动动画第一帧，这样：
                // 1. Scene 视图里不进 Play 也能看见怪物本体
                // 2. Animator 真正开始播放后，仍然会从同一套帧资源接管显示
                if (defaultSprite != null)
                {
                    spriteRenderer.sprite = defaultSprite;
                }

                // 路面/路径美术层在测试场景里经常使用更高的 sortingOrder（例如 3~6）。
                // 如果怪物主体还保持在 0，就会在 Play 里被白色道路直接盖住，
                // 视觉上只剩血条像是在路上漂浮。
                //
                // 因此这里显式把怪物主体抬到一个稳定高于道路美术层的排序值。
                spriteRenderer.sortingOrder = EnemyBodySortingOrder;

                Transform healthBarRoot = prefabRoot.transform.Find(HealthBarRootName);
                if (healthBarRoot != null)
                {
                    AlignHealthBarAboveVisual(prefabRoot.transform, visualRoot, spriteRenderer, healthBarRoot);
                    NormalizeHealthBarSorting(healthBarRoot);
                }

                Animator animator = visualRoot.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = visualRoot.gameObject.AddComponent<Animator>();
                }

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                EditorUtility.SetDirty(animator);
                EditorUtility.SetDirty(visualRoot.gameObject);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        /// <summary>
        /// 这里不再使用“所有怪物同一个绝对 y 值”的做法。
        ///
        /// 原因是不同怪物首帧的体型、pivot、缩放倍率都不一样，
        /// 统一写死一个高度只会让大怪太近、小怪太远。
        ///
        /// 真正应该统一的是：
        /// - 血条底边距离怪物形象最高点的额外间距
        ///
        /// 所以这里的做法是：
        /// 1. 读取当前怪物主显示 sprite 在 prefab 根坐标里的最高点
        /// 2. 读取当前血条所有 SpriteRenderer 在 prefab 根坐标里的最低点
        /// 3. 把整个 HealthBarRoot 沿 y 轴平移，让“血条最低点 = 怪物最高点 + 固定间距”
        /// </summary>
        private static void AlignHealthBarAboveVisual(
            Transform prefabRoot,
            Transform visualRoot,
            SpriteRenderer visualSpriteRenderer,
            Transform healthBarRoot)
        {
            if (prefabRoot == null || visualRoot == null || visualSpriteRenderer == null || healthBarRoot == null)
            {
                return;
            }

            if (!TryGetSpriteRendererVerticalBoundsInRootSpace(visualSpriteRenderer, prefabRoot, out _, out float visualTopY))
            {
                return;
            }

            SpriteRenderer[] healthBarRenderers = healthBarRoot.GetComponentsInChildren<SpriteRenderer>(true);
            if (healthBarRenderers == null || healthBarRenderers.Length == 0)
            {
                return;
            }

            if (!TryGetRendererGroupVerticalBoundsInRootSpace(healthBarRenderers, prefabRoot, out float healthBarBottomY, out _))
            {
                return;
            }

            float desiredHealthBarBottomY = visualTopY + HealthBarGapFromVisualTop;
            float deltaY = desiredHealthBarBottomY - healthBarBottomY;
            if (Mathf.Abs(deltaY) <= 0.0001f)
            {
                return;
            }

            Vector3 localPosition = healthBarRoot.localPosition;
            localPosition.y += deltaY;
            healthBarRoot.localPosition = localPosition;
            EditorUtility.SetDirty(healthBarRoot.gameObject);
        }

        private static bool TryGetRendererGroupVerticalBoundsInRootSpace(
            IReadOnlyList<SpriteRenderer> renderers,
            Transform root,
            out float minY,
            out float maxY)
        {
            minY = float.PositiveInfinity;
            maxY = float.NegativeInfinity;

            if (renderers == null || root == null)
            {
                return false;
            }

            bool hasAnyBounds = false;
            for (int index = 0; index < renderers.Count; index++)
            {
                SpriteRenderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                if (!TryGetSpriteRendererVerticalBoundsInRootSpace(renderer, root, out float rendererMinY, out float rendererMaxY))
                {
                    continue;
                }

                hasAnyBounds = true;
                minY = Mathf.Min(minY, rendererMinY);
                maxY = Mathf.Max(maxY, rendererMaxY);
            }

            return hasAnyBounds;
        }

        private static bool TryGetSpriteRendererVerticalBoundsInRootSpace(
            SpriteRenderer renderer,
            Transform root,
            out float minY,
            out float maxY)
        {
            minY = 0f;
            maxY = 0f;

            if (renderer == null || renderer.sprite == null || root == null)
            {
                return false;
            }

            Bounds spriteBounds = renderer.sprite.bounds;
            Vector3[] localCorners =
            {
                new Vector3(spriteBounds.min.x, spriteBounds.min.y, 0f),
                new Vector3(spriteBounds.min.x, spriteBounds.max.y, 0f),
                new Vector3(spriteBounds.max.x, spriteBounds.min.y, 0f),
                new Vector3(spriteBounds.max.x, spriteBounds.max.y, 0f)
            };

            minY = float.PositiveInfinity;
            maxY = float.NegativeInfinity;

            for (int index = 0; index < localCorners.Length; index++)
            {
                Vector3 worldPoint = renderer.transform.TransformPoint(localCorners[index]);
                Vector3 rootLocalPoint = root.InverseTransformPoint(worldPoint);
                minY = Mathf.Min(minY, rootLocalPoint.y);
                maxY = Mathf.Max(maxY, rootLocalPoint.y);
            }

            return true;
        }

        private static void NormalizeHealthBarSorting(Transform healthBarRoot)
        {
            if (healthBarRoot == null)
            {
                return;
            }

            SpriteRenderer[] healthBarRenderers = healthBarRoot.GetComponentsInChildren<SpriteRenderer>(true);
            for (int index = 0; index < healthBarRenderers.Length; index++)
            {
                SpriteRenderer renderer = healthBarRenderers[index];
                if (renderer == null)
                {
                    continue;
                }

                renderer.sortingOrder = HealthBarSortingOrder;
                EditorUtility.SetDirty(renderer);
            }
        }

        private static string GetPrefabPath(string prefabName)
        {
            string prefabPath = $"Assets/Prefabs/TowerDefense/Runtime/Enemies/{prefabName}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                throw new FileNotFoundException($"Missing enemy prefab: {prefabPath}");
            }

            return prefabPath;
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

        private readonly struct EnemyAnimationBinding
        {
            public EnemyAnimationBinding(string frameFolderName, string prefabName, string animationAssetBaseName)
            {
                FrameFolderName = frameFolderName;
                PrefabName = prefabName;
                AnimationAssetBaseName = animationAssetBaseName;
            }

            public string FrameFolderName { get; }
            public string PrefabName { get; }
            public string AnimationAssetBaseName { get; }
        }
    }
}

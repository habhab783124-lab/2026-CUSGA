using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// `EnemyPathPointOrderMode` 描述“把一批散落在 Scene 里的点收成路径时，该按什么规则排顺序”。
    ///
    /// 这里故意把模式做得很直白，因为这类工具的目标不是炫技，而是减少作者来回重排点位的时间：
    /// - `HierarchyOrder`
    ///   适合你已经在层级里手动排过点位顺序，只想一键收口成正式路径。
    /// - `LeftToRight`
    ///   适合近似横向推进的路。
    /// - `TopToBottom`
    ///   适合近似纵向推进的路。
    /// - `NearestChain`
    ///   适合路线拐弯比较多、但点位在空间上已经大致摆对的情况。
    /// </summary>
    internal enum EnemyPathPointOrderMode
    {
        HierarchyOrder,
        LeftToRight,
        TopToBottom,
        NearestChain
    }

    /// <summary>
    /// `EnemyPathAuthoringUtility` 把“路径作者工具真正会重复做的低层操作”集中收口。
    ///
    /// 这样做有两个好处：
    /// 1. `EnemyPathEditor` 和独立 `EditorWindow` 可以共用同一套逻辑，避免一处修了另一处忘记同步。
    /// 2. 以后如果你还想继续加“自动插值补点”“按折线段吸附”“批量重命名”等功能，
    ///    也能继续围绕这一个工具层扩展，而不是把 Scene 改写逻辑散在各个 Inspector 里。
    /// </summary>
    internal static class EnemyPathAuthoringUtility
    {
        internal const string WaypointRootName = "Waypoints";
        private const string ReadabilityRootName = "__PathReadability";

        /// <summary>
        /// 统一确保 `EnemyPath` 有一个明确的 `Waypoints` 根节点。
        ///
        /// 我们这里优先坚持“路径点有独立根节点”的结构，而不是继续把点散挂在 `EnemyPath` 自己下面，
        /// 因为后者一旦和可读性覆盖层、装饰物或作者临时辅助对象混在一起，就很容易让层级变乱。
        /// </summary>
        internal static Transform EnsureWaypointRoot(EnemyPath enemyPath)
        {
            if (enemyPath == null)
            {
                return null;
            }

            SerializedObject serializedObject = new SerializedObject(enemyPath);
            SerializedProperty waypointRootProperty = serializedObject.FindProperty("waypointRootReference");
            Transform existingRoot = waypointRootProperty != null ? waypointRootProperty.objectReferenceValue as Transform : null;
            if (existingRoot != null)
            {
                return existingRoot;
            }

            Transform rootFromHierarchy = enemyPath.transform.Find(WaypointRootName);
            if (rootFromHierarchy == null)
            {
                GameObject rootObject = new GameObject(WaypointRootName);
                Undo.RegisterCreatedObjectUndo(rootObject, "创建 Waypoints 根节点");
                rootFromHierarchy = rootObject.transform;
                rootFromHierarchy.SetParent(enemyPath.transform, false);
                rootFromHierarchy.localPosition = Vector3.zero;
                rootFromHierarchy.localRotation = Quaternion.identity;
                rootFromHierarchy.localScale = Vector3.one;
            }

            if (waypointRootProperty != null)
            {
                waypointRootProperty.objectReferenceValue = rootFromHierarchy;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            MarkSceneDirty(enemyPath);
            return rootFromHierarchy;
        }

        /// <summary>
        /// 读取当前路径里“真正作为路径点使用”的对象列表。
        ///
        /// 这里故意只认两种来源：
        /// - 已显式指定的 `Waypoints` 根节点的直接子节点
        /// - 兼容旧结构时，`EnemyPath` 直接子节点中排除可读性根节点后的结果
        ///
        /// 这样作者在工具里看到的顺序，就会尽量和实际运行时缓存路径点时看到的顺序一致。
        /// </summary>
        internal static List<Transform> GetWaypointChildren(EnemyPath enemyPath)
        {
            List<Transform> results = new List<Transform>();
            if (enemyPath == null)
            {
                return results;
            }

            Transform waypointRoot = enemyPath.WaypointRoot;
            if (waypointRoot != null)
            {
                foreach (Transform child in waypointRoot)
                {
                    if (child != null)
                    {
                        results.Add(child);
                    }
                }

                return results;
            }

            foreach (Transform child in enemyPath.transform)
            {
                if (child == null || child.name == ReadabilityRootName || child.name == WaypointRootName)
                {
                    continue;
                }

                results.Add(child);
            }

            return results;
        }

        /// <summary>
        /// 从当前 Unity 选择集中筛出“适合拿来做路径点”的 Transform。
        ///
        /// 这里不会擅自限制点必须已经挂在 `EnemyPath` 下面，
        /// 因为你的需求就是“先在场景里随手摆点，再选中后让工具来收口”。
        /// </summary>
        internal static List<Transform> GetSelectedCandidatePoints(EnemyPath enemyPath)
        {
            List<Transform> points = new List<Transform>();
            Transform waypointRoot = enemyPath != null ? enemyPath.WaypointRoot : null;

            foreach (Transform selectedTransform in Selection.transforms)
            {
                if (selectedTransform == null)
                {
                    continue;
                }

                if (enemyPath != null && selectedTransform == enemyPath.transform)
                {
                    continue;
                }

                if (waypointRoot != null && selectedTransform == waypointRoot)
                {
                    continue;
                }

                if (selectedTransform.name == ReadabilityRootName || selectedTransform.name == WaypointRootName)
                {
                    continue;
                }

                if (!points.Contains(selectedTransform))
                {
                    points.Add(selectedTransform);
                }
            }

            return points;
        }

        /// <summary>
        /// 按指定规则对一批点进行排序。
        ///
        /// 这一步的目标不是“永远自动推断出正确关卡设计”，
        /// 而是尽量把第一轮顺序排到接近作者意图，让后续微调成本变小。
        /// </summary>
        internal static List<Transform> BuildOrderedSelection(
            IReadOnlyList<Transform> sourcePoints,
            EnemyPathPointOrderMode orderMode,
            Transform chainStartReference)
        {
            List<Transform> points = sourcePoints != null
                ? sourcePoints.Where(point => point != null).Distinct().ToList()
                : new List<Transform>();

            switch (orderMode)
            {
                case EnemyPathPointOrderMode.LeftToRight:
                    points = points
                        .OrderBy(point => point.position.x)
                        .ThenByDescending(point => point.position.y)
                        .ToList();
                    break;

                case EnemyPathPointOrderMode.TopToBottom:
                    points = points
                        .OrderByDescending(point => point.position.y)
                        .ThenBy(point => point.position.x)
                        .ToList();
                    break;

                case EnemyPathPointOrderMode.NearestChain:
                    points = BuildNearestChain(points, chainStartReference);
                    break;

                default:
                    points = points
                        .OrderBy(GetHierarchyOrderKey)
                        .ToList();
                    break;
            }

            return points;
        }

        /// <summary>
        /// 把当前列表顺序真正落回 Scene 层级。
        ///
        /// 这一步非常关键，因为运行时 `EnemyPath` 就是按层级顺序缓存路径点的。
        /// 换句话说，编辑器列表里“看起来顺了”还不够，必须真正改到 Hierarchy 才算生效。
        /// </summary>
        internal static void ApplyWaypointOrder(EnemyPath enemyPath, IList<Transform> orderedWaypoints, bool renameWaypoints)
        {
            if (enemyPath == null || orderedWaypoints == null)
            {
                return;
            }

            Transform waypointRoot = EnsureWaypointRoot(enemyPath);
            if (waypointRoot == null)
            {
                return;
            }

            Undo.RecordObject(enemyPath, "更新敌人路径顺序");
            Undo.RecordObject(waypointRoot, "更新敌人路径顺序");

            for (int index = 0; index < orderedWaypoints.Count; index++)
            {
                Transform waypoint = orderedWaypoints[index];
                if (waypoint == null)
                {
                    continue;
                }

                if (waypoint.parent != waypointRoot)
                {
                    Undo.SetTransformParent(waypoint, waypointRoot, "把点收进 EnemyPath");
                }

                waypoint.SetSiblingIndex(index);

                if (renameWaypoints)
                {
                    Undo.RecordObject(waypoint.gameObject, "重命名路径点");
                    waypoint.name = BuildWaypointName(index, orderedWaypoints.Count);
                }
            }

            enemyPath.EditorRefreshAuthoringState();
            MarkSceneDirty(enemyPath);
        }

        /// <summary>
        /// 仅对现有路径点重新编号，不改顺序。
        /// </summary>
        internal static void RenumberWaypoints(EnemyPath enemyPath)
        {
            List<Transform> currentWaypoints = GetWaypointChildren(enemyPath);
            for (int index = 0; index < currentWaypoints.Count; index++)
            {
                Transform waypoint = currentWaypoints[index];
                if (waypoint == null)
                {
                    continue;
                }

                Undo.RecordObject(waypoint.gameObject, "重命名路径点");
                waypoint.name = BuildWaypointName(index, currentWaypoints.Count);
            }

            enemyPath?.EditorRefreshAuthoringState();
            MarkSceneDirty(enemyPath);
        }

        /// <summary>
        /// Returns the currently selected points that already belong to the target path.
        ///
        /// We keep this as a dedicated helper because "selected points in scene" and
        /// "points that are already part of the path order" are two different concepts.
        /// The swap shortcut only makes sense for the latter.
        /// </summary>
        internal static List<Transform> GetSelectedExistingWaypoints(EnemyPath enemyPath)
        {
            List<Transform> currentWaypoints = GetWaypointChildren(enemyPath);
            return GetSelectedCandidatePoints(enemyPath)
                .Where(currentWaypoints.Contains)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Returns the currently selected points that are not yet inside the target path.
        ///
        /// This is the list used by the "insert after current point" and "append to end"
        /// shortcuts, because those actions are meant to pull fresh scene points into an
        /// already-authored path without forcing the author to rebuild the whole order.
        /// </summary>
        internal static List<Transform> GetSelectedNewWaypointCandidates(EnemyPath enemyPath)
        {
            List<Transform> currentWaypoints = GetWaypointChildren(enemyPath);
            return GetSelectedCandidatePoints(enemyPath)
                .Where(point => !currentWaypoints.Contains(point))
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Swaps the order of exactly two selected waypoints.
        ///
        /// This is intentionally strict: if the author selected anything other than two
        /// existing path points, the shortcut simply refuses to run. Tools like this
        /// should be predictable rather than "smart" in surprising ways.
        /// </summary>
        internal static bool TrySwapSelectedWaypoints(EnemyPath enemyPath, bool renameWaypoints)
        {
            if (enemyPath == null)
            {
                return false;
            }

            List<Transform> selectedWaypoints = GetSelectedExistingWaypoints(enemyPath);
            if (selectedWaypoints.Count != 2)
            {
                return false;
            }

            List<Transform> currentWaypoints = GetWaypointChildren(enemyPath);
            int firstIndex = currentWaypoints.IndexOf(selectedWaypoints[0]);
            int secondIndex = currentWaypoints.IndexOf(selectedWaypoints[1]);
            if (firstIndex < 0 || secondIndex < 0 || firstIndex == secondIndex)
            {
                return false;
            }

            Transform cachedWaypoint = currentWaypoints[firstIndex];
            currentWaypoints[firstIndex] = currentWaypoints[secondIndex];
            currentWaypoints[secondIndex] = cachedWaypoint;
            ApplyWaypointOrder(enemyPath, currentWaypoints, renameWaypoints);
            return true;
        }

        /// <summary>
        /// Inserts the selected external points right after the provided anchor waypoint.
        ///
        /// This is the most practical "micro-fix" shortcut when the author notices:
        /// "these two new points should live after this waypoint, but I do not want to
        /// rebuild the whole path from scratch".
        /// </summary>
        internal static bool TryInsertSelectedCandidatesAfter(
            EnemyPath enemyPath,
            Transform anchorWaypoint,
            EnemyPathPointOrderMode orderMode,
            Transform chainStartReference,
            bool renameWaypoints)
        {
            if (enemyPath == null || anchorWaypoint == null)
            {
                return false;
            }

            List<Transform> currentWaypoints = GetWaypointChildren(enemyPath);
            int anchorIndex = currentWaypoints.IndexOf(anchorWaypoint);
            if (anchorIndex < 0)
            {
                return false;
            }

            List<Transform> newPoints = BuildOrderedSelection(
                GetSelectedNewWaypointCandidates(enemyPath),
                orderMode,
                chainStartReference);
            if (newPoints.Count == 0)
            {
                return false;
            }

            currentWaypoints.InsertRange(anchorIndex + 1, newPoints);
            ApplyWaypointOrder(enemyPath, currentWaypoints, renameWaypoints);
            return true;
        }

        /// <summary>
        /// Appends the selected external points to the end of the current path.
        ///
        /// This is the fastest authoring path when the route is still growing outward
        /// and the new points clearly belong at the tail of the line.
        /// </summary>
        internal static bool TryAppendSelectedCandidatesToEnd(
            EnemyPath enemyPath,
            EnemyPathPointOrderMode orderMode,
            Transform chainStartReference,
            bool renameWaypoints)
        {
            if (enemyPath == null)
            {
                return false;
            }

            List<Transform> currentWaypoints = GetWaypointChildren(enemyPath);
            List<Transform> newPoints = BuildOrderedSelection(
                GetSelectedNewWaypointCandidates(enemyPath),
                orderMode,
                chainStartReference);
            if (newPoints.Count == 0)
            {
                return false;
            }

            currentWaypoints.AddRange(newPoints);
            ApplyWaypointOrder(enemyPath, currentWaypoints, renameWaypoints);
            return true;
        }

        /// <summary>
        /// Moves one existing waypoint to the end of the path.
        ///
        /// Even though this sounds simple, it is a very common correction during route
        /// authoring: one point is fine spatially, but it belongs to the tail instead
        /// of the middle.
        /// </summary>
        internal static bool TryMoveWaypointToEnd(EnemyPath enemyPath, Transform waypoint, bool renameWaypoints)
        {
            if (enemyPath == null || waypoint == null)
            {
                return false;
            }

            List<Transform> currentWaypoints = GetWaypointChildren(enemyPath);
            int currentIndex = currentWaypoints.IndexOf(waypoint);
            if (currentIndex < 0 || currentIndex == currentWaypoints.Count - 1)
            {
                return false;
            }

            currentWaypoints.RemoveAt(currentIndex);
            currentWaypoints.Add(waypoint);
            ApplyWaypointOrder(enemyPath, currentWaypoints, renameWaypoints);
            return true;
        }

        /// <summary>
        /// Moves one existing waypoint so it sits immediately after another existing waypoint.
        ///
        /// This is the exact "one or two points ended up in the wrong order" correction
        /// that map authors hit all the time. It is more precise than a simple swap,
        /// because the destination is expressed as "after this point" rather than
        /// "exchange these two indices".
        /// </summary>
        internal static bool TryMoveExistingWaypointAfter(
            EnemyPath enemyPath,
            Transform waypointToMove,
            Transform previousWaypoint,
            bool renameWaypoints)
        {
            if (enemyPath == null || waypointToMove == null || previousWaypoint == null || waypointToMove == previousWaypoint)
            {
                return false;
            }

            List<Transform> currentWaypoints = GetWaypointChildren(enemyPath);
            int moveIndex = currentWaypoints.IndexOf(waypointToMove);
            int previousIndex = currentWaypoints.IndexOf(previousWaypoint);
            if (moveIndex < 0 || previousIndex < 0)
            {
                return false;
            }

            currentWaypoints.RemoveAt(moveIndex);

            // When the moving point originally lived before the anchor,
            // the anchor index shifts left by one after removal.
            if (moveIndex < previousIndex)
            {
                previousIndex--;
            }

            currentWaypoints.Insert(previousIndex + 1, waypointToMove);
            ApplyWaypointOrder(enemyPath, currentWaypoints, renameWaypoints);
            return true;
        }

        /// <summary>
        /// 把当前场景状态标记为已修改。
        ///
        /// 这看起来像个很小的收尾，但其实非常重要：
        /// 如果作者辛辛苦苦排好了路径，Scene 却没被标记为 dirty，
        /// 关闭场景时就很容易把劳动成果悄悄丢掉。
        /// </summary>
        internal static void MarkSceneDirty(UnityEngine.Object context)
        {
            if (context == null)
            {
                return;
            }

            EditorUtility.SetDirty(context);

            if (context is Component component)
            {
                EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            }
            else if (context is GameObject gameObject)
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }

        /// <summary>
        /// 把当前选择集按“距离链”排成一条折线。
        ///
        /// 这是一个很实用但不过度聪明的贪心法：
        /// - 起点优先用作者指定的点
        /// - 没指定时用当前激活选择
        /// - 再没有就退回到最靠左的点
        ///
        /// 它不能替代作者判断，但非常适合“我点已经摆对了，只是懒得手动排顺序”的场景。
        /// </summary>
        private static List<Transform> BuildNearestChain(List<Transform> points, Transform chainStartReference)
        {
            List<Transform> remaining = new List<Transform>(points);
            List<Transform> ordered = new List<Transform>(points.Count);
            if (remaining.Count == 0)
            {
                return ordered;
            }

            Transform current = chainStartReference != null && remaining.Contains(chainStartReference)
                ? chainStartReference
                : Selection.activeTransform != null && remaining.Contains(Selection.activeTransform)
                    ? Selection.activeTransform
                    : remaining
                        .OrderBy(point => point.position.x)
                        .ThenByDescending(point => point.position.y)
                        .First();

            ordered.Add(current);
            remaining.Remove(current);

            while (remaining.Count > 0)
            {
                Transform next = remaining
                    .OrderBy(point => (point.position - current.position).sqrMagnitude)
                    .ThenBy(GetHierarchyOrderKey)
                    .First();
                ordered.Add(next);
                remaining.Remove(next);
                current = next;
            }

            return ordered;
        }

        /// <summary>
        /// 生成层级顺序键。
        ///
        /// 这里不用名字排序，是为了避免作者刚好把点命名得乱七八糟时，
        /// “看起来层级排好了，工具却按字典序给你重新打乱”的尴尬情况。
        /// </summary>
        private static string GetHierarchyOrderKey(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            Stack<string> hierarchyParts = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                hierarchyParts.Push($"{current.GetSiblingIndex():D4}_{current.name}");
                current = current.parent;
            }

            return string.Join("/", hierarchyParts);
        }

        /// <summary>
        /// 根据点数动态决定编号宽度。
        ///
        /// 这样 9 个点时不会显得太啰嗦，超过 99 个点时也不会因为位数不够而排序混乱。
        /// </summary>
        private static string BuildWaypointName(int zeroBasedIndex, int totalCount)
        {
            int digits = Mathf.Max(2, totalCount.ToString().Length);
            return $"Waypoint_{(zeroBasedIndex + 1).ToString($"D{digits}")}";
        }
    }

    /// <summary>
    /// `EnemyPathAuthoringTool` 是给关卡作者使用的“路径制作台”。
    ///
    /// 它的目标非常明确：
    /// - 先把你在 Scene 里随手摆好的点一键收成 `EnemyPath`
    /// - 再把“顺序错了两三个点”的调整成本降到最低
    ///
    /// 所以这个窗口故意围绕两个核心工作流来设计：
    /// 1. 收点：选中一批点 -> 选择排序规则 -> 一键收进路径
    /// 2. 微调：在列表里拖拽、上移、下移、反转、重命名 -> 一键应用到层级
    /// </summary>
    public sealed class EnemyPathAuthoringTool : EditorWindow
    {
        [SerializeField] private EnemyPath targetPath; // 中文：当前正在编辑的 EnemyPath
        [SerializeField] private EnemyPathPointOrderMode collectOrderMode = EnemyPathPointOrderMode.HierarchyOrder; // 中文：收点排序模式
        [SerializeField] private bool renameWaypointsOnApply = true; // 中文：应用时是否自动重命名
        [SerializeField] private Transform chainStartReference; // 中文：最近邻排序时的起点参考
        [SerializeField] private List<Transform> waypointBuffer = new List<Transform>(); // 中文：当前编辑中的路径点顺序缓存

        private ReorderableList _waypointList; // 中文：可拖拽重排列表

        [MenuItem("Tools/Tower Defense/路径点编辑工具")]
        public static void OpenWindow()
        {
            EnemyPathAuthoringTool window = GetWindow<EnemyPathAuthoringTool>("路径点工具");
            window.minSize = new Vector2(480f, 380f);
            window.TryAdoptCurrentSelection();
            window.EnsureList();
        }

        internal static void OpenWindow(EnemyPath enemyPath)
        {
            EnemyPathAuthoringTool window = GetWindow<EnemyPathAuthoringTool>("路径点工具");
            window.minSize = new Vector2(480f, 380f);
            window.targetPath = enemyPath;
            window.RefreshWaypointBufferFromScene();
            window.EnsureList();
            window.Repaint();
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            EnsureList();
            TryAdoptCurrentSelection();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            Repaint();
        }

        private void OnGUI()
        {
            EnsureList();
            DrawTargetPathSection();
            EditorGUILayout.Space(10f);
            DrawSelectionCollectSection();
            EditorGUILayout.Space(10f);
            DrawQuickFixSection();
            EditorGUILayout.Space(10f);
            DrawWaypointListSection();
            EditorGUILayout.Space(10f);
            DrawApplySection();
        }

        private void DrawTargetPathSection()
        {
            EditorGUILayout.LabelField("当前路径", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            targetPath = (EnemyPath)EditorGUILayout.ObjectField("EnemyPath 对象", targetPath, typeof(EnemyPath), true);
            if (EditorGUI.EndChangeCheck())
            {
                RefreshWaypointBufferFromScene();
            }

            if (targetPath == null)
            {
                EditorGUILayout.HelpBox("先选中一个 EnemyPath，或者把 EnemyPath 拖到这里。工具会围绕这条路径收点和调顺序。", MessageType.Info);
                return;
            }

            List<Transform> currentWaypoints = EnemyPathAuthoringUtility.GetWaypointChildren(targetPath);
            string waypointRootName = targetPath.WaypointRoot != null ? targetPath.WaypointRoot.name : "(直接子节点)";
            EditorGUILayout.HelpBox(
                $"当前路径：{targetPath.name}\n当前路径点数量：{currentWaypoints.Count}\n路径点根节点：{waypointRootName}",
                MessageType.None);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("指定 / 创建 Waypoints 根节点"))
            {
                EnemyPathAuthoringUtility.EnsureWaypointRoot(targetPath);
                targetPath.EditorRefreshAuthoringState();
                RefreshWaypointBufferFromScene();
            }

            if (GUILayout.Button("从场景刷新当前顺序"))
            {
                RefreshWaypointBufferFromScene();
            }

            if (GUILayout.Button("刷新路径表现"))
            {
                targetPath.EditorRefreshAuthoringState();
                EnemyPathAuthoringUtility.MarkSceneDirty(targetPath);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawQuickFixSection()
        {
            EditorGUILayout.LabelField("快速修正", EditorStyles.boldLabel);

            if (targetPath == null)
            {
                return;
            }

            List<Transform> selectedExistingWaypoints = EnemyPathAuthoringUtility.GetSelectedExistingWaypoints(targetPath);
            List<Transform> selectedNewWaypoints = EnemyPathAuthoringUtility.GetSelectedNewWaypointCandidates(targetPath);
            Transform activeSelectedWaypoint = Selection.activeTransform != null && selectedExistingWaypoints.Contains(Selection.activeTransform)
                ? Selection.activeTransform
                : null;
            Transform secondarySelectedWaypoint = selectedExistingWaypoints.Count == 2
                ? selectedExistingWaypoints.FirstOrDefault(point => point != activeSelectedWaypoint)
                : null;

            EditorGUILayout.HelpBox(
                $"已选路径点：{selectedExistingWaypoints.Count}\n已选待插入点：{selectedNewWaypoints.Count}",
                MessageType.None);

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(selectedExistingWaypoints.Count != 2))
            {
                if (GUILayout.Button("交换两个已选路径点"))
                {
                    if (EnemyPathAuthoringUtility.TrySwapSelectedWaypoints(targetPath, renameWaypointsOnApply))
                    {
                        RefreshWaypointBufferFromScene();
                    }
                }
            }

            using (new EditorGUI.DisabledScope(activeSelectedWaypoint == null || secondarySelectedWaypoint == null))
            {
                if (GUILayout.Button("把激活点插到另一个点后面"))
                {
                    if (EnemyPathAuthoringUtility.TryMoveExistingWaypointAfter(
                        targetPath,
                        activeSelectedWaypoint,
                        secondarySelectedWaypoint,
                        renameWaypointsOnApply))
                    {
                        RefreshWaypointBufferFromScene();
                    }
                }
            }

            using (new EditorGUI.DisabledScope(selectedNewWaypoints.Count == 0 || _waypointList == null || _waypointList.index < 0 || _waypointList.index >= waypointBuffer.Count))
            {
                if (GUILayout.Button("已选点插到当前点后面"))
                {
                    Transform anchorWaypoint = waypointBuffer[_waypointList.index];
                    if (EnemyPathAuthoringUtility.TryInsertSelectedCandidatesAfter(
                        targetPath,
                        anchorWaypoint,
                        collectOrderMode,
                        chainStartReference,
                        renameWaypointsOnApply))
                    {
                        RefreshWaypointBufferFromScene();
                    }
                }
            }

            using (new EditorGUI.DisabledScope(selectedNewWaypoints.Count == 0))
            {
                if (GUILayout.Button("已选点插到末尾"))
                {
                    if (EnemyPathAuthoringUtility.TryAppendSelectedCandidatesToEnd(
                        targetPath,
                        collectOrderMode,
                        chainStartReference,
                        renameWaypointsOnApply))
                    {
                        RefreshWaypointBufferFromScene();
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSelectionCollectSection()
        {
            EditorGUILayout.LabelField("收集当前选中点", EditorStyles.boldLabel);

            List<Transform> selectedPoints = targetPath != null
                ? EnemyPathAuthoringUtility.GetSelectedCandidatePoints(targetPath)
                : new List<Transform>();

            EditorGUILayout.HelpBox(
                $"当前选中可用点数量：{selectedPoints.Count}\n提示：如果你想让最近邻排序更稳定，可以把期望的起点也一起选中，并把它设成下方的起点参考。",
                MessageType.None);

            collectOrderMode = (EnemyPathPointOrderMode)EditorGUILayout.EnumPopup("排序规则", collectOrderMode);
            renameWaypointsOnApply = EditorGUILayout.Toggle("应用时自动编号", renameWaypointsOnApply);

            using (new EditorGUI.DisabledScope(collectOrderMode != EnemyPathPointOrderMode.NearestChain))
            {
                chainStartReference = (Transform)EditorGUILayout.ObjectField(
                    "最近邻起点",
                    chainStartReference,
                    typeof(Transform),
                    true);
            }

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(targetPath == null || selectedPoints.Count == 0))
            {
                if (GUILayout.Button("用选中点替换当前路径"))
                {
                    List<Transform> orderedPoints = EnemyPathAuthoringUtility.BuildOrderedSelection(
                        selectedPoints,
                        collectOrderMode,
                        chainStartReference);
                    waypointBuffer = orderedPoints;
                    EnemyPathAuthoringUtility.ApplyWaypointOrder(targetPath, waypointBuffer, renameWaypointsOnApply);
                    RefreshWaypointBufferFromScene();
                }

                if (GUILayout.Button("把选中点追加到当前路径"))
                {
                    List<Transform> merged = EnemyPathAuthoringUtility.GetWaypointChildren(targetPath);
                    List<Transform> newPoints = EnemyPathAuthoringUtility.BuildOrderedSelection(
                        selectedPoints.Where(point => !merged.Contains(point)).ToList(),
                        collectOrderMode,
                        chainStartReference);
                    merged.AddRange(newPoints);
                    waypointBuffer = merged;
                    EnemyPathAuthoringUtility.ApplyWaypointOrder(targetPath, waypointBuffer, renameWaypointsOnApply);
                    RefreshWaypointBufferFromScene();
                }
            }

            if (GUILayout.Button("让工具接管当前选中的 EnemyPath"))
            {
                TryAdoptCurrentSelection();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawWaypointListSection()
        {
            EditorGUILayout.LabelField("路径点顺序", EditorStyles.boldLabel);

            if (targetPath == null)
            {
                return;
            }

            if (_waypointList != null)
            {
                _waypointList.DoLayoutList();
            }

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(_waypointList == null || _waypointList.index <= 0 || _waypointList.index >= waypointBuffer.Count))
            {
                if (GUILayout.Button("上移"))
                {
                    MoveBufferItem(_waypointList.index, _waypointList.index - 1);
                }
            }

            using (new EditorGUI.DisabledScope(_waypointList == null || _waypointList.index < 0 || _waypointList.index >= waypointBuffer.Count - 1))
            {
                if (GUILayout.Button("下移"))
                {
                    MoveBufferItem(_waypointList.index, _waypointList.index + 1);
                }
            }

            using (new EditorGUI.DisabledScope(_waypointList == null || _waypointList.index < 0 || _waypointList.index >= waypointBuffer.Count))
            {
                if (GUILayout.Button("选中当前点"))
                {
                    Selection.activeTransform = waypointBuffer[_waypointList.index];
                    SceneView.FrameLastActiveSceneView();
                }
            }

            if (GUILayout.Button("反转"))
            {
                waypointBuffer.Reverse();
                ApplyBufferToScene();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawApplySection()
        {
            if (targetPath == null)
            {
                return;
            }

            EditorGUILayout.LabelField("应用到场景", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "列表里的顺序只是编辑缓冲。点击下面的按钮后，工具才会真正把层级顺序改回 Scene，运行时也才会按这个顺序走。",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("应用当前列表到场景"))
            {
                ApplyBufferToScene();
            }

            if (GUILayout.Button("重新编号"))
            {
                EnemyPathAuthoringUtility.RenumberWaypoints(targetPath);
                RefreshWaypointBufferFromScene();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void EnsureList()
        {
            if (_waypointList != null)
            {
                return;
            }

            _waypointList = new ReorderableList(waypointBuffer, typeof(Transform), true, true, false, false);
            _waypointList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, "当前路径点顺序（可直接拖拽重排）");
            };
            _waypointList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                if (index < 0 || index >= waypointBuffer.Count)
                {
                    return;
                }

                Transform waypoint = waypointBuffer[index];
                rect.y += 2f;

                Rect indexRect = new Rect(rect.x, rect.y, 42f, EditorGUIUtility.singleLineHeight);
                Rect objectRect = new Rect(rect.x + 46f, rect.y, rect.width - 160f, EditorGUIUtility.singleLineHeight);
                Rect positionRect = new Rect(rect.x + rect.width - 108f, rect.y, 108f, EditorGUIUtility.singleLineHeight);

                EditorGUI.LabelField(indexRect, $"#{index + 1:D2}");
                waypointBuffer[index] = (Transform)EditorGUI.ObjectField(objectRect, waypoint, typeof(Transform), true);

                string positionSummary = waypoint != null
                    ? $"{waypoint.position.x:0.0}, {waypoint.position.y:0.0}"
                    : "缺失";
                EditorGUI.LabelField(positionRect, positionSummary, EditorStyles.miniLabel);
            };
            _waypointList.onReorderCallback = _ =>
            {
                ApplyBufferToScene();
            };
            _waypointList.onSelectCallback = _ =>
            {
                if (_waypointList.index >= 0 && _waypointList.index < waypointBuffer.Count && waypointBuffer[_waypointList.index] != null)
                {
                    Selection.activeTransform = waypointBuffer[_waypointList.index];
                }
            };
        }

        private void TryAdoptCurrentSelection()
        {
            if (Selection.activeTransform == null)
            {
                return;
            }

            EnemyPath selectedPath = Selection.activeTransform.GetComponent<EnemyPath>();
            if (selectedPath == null)
            {
                selectedPath = Selection.activeTransform.GetComponentInParent<EnemyPath>();
            }

            if (selectedPath != null)
            {
                targetPath = selectedPath;
                RefreshWaypointBufferFromScene();
            }
        }

        private void RefreshWaypointBufferFromScene()
        {
            waypointBuffer = targetPath != null
                ? EnemyPathAuthoringUtility.GetWaypointChildren(targetPath)
                : new List<Transform>();

            _waypointList = null;
            EnsureList();
            Repaint();
        }

        private void ApplyBufferToScene()
        {
            if (targetPath == null)
            {
                return;
            }

            waypointBuffer = waypointBuffer.Where(point => point != null).Distinct().ToList();
            EnemyPathAuthoringUtility.ApplyWaypointOrder(targetPath, waypointBuffer, renameWaypointsOnApply);
            RefreshWaypointBufferFromScene();
        }

        private void MoveBufferItem(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= waypointBuffer.Count || toIndex < 0 || toIndex >= waypointBuffer.Count)
            {
                return;
            }

            Transform movedWaypoint = waypointBuffer[fromIndex];
            waypointBuffer.RemoveAt(fromIndex);
            waypointBuffer.Insert(toIndex, movedWaypoint);
            _waypointList.index = toIndex;
            ApplyBufferToScene();
        }
    }
}

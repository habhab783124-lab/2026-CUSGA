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
    /// `EnemyPathPointOrderMode` 鎻忚堪鈥滄妸涓€鎵规暎钀藉湪 Scene 閲岀殑鐐规敹鎴愯矾寰勬椂锛岃鎸変粈涔堣鍒欐帓椤哄簭鈥濄€?    ///
    /// 杩欓噷鏁呮剰鎶婃ā寮忓仛寰楀緢鐩寸櫧锛屽洜涓鸿繖绫诲伐鍏风殑鐩爣涓嶆槸鐐妧锛岃€屾槸鍑忓皯浣滆€呮潵鍥為噸鎺掔偣浣嶇殑鏃堕棿锛?    /// - `HierarchyOrder`
    ///   閫傚悎浣犲凡缁忓湪灞傜骇閲屾墜鍔ㄦ帓杩囩偣浣嶉『搴忥紝鍙兂涓€閿敹鍙ｆ垚姝ｅ紡璺緞銆?    /// - `LeftToRight`
    ///   閫傚悎杩戜技妯悜鎺ㄨ繘鐨勮矾銆?    /// - `TopToBottom`
    ///   閫傚悎杩戜技绾靛悜鎺ㄨ繘鐨勮矾銆?    /// - `NearestChain`
    ///   閫傚悎璺嚎鎷愬集姣旇緝澶氥€佷絾鐐逛綅鍦ㄧ┖闂翠笂宸茬粡澶ц嚧鎽嗗鐨勬儏鍐点€?    /// </summary>
    internal enum EnemyPathPointOrderMode
    {
        HierarchyOrder,
        LeftToRight,
        TopToBottom,
        NearestChain
    }

    internal enum EnemyPathPlacementMode
    {
        Orthogonal,
        Freeform
    }

    /// <summary>
    /// `EnemyPathAuthoringUtility` 鎶娾€滆矾寰勪綔鑰呭伐鍏风湡姝ｄ細閲嶅鍋氱殑浣庡眰鎿嶄綔鈥濋泦涓敹鍙ｃ€?    ///
    /// 杩欐牱鍋氭湁涓や釜濂藉锛?    /// 1. `EnemyPathEditor` 鍜岀嫭绔?`EditorWindow` 鍙互鍏辩敤鍚屼竴濂楅€昏緫锛岄伩鍏嶄竴澶勪慨浜嗗彟涓€澶勫繕璁板悓姝ャ€?    /// 2. 浠ュ悗濡傛灉浣犺繕鎯崇户缁姞鈥滆嚜鍔ㄦ彃鍊艰ˉ鐐光€濃€滄寜鎶樼嚎娈靛惛闄勨€濃€滄壒閲忛噸鍛藉悕鈥濈瓑鍔熻兘锛?    ///    涔熻兘缁х画鍥寸粫杩欎竴涓伐鍏峰眰鎵╁睍锛岃€屼笉鏄妸 Scene 鏀瑰啓閫昏緫鏁ｅ湪鍚勪釜 Inspector 閲屻€?    /// </summary>
    internal static class EnemyPathAuthoringUtility
    {
        internal const string WaypointRootName = "Waypoints";
        private const string ReadabilityRootName = "__PathReadability";

        /// <summary>
        /// 缁熶竴纭繚 `EnemyPath` 鏈変竴涓槑纭殑 `Waypoints` 鏍硅妭鐐广€?        ///
        /// 鎴戜滑杩欓噷浼樺厛鍧氭寔鈥滆矾寰勭偣鏈夌嫭绔嬫牴鑺傜偣鈥濈殑缁撴瀯锛岃€屼笉鏄户缁妸鐐规暎鎸傚湪 `EnemyPath` 鑷繁涓嬮潰锛?        /// 鍥犱负鍚庤€呬竴鏃﹀拰鍙鎬ц鐩栧眰銆佽楗扮墿鎴栦綔鑰呬复鏃惰緟鍔╁璞℃贩鍦ㄤ竴璧凤紝灏卞緢瀹规槗璁╁眰绾у彉涔便€?        /// </summary>
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
        /// 璇诲彇褰撳墠璺緞閲屸€滅湡姝ｄ綔涓鸿矾寰勭偣浣跨敤鈥濈殑瀵硅薄鍒楄〃銆?        ///
        /// 杩欓噷鏁呮剰鍙涓ょ鏉ユ簮锛?        /// - 宸叉樉寮忔寚瀹氱殑 `Waypoints` 鏍硅妭鐐圭殑鐩存帴瀛愯妭鐐?        /// - 鍏煎鏃х粨鏋勬椂锛宍EnemyPath` 鐩存帴瀛愯妭鐐逛腑鎺掗櫎鍙鎬ф牴鑺傜偣鍚庣殑缁撴灉
        ///
        /// 杩欐牱浣滆€呭湪宸ュ叿閲岀湅鍒扮殑椤哄簭锛屽氨浼氬敖閲忓拰瀹為檯杩愯鏃剁紦瀛樿矾寰勭偣鏃剁湅鍒扮殑椤哄簭涓€鑷淬€?        /// </summary>
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
        /// 浠庡綋鍓?Unity 閫夋嫨闆嗕腑绛涘嚭鈥滈€傚悎鎷挎潵鍋氳矾寰勭偣鈥濈殑 Transform銆?        ///
        /// 杩欓噷涓嶄細鎿呰嚜闄愬埗鐐瑰繀椤诲凡缁忔寕鍦?`EnemyPath` 涓嬮潰锛?        /// 鍥犱负浣犵殑闇€姹傚氨鏄€滃厛鍦ㄥ満鏅噷闅忔墜鎽嗙偣锛屽啀閫変腑鍚庤宸ュ叿鏉ユ敹鍙ｂ€濄€?        /// </summary>
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
        /// 鎸夋寚瀹氳鍒欏涓€鎵圭偣杩涜鎺掑簭銆?        ///
        /// 杩欎竴姝ョ殑鐩爣涓嶆槸鈥滄案杩滆嚜鍔ㄦ帹鏂嚭姝ｇ‘鍏冲崱璁捐鈥濓紝
        /// 鑰屾槸灏介噺鎶婄涓€杞『搴忔帓鍒版帴杩戜綔鑰呮剰鍥撅紝璁╁悗缁井璋冩垚鏈彉灏忋€?        /// </summary>
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
        /// 鎶婂綋鍓嶅垪琛ㄩ『搴忕湡姝ｈ惤鍥?Scene 灞傜骇銆?        ///
        /// 杩欎竴姝ラ潪甯稿叧閿紝鍥犱负杩愯鏃?`EnemyPath` 灏辨槸鎸夊眰绾ч『搴忕紦瀛樿矾寰勭偣鐨勩€?        /// 鎹㈠彞璇濊锛岀紪杈戝櫒鍒楄〃閲屸€滅湅璧锋潵椤轰簡鈥濊繕涓嶅锛屽繀椤荤湡姝ｆ敼鍒?Hierarchy 鎵嶇畻鐢熸晥銆?        /// </summary>
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
        /// 浠呭鐜版湁璺緞鐐归噸鏂扮紪鍙凤紝涓嶆敼椤哄簭銆?        /// </summary>
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
        /// Creates one new waypoint at the end of the current path.
        ///
        /// This is the smallest missing primitive for a fully tool-driven workflow:
        /// until now the path tool could only reorder or collect existing points, but it could not
        /// actually author a brand-new point into the scene.
        /// </summary>
        internal static Transform CreateWaypointAtEnd(EnemyPath enemyPath, Vector3 worldPosition, bool renameWaypoints)
        {
            if (enemyPath == null)
            {
                return null;
            }

            Transform waypointRoot = EnsureWaypointRoot(enemyPath);
            if (waypointRoot == null)
            {
                return null;
            }

            List<Transform> currentWaypoints = GetWaypointChildren(enemyPath);
            GameObject waypointObject = new GameObject("Waypoint_New");
            Undo.RegisterCreatedObjectUndo(waypointObject, "创建路径点");
            Transform newWaypoint = waypointObject.transform;
            Undo.SetTransformParent(newWaypoint, waypointRoot, "把路径点收进 EnemyPath");
            newWaypoint.position = worldPosition;

            currentWaypoints.Add(newWaypoint);
            ApplyWaypointOrder(enemyPath, currentWaypoints, renameWaypoints);
            return newWaypoint;
        }

        /// <summary>
        /// Creates one new waypoint immediately after an existing waypoint.
        ///
        /// This is the authoring-friendly version of "insert point here":
        /// the user can keep editing the route in local order without rebuilding the whole chain.
        /// </summary>
        internal static Transform CreateWaypointAfter(
            EnemyPath enemyPath,
            Transform previousWaypoint,
            Vector3 worldPosition,
            bool renameWaypoints)
        {
            if (enemyPath == null || previousWaypoint == null)
            {
                return null;
            }

            Transform waypointRoot = EnsureWaypointRoot(enemyPath);
            if (waypointRoot == null)
            {
                return null;
            }

            List<Transform> currentWaypoints = GetWaypointChildren(enemyPath);
            int previousIndex = currentWaypoints.IndexOf(previousWaypoint);
            if (previousIndex < 0)
            {
                return null;
            }

            GameObject waypointObject = new GameObject("Waypoint_New");
            Undo.RegisterCreatedObjectUndo(waypointObject, "创建路径点");
            Transform newWaypoint = waypointObject.transform;
            Undo.SetTransformParent(newWaypoint, waypointRoot, "把路径点收进 EnemyPath");
            newWaypoint.position = worldPosition;

            currentWaypoints.Insert(previousIndex + 1, newWaypoint);
            ApplyWaypointOrder(enemyPath, currentWaypoints, renameWaypoints);
            return newWaypoint;
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
        /// 鎶婂綋鍓嶅満鏅姸鎬佹爣璁颁负宸蹭慨鏀广€?        ///
        /// 杩欑湅璧锋潵鍍忎釜寰堝皬鐨勬敹灏撅紝浣嗗叾瀹為潪甯搁噸瑕侊細
        /// 濡傛灉浣滆€呰緵杈涜嫤鑻︽帓濂戒簡璺緞锛孲cene 鍗存病琚爣璁颁负 dirty锛?        /// 鍏抽棴鍦烘櫙鏃跺氨寰堝鏄撴妸鍔冲姩鎴愭灉鎮勬倓涓㈡帀銆?        /// </summary>
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
        /// 鎶婂綋鍓嶉€夋嫨闆嗘寜鈥滆窛绂婚摼鈥濇帓鎴愪竴鏉℃姌绾裤€?        ///
        /// 杩欐槸涓€涓緢瀹炵敤浣嗕笉杩囧害鑱槑鐨勮椽蹇冩硶锛?        /// - 璧风偣浼樺厛鐢ㄤ綔鑰呮寚瀹氱殑鐐?        /// - 娌℃寚瀹氭椂鐢ㄥ綋鍓嶆縺娲婚€夋嫨
        /// - 鍐嶆病鏈夊氨閫€鍥炲埌鏈€闈犲乏鐨勭偣
        ///
        /// 瀹冧笉鑳芥浛浠ｄ綔鑰呭垽鏂紝浣嗛潪甯搁€傚悎鈥滄垜鐐瑰凡缁忔憜瀵逛簡锛屽彧鏄噿寰楁墜鍔ㄦ帓椤哄簭鈥濈殑鍦烘櫙銆?        /// </summary>
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
        /// 鐢熸垚灞傜骇椤哄簭閿€?        ///
        /// 杩欓噷涓嶇敤鍚嶅瓧鎺掑簭锛屾槸涓轰簡閬垮厤浣滆€呭垰濂芥妸鐐瑰懡鍚嶅緱涔变竷鍏碂鏃讹紝
        /// 鈥滅湅璧锋潵灞傜骇鎺掑ソ浜嗭紝宸ュ叿鍗存寜瀛楀吀搴忕粰浣犻噸鏂版墦涔扁€濈殑灏村艾鎯呭喌銆?        /// </summary>
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
        /// 鏍规嵁鐐规暟鍔ㄦ€佸喅瀹氱紪鍙峰搴︺€?        ///
        /// 杩欐牱 9 涓偣鏃朵笉浼氭樉寰楀お鍟板棪锛岃秴杩?99 涓偣鏃朵篃涓嶄細鍥犱负浣嶆暟涓嶅鑰屾帓搴忔贩涔便€?        /// </summary>
        private static string BuildWaypointName(int zeroBasedIndex, int totalCount)
        {
            int digits = Mathf.Max(2, totalCount.ToString().Length);
            return $"Waypoint_{(zeroBasedIndex + 1).ToString($"D{digits}")}";
        }
    }

    /// <summary>
    /// Planner-facing path authoring window.
    ///
    /// This window is intentionally focused on two jobs:
    /// 1. collect loose scene points into one formal `EnemyPath`
    /// 2. make small path-order fixes cheap and predictable
    /// </summary>
    public sealed class EnemyPathAuthoringTool : EditorWindow
    {
        [SerializeField] private EnemyPath targetPath; // 当前正在编辑的 EnemyPath
        [SerializeField] private EnemyPathPointOrderMode collectOrderMode = EnemyPathPointOrderMode.HierarchyOrder; // 选中点收集后的默认排序方式
        [SerializeField] private bool renameWaypointsOnApply = true; // 应用顺序时是否自动重命名路径点
        [SerializeField] private Transform chainStartReference; // 最近邻串联排序时的起点参考
        [SerializeField] private List<Transform> waypointBuffer = new List<Transform>(); // 当前窗口里缓存的路径点列表

        private ReorderableList _waypointList; // Inspector 风格的路径点重排列表
        [SerializeField] private Vector2 scrollPosition; // 窗口滚动位置

        [SerializeField] private bool sceneClickPlacementMode; // 是否启用 Scene 视图点击放点模式
        [SerializeField] private bool insertCreatedPointAfterSelection = true; // 新点默认插在当前选中点之后
        [SerializeField] private bool frameCreatedWaypointAfterCreate = true; // 创建后是否自动聚焦新路径点
        [SerializeField] private float createdWaypointZ = 0f; // 新建路径点默认 Z 值
        [SerializeField] private Vector2 createdWaypointOffset = new Vector2(4f, 0f); // 在当前点后补点时的默认偏移
        [SerializeField] private EnemyPathPlacementMode placementMode = EnemyPathPlacementMode.Orthogonal; // 路径点放置模式

        [MenuItem("Tools/Tower Defense/Authoring/路径点编辑工具")]
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
            SceneView.duringSceneGui += OnSceneViewGui;
            EnsureList();
            TryAdoptCurrentSelection();
            if (targetPath == null)
            {
                TowerDefenseAuthoringSceneContext sharedContext = TowerDefenseAuthoringSceneContext.GetOrCreate();
                if (sharedContext.CurrentPath != null)
                {
                    targetPath = sharedContext.CurrentPath;
                    RefreshWaypointBufferFromScene();
                }
            }
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            SceneView.duringSceneGui -= OnSceneViewGui;
        }

        private void OnSelectionChanged()
        {
            Repaint();
        }

        private void OnGUI()
        {
            EnsureList();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            try
            {
                DrawTargetPathSection();
                EditorGUILayout.Space(10f);
                DrawWaypointCreateSection();
                EditorGUILayout.Space(10f);
                DrawSelectionCollectSection();
                EditorGUILayout.Space(10f);
                DrawQuickFixSection();
                EditorGUILayout.Space(10f);
                DrawWaypointListSection();
                EditorGUILayout.Space(10f);
                DrawApplySection();
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawTargetPathSection()
        {
            EditorGUILayout.LabelField("当前路径", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                TowerDefenseAuthoringSceneContext.GetOrCreate().BuildSummary() +
                "\n如需切换到当前活动场景里的共享路径，请使用下方“从场景刷新当前顺序”。",
                MessageType.None);

            EditorGUI.BeginChangeCheck();
            targetPath = (EnemyPath)EditorGUILayout.ObjectField("EnemyPath 对象", targetPath, typeof(EnemyPath), true);
            if (EditorGUI.EndChangeCheck())
            {
                RefreshWaypointBufferFromScene();
            }

            if (targetPath == null)
            {
                EditorGUILayout.HelpBox("先点过任一工具里的“接管当前场景”，或手动指定一个 EnemyPath。工具会围绕这条路径做收点和调顺序。", MessageType.Info);
                return;
            }

            List<Transform> currentWaypoints = EnemyPathAuthoringUtility.GetWaypointChildren(targetPath);
            string waypointRootName = targetPath.WaypointRoot != null ? targetPath.WaypointRoot.name : "(直接子节点)";
            EditorGUILayout.HelpBox(
                $"当前路径：{targetPath.name}\n当前路径点数量：{currentWaypoints.Count}\n路径点根节点：{waypointRootName}",
                MessageType.None);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("接管当前场景"))
            {
                TowerDefenseAuthoringSceneContext sharedContext = TowerDefenseAuthoringSceneContext.CaptureActiveSceneContext();
                if (sharedContext.CurrentPath != null)
                {
                    targetPath = sharedContext.CurrentPath;
                }
                RefreshWaypointBufferFromScene();
            }

            if (GUILayout.Button("指定 / 创建 Waypoints 根节点"))
            {
                EnemyPathAuthoringUtility.EnsureWaypointRoot(targetPath);
                targetPath.EditorRefreshAuthoringState();
                RefreshWaypointBufferFromScene();
            }

            if (GUILayout.Button("从场景刷新当前顺序"))
            {
                TowerDefenseAuthoringSceneContext sharedContext = TowerDefenseAuthoringSceneContext.GetOrCreate();
                if (sharedContext.CurrentPath != null)
                {
                    targetPath = sharedContext.CurrentPath;
                }
                RefreshWaypointBufferFromScene();
            }

            if (GUILayout.Button("刷新路径表现"))
            {
                targetPath.EditorRefreshAuthoringState();
                EnemyPathAuthoringUtility.MarkSceneDirty(targetPath);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawWaypointCreateSection()
        {
            EditorGUILayout.LabelField("创建新路径点", EditorStyles.boldLabel);

            if (targetPath == null)
            {
                EditorGUILayout.HelpBox("先指定一条 EnemyPath，路径点创建器才知道新点应该挂到哪条路径下面。", MessageType.Info);
                return;
            }

            insertCreatedPointAfterSelection = EditorGUILayout.Toggle("优先插到当前选中点后面", insertCreatedPointAfterSelection);
            frameCreatedWaypointAfterCreate = EditorGUILayout.Toggle("创建后聚焦并选中新点", frameCreatedWaypointAfterCreate);
            placementMode = (EnemyPathPlacementMode)EditorGUILayout.EnumPopup("放置模式", placementMode);
            createdWaypointZ = EditorGUILayout.FloatField("创建点所在 Z 平面", createdWaypointZ);
            createdWaypointOffset = EditorGUILayout.Vector2Field("补点默认偏移", createdWaypointOffset);

            EditorGUILayout.HelpBox(
                "这里补上了从零做路径时缺的那一步：你现在可以直接创建新路径点，而不是先手工在 Hierarchy 里建空物体。\n- 正交放置：相邻两个点自动通过横平竖直的方式连接。\n- 随意放置：相邻两个点可以直接形成斜线段。\n- 在 Scene 视图中心创建：适合快速落一个新点。\n- 在当前点后按偏移创建：适合顺着路线往前补点。\n- Scene 点击放点模式：开启后直接在 Scene 里左键落点，Esc 退出。",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("在 Scene 视图中心创建"))
                {
                    CreateWaypointAtSceneViewPivot();
                }

                if (GUILayout.Button("在当前点后按偏移创建"))
                {
                    CreateWaypointAfterCurrentSelectionWithOffset();
                }
            }

            string toggleLabel = sceneClickPlacementMode ? "关闭 Scene 点击放点模式" : "开启 Scene 点击放点模式";
            if (GUILayout.Button(toggleLabel))
            {
                sceneClickPlacementMode = !sceneClickPlacementMode;
                SceneView.RepaintAll();
                Repaint();
            }
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
                $"当前选中可用点数量：{selectedPoints.Count}\n提示：如果你想让最近邻排序更稳定，可以把期望的起点一起选中，并把它设成下方的起点参考。",
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

            if (GUILayout.Button("接管当前场景"))
            {
                TowerDefenseAuthoringSceneContext sharedContext = TowerDefenseAuthoringSceneContext.CaptureActiveSceneContext();
                if (sharedContext.CurrentPath != null)
                {
                    targetPath = sharedContext.CurrentPath;
                }
                else
                {
                    TryAdoptCurrentSelection();
                }
                RefreshWaypointBufferFromScene();
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
                "列表里的顺序只是编辑缓冲。点下面的按钮后，工具才会真正把层级顺序改回 Scene，运行时也才会按这个顺序走。",
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

            if (GUILayout.Button("删除当前路径全部点"))
            {
                DeleteAllWaypointsOnCurrentPath();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void OnSceneViewGui(SceneView sceneView)
        {
            if (!sceneClickPlacementMode || targetPath == null)
            {
                return;
            }

            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(12f, 12f, 320f, 64f), EditorStyles.helpBox);
            GUILayout.Label("路径点放置模式已开启", EditorStyles.boldLabel);
            GUILayout.Label("左键点击 Scene 直接放点，Esc 退出。", EditorStyles.wordWrappedMiniLabel);
            GUILayout.EndArea();
            Handles.EndGUI();

            Event currentEvent = Event.current;
            if (currentEvent == null)
            {
                return;
            }

            if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Escape)
            {
                sceneClickPlacementMode = false;
                currentEvent.Use();
                Repaint();
                SceneView.RepaintAll();
                return;
            }

            if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0 || currentEvent.alt)
            {
                return;
            }

            Plane authoringPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, createdWaypointZ));
            Ray mouseRay = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
            if (!authoringPlane.Raycast(mouseRay, out float enter))
            {
                return;
            }

            Vector3 worldPoint = mouseRay.GetPoint(enter);
            CreateWaypointAtWorldPosition(worldPoint);
            currentEvent.Use();
        }

        private void CreateWaypointAtSceneViewPivot()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            Vector3 pivot = sceneView != null ? sceneView.pivot : Vector3.zero;
            CreateWaypointAtWorldPosition(new Vector3(pivot.x, pivot.y, createdWaypointZ));
        }

        private void CreateWaypointAfterCurrentSelectionWithOffset()
        {
            if (targetPath == null)
            {
                return;
            }

            List<Transform> currentWaypoints = EnemyPathAuthoringUtility.GetWaypointChildren(targetPath);
            Transform activeWaypoint = Selection.activeTransform != null && currentWaypoints.Contains(Selection.activeTransform)
                ? Selection.activeTransform
                : currentWaypoints.LastOrDefault();
            if (activeWaypoint == null)
            {
                CreateWaypointAtWorldPosition(new Vector3(createdWaypointOffset.x, createdWaypointOffset.y, createdWaypointZ));
                return;
            }

            Vector3 worldPosition = activeWaypoint.position + new Vector3(createdWaypointOffset.x, createdWaypointOffset.y, 0f);
            worldPosition.z = createdWaypointZ;
            CreateWaypointAtWorldPosition(worldPosition, activeWaypoint);
        }

        private void CreateWaypointAtWorldPosition(Vector3 worldPosition, Transform preferredAnchor = null)
        {
            if (targetPath == null)
            {
                return;
            }

            List<Transform> currentWaypoints = EnemyPathAuthoringUtility.GetWaypointChildren(targetPath);
            Transform selectedWaypoint = Selection.activeTransform != null && currentWaypoints.Contains(Selection.activeTransform)
                ? Selection.activeTransform
                : null;
            Transform anchorWaypoint = preferredAnchor != null ? preferredAnchor : selectedWaypoint;

            if (placementMode == EnemyPathPlacementMode.Orthogonal && anchorWaypoint != null)
            {
                worldPosition = BuildOrthogonalPlacementPosition(anchorWaypoint.position, worldPosition);
            }

            Transform createdWaypoint = insertCreatedPointAfterSelection && anchorWaypoint != null
                ? EnemyPathAuthoringUtility.CreateWaypointAfter(targetPath, anchorWaypoint, worldPosition, renameWaypointsOnApply)
                : EnemyPathAuthoringUtility.CreateWaypointAtEnd(targetPath, worldPosition, renameWaypointsOnApply);
            if (createdWaypoint == null)
            {
                return;
            }

            RefreshWaypointBufferFromScene();
            Selection.activeTransform = createdWaypoint;

            if (frameCreatedWaypointAfterCreate && SceneView.lastActiveSceneView != null)
            {
                SceneView.FrameLastActiveSceneView();
            }
        }

        private static Vector3 BuildOrthogonalPlacementPosition(Vector3 anchorPosition, Vector3 requestedPosition)
        {
            Vector3 delta = requestedPosition - anchorPosition;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                return new Vector3(requestedPosition.x, anchorPosition.y, requestedPosition.z);
            }

            return new Vector3(anchorPosition.x, requestedPosition.y, requestedPosition.z);
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
                    : "缂哄け";
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

        private void DeleteAllWaypointsOnCurrentPath()
        {
            if (targetPath == null)
            {
                return;
            }

            List<Transform> currentWaypoints = EnemyPathAuthoringUtility.GetWaypointChildren(targetPath);
            for (int index = 0; index < currentWaypoints.Count; index++)
            {
                Transform waypoint = currentWaypoints[index];
                if (waypoint != null)
                {
                    Undo.DestroyObjectImmediate(waypoint.gameObject);
                }
            }

            targetPath.EditorRefreshAuthoringState();
            EnemyPathAuthoringUtility.MarkSceneDirty(targetPath);
            RefreshWaypointBufferFromScene();
        }
    }
}



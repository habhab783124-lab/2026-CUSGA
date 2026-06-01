using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 放置网格作者工具。
///
/// 这套工具现在负责两类事情：
/// 1. 确保正式关卡场景里真正存在 `PlacementGrid`，并接好 `TowerDefenseGame` 的引用。
/// 2. 在编辑器里把 `BuildZone + PlacementBlocker` 预烘成静态遮罩 Bake 数据。
///
/// 这样做之后，作者仍然继续使用 Scene 里的碰撞体和区域来画地图语义，
/// 但运行时放置判定就可以优先读取已经算好的格子数据，
/// 不必每次进关都重新把整张地图现算一遍。
/// </summary>
public static class PlacementGridAuthoringTool
{
    private const string PlacementGridObjectName = "PlacementGrid";
    private const string PlacementGridReferencePropertyName = "placementGridReference";

    private static readonly string[] FormalLevelScenePaths =
    {
        "Assets/Scenes/Tutorial Level.unity",
        "Assets/Scenes/Level 2.unity",
        "Assets/Scenes/Level 3.unity",
        "Assets/Scenes/level 4.unity"
    };

    [MenuItem("Tools/Tower Defense/Authoring/放置网格/应用到所有正式关卡")]
    public static void ApplyPlacementGridToFormalLevels()
    {
        string report = ApplyPlacementGridToFormalLevelsCore();
        EditorUtility.DisplayDialog("放置网格应用完成", report, "好的");
        Debug.Log(report);
    }

    [MenuItem("Tools/Tower Defense/Authoring/放置网格/静默应用到所有正式关卡")]
    public static void ApplyPlacementGridToFormalLevelsSilent()
    {
        string report = ApplyPlacementGridToFormalLevelsCore();
        Debug.Log(report);
    }

    [MenuItem("Tools/Tower Defense/Authoring/放置网格/检查当前场景")]
    public static void CheckActiveScenePlacementGrid()
    {
        bool changed = EnsurePlacementGridInActiveScene(out string report);
        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        EditorUtility.DisplayDialog("当前场景放置网格检查", report, "好的");
        Debug.Log(report);
    }

    [MenuItem("Tools/Tower Defense/Authoring/放置网格/Bake 当前场景静态遮罩")]
    public static void BakeActiveScenePlacementStaticMask()
    {
        bool changed = BakePlacementStaticMaskInActiveScene(out string report);
        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        EditorUtility.DisplayDialog("当前场景静态遮罩 Bake", report, "好的");
        Debug.Log(report);
    }

    [MenuItem("Tools/Tower Defense/Authoring/放置网格/Bake 并保存所有正式关卡静态遮罩")]
    public static void BakePlacementStaticMaskToFormalLevels()
    {
        string report = BakePlacementStaticMaskToFormalLevelsCore();
        EditorUtility.DisplayDialog("正式关卡静态遮罩 Bake 完成", report, "好的");
        Debug.Log(report);
    }

    [MenuItem("Tools/Tower Defense/Authoring/放置网格/清除当前场景静态遮罩 Bake")]
    public static void ClearActiveScenePlacementStaticMaskBake()
    {
        PlacementGrid placementGrid = Object.FindFirstObjectByType<PlacementGrid>();
        if (placementGrid == null)
        {
            EditorUtility.DisplayDialog("清除静态遮罩 Bake", "当前场景没有 PlacementGrid。", "好的");
            return;
        }

        Undo.RecordObject(placementGrid, "Clear Placement Static Mask Bake");
        bool changed = placementGrid.ClearPlacementStaticMaskBakeData();
        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        string report = changed
            ? "[清除] 已移除当前场景里保存的静态遮罩 Bake 数据。"
            : "[跳过] 当前场景本来就没有静态遮罩 Bake 数据。";
        EditorUtility.DisplayDialog("清除静态遮罩 Bake", report, "好的");
        Debug.Log(report);
    }

    private static string ApplyPlacementGridToFormalLevelsCore()
    {
        if (Application.isPlaying)
        {
            return "请先退出 Play Mode，再应用放置网格。";
        }

        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        StringBuilder reportBuilder = new StringBuilder();

        try
        {
            for (int i = 0; i < FormalLevelScenePaths.Length; i++)
            {
                string scenePath = FormalLevelScenePaths[i];
                if (!System.IO.File.Exists(scenePath))
                {
                    reportBuilder.AppendLine($"[失败] 找不到场景：{scenePath}");
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                bool changed = EnsurePlacementGridInActiveScene(out string sceneReport);
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }

                reportBuilder.AppendLine($"{scenePath}");
                reportBuilder.AppendLine(sceneReport);
                reportBuilder.AppendLine();
            }
        }
        finally
        {
            RestoreSceneSetup(originalSetup);
        }

        return reportBuilder.ToString();
    }

    private static string BakePlacementStaticMaskToFormalLevelsCore()
    {
        if (Application.isPlaying)
        {
            return "请先退出 Play Mode，再执行静态遮罩 Bake。";
        }

        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        StringBuilder reportBuilder = new StringBuilder();

        try
        {
            for (int i = 0; i < FormalLevelScenePaths.Length; i++)
            {
                string scenePath = FormalLevelScenePaths[i];
                if (!System.IO.File.Exists(scenePath))
                {
                    reportBuilder.AppendLine($"[失败] 找不到场景：{scenePath}");
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                bool changed = BakePlacementStaticMaskInActiveScene(out string sceneReport);
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }

                reportBuilder.AppendLine($"{scenePath}");
                reportBuilder.AppendLine(sceneReport);
                reportBuilder.AppendLine();
            }
        }
        finally
        {
            RestoreSceneSetup(originalSetup);
        }

        return reportBuilder.ToString();
    }

    private static bool EnsurePlacementGridInActiveScene(out string report)
    {
        bool changed = false;
        StringBuilder reportBuilder = new StringBuilder();

        TowerDefenseGame game = Object.FindFirstObjectByType<TowerDefenseGame>();
        if (game == null)
        {
            report = "[跳过] 当前场景没有 TowerDefenseGame。";
            return false;
        }

        PlacementGrid placementGrid = Object.FindFirstObjectByType<PlacementGrid>();
        if (placementGrid == null)
        {
            GameObject gridObject = new GameObject(PlacementGridObjectName);
            placementGrid = gridObject.AddComponent<PlacementGrid>();
            Undo.RegisterCreatedObjectUndo(gridObject, "Create PlacementGrid");
            changed = true;
            reportBuilder.AppendLine("[新增] 已创建 PlacementGrid。");
        }
        else
        {
            reportBuilder.AppendLine("[存在] 场景已包含 PlacementGrid。");
        }

        if (placementGrid.name != PlacementGridObjectName)
        {
            Undo.RecordObject(placementGrid.gameObject, "Rename PlacementGrid");
            placementGrid.name = PlacementGridObjectName;
            changed = true;
            reportBuilder.AppendLine("[调整] 已统一对象名为 PlacementGrid。");
        }

        SerializedObject serializedGame = new SerializedObject(game);
        SerializedProperty placementGridProperty = serializedGame.FindProperty(PlacementGridReferencePropertyName);
        if (placementGridProperty == null)
        {
            reportBuilder.AppendLine("[警告] TowerDefenseGame 上找不到 placementGridReference 字段。");
            report = reportBuilder.ToString();
            return changed;
        }

        if (placementGridProperty.objectReferenceValue != placementGrid)
        {
            placementGridProperty.objectReferenceValue = placementGrid;
            serializedGame.ApplyModifiedProperties();
            changed = true;
            reportBuilder.AppendLine("[接线] 已把 TowerDefenseGame.placementGridReference 指向 PlacementGrid。");
        }
        else
        {
            reportBuilder.AppendLine("[接线] TowerDefenseGame 已引用 PlacementGrid。");
        }

        report = reportBuilder.ToString();
        return changed;
    }

    private static bool BakePlacementStaticMaskInActiveScene(out string report)
    {
        bool changed = false;
        StringBuilder reportBuilder = new StringBuilder();

        if (Application.isPlaying)
        {
            report = "请先退出 Play Mode，再执行静态遮罩 Bake。";
            return false;
        }

        changed |= EnsurePlacementGridInActiveScene(out string gridReport);
        reportBuilder.AppendLine(gridReport);

        BuildZone buildZone = Object.FindFirstObjectByType<BuildZone>();
        if (buildZone == null)
        {
            reportBuilder.AppendLine("[失败] 当前场景没有 BuildZone，无法生成静态遮罩 Bake。");
            report = reportBuilder.ToString();
            return changed;
        }

        PlacementGrid placementGrid = Object.FindFirstObjectByType<PlacementGrid>();
        if (placementGrid == null)
        {
            reportBuilder.AppendLine("[失败] 当前场景没有 PlacementGrid，无法生成静态遮罩 Bake。");
            report = reportBuilder.ToString();
            return changed;
        }

        PlacementStaticMaskBakeData bakeData = PlacementStaticMask.BuildBakeData(
            buildZone,
            placementGrid,
            message => reportBuilder.AppendLine($"[Bake] {message}"));
        if (bakeData == null)
        {
            reportBuilder.AppendLine("[失败] PlacementStaticMask.BuildBakeData 返回空结果。");
            report = reportBuilder.ToString();
            return changed;
        }

        Undo.RecordObject(placementGrid, "Bake Placement Static Mask");
        bool bakeChanged = placementGrid.ApplyPlacementStaticMaskBakeData(bakeData);
        changed |= bakeChanged;

        if (bakeChanged)
        {
            reportBuilder.AppendLine("[写入] 已把静态遮罩 Bake 数据写入 PlacementGrid。");
        }
        else
        {
            reportBuilder.AppendLine("[存在] 当前场景的静态遮罩 Bake 数据已经是最新结果。");
        }

        reportBuilder.AppendLine($"[摘要] {placementGrid.GetPlacementStaticMaskBakeSummary()}");
        report = reportBuilder.ToString();
        return changed;
    }

    private static void RestoreSceneSetup(SceneSetup[] originalSetup)
    {
        if (originalSetup != null && originalSetup.Length > 0)
        {
            EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }
    }
}

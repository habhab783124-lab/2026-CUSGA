using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// 这个作者工具负责把 `LevelSelect` 场景真正物化成“打开就能看到并编辑的 UI 场景”。
    ///
    /// `LevelSelectController` 本身虽然支持首次打开时自动补骨架，
    /// 但这个工具还能把那套骨架直接保存回场景文件里，
    /// 让用户一打开 `LevelSelect` 就已经能在 Hierarchy / Scene 里看到那些对象。
    /// </summary>
    public static class LevelSelectSceneAuthoringTool
    {
        private const string LevelSelectScenePath = "Assets/Scenes/LevelSelect.unity"; // 中文：等级Select场景路径

        [MenuItem("Tools/Tower Defense/物化关卡选择场景")]
        public static void BatchCreateOrUpdateLevelSelectScene()
        {
            EditorSceneManager.OpenScene(LevelSelectScenePath, OpenSceneMode.Single);

            LevelSelectController controller = UnityEngine.Object.FindFirstObjectByType<LevelSelectController>();
            if (controller == null)
            {
                throw new InvalidOperationException("LevelSelect 场景缺少 LevelSelectController。");
            }

            controller.EditorMaterializeSceneUi();

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            EditorSceneManager.SaveScene(controller.gameObject.scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("LevelSelectSceneAuthoringTool：关卡选择场景物化完成。");
        }
    }
}

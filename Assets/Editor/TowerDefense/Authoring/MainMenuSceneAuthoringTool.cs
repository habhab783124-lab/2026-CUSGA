using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// 这个作者工具负责把 `MainMenu` 场景显式物化成可编辑的真实 UI 场景。
    ///
    /// 现在主菜单控制器已经不再在 `OnEnable / OnValidate` 中自动生成和回写页面，
    /// 所以如果你想从一个空壳场景重新补默认骨架，应通过这个显式工具执行。
    /// </summary>
    public static class MainMenuSceneAuthoringTool
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Tools/Tower Defense/Materialize Main Menu Scene")]
        public static void BatchCreateOrUpdateMainMenuScene()
        {
            EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

            MainMenuController controller = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
            if (controller == null)
            {
                throw new InvalidOperationException("MainMenu scene is missing MainMenuController.");
            }

            controller.EditorMaterializeDefaultSceneUi();

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            EditorSceneManager.SaveScene(controller.gameObject.scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("MainMenuSceneAuthoringTool: MainMenu scene materialized successfully.");
        }
    }
}

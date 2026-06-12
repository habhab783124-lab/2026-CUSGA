using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense.Editor
{
    [CustomEditor(typeof(TowerDefenseUiPreviewController))]
    public sealed class TowerDefenseUiPreviewControllerEditor : UnityEditor.Editor
    {
        private const string SyncMenuPath = "Tools/Tower Defense/同步 UI 模板到所有关卡";
        private const string OpenPreviewMenuPath = "Tools/Tower Defense/打开 UI 调试场景";

        /// <summary>
        /// Where the UI preview scene lives. It is derived from the template scene
        /// so that the HUDCanvas structure stays identical to what gets synced.
        /// </summary>
        private const string PreviewScenePath = "Assets/Scenes/TowerDefenseUiPreview.unity";

        private const string TemplateScenePath = "Assets/Scenes/TowerDefenseUiTemplate.unity";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);

            TowerDefenseUiPreviewController controller = (TowerDefenseUiPreviewController)target;
            if (controller == null) return;

            // Refresh
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Preview", GUILayout.Height(28f)))
            {
                controller.RefreshPreview();
            }

            GUI.backgroundColor = new Color(0.35f, 0.72f, 0.45f);
            if (GUILayout.Button("Refresh & Apply to All Levels", GUILayout.Height(28f)))
            {
                controller.RefreshPreview();
                SyncToAllLevels();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8f);

            // Sync section
            EditorGUILayout.LabelField("Sync to Levels", EditorStyles.boldLabel);

            GUI.backgroundColor = new Color(0.28f, 0.55f, 0.85f);
            if (GUILayout.Button("Apply HUD to All Tower-Defense Levels", GUILayout.Height(36f)))
            {
                SyncToAllLevels();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.HelpBox(
                "Copies the HUDCanvas (and EventSystem) from the current scene "
                + "to every final level (Tutorial, Level 2, Level 3, Level 4).\n\n"
                + "Workflow:\n"
                + "1. Open this scene via Tools → Tower Defense → 打开 UI 调试场景\n"
                + "2. Use the Preview State dropdown to check each HUD state\n"
                + "3. Adjust layout, colors, fonts in the scene\n"
                + "4. Click \"Apply HUD to All Tower-Defense Levels\" above\n\n"
                + "The same sync is also available as a menu item.",
                MessageType.Info);
        }

        // ────────────────────────────
        //  Sync
        // ────────────────────────────

        private static void SyncToAllLevels()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[TowerDefenseUiPreview] Save cancelled. Sync aborted.");
                return;
            }

            TowerDefenseMapToolkitWindow.SyncFinalTowerDefenseLevelsFromUiTemplate();
        }

        [MenuItem(SyncMenuPath)]
        private static void SyncFromMenu()
        {
            SyncToAllLevels();
        }

        // ────────────────────────────
        //  Open / create preview scene
        // ────────────────────────────

        [MenuItem(OpenPreviewMenuPath, priority = 30)]
        private static void OpenUiPreviewScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[TowerDefenseUiPreview] Save cancelled. Open aborted.");
                return;
            }

            if (!File.Exists(PreviewScenePath))
            {
                CreateUiPreviewScene();
            }

            EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
        }

        private static void CreateUiPreviewScene()
        {
            if (!File.Exists(TemplateScenePath))
            {
                Debug.LogError(
                    $"[TowerDefenseUiPreview] Template scene not found at '{TemplateScenePath}'. "
                    + "Cannot create preview scene.");
                return;
            }

            // Open the template, copy everything, add the preview controller, save as preview
            Scene templateScene = EditorSceneManager.OpenScene(TemplateScenePath, OpenSceneMode.Single);

            // Ensure scene-authored HUD objects exist in the template before creating the preview
            TowerDefenseMapToolkitUtility.EnsurePopupAndActionButtonsExist(templateScene);

            // Ensure TowerDefenseUiPreviewController exists in the scene
            TowerDefenseUiPreviewController existingController =
                Object.FindFirstObjectByType<TowerDefenseUiPreviewController>(FindObjectsInactive.Include);

            if (existingController == null)
            {
                GameObject controllerObject = new GameObject("TowerDefenseUiPreviewController");
                controllerObject.AddComponent<TowerDefenseUiPreviewController>();
                Undo.RegisterCreatedObjectUndo(controllerObject, "Create UI Preview Controller");
            }

            // Remove any TowerDefenseGame instance that might interfere in edit mode
            TowerDefenseGame gameInstance = Object.FindFirstObjectByType<TowerDefenseGame>(FindObjectsInactive.Include);
            if (gameInstance != null)
            {
                gameInstance.enabled = false;
                Debug.Log("[TowerDefenseUiPreview] Disabled TowerDefenseGame in preview scene to avoid edit-mode interference.");
            }

            // Save as the preview scene
            EditorSceneManager.SaveScene(templateScene, PreviewScenePath);

            Debug.Log(
                $"[TowerDefenseUiPreview] Created preview scene at '{PreviewScenePath}'. "
                + "You can now adjust the HUD and use the sync button to apply changes to all levels.");
        }
    }
}

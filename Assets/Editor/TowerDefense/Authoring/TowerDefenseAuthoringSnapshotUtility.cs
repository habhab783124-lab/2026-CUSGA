using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace TowerDefense.Editor
{
    /// <summary>
    /// Shared helper for scene-level authoring snapshots.
    ///
    /// Why this exists:
    /// 1. Map authoring tools can create a lot of scene objects quickly.
    /// 2. When a generation pass goes wrong, plain Undo is sometimes not enough or too fragile.
    /// 3. Authors need one explicit "safe point" mechanism before batch operations.
    ///
    /// This helper intentionally stays scene-file based:
    /// - create a snapshot copy of the current scene file
    /// - restore the most recent snapshot back into the current scene path
    ///
    /// It does not try to invent a second serialization format.
    /// </summary>
    internal static class TowerDefenseAuthoringSnapshotUtility
    {
        private const string SnapshotFolder = "Assets/Scenes/__AuthoringSnapshots";

        internal static bool TryCreateSnapshotForActiveScene(out string snapshotPath, out string errorMessage)
        {
            snapshotPath = string.Empty;
            errorMessage = string.Empty;

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || string.IsNullOrWhiteSpace(activeScene.path))
            {
                errorMessage = "当前活动场景无效，或场景还没有保存到磁盘。";
                return false;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                errorMessage = "用户取消了场景保存，未创建快照。";
                return false;
            }

            Directory.CreateDirectory(Path.GetFullPath(SnapshotFolder));

            string sceneFileName = Path.GetFileNameWithoutExtension(activeScene.path);
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string targetRelativePath = $"{SnapshotFolder}/{sceneFileName}__snapshot__{timestamp}.unity";
            string sourceAbsolutePath = Path.GetFullPath(activeScene.path);
            string targetAbsolutePath = Path.GetFullPath(targetRelativePath);

            File.Copy(sourceAbsolutePath, targetAbsolutePath, overwrite: true);
            string sourceMetaPath = sourceAbsolutePath + ".meta";
            string targetMetaPath = targetAbsolutePath + ".meta";
            if (File.Exists(sourceMetaPath))
            {
                File.Copy(sourceMetaPath, targetMetaPath, overwrite: true);
            }

            AssetDatabase.Refresh();
            snapshotPath = targetRelativePath;
            return true;
        }

        internal static bool TryRestoreLatestSnapshotForActiveScene(out string restoredFromPath, out string errorMessage)
        {
            restoredFromPath = string.Empty;
            errorMessage = string.Empty;

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || string.IsNullOrWhiteSpace(activeScene.path))
            {
                errorMessage = "当前活动场景无效，或场景还没有保存到磁盘。";
                return false;
            }

            string sceneFileName = Path.GetFileNameWithoutExtension(activeScene.path);
            string snapshotDirectory = Path.GetFullPath(SnapshotFolder);
            if (!Directory.Exists(snapshotDirectory))
            {
                errorMessage = "当前还没有可恢复的作者快照。";
                return false;
            }

            string latestSnapshotAbsolutePath = Directory.GetFiles(snapshotDirectory, $"{sceneFileName}__snapshot__*.unity")
                .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(latestSnapshotAbsolutePath))
            {
                errorMessage = "当前场景还没有匹配的作者快照。";
                return false;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                errorMessage = "用户取消了场景保存，未执行快照恢复。";
                return false;
            }

            string targetAbsolutePath = Path.GetFullPath(activeScene.path);
            File.Copy(latestSnapshotAbsolutePath, targetAbsolutePath, overwrite: true);
            string latestSnapshotMetaPath = latestSnapshotAbsolutePath + ".meta";
            string targetMetaPath = targetAbsolutePath + ".meta";
            if (File.Exists(latestSnapshotMetaPath))
            {
                File.Copy(latestSnapshotMetaPath, targetMetaPath, overwrite: true);
            }

            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(activeScene.path, OpenSceneMode.Single);
            restoredFromPath = latestSnapshotAbsolutePath.Replace('\\', '/');
            return true;
        }
    }
}

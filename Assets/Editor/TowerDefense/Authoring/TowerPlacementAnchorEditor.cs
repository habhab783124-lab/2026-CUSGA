using UnityEditor;
using UnityEngine;

namespace TowerDefense.Editor
{
    [CustomEditor(typeof(TowerPlacementAnchor))]
    public sealed class TowerPlacementAnchorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            TowerPlacementAnchor anchor = (TowerPlacementAnchor)target;

            // Draw the default inspector (shows the anchorPoint Transform field).
            DrawDefaultInspector();

            Transform anchorTransform = anchor.AnchorPoint;
            if (anchorTransform == null)
            {
                EditorGUILayout.HelpBox(
                    "把一个子物体拖入上面的 Anchor Point 字段，该子物体的位置就是塔的视觉底部。",
                    MessageType.Warning);
                return;
            }

            if (anchorTransform == anchor.transform)
            {
                EditorGUILayout.HelpBox(
                    "Anchor Point 应该指向一个子物体，不能指向自己。",
                    MessageType.Error);
                return;
            }

            float localY = anchorTransform.localPosition.y;
            float lossyScaleY = anchor.transform.lossyScale.y;
            float worldOffset = -localY * lossyScaleY;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("计算结果", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.FloatField("锚点本地 Y (localPosition.y)", localY);
            EditorGUILayout.FloatField("世界空间偏移 (world offset)", worldOffset);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.HelpBox(
                $"拖拽 / 放置时鼠标对齐锚点。当前偏移 = {worldOffset:F3} 世界单位。",
                MessageType.Info);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("快速定位锚点", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("对齐到 Tight Mesh 底部"))
                {
                    SnapToTightMeshBottom(anchorTransform);
                }

                if (GUILayout.Button("对齐到 Collider 中心"))
                {
                    SnapToColliderCenter(anchorTransform);
                }
            }

            if (GUILayout.Button("重置到根节点"))
            {
                Undo.RecordObject(anchorTransform, "Reset Placement Anchor");
                anchorTransform.localPosition = Vector3.zero;
            }
        }

        private void SnapToTightMeshBottom(Transform anchorTransform)
        {
            Transform root = anchorTransform.parent;
            if (root == null)
            {
                return;
            }

            SpriteRenderer sr = root.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null)
            {
                Debug.LogWarning("根节点没有 SpriteRenderer，无法自动对齐。");
                return;
            }

            Undo.RecordObject(anchorTransform, "Snap Anchor to Mesh Bottom");
            Vector3 lp = anchorTransform.localPosition;
            lp.y = sr.sprite.bounds.min.y;
            anchorTransform.localPosition = lp;
        }

        private void SnapToColliderCenter(Transform anchorTransform)
        {
            Transform root = anchorTransform.parent;
            if (root == null)
            {
                return;
            }

            CircleCollider2D collider = root.GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                Debug.LogWarning("根节点没有 CircleCollider2D，无法自动对齐。");
                return;
            }

            Undo.RecordObject(anchorTransform, "Snap Anchor to Collider Center");
            Vector3 lp = anchorTransform.localPosition;
            lp.y = collider.offset.y;
            anchorTransform.localPosition = lp;
        }
    }
}

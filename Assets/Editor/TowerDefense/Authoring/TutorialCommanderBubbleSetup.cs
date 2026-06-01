using UnityEditor;
using UnityEngine;
using TMPro;

public static class TutorialCommanderBubbleSetup
{
    [MenuItem("Tower Defense/Tutorial/Setup Commander Bubble in Scene")]
    private static void SetupCommanderBubble()
    {
        Level01TutorialDirector director = Object.FindFirstObjectByType<Level01TutorialDirector>();
        if (director == null)
        {
            EditorUtility.DisplayDialog(
                "Setup Failed",
                "当前场景中未找到 Level01TutorialDirector。请先打开 Tutorial Level 场景。",
                "OK");
            return;
        }

        SerializedObject directorSo = new SerializedObject(director);
        SerializedProperty bubbleProp = directorSo.FindProperty("commanderBubble");
        if (bubbleProp == null)
        {
            EditorUtility.DisplayDialog("Setup Failed", "Level01TutorialDirector 中未找到 commanderBubble 字段。", "OK");
            return;
        }

        if (bubbleProp.objectReferenceValue != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "Already Configured",
                    "commanderBubble 已有引用，是否重新创建？",
                    "重新创建", "取消"))
            {
                return;
            }
        }

        Sprite bubbleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/Story/Sprites/UI/center_bubble.png");
        Sprite portraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/Story/Sprites/Character/shen_left.png");
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/_Project/Fonts/zpix SDF.asset");

        GameObject root = new GameObject("CommanderBubble");
        Undo.RegisterCreatedObjectUndo(root, "Create Commander Bubble");
        root.transform.SetParent(director.transform.parent, false);
        root.transform.localPosition = new Vector3(-5.5f, -3.2f, 0f);

        GameObject spriteChild = new GameObject("BubbleSprite");
        spriteChild.transform.SetParent(root.transform, false);
        SpriteRenderer sr = spriteChild.AddComponent<SpriteRenderer>();
        if (bubbleSprite != null)
        {
            sr.sprite = bubbleSprite;
        }

        sr.sortingOrder = 50;
        spriteChild.transform.localScale = new Vector3(2.2f, 1.1f, 1f);

        TutorialCommanderBubble bubble = root.AddComponent<TutorialCommanderBubble>();

        SerializedObject bubbleSo = new SerializedObject(bubble);
        SetProperty(bubbleSo, "bubbleRenderer", sr);
        SetProperty(bubbleSo, "portraitSprite", portraitSprite);
        SetProperty(bubbleSo, "fontAsset", fontAsset);
        bubbleSo.ApplyModifiedPropertiesWithoutUndo();

        directorSo.Update();
        bubbleProp.objectReferenceValue = bubble;
        directorSo.ApplyModifiedProperties();

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);

        Debug.Log("[TutorialCommanderBubbleSetup] CommanderBubble 已创建并绑定到 Level01TutorialDirector。");
    }

    [MenuItem("Tower Defense/Tutorial/Setup Drag Hint Overlay in Scene")]
    private static void SetupDragHintOverlay()
    {
        Level01TutorialDirector director = Object.FindFirstObjectByType<Level01TutorialDirector>();
        if (director == null)
        {
            EditorUtility.DisplayDialog(
                "Setup Failed",
                "当前场景中未找到 Level01TutorialDirector。请先打开 Tutorial Level 场景。",
                "OK");
            return;
        }

        SerializedObject directorSo = new SerializedObject(director);
        SerializedProperty hintProp = directorSo.FindProperty("dragHintOverlay");
        if (hintProp == null)
        {
            EditorUtility.DisplayDialog("Setup Failed", "Level01TutorialDirector 中未找到 dragHintOverlay 字段。", "OK");
            return;
        }

        if (hintProp.objectReferenceValue != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "Already Configured",
                    "dragHintOverlay 已有引用，是否重新创建？",
                    "重新创建", "取消"))
            {
                return;
            }
        }

        GameObject root = new GameObject("DragHintOverlay");
        Undo.RegisterCreatedObjectUndo(root, "Create Drag Hint Overlay");
        root.transform.SetParent(director.transform.parent, false);

        TutorialDragHintOverlay overlay = root.AddComponent<TutorialDragHintOverlay>();

        SerializedObject overlaySo = new SerializedObject(overlay);
        SerializedProperty startPos = overlaySo.FindProperty("startAnchorPosition");
        if (startPos != null) startPos.vector2Value = new Vector2(200f, 120f);
        SerializedProperty targetPos = overlaySo.FindProperty("targetWorldPosition");
        if (targetPos != null) targetPos.vector3Value = new Vector3(0f, 0f, 0f);
        overlaySo.ApplyModifiedPropertiesWithoutUndo();

        directorSo.Update();
        hintProp.objectReferenceValue = overlay;
        directorSo.ApplyModifiedProperties();

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);

        Debug.Log("[TutorialCommanderBubbleSetup] DragHintOverlay 已创建并绑定到 Level01TutorialDirector。");
    }

    [MenuItem("Tower Defense/Tutorial/Setup Camera Director in Scene")]
    private static void SetupCameraDirector()
    {
        Level01TutorialDirector director = Object.FindFirstObjectByType<Level01TutorialDirector>();
        if (director == null)
        {
            EditorUtility.DisplayDialog(
                "Setup Failed",
                "当前场景中未找到 Level01TutorialDirector。请先打开 Tutorial Level 场景。",
                "OK");
            return;
        }

        SerializedObject directorSo = new SerializedObject(director);
        SerializedProperty camProp = directorSo.FindProperty("cameraDirector");
        if (camProp == null)
        {
            EditorUtility.DisplayDialog("Setup Failed", "Level01TutorialDirector 中未找到 cameraDirector 字段。", "OK");
            return;
        }

        if (camProp.objectReferenceValue != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "Already Configured",
                    "cameraDirector 已有引用，是否重新创建？",
                    "重新创建", "取消"))
            {
                return;
            }
        }

        GameObject root = new GameObject("TutorialCameraDirector");
        Undo.RegisterCreatedObjectUndo(root, "Create Tutorial Camera Director");
        root.transform.SetParent(director.transform.parent, false);

        TutorialCameraDirector camDirector = root.AddComponent<TutorialCameraDirector>();

        directorSo.Update();
        camProp.objectReferenceValue = camDirector;
        directorSo.ApplyModifiedProperties();

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);

        Debug.Log("[TutorialCommanderBubbleSetup] TutorialCameraDirector 已创建并绑定到 Level01TutorialDirector。");
    }

    private static void SetProperty(SerializedObject so, string name, Object value)
    {
        SerializedProperty prop = so.FindProperty(name);
        if (prop != null)
        {
            prop.objectReferenceValue = value;
        }
    }
}

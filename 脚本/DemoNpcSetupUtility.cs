using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class DemoNpcSetupUtility
{
    private const string WhiteTexturePath = "Assets/Editor/Placeholders/WhiteRect.png";
    private const string BlackTexturePath = "Assets/Editor/Placeholders/BlackRect.png";

    [MenuItem("Tools/Demo Setup/Rebuild NPC Demo Pair", false)]
    public static void RebuildNpcPair()
    {
        var activeScene = SceneManager.GetActiveScene();

        if (!activeScene.IsValid())
        {
            EditorUtility.DisplayDialog("场景未就绪", "请先打开一个 Scene 后再执行。", "确认");
            return;
        }

        var whiteSprite = EnsureRectangleSprite(WhiteTexturePath, Color.white);
        var blackSprite = EnsureRectangleSprite(BlackTexturePath, Color.black);

        if (whiteSprite == null || blackSprite == null)
        {
            EditorUtility.DisplayDialog("占位素材失败", "未能创建占位方块素材，请检查 Editor/Placeholders 目录写权限。", "确认");
            return;
        }

        RemoveIfExists("NPC");
        RemoveIfExists("NPC2");

        var player = CreatePlayer(whiteSprite, activeScene);
        var interactNpc = CreateInteractableNpc(blackSprite, player.transform.position, activeScene);

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorUtility.DisplayDialog("重建完成", "NPC 与 NPC2 已重建，使用白色/黑色长方形占位完成。", "确认");
    }

    private static void RemoveIfExists(string objectName)
    {
        var old = GameObject.Find(objectName);
        if (old != null)
        {
            Object.DestroyImmediate(old);
        }
    }

    private static GameObject CreatePlayer(Sprite whiteSprite, Scene scene)
    {
        var npc = new GameObject("NPC");
        SceneManager.MoveGameObjectToScene(npc, scene);

        npc.transform.position = new Vector3(-3f, -1.5f, 0f);

        var spriteRenderer = npc.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = whiteSprite;
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = 2;
        spriteRenderer.size = new Vector2(1f, 1.8f);

        var rb = npc.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.freezeRotation = true;

        var collider = npc.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.8f, 1.8f);
        collider.offset = new Vector2(0f, 0.9f);
        collider.isTrigger = false;

        var controller = npc.AddComponent<PlayerController>();
        var controllerSerialized = new SerializedObject(controller);

        SetSerializedFloat(controllerSerialized, "moveSpeed", 5f);
        SetSerializedBool(controllerSerialized, "useBoundary", true);
        SetSerializedFloat(controllerSerialized, "minX", -8f);
        SetSerializedFloat(controllerSerialized, "maxX", 8f);
        SetSerializedString(controllerSerialized, "horizontalAxis", "Horizontal");
        SetSerializedFloat(controllerSerialized, "inputDeadZone", 0.01f);
        SetSerializedFloat(controllerSerialized, "moveSmoothing", 20f);
        SetSerializedObjectRef(controllerSerialized, "animator", npc.GetComponent<Animator>());
        SetSerializedBool(controllerSerialized, "isFrozen", false);

        controllerSerialized.ApplyModifiedProperties();
        return npc;
    }

    private static GameObject CreateInteractableNpc(Sprite blackSprite, Vector3 playerPos, Scene scene)
    {
        var npc2 = new GameObject("NPC2");
        SceneManager.MoveGameObjectToScene(npc2, scene);

        npc2.transform.position = playerPos + new Vector3(4f, 0f, 0f);

        var spriteRenderer = npc2.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = blackSprite;
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = 2;
        spriteRenderer.size = new Vector2(1f, 1.8f);

        var collider = npc2.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(1.2f, 2f);
        collider.offset = new Vector2(0f, 0.9f);

        var interactable = npc2.AddComponent<NPCInteractable>();
        var so = new SerializedObject(interactable);

        SetSerializedBool(so, "interactable", true);
        SetSerializedString(so, "interactionPrompt", "按 E 交互");
        SetSerializedInt(so, "playerLayer", -1);
        SetSerializedFloat(so, "typingSpeed", 0.05f);

        var promptRoot = CreatePrompt(npc2.transform);
        var dialogueCanvasRoot = CreateWorldDialogueCanvas(npc2.transform, out var dialogueText);

        SetSerializedObjectRef(so, "interactPromptRoot", promptRoot);
        SetSerializedObjectRef(so, "interactPromptText", promptRoot.GetComponentInChildren<TextMeshProUGUI>());
        SetSerializedObjectRef(so, "dialogueCanvasRoot", dialogueCanvasRoot);
        SetSerializedObjectRef(so, "dialogueText", dialogueText);

        var linesProp = so.FindProperty("dialogueLines");
        if (linesProp != null && linesProp.isArray)
        {
            linesProp.arraySize = 3;
            linesProp.GetArrayElementAtIndex(0).stringValue = "这是 NPC2 的测试对话。";
            linesProp.GetArrayElementAtIndex(1).stringValue = "按左键可继续下一句。";
            linesProp.GetArrayElementAtIndex(2).stringValue = "完成后主角会自动恢复移动。";
        }

        so.ApplyModifiedProperties();
        return npc2;
    }

    private static GameObject CreatePrompt(Transform parent)
    {
        var promptRoot = new GameObject("InteractPrompt");
        promptRoot.transform.SetParent(parent, false);
        promptRoot.transform.localPosition = new Vector3(0f, 2.2f, 0f);
        promptRoot.transform.localScale = Vector3.one * 0.01f;

        var promptCanvasObj = new GameObject("PromptCanvas");
        promptCanvasObj.transform.SetParent(promptRoot.transform, false);

        var promptCanvas = promptCanvasObj.AddComponent<Canvas>();
        promptCanvas.renderMode = RenderMode.WorldSpace;
        promptCanvasObj.AddComponent<CanvasScaler>();
        promptCanvasObj.AddComponent<GraphicRaycaster>();

        var promptRect = promptCanvasObj.GetComponent<RectTransform>();
        promptRect.sizeDelta = new Vector2(180, 40);

        var promptBg = new GameObject("PromptBg");
        promptBg.transform.SetParent(promptCanvasObj.transform, false);
        var promptBgRect = promptBg.AddComponent<RectTransform>();
        promptBgRect.sizeDelta = new Vector2(180, 40);
        var promptBgImg = promptBg.AddComponent<Image>();
        promptBgImg.color = new Color(0f, 0f, 0f, 0.65f);

        var promptTextObj = new GameObject("PromptText");
        promptTextObj.transform.SetParent(promptBg.transform, false);
        var promptTextRect = promptTextObj.AddComponent<RectTransform>();
        promptTextRect.sizeDelta = new Vector2(170, 30);

        var promptText = promptTextObj.AddComponent<TextMeshProUGUI>();
        promptText.text = "按 E 交互";
        promptText.color = Color.white;
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.fontSize = 32;

        return promptRoot;
    }

    private static GameObject CreateWorldDialogueCanvas(Transform parent, out TextMeshProUGUI dialogueText)
    {
        var dialogueCanvasRoot = new GameObject("DialogueCanvas");
        dialogueCanvasRoot.transform.SetParent(parent, false);
        dialogueCanvasRoot.transform.localPosition = new Vector3(0f, 1.9f, 0f);
        dialogueCanvasRoot.transform.localScale = Vector3.one * 0.01f;

        var canvas = dialogueCanvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;
        dialogueCanvasRoot.AddComponent<CanvasScaler>();
        dialogueCanvasRoot.AddComponent<GraphicRaycaster>();

        var canvasRect = dialogueCanvasRoot.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(260, 120);

        var bg = new GameObject("DialogueBackground");
        bg.transform.SetParent(dialogueCanvasRoot.transform, false);
        var bgRect = bg.AddComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(260, 120);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.8f);

        var textObj = new GameObject("DialogueText");
        textObj.transform.SetParent(bg.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(240, 100);
        dialogueText = textObj.AddComponent<TextMeshProUGUI>();
        dialogueText.text = string.Empty;
        dialogueText.color = Color.white;
        dialogueText.fontSize = 32;
        dialogueText.alignment = TextAlignmentOptions.TopLeft;
        dialogueText.enableWordWrapping = true;
        dialogueText.rectTransform.anchorMin = new Vector2(0f, 1f);
        dialogueText.rectTransform.anchorMax = new Vector2(1f, 1f);
        dialogueText.rectTransform.pivot = new Vector2(0.5f, 1f);
        dialogueText.rectTransform.anchoredPosition = new Vector2(0f, -6f);

        dialogueCanvasRoot.SetActive(false);
        return dialogueCanvasRoot;
    }

    private static Sprite EnsureRectangleSprite(string assetPath, Color color)
    {
        var dir = Path.GetDirectoryName(assetPath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (!File.Exists(assetPath))
        {
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var pixels = new Color[64 * 64];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            tex.SetPixels(pixels);
            tex.Apply();

            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(assetPath);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private static void SetSerializedFloat(SerializedObject so, string field, float value)
    {
        var p = so.FindProperty(field);
        if (p != null)
        {
            p.floatValue = value;
        }
    }

    private static void SetSerializedInt(SerializedObject so, string field, int value)
    {
        var p = so.FindProperty(field);
        if (p != null)
        {
            p.intValue = value;
        }
    }

    private static void SetSerializedBool(SerializedObject so, string field, bool value)
    {
        var p = so.FindProperty(field);
        if (p != null)
        {
            p.boolValue = value;
        }
    }

    private static void SetSerializedString(SerializedObject so, string field, string value)
    {
        var p = so.FindProperty(field);
        if (p != null)
        {
            p.stringValue = value;
        }
    }

    private static void SetSerializedObjectRef(SerializedObject so, string field, Object value)
    {
        var p = so.FindProperty(field);
        if (p != null)
        {
            p.objectReferenceValue = value;
        }
    }
}

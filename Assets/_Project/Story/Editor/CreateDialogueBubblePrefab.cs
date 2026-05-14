using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生成默认 World Space 对话气泡预制体：Bubble 随文本改 sizeDelta，Background 全铺 + Sliced，文本换行。
/// </summary>
public static class CreateDialogueBubblePrefab
{
    private const string PrefabPath = "Assets/_Project/Story/Prefabs/DialogueBubble.prefab";

    [MenuItem("Tools/Story/Create Dialogue Bubble Prefab")]
    public static void Create()
    {
        EnsureFolder("Assets/_Project");
        EnsureFolder("Assets/_Project/Story");
        EnsureFolder("Assets/_Project/Story/Prefabs");

        var root = new GameObject("DialogueBubble");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 500;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 50f;
        root.AddComponent<GraphicRaycaster>();

        var rootRt = root.GetComponent<RectTransform>();
        rootRt.sizeDelta = new Vector2(1f, 1f);

        var bubbleRt = new GameObject("Bubble", typeof(RectTransform)).GetComponent<RectTransform>();
        bubbleRt.SetParent(rootRt, false);
        bubbleRt.anchorMin = new Vector2(0.5f, 0.5f);
        bubbleRt.anchorMax = new Vector2(0.5f, 0.5f);
        bubbleRt.pivot = new Vector2(0.5f, 0f);
        bubbleRt.anchoredPosition = Vector2.zero;
        bubbleRt.sizeDelta = new Vector2(4f, 1.5f);

        var contentRt = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        contentRt.SetParent(bubbleRt, false);
        contentRt.anchorMin = Vector2.zero;
        contentRt.anchorMax = Vector2.one;
        contentRt.pivot = new Vector2(0.5f, 0.5f);
        contentRt.anchoredPosition3D = Vector3.zero;
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;

        var bgRt = new GameObject("Background", typeof(RectTransform)).GetComponent<RectTransform>();
        bgRt.SetParent(contentRt, false);
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var bg = bgRt.gameObject.AddComponent<Image>();
        bg.type = Image.Type.Sliced;
        bg.preserveAspect = false;
        bg.useSpriteMesh = false;
        bg.fillCenter = true;
        bg.color = Color.white;

        var textRt = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
        textRt.SetParent(contentRt, false);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        var tmp = textRt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 0.52f;
        tmp.color = Color.black;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;
        tmp.overflowMode = TextOverflowModes.Overflow;

        var tailRt = new GameObject("Tail", typeof(RectTransform)).GetComponent<RectTransform>();
        tailRt.SetParent(bubbleRt, false);
        tailRt.anchorMin = new Vector2(0.5f, 0f);
        tailRt.anchorMax = new Vector2(0.5f, 0f);
        tailRt.pivot = new Vector2(0.5f, 1f);
        tailRt.anchoredPosition = Vector2.zero;
        tailRt.sizeDelta = new Vector2(0.8f, 0.55f);

        var tailImg = tailRt.gameObject.AddComponent<Image>();
        tailImg.enabled = false;
        tailImg.raycastTarget = false;
        tailImg.type = Image.Type.Simple;
        tailImg.preserveAspect = true;
        tailImg.color = Color.white;

        var tailGraphic = tailRt.gameObject.AddComponent<DialogueBubbleTailGraphic>();
        tailGraphic.raycastTarget = false;
        tailGraphic.color = Color.white;

        var view = root.AddComponent<DialogueBubbleView>();
        Wire(view, "rootCanvas", canvas);
        Wire(view, "bubbleFrame", bubbleRt);
        Wire(view, "contentRoot", contentRt);
        Wire(view, "backgroundImage", bg);
        Wire(view, "tailRect", tailRt);
        Wire(view, "tailImage", tailImg);
        Wire(view, "tailGraphic", tailGraphic);
        Wire(view, "lineText", tmp);

        canvas.gameObject.SetActive(false);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        Debug.Log("已生成: " + PrefabPath + "。请把气泡 Sprite 拖到 Background，并在 Sprite Editor 设 Border。");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        int slash = path.LastIndexOf('/');
        if (slash <= 0)
        {
            return;
        }

        string parent = path.Substring(0, slash);
        string name = path.Substring(slash + 1);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, name);
    }

    private static void Wire(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p != null)
        {
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

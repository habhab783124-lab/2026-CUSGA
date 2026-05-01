using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates a default world-space interaction prompt prefab.
/// </summary>
public static class CreateInteractionPromptPrefab
{
    private const string PrefabPath = "Assets/_Project/Story/Prefabs/InteractionPrompt.prefab";

    [MenuItem("Tools/Story/Create Interaction Prompt Prefab")]
    public static void Create()
    {
        EnsureFolder("Assets/_Project");
        EnsureFolder("Assets/_Project/Story");
        EnsureFolder("Assets/_Project/Story/Prefabs");

        var root = new GameObject("InteractionPrompt");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 520;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 50f;
        root.AddComponent<GraphicRaycaster>();

        var rootRt = root.GetComponent<RectTransform>();
        rootRt.sizeDelta = new Vector2(1f, 1f);

        // Simple background pill
        var plateRt = new GameObject("Plate", typeof(RectTransform)).GetComponent<RectTransform>();
        plateRt.SetParent(rootRt, false);
        plateRt.anchorMin = new Vector2(0.5f, 0.5f);
        plateRt.anchorMax = new Vector2(0.5f, 0.5f);
        plateRt.pivot = new Vector2(0.5f, 0.5f);
        plateRt.sizeDelta = new Vector2(1.2f, 0.55f);

        var bg = plateRt.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        bg.raycastTarget = false;

        // Text
        var textRt = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
        textRt.SetParent(plateRt, false);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(0.08f, 0.05f);
        textRt.offsetMax = new Vector2(-0.08f, -0.05f);

        var tmp = textRt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = "[E]";
        tmp.fontSize = 0.5f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        var view = root.AddComponent<InteractionPromptView>();
        Wire(view, "rootCanvas", canvas);
        Wire(view, "promptText", tmp);

        canvas.gameObject.SetActive(false);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        Debug.Log("已生成: " + PrefabPath);
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


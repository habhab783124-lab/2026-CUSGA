using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// `LevelSelectCard` 只负责“单张关卡卡片”的显示与点击入口。
///
/// 这样做的好处是：
/// 1. 关卡选择页总控不用把每个子文本和图片都攥在自己手里。
/// 2. 以后你如果想单独改某一张卡片的层级、配色或装饰，改动范围更小。
/// 3. 卡片对象本身就是场景里的真实 UI 物体，便于直接在 Scene 视图里拖动和调整。
///
/// 这一版里，卡片视觉默认由 Scene 自己维护；
/// 控制器运行时只负责给它挂点击行为，不再持续回写标题、颜色和图标。
/// </summary>
public sealed class LevelSelectCard : MonoBehaviour
{
    [Header("Scene Refs")]
    [SerializeField] private Button selectButton; // 中文：select按钮
    [SerializeField] private Image backgroundImage; // 中文：背景Image
    [SerializeField] private Image accentStripImage; // 中文：accentStripImage
    [SerializeField] private Image iconImage; // 中文：图标Image
    [SerializeField] private TextMeshProUGUI titleText; // 中文：标题文本
    [SerializeField] private TextMeshProUGUI subtitleText; // 中文：副标题文本
    [SerializeField] private TextMeshProUGUI descriptionText; // 中文：描述文本
    [SerializeField] private TextMeshProUGUI statusText; // 中文：状态文本

    /// <summary>
    /// 作者工具在首次搭默认骨架时，会把生成出来的子引用直接塞回卡片组件。
    /// 这样后面就不需要再靠对象名回查这些 UI。
    /// </summary>
    public void CaptureGeneratedReferences(
        Button button,
        Image background,
        Image accentStrip,
        Image icon,
        TextMeshProUGUI title,
        TextMeshProUGUI subtitle,
        TextMeshProUGUI description,
        TextMeshProUGUI status)
    {
        selectButton = button;
        backgroundImage = background;
        accentStripImage = accentStrip;
        iconImage = icon;
        titleText = title;
        subtitleText = subtitle;
        descriptionText = description;
        statusText = status;
    }

    /// <summary>
    /// 把关卡定义数据真正刷到卡片上。
    ///
    /// 这里保持“只改显示，不改布局”的边界：
    /// - 会同步文本、颜色、字体和按钮状态
    /// - 不会强行把你在 Scene 里手调过的位置又改回去
    /// </summary>
    public void ApplyPresentation(
        string title,
        string subtitle,
        string description,
        string status,
        Color accentColor,
        Sprite iconSprite,
        TMP_FontAsset titleFontAsset,
        TMP_FontAsset bodyFontAsset,
        TMP_FontAsset accentFontAsset,
        bool interactable)
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = Color.Lerp(new Color(0.08f, 0.1f, 0.14f, 0.96f), accentColor, 0.14f);
        }

        if (accentStripImage != null)
        {
            accentStripImage.color = accentColor;
        }

        if (iconImage != null)
        {
            iconImage.sprite = iconSprite;
            iconImage.color = interactable ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            iconImage.gameObject.SetActive(iconSprite != null);
        }

        ApplyText(titleText, title, Color.white, titleFontAsset);
        ApplyText(subtitleText, subtitle, accentColor, accentFontAsset);
        ApplyText(descriptionText, description, new Color(0.83f, 0.88f, 0.94f, interactable ? 1f : 0.66f), bodyFontAsset);
        ApplyText(statusText, status, interactable ? accentColor : new Color(0.75f, 0.78f, 0.84f, 0.86f), accentFontAsset);

        if (selectButton != null)
        {
            selectButton.interactable = interactable;

            ColorBlock colors = selectButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.Lerp(accentColor, Color.white, 0.45f);
            colors.pressedColor = Color.Lerp(accentColor, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.28f, 0.3f, 0.34f, 0.78f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            selectButton.colors = colors;
        }
    }

    /// <summary>
    /// 关卡按钮点击逻辑统一从总控层灌进来。
    /// 卡片自己不保留场景跳转逻辑，避免单卡和总控的职责混在一起。
    /// </summary>
    public void SetClickAction(UnityAction action)
    {
        if (selectButton == null)
        {
            return;
        }

        selectButton.onClick.RemoveAllListeners();
        if (action != null)
        {
            selectButton.onClick.AddListener(action);
        }

        selectButton.interactable = action != null;
    }

    private void OnValidate()
    {
        // 这里只做最轻量的兜底：
        // 如果按钮引用已经有了，但背景图没记住，就从按钮同物体补回来。
        // 这样能减少“忘记拖一个同物体组件”的低级失误。
        if (selectButton != null && backgroundImage == null)
        {
            backgroundImage = selectButton.GetComponent<Image>();
        }
    }

    private static void ApplyText(TextMeshProUGUI label, string value, Color color, TMP_FontAsset preferredFontAsset)
    {
        if (label == null)
        {
            return;
        }

        label.text = value;
        label.color = color;

        TMP_FontAsset resolvedFontAsset = ResolveFontAsset(preferredFontAsset);
        if (resolvedFontAsset != null)
        {
            label.font = resolvedFontAsset;
        }
    }

    private static TMP_FontAsset ResolveFontAsset(TMP_FontAsset preferredFontAsset)
    {
        if (preferredFontAsset != null)
        {
            return preferredFontAsset;
        }

        if (TMP_Settings.defaultFontAsset != null)
        {
            return TMP_Settings.defaultFontAsset;
        }

        return null;
    }
}

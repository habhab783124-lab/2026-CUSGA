using UnityEngine;

/// <summary>
/// `Level04RingGuide` 是 `Level04` 关卡专用的作者辅助组件。
///
/// 它不参与任何运行时玩法判定，也不会改变塔位合法性。
/// 这个组件只负责把“三环能源中枢”的设计语义明确挂在场景里，
/// 让后续继续手改地图时，仍然能一眼看出每一层空地原本承担什么职责：
/// - 外环：更安全，适合稳定布塔
/// - 中环：过渡层，适合补线或中继
/// - 内环：高价值，同时也是压力最高的核心争夺区
///
/// 之所以把这层信息做成独立组件，而不是只靠对象命名，
/// 是因为地图持续迭代时，最容易丢的是“这块空地为什么在这里”的设计意图。
/// 现在把标签、颜色和锚点一起序列化下来，Scene 视图就能持续提醒作者。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level04RingGuide : MonoBehaviour
{
    [Header("Scene Labels")]
    [SerializeField] private bool showSceneLabels = true;
    [SerializeField] private string outerRingLabel = "外环：安全塔位";
    [SerializeField] private string midRingLabel = "中环：过渡塔位";
    [SerializeField] private string innerRingLabel = "内环：高价值塔位";

    [Header("Label Anchors")]
    [SerializeField] private Vector3 outerRingLabelLocalPosition = new Vector3(-6.4f, 5.4f, 0f);
    [SerializeField] private Vector3 midRingLabelLocalPosition = new Vector3(1.4f, 3.35f, 0f);
    [SerializeField] private Vector3 innerRingLabelLocalPosition = new Vector3(1.2f, 0.95f, 0f);

    [Header("Label Colors")]
    [SerializeField] private Color outerRingColor = new Color(0.22f, 0.72f, 1f, 1f);
    [SerializeField] private Color midRingColor = new Color(0.36f, 0.92f, 0.68f, 1f);
    [SerializeField] private Color innerRingColor = new Color(1f, 0.72f, 0.28f, 1f);

    public bool ShowSceneLabels => showSceneLabels;
    public string OuterRingLabel => outerRingLabel;
    public string MidRingLabel => midRingLabel;
    public string InnerRingLabel => innerRingLabel;
    public Vector3 OuterRingLabelWorldPosition => transform.TransformPoint(outerRingLabelLocalPosition);
    public Vector3 MidRingLabelWorldPosition => transform.TransformPoint(midRingLabelLocalPosition);
    public Vector3 InnerRingLabelWorldPosition => transform.TransformPoint(innerRingLabelLocalPosition);
    public Color OuterRingColor => outerRingColor;
    public Color MidRingColor => midRingColor;
    public Color InnerRingColor => innerRingColor;
}

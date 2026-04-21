using UnityEngine;

/// <summary>
/// `DefensePointFlag` 表示地图中的一个防御点旗帜。
///
/// 当前版本通常还是单防御点共享生命值，
/// 但文档已经明确要求后续地图必须允许扩展到多个防御点，
/// 所以这里先把“防御点”做成显式场景对象。
///
/// 这一轮又给它补了一层更明确的目标表现，
/// 让玩家在战斗中更容易看出：
/// - 真正要守的是哪里
/// - 最终交战压力会汇聚到哪里
/// </summary>
[ExecuteAlways]
public sealed class DefensePointFlag : MonoBehaviour
{
    private const string ReadabilityRootName = "__DefensePointReadability";

    [Header("Identity")]
    [SerializeField] private string pointId = "Core";
    [SerializeField] private string displayName = "Defense Point";

    [Header("Readability Visual")]
    [SerializeField] private bool showReadabilityMarker = true;
    [SerializeField] private bool autoCreateReadabilityRoot = true;
    [SerializeField] private Transform readabilityRootReference;
    [SerializeField] private Material readabilityMaterialOverride;
    [SerializeField] private Color coreColor = new Color(0.2f, 0.95f, 1f, 0.98f);
    [SerializeField] private Color defenseZoneColor = new Color(0.72f, 1f, 0.98f, 0.92f);
    [SerializeField] private float coreRingRadius = 0.42f;
    [SerializeField] private float defenseZoneRadius = 1.2f;
    [SerializeField] private float frameHalfSize = 0.56f;
    [SerializeField] private float frameCornerLength = 0.22f;
    [SerializeField] private int readabilitySortingOrder = 6;

    [Header("Scene Gizmo")]
    [SerializeField] private Color gizmoColor = new Color(0.15f, 0.9f, 1f, 1f);
    [SerializeField] private float gizmoRadius = 0.35f;

    private int _lastReadabilityHash;

    public string PointId => string.IsNullOrWhiteSpace(pointId) ? name : pointId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? PointId : displayName;
    public Vector3 WorldPosition => transform.position;

    /// <summary>
    /// 给编辑器作者工作流一个显式刷新入口。
    ///
    /// 当你刚手动补完目标表现根、换了材质或调了半径参数时，
    /// 可以直接通过这一步让防御点标记重新同步。
    /// </summary>
    public void EditorRefreshAuthoringState()
    {
        RefreshReadabilityVisuals(force: true);
    }

    private void Awake()
    {
        RefreshReadabilityVisuals(force: true);
    }

    private void OnEnable()
    {
        RefreshReadabilityVisuals(force: true);
    }

    private void OnValidate()
    {
        if (readabilityRootReference == null)
        {
            Transform existingRoot = transform.Find(ReadabilityRootName);
            if (existingRoot != null)
            {
                readabilityRootReference = existingRoot;
            }
        }

        RefreshReadabilityVisuals(force: true);
    }

    private void OnDrawGizmos()
    {
        RefreshReadabilityVisuals(force: false);

        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, gizmoRadius);
        Gizmos.DrawWireSphere(transform.position, gizmoRadius * 1.6f);
    }

    /// <summary>
    /// 只在必要时刷新目标标记。
    /// </summary>
    private void RefreshReadabilityVisuals(bool force)
    {
        if (!showReadabilityMarker)
        {
            Transform readabilityRoot = ResolveReadabilityRoot(allowCreate: false);
            if (readabilityRoot != null && readabilityRoot.gameObject.activeSelf)
            {
                readabilityRoot.gameObject.SetActive(false);
            }

            _lastReadabilityHash = 0;
            return;
        }

        int readabilityHash = ComputeReadabilityHash();
        if (!force && readabilityHash == _lastReadabilityHash)
        {
            return;
        }

        _lastReadabilityHash = readabilityHash;
        RebuildReadabilityVisuals();
    }

    /// <summary>
    /// 给防御点补一套更清楚的目标表现。
    ///
    /// 这里故意做成“两层圈 + 一个角框”的形式：
    /// - 小圈强调核心位置
    /// - 大圈强调最终防守压力区
    /// - 角框强调这是整张图最重要的目标点
    ///
    /// 这样比单纯一个 Gizmo 点更适合在 Game 视图里给玩家稳定提示。
    /// </summary>
    private void RebuildReadabilityVisuals()
    {
        Transform readabilityRoot = ResolveReadabilityRoot(allowCreate: autoCreateReadabilityRoot);
        if (readabilityRoot == null)
        {
            return;
        }

        readabilityRoot.gameObject.SetActive(true);

        LineRenderer defenseZone = BattlefieldReadabilityVisualUtility.EnsureLineRenderer(
            readabilityRoot,
            "DefenseZoneRing",
            readabilitySortingOrder,
            0.1f,
            defenseZoneColor,
            loop: true,
            sharedMaterialOverride: readabilityMaterialOverride);
        BattlefieldReadabilityVisualUtility.SetCircle(
            defenseZone,
            defenseZoneRadius,
            28,
            0.1f,
            defenseZoneColor);

        LineRenderer coreRing = BattlefieldReadabilityVisualUtility.EnsureLineRenderer(
            readabilityRoot,
            "CoreRing",
            readabilitySortingOrder + 1,
            0.11f,
            coreColor,
            loop: true,
            sharedMaterialOverride: readabilityMaterialOverride);
        BattlefieldReadabilityVisualUtility.SetCircle(
            coreRing,
            coreRingRadius,
            24,
            0.11f,
            coreColor);

        LineRenderer cornerFrame = BattlefieldReadabilityVisualUtility.EnsureLineRenderer(
            readabilityRoot,
            "CornerFrame",
            readabilitySortingOrder + 2,
            0.09f,
            defenseZoneColor,
            loop: false,
            sharedMaterialOverride: readabilityMaterialOverride);
        BattlefieldReadabilityVisualUtility.SetCornerFrame(
            cornerFrame,
            frameHalfSize,
            frameCornerLength,
            0.09f,
            defenseZoneColor);

        LineRenderer coreDiamond = BattlefieldReadabilityVisualUtility.EnsureLineRenderer(
            readabilityRoot,
            "CoreDiamond",
            readabilitySortingOrder + 3,
            0.08f,
            coreColor,
            loop: true,
            sharedMaterialOverride: readabilityMaterialOverride);
        BattlefieldReadabilityVisualUtility.SetDiamond(
            coreDiamond,
            coreRingRadius * 0.72f,
            0.08f,
            coreColor);
    }

    private int ComputeReadabilityHash()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + showReadabilityMarker.GetHashCode();
            hash = hash * 31 + autoCreateReadabilityRoot.GetHashCode();
            hash = hash * 31 + coreColor.GetHashCode();
            hash = hash * 31 + defenseZoneColor.GetHashCode();
            hash = hash * 31 + coreRingRadius.GetHashCode();
            hash = hash * 31 + defenseZoneRadius.GetHashCode();
            hash = hash * 31 + frameHalfSize.GetHashCode();
            hash = hash * 31 + frameCornerLength.GetHashCode();
            hash = hash * 31 + readabilitySortingOrder;
            hash = hash * 31 + (readabilityRootReference != null ? readabilityRootReference.GetInstanceID() : 0);
            hash = hash * 31 + (readabilityMaterialOverride != null ? readabilityMaterialOverride.GetInstanceID() : 0);
            return hash;
        }
    }

    private Transform ResolveReadabilityRoot(bool allowCreate)
    {
        if (readabilityRootReference != null)
        {
            return readabilityRootReference;
        }

        Transform existingRoot = transform.Find(ReadabilityRootName);
        if (existingRoot != null)
        {
            readabilityRootReference = existingRoot;
            return existingRoot;
        }

        if (!allowCreate)
        {
            return null;
        }

        readabilityRootReference = BattlefieldReadabilityVisualUtility.EnsureChild(transform, ReadabilityRootName);
        return readabilityRootReference;
    }
}

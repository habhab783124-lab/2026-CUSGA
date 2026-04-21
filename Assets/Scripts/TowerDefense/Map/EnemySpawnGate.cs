using UnityEngine;

/// <summary>
/// `EnemySpawnGate` 表示地图上的一个出怪口旗帜。
///
/// 新玩法要求“地图中需要有数量大于一的出怪口通向特定的防御点”，
/// 所以这里不再把刷怪入口只当成 `WaveSpawner` 里的一条路径引用，
/// 而是把它提升成一个独立的场景对象。
///
/// 当前这个脚本承担两层职责：
/// 1. 地图结构层：标记这里是一个出怪口，并显式关联路径与防御点。
/// 2. 场景表现层：补一个更容易读的出怪口标记，让玩家更快看出“敌人会从哪里压进来”。
/// </summary>
[ExecuteAlways]
public sealed class EnemySpawnGate : MonoBehaviour
{
    private const string ReadabilityRootName = "__SpawnGateReadability";

    [Header("Identity")]
    [SerializeField] private string gateId = "Gate01";
    [SerializeField] private string displayName = "Spawn Gate";

    [Header("Route")]
    [SerializeField] private EnemyPath enemyPathReference;
    [SerializeField] private DefensePointFlag targetDefensePointReference;

    [Header("Readability Visual")]
    [SerializeField] private bool showReadabilityMarker = true;
    [SerializeField] private bool autoCreateReadabilityRoot = true;
    [SerializeField] private Transform readabilityRootReference;
    [SerializeField] private Material readabilityMaterialOverride;
    [SerializeField] private Color readabilityColor = new Color(1f, 0.46f, 0.18f, 0.96f);
    [SerializeField] private Color secondaryReadabilityColor = new Color(1f, 0.9f, 0.72f, 0.92f);
    [SerializeField] private float outerRingRadius = 0.58f;
    [SerializeField] private float innerRingRadius = 0.34f;
    [SerializeField] private float leadLength = 0.82f;
    [SerializeField] private float chevronSize = 0.34f;
    [SerializeField] private int readabilitySortingOrder = 5;

    [Header("Scene Gizmo")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0.4f, 0.22f, 1f);
    [SerializeField] private float gizmoRadius = 0.28f;

    private int _lastReadabilityHash;

    public string GateId => string.IsNullOrWhiteSpace(gateId) ? name : gateId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? GateId : displayName;
    public bool IsConfigured => enemyPathReference != null;
    public EnemyPath EnemyPath => enemyPathReference;
    public DefensePointFlag TargetDefensePoint => targetDefensePointReference;

    /// <summary>
    /// 返回敌人的出生位置。
    /// 优先使用路径首点；如果暂时没配路径，就回退到出怪口自身坐标，方便场景调试。
    /// </summary>
    public Vector3 GetSpawnPosition()
    {
        return enemyPathReference != null ? enemyPathReference.GetSpawnPosition() : transform.position;
    }

    /// <summary>
    /// 给编辑器作者工作流一个显式刷新入口。
    ///
    /// 当你刚手动补完可读性根、改了材质或调整了路径引用时，
    /// 可以通过这一步立刻让出怪口标记重新同步。
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

        if (targetDefensePointReference != null)
        {
            Gizmos.DrawLine(transform.position, targetDefensePointReference.WorldPosition);
        }
    }

    /// <summary>
    /// 只在必要时刷新出怪口标记。
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
    /// 给出怪口补一套更容易读的占位表现。
    ///
    /// 这套表现的意图很明确：
    /// - 圆环告诉玩家“这里是一个入口”
    /// - 前导线和箭头告诉玩家“敌人会朝这个方向出来”
    ///
    /// 这样即使后续把正式旗帜、建筑和贴图全部替换掉，
    /// 当前样例关卡也仍然保留一层稳定可读的原型表达。
    /// </summary>
    private void RebuildReadabilityVisuals()
    {
        Transform readabilityRoot = ResolveReadabilityRoot(allowCreate: autoCreateReadabilityRoot);
        if (readabilityRoot == null)
        {
            return;
        }

        readabilityRoot.gameObject.SetActive(true);

        LineRenderer outerRing = BattlefieldReadabilityVisualUtility.EnsureLineRenderer(
            readabilityRoot,
            "OuterRing",
            readabilitySortingOrder,
            0.11f,
            readabilityColor,
            loop: true,
            sharedMaterialOverride: readabilityMaterialOverride);
        BattlefieldReadabilityVisualUtility.SetCircle(outerRing, outerRingRadius, 24, 0.11f, readabilityColor);

        LineRenderer innerRing = BattlefieldReadabilityVisualUtility.EnsureLineRenderer(
            readabilityRoot,
            "InnerRing",
            readabilitySortingOrder + 1,
            0.08f,
            secondaryReadabilityColor,
            loop: true,
            sharedMaterialOverride: readabilityMaterialOverride);
        BattlefieldReadabilityVisualUtility.SetCircle(innerRing, innerRingRadius, 20, 0.08f, secondaryReadabilityColor);

        Vector3 forwardDirection = ResolveForwardDirection();
        float forwardAngle = Mathf.Atan2(forwardDirection.y, forwardDirection.x) * Mathf.Rad2Deg;

        LineRenderer leadLine = BattlefieldReadabilityVisualUtility.EnsureLineRenderer(
            readabilityRoot,
            "LeadLine",
            readabilitySortingOrder,
            0.09f,
            readabilityColor,
            loop: false,
            sharedMaterialOverride: readabilityMaterialOverride);
        Vector3[] leadPoints =
        {
            Vector3.zero,
            forwardDirection * leadLength
        };
        BattlefieldReadabilityVisualUtility.SetPolyline(leadLine, leadPoints, false, 0.09f, readabilityColor);

        Transform chevronNear = BattlefieldReadabilityVisualUtility.EnsureChild(readabilityRoot, "ChevronNear");
        chevronNear.localPosition = forwardDirection * (leadLength * 0.42f);
        chevronNear.localRotation = Quaternion.Euler(0f, 0f, forwardAngle);
        chevronNear.localScale = Vector3.one;

        LineRenderer chevronNearRenderer = BattlefieldReadabilityVisualUtility.EnsureLineRenderer(
            chevronNear,
            "Shape",
            readabilitySortingOrder + 1,
            0.08f,
            secondaryReadabilityColor,
            loop: false,
            sharedMaterialOverride: readabilityMaterialOverride);
        BattlefieldReadabilityVisualUtility.SetArrow(chevronNearRenderer, chevronSize, 0.08f, secondaryReadabilityColor);

        Transform chevronFar = BattlefieldReadabilityVisualUtility.EnsureChild(readabilityRoot, "ChevronFar");
        chevronFar.localPosition = forwardDirection * (leadLength * 0.76f);
        chevronFar.localRotation = Quaternion.Euler(0f, 0f, forwardAngle);
        chevronFar.localScale = Vector3.one;

        LineRenderer chevronFarRenderer = BattlefieldReadabilityVisualUtility.EnsureLineRenderer(
            chevronFar,
            "Shape",
            readabilitySortingOrder + 1,
            0.08f,
            readabilityColor,
            loop: false,
            sharedMaterialOverride: readabilityMaterialOverride);
        BattlefieldReadabilityVisualUtility.SetArrow(chevronFarRenderer, chevronSize * 0.92f, 0.08f, readabilityColor);
    }

    /// <summary>
    /// 优先读取路径的初始朝向，让出怪口提示跟真实刷怪方向一致。
    /// 如果路径暂时没接好，就回退成朝右的安全方向。
    /// </summary>
    private Vector3 ResolveForwardDirection()
    {
        if (enemyPathReference != null && enemyPathReference.TryGetInitialDirection(out Vector3 pathDirection))
        {
            return pathDirection;
        }

        return Vector3.right;
    }

    private int ComputeReadabilityHash()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + showReadabilityMarker.GetHashCode();
            hash = hash * 31 + autoCreateReadabilityRoot.GetHashCode();
            hash = hash * 31 + readabilityColor.GetHashCode();
            hash = hash * 31 + secondaryReadabilityColor.GetHashCode();
            hash = hash * 31 + outerRingRadius.GetHashCode();
            hash = hash * 31 + innerRingRadius.GetHashCode();
            hash = hash * 31 + leadLength.GetHashCode();
            hash = hash * 31 + chevronSize.GetHashCode();
            hash = hash * 31 + readabilitySortingOrder;
            hash = hash * 31 + (enemyPathReference != null ? enemyPathReference.GetInstanceID() : 0);
            hash = hash * 31 + (targetDefensePointReference != null ? targetDefensePointReference.GetInstanceID() : 0);
            hash = hash * 31 + (readabilityRootReference != null ? readabilityRootReference.GetInstanceID() : 0);
            hash = hash * 31 + (readabilityMaterialOverride != null ? readabilityMaterialOverride.GetInstanceID() : 0);

            Vector3 forwardDirection = ResolveForwardDirection();
            hash = hash * 31 + forwardDirection.GetHashCode();
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

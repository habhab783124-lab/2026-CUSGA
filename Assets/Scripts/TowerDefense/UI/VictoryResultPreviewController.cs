using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// `VictoryResultPreviewController` keeps the preview scene aligned with the real runtime page.
///
/// The old preview flow generated a second UI tree in code, which made the preview scene drift away
/// from the formal tower-defense victory prefab over time. This controller now plays a much smaller
/// role:
/// - load / keep exactly one `VictoryResultPage` prefab instance in the preview scene
/// - feed that real page with fake preview data
/// - remove legacy preview-only canvas roots so the scene stays single-source
///
/// In other words, the preview scene is now a thin shell around the real prefab instead of a second
/// implementation that just happens to look similar.
/// </summary>
[ExecuteAlways]
public sealed class VictoryResultPreviewController : MonoBehaviour
{
    private const string PreviewPrefabAssetPath = "Assets/Resources/TowerDefense/UI/VictoryResultPage.prefab";
    private const string PreviewPrefabResourcePath = "TowerDefense/UI/VictoryResultPage";

    [Header("Preview Copy")]
    [SerializeField] private VictoryResultPageView.ResultPageTone previewTone = VictoryResultPageView.ResultPageTone.Victory;
    [SerializeField] private string signalTitle = "指挥链路接通";
    [SerializeField] private string signalStatus = "战区信号稳定";
    [SerializeField] private string signalChannel = "HOLO-LINK / FRONT 02";
    [SerializeField] private string titleText = "防线稳定";
    [SerializeField] private string subtitleText = "战术全息简报已生成";
    [SerializeField] private string reportHeader = "战区汇报";
    [SerializeField] private string integrityRow = "基地完整度：78%";
    [SerializeField] private string scrapRow = "回收废料：146";
    [SerializeField] private string eventRow = "关键记录：第二出怪口压力已解除";
    [SerializeField] private string footerHint = "等待接收后续指令";
    [SerializeField] private string commanderName = "指挥官";
    [SerializeField] private string commanderCodename = "前线总控 / C-07";
    [SerializeField, TextArea(2, 4)] private string dialogueText =
        "做得不错，这一波我们守住了。\n前线已经稳定，准备接收下一阶段指令。";
    [SerializeField] private string continueButtonText = "继续汇报";
    [SerializeField] private string continueHintText = "同步完成后可继续推进剧情";

    [Header("Preview Behavior")]
    [SerializeField] private bool removeLegacyPreviewCanvas = true;
    [SerializeField] private bool refreshOnValidate = true;

    private const string LegacyPreviewRootName = "VictoryPreviewCanvas";
    private const string FormalPreviewRootName = "VictoryResultPage";

    private VictoryResultPageView _previewPageView;

    private void OnEnable()
    {
        RebuildPreviewIfNeeded();
    }

    private void OnValidate()
    {
        if (refreshOnValidate)
        {
            RebuildPreviewIfNeeded();
        }
    }

    private void RebuildPreviewIfNeeded()
    {
        CleanupLegacyPreviewCanvas();
        _previewPageView = EnsurePreviewPageView();
        if (_previewPageView == null)
        {
            return;
        }

        _previewPageView.BindContinueAction(null);
        _previewPageView.Show(BuildPreviewContent());

        // The preview scene is used for layout review, so the page should stay visible after the
        // fake content is applied instead of relying on gameplay flow to re-open it.
        if (!_previewPageView.gameObject.activeSelf)
        {
            _previewPageView.gameObject.SetActive(true);
        }
    }

    private void CleanupLegacyPreviewCanvas()
    {
        if (!removeLegacyPreviewCanvas)
        {
            return;
        }

        GameObject legacyCanvas = GameObject.Find("VictoryPreviewCanvas");
        if (legacyCanvas == null)
        {
            return;
        }

        if (legacyCanvas.GetComponent<VictoryResultPageView>() != null)
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.DestroyObjectImmediate(legacyCanvas);
            return;
        }
#endif

        Destroy(legacyCanvas);
    }

    private VictoryResultPageView EnsurePreviewPageView()
    {
        VictoryResultPageView[] existingViews = FindObjectsByType<VictoryResultPageView>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        VictoryResultPageView primaryView = null;
        for (int index = 0; index < existingViews.Length; index++)
        {
            VictoryResultPageView candidate = existingViews[index];
            if (candidate == null)
            {
                continue;
            }

            if (primaryView == null)
            {
                primaryView = candidate;
                continue;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.DestroyObjectImmediate(candidate.gameObject);
            }
            else
#endif
            {
                Destroy(candidate.gameObject);
            }
        }

        if (primaryView != null)
        {
            primaryView.gameObject.name = FormalPreviewRootName;
            primaryView.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            primaryView.transform.localScale = Vector3.one;
            return primaryView;
        }

        GameObject prefabRoot = LoadPreviewPrefabRoot();
        if (prefabRoot == null)
        {
            Debug.LogWarning(
                $"VictoryResultPreviewController could not load preview prefab at '{PreviewPrefabAssetPath}'.",
                this);
            return null;
        }

        GameObject previewInstance = InstantiatePreviewPrefab(prefabRoot);
        if (previewInstance == null)
        {
            return null;
        }

        previewInstance.name = prefabRoot.name;
        previewInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        previewInstance.transform.localScale = Vector3.one;

        VictoryResultPageView previewView = previewInstance.GetComponent<VictoryResultPageView>();
        if (previewView == null)
        {
            previewView = previewInstance.AddComponent<VictoryResultPageView>();
        }

        return previewView;
    }

    private static GameObject LoadPreviewPrefabRoot()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(PreviewPrefabAssetPath);
        }
#endif

        return Resources.Load<GameObject>(PreviewPrefabResourcePath);
    }

    private static GameObject InstantiatePreviewPrefab(GameObject prefabRoot)
    {
        if (prefabRoot == null)
        {
            return null;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Object instance = PrefabUtility.InstantiatePrefab(prefabRoot);
            return instance as GameObject;
        }
#endif

        return Instantiate(prefabRoot);
    }

    private static bool IsLegacyPreviewRoot(GameObject candidate)
    {
        return candidate != null && string.Equals(candidate.name, LegacyPreviewRootName, System.StringComparison.Ordinal);
    }

    private VictoryResultPageContent BuildPreviewContent()
    {
        return new VictoryResultPageContent(
            tone: previewTone,
            signalTitle: signalTitle,
            signalStatus: signalStatus,
            signalChannel: signalChannel,
            title: titleText,
            subtitle: subtitleText,
            reportHeader: reportHeader,
            integrityRow: integrityRow,
            scrapRow: scrapRow,
            eventRow: eventRow,
            footerHint: footerHint,
            commanderName: commanderName,
            commanderCodename: commanderCodename,
            dialogueText: dialogueText,
            continueButtonText: continueButtonText,
            continueHintText: continueHintText);
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 2D 触发器：玩家进入后加载指定场景。挂在带 Collider2D（Is Trigger）的物体上。
/// 第二个场景需在 File → Build Settings 里加入列表。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public sealed class StoryScenePortal2D : MonoBehaviour
{
    [SerializeField] private string targetSceneName;
    [SerializeField] private string playerTag = "Player";

    private void Reset()
    {
        var c = GetComponent<Collider2D>();
        if (c != null)
        {
            c.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning($"{nameof(StoryScenePortal2D)}: 未设置 Target Scene Name。", this);
            return;
        }

        if (!other.CompareTag(playerTag))
        {
            return;
        }

        SceneManager.LoadScene(targetSceneName);
    }
}

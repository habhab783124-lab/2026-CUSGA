using System.Collections;
using UnityEngine;

/// <summary>
/// 对话结束回调里，<see cref="DialogueRunner.OnDisable"/> 可能先于回调触发并禁用 StorySystems，
/// 此时不能在已禁用的 MonoBehaviour 上 StartCoroutine。本工具把协程挂到仍激活的宿主上。
/// </summary>
public static class CutsceneCoroutineUtility
{
    public static void StartPreferredOrFallback(MonoBehaviour preferred, MonoBehaviour fallback, IEnumerator routine)
    {
        if (preferred != null && preferred.gameObject.activeInHierarchy)
        {
            preferred.StartCoroutine(routine);
            return;
        }

        if (fallback != null && fallback.gameObject.activeInHierarchy)
        {
            fallback.StartCoroutine(routine);
            return;
        }

        var go = new GameObject("CutsceneCoroutineHost");
        var host = go.AddComponent<CutsceneCoroutineHost>();
        host.StartCoroutine(host.RunThenDestroy(routine));
    }
}

internal sealed class CutsceneCoroutineHost : MonoBehaviour
{
    public IEnumerator RunThenDestroy(IEnumerator inner)
    {
        yield return StartCoroutine(inner);
        Destroy(gameObject);
    }
}

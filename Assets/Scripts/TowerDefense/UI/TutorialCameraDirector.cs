using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TutorialCameraDirector : MonoBehaviour
{
    [SerializeField] private float moveDuration = 0.6f;
    [SerializeField] private float focusOrthoSize = 7f;
    [SerializeField] [Range(0f, 1f)] private float focusTravel = 0.55f;
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Camera _camera;
    private Vector3 _originPosition;
    private float _originOrthoSize;
    private Coroutine _routine;

    private void Awake()
    {
        _camera = Camera.main;
        if (_camera != null)
        {
            _originPosition = _camera.transform.position;
            _originOrthoSize = _camera.orthographicSize;
        }
    }

    public void FocusOn(Vector3 worldTarget, Action onArrived = null)
    {
        if (_camera == null)
        {
            onArrived?.Invoke();
            return;
        }

        Vector3 fullTarget = new Vector3(worldTarget.x, worldTarget.y, _originPosition.z);
        Vector3 target = Vector3.Lerp(_originPosition, fullTarget, focusTravel);
        StopCurrent();
        _routine = StartCoroutine(MoveRoutine(_camera.transform.position, target, _camera.orthographicSize, focusOrthoSize, onArrived));
    }

    public void ReturnToOrigin(Action onComplete = null)
    {
        if (_camera == null)
        {
            onComplete?.Invoke();
            return;
        }

        StopCurrent();
        _routine = StartCoroutine(MoveRoutine(_camera.transform.position, _originPosition, _camera.orthographicSize, _originOrthoSize, onComplete));
    }

    private System.Collections.IEnumerator MoveRoutine(Vector3 fromPos, Vector3 toPos, float fromSize, float toSize, Action onComplete)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, moveDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curved = moveCurve != null ? moveCurve.Evaluate(t) : t;

            _camera.transform.position = Vector3.LerpUnclamped(fromPos, toPos, curved);
            _camera.orthographicSize = Mathf.LerpUnclamped(fromSize, toSize, curved);
            yield return null;
        }

        _camera.transform.position = toPos;
        _camera.orthographicSize = toSize;
        _routine = null;
        onComplete?.Invoke();
    }

    private void StopCurrent()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    private void OnDestroy()
    {
        StopCurrent();
        if (_camera != null)
        {
            _camera.transform.position = _originPosition;
            _camera.orthographicSize = _originOrthoSize;
        }
    }
}

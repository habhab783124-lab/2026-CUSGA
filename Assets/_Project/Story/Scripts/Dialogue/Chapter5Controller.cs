using System.Collections;
using UnityEngine;

public sealed class Chapter5Controller : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private Chapter5 chapter5;

    [Header("Bus")]
    [SerializeField] private string busObjectName = "Bus";
    [SerializeField] private float busMoveSpeed = 3f;
    [SerializeField] private float busExtraLeftBeyondCamera = 4f;
    [SerializeField] private float busArriveEpsilon = 0.02f;

    private Transform bus;
    private Vector3 busTargetPosition;
    private bool hasCachedBusTargetPosition;
    private bool busEntranceStarted;
    private Coroutine busEntranceRoutine;
    private bool wasDialoguePreviouslyActive;

    private void Awake()
    {
        if (chapter5 == null)
        {
            chapter5 = GetComponent<Chapter5>();
        }

        ResolveBusReference();
        CacheBusTargetPosition();
        PlaceBusOffscreenLeft();
    }

    private void Update()
    {
        if (chapter5 == null)
        {
            return;
        }

        bool dialogueActiveNow = IsChapter5CharacterDialogueActive();
        if (dialogueActiveNow)
        {
            wasDialoguePreviouslyActive = true;
            return;
        }

        if (!wasDialoguePreviouslyActive || busEntranceStarted)
        {
            return;
        }

        StartBusEntrance();
    }

    private bool IsChapter5CharacterDialogueActive()
    {
        System.Reflection.FieldInfo field = typeof(Chapter5).GetField("characterDialogueActive", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (field == null)
        {
            return false;
        }

        object value = field.GetValue(chapter5);
        return value is bool active && active;
    }

    private void ResolveBusReference()
    {
        if (bus == null)
        {
            GameObject busObject = GameObject.Find(busObjectName);
            if (busObject != null)
            {
                bus = busObject.transform;
            }
        }
    }

    private void CacheBusTargetPosition()
    {
        if (bus != null)
        {
            busTargetPosition = bus.position;
            hasCachedBusTargetPosition = true;
        }
    }

    private void PlaceBusOffscreenLeft()
    {
        if (bus == null || !hasCachedBusTargetPosition)
        {
            return;
        }

        bus.position = new Vector3(-20f, busTargetPosition.y, busTargetPosition.z);
    }

    private void StartBusEntrance()
    {
        ResolveBusReference();
        if (bus == null || !hasCachedBusTargetPosition)
        {
            return;
        }

        busEntranceStarted = true;
        if (busEntranceRoutine != null)
        {
            StopCoroutine(busEntranceRoutine);
        }

        busEntranceRoutine = StartCoroutine(BusEntranceRoutine());
    }

    private IEnumerator BusEntranceRoutine()
    {
        while (bus != null && Vector3.Distance(bus.position, busTargetPosition) > busArriveEpsilon)
        {
            bus.position = Vector3.MoveTowards(bus.position, busTargetPosition, busMoveSpeed * Time.deltaTime);
            yield return null;
        }

        if (bus != null)
        {
            bus.position = busTargetPosition;
        }

        busEntranceRoutine = null;
    }
}

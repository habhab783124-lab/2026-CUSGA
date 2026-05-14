using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TruthRevealedSceneController : MonoBehaviour
{
    [SerializeField] private TruthRevealed truthRevealed;
    [SerializeField] private string nextSceneName = "chapter10";
    [SerializeField] private int mouseButton = 0;
    [SerializeField] private float fadeOutToBlackDuration = 0.75f;
    [SerializeField] private float fadeInFromBlackDuration = 0.75f;

    private bool transitionQueued;

    private void Reset()
    {
        truthRevealed = GetComponent<TruthRevealed>();
    }

    private void Awake()
    {
        if (truthRevealed == null)
        {
            truthRevealed = GetComponent<TruthRevealed>();
        }
    }

    private void Update()
    {
        if (transitionQueued || truthRevealed == null)
        {
            return;
        }

        if (!truthRevealed.HasReachedConversationEnd || truthRevealed.IsTyping)
        {
            return;
        }

        if (!Input.GetMouseButtonDown(mouseButton))
        {
            return;
        }

        transitionQueued = true;
        ScreenFadeTransition.Play(nextSceneName, fadeOutToBlackDuration, fadeInFromBlackDuration);
    }
}

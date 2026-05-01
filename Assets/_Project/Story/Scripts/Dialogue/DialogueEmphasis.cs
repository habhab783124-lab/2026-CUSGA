using System;
using UnityEngine;

[Serializable]
public struct DialogueEmphasis
{
    [Tooltip("Whether this line should play emphasis effects (scale/shake).")]
    public bool enabled;

    [Header("Scale")]
    [Tooltip("Temporary scale multiplier applied to the bubble while typing.")]
    [Range(1f, 2.5f)]
    public float scaleMultiplier;

    [Header("Shake")]
    [Tooltip("World-space shake magnitude while typing.")]
    [Range(0f, 0.5f)]
    public float shakeMagnitude;
}


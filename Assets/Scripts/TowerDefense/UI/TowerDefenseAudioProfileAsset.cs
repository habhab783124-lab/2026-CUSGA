using UnityEngine;

/// <summary>
/// `TowerDefenseAudioProfileAsset` 把塔防关卡共用的背景音乐和核心音效收口成一份共享配置。
///
/// 这样后续如果你想：
/// - 换四关共用 BGM
/// - 替换放置音效
/// - 调整三种塔的命中音效
/// 就不需要再到代码里改硬编码路径，也不需要逐关卡手工拖五次引用。
/// </summary>
[CreateAssetMenu(
    fileName = "TowerDefenseAudioProfile",
    menuName = "Tower Defense/Audio/Audio Profile")]
public sealed class TowerDefenseAudioProfileAsset : ScriptableObject
{
    [Header("Background Music")]
    [SerializeField] private AudioClip backgroundMusic;

    [Header("Placement")]
    [SerializeField] private AudioClip placeStructureClip;

    [Header("Tower Effects")]
    [SerializeField] private AudioClip singleTargetImpactClip;
    [SerializeField] private AudioClip slowFieldImpactClip;
    [SerializeField] private AudioClip bombardImpactClip;

    public AudioClip BackgroundMusic => backgroundMusic;
    public AudioClip PlaceStructureClip => placeStructureClip;
    public AudioClip SingleTargetImpactClip => singleTargetImpactClip;
    public AudioClip SlowFieldImpactClip => slowFieldImpactClip;
    public AudioClip BombardImpactClip => bombardImpactClip;
}

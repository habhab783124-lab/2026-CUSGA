using UnityEngine;

/// <summary>
/// `TowerDefenseAudioCoordinator` 收口塔防关卡运行时的基础音频播放。
///
/// 当前阶段我们故意保持它很轻：
/// - 一个 BGM AudioSource 负责关卡背景循环
/// - 一个 SFX AudioSource 负责短促音效 one-shot
///
/// 这样以后如果你想继续加：
/// - 音量滑条
/// - 开关静音
/// - 混音器分组
/// - 不同关卡 BGM
/// 都有一个稳定入口可以继续扩展，而不是把播放调用散落在很多脚本里。
/// </summary>
public sealed class TowerDefenseAudioCoordinator
{
    private readonly AudioSource _bgmSource;
    private readonly AudioSource _sfxSource;

    private readonly AudioClip _backgroundMusic;
    private readonly AudioClip _placeStructureClip;
    private readonly AudioClip _singleTargetImpactClip;
    private readonly AudioClip _slowFieldImpactClip;
    private readonly AudioClip _bombardImpactClip;

    public TowerDefenseAudioCoordinator(
        AudioSource bgmSource,
        AudioSource sfxSource,
        AudioClip backgroundMusic,
        AudioClip placeStructureClip,
        AudioClip singleTargetImpactClip,
        AudioClip slowFieldImpactClip,
        AudioClip bombardImpactClip)
    {
        _bgmSource = bgmSource;
        _sfxSource = sfxSource;
        _backgroundMusic = backgroundMusic;
        _placeStructureClip = placeStructureClip;
        _singleTargetImpactClip = singleTargetImpactClip;
        _slowFieldImpactClip = slowFieldImpactClip;
        _bombardImpactClip = bombardImpactClip;
    }

    /// <summary>
    /// Starts looping the tower-defense background music if it is not already playing.
    /// </summary>
    public void StartBackgroundMusic()
    {
        if (_bgmSource == null || _backgroundMusic == null)
        {
            return;
        }

        if (_bgmSource.clip == _backgroundMusic && _bgmSource.isPlaying)
        {
            return;
        }

        _bgmSource.clip = _backgroundMusic;
        _bgmSource.loop = true;
        _bgmSource.playOnAwake = false;
        _bgmSource.spatialBlend = 0f;
        _bgmSource.Play();
    }

    public void PlayPlaceStructure()
    {
        PlayOneShot(_placeStructureClip);
    }

    public void PlaySingleTargetImpact()
    {
        PlayOneShot(_singleTargetImpactClip);
    }

    public void PlaySlowFieldImpact()
    {
        PlayOneShot(_slowFieldImpactClip);
    }

    public void PlayBombardImpact()
    {
        PlayOneShot(_bombardImpactClip);
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (_sfxSource == null || clip == null)
        {
            return;
        }

        _sfxSource.playOnAwake = false;
        _sfxSource.spatialBlend = 0f;
        _sfxSource.PlayOneShot(clip);
    }
}

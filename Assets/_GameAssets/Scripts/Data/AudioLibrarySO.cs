using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ses kategorileri
/// </summary>
public enum AudioCategory
{
    SFX,
    Footstep,
    Impact,
    Ability,
    Collectable,
    UI,
    Voice,
    Ambience
}

/// <summary>
/// Tekil ses grubu kaydı
/// </summary>
[System.Serializable]
public class AudioEntry
{
    public string key;
    public AudioCategory category = AudioCategory.SFX;
    public AudioClip[] clips;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.1f, 3f)] public float minPitch = 0.9f;
    [Range(0.1f, 3f)] public float maxPitch = 1.1f;
}

/// <summary>
/// CivCiv 3D - Kategorize Ses Kütüphanesi (AudioLibrarySO)
/// Adım, yetenek, darbe, arayüz ve ortam seslerini merkezi olarak depolar ve yönetir.
/// </summary>
[CreateAssetMenu(fileName = "AudioLibrary", menuName = "CivCiv/AudioLibrary")]
public class AudioLibrarySO : ScriptableObject
{
    public const string DefaultFolder = "Assets/_GameAssets/Data/Audio";

    [Header("--- Primary Categories (Öncelikli Kategoriler) ---")]
    [Tooltip("Karakter adım sesleri")]
    [SerializeField] private AudioClip[] _footstepClips;

    [Tooltip("Dash ve hızlanma sesleri")]
    [SerializeField] private AudioClip[] _dashClips;

    [Tooltip("Hasar ve darbe sesleri")]
    [SerializeField] private AudioClip[] _damageClips;

    [Tooltip("Zıplama sesleri")]
    [SerializeField] private AudioClip[] _jumpClips;

    [Tooltip("Yere iniş (land) sesleri")]
    [SerializeField] private AudioClip[] _landClips;

    [Header("--- Custom Audio Groups (Özel Ses Grupları) ---")]
    [SerializeField] private List<AudioEntry> _entries = new List<AudioEntry>();

    public AudioClip[] FootstepClips { get => _footstepClips; set => _footstepClips = value; }
    public AudioClip[] DashClips { get => _dashClips; set => _dashClips = value; }
    public AudioClip[] DamageClips { get => _damageClips; set => _damageClips = value; }
    public AudioClip[] JumpClips { get => _jumpClips; set => _jumpClips = value; }
    public AudioClip[] LandClips { get => _landClips; set => _landClips = value; }
    public List<AudioEntry> Entries => _entries;

    public AudioClip GetRandomFootstep() => GetRandomClip(_footstepClips);
    public AudioClip GetRandomDash() => GetRandomClip(_dashClips);
    public AudioClip GetRandomDamage() => GetRandomClip(_damageClips);
    public AudioClip GetRandomJump() => GetRandomClip(_jumpClips);
    public AudioClip GetRandomLand() => GetRandomClip(_landClips);

    public AudioClip GetRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;
        int index = UnityEngine.Random.Range(0, clips.Length);
        return clips[index];
    }

    public AudioEntry GetEntry(string key)
    {
        if (string.IsNullOrEmpty(key) || _entries == null) return null;
        for (int i = 0; i < _entries.Count; i++)
        {
            if (string.Equals(_entries[i].key, key, StringComparison.OrdinalIgnoreCase))
            {
                return _entries[i];
            }
        }
        return null;
    }

    public AudioClip GetRandomClip(string key)
    {
        AudioEntry entry = GetEntry(key);
        if (entry != null && entry.clips != null && entry.clips.Length > 0)
        {
            return GetRandomClip(entry.clips);
        }
        return null;
    }

    public void AddEntry(AudioEntry entry)
    {
        if (entry == null) return;
        if (_entries == null) _entries = new List<AudioEntry>();
        _entries.Add(entry);
    }
}

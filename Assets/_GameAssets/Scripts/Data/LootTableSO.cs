using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CivCiv 3D - Ağırlıklı Ganimet Tablosu (ScriptableObject)
/// Kırılabilir sandıklar, düşmanlar veya ödül noktaları için ağırlıklı rastgele seçim yapar.
/// </summary>
[CreateAssetMenu(fileName = "DefaultLootTable", menuName = "CivCiv/Loot Table")]
public class LootTableSO : ScriptableObject
{
    public const string DefaultAssetPath = "Assets/_GameAssets/Data/DefaultLootTable.asset";

    [Serializable]
    public class LootEntry
    {
        [Tooltip("Düşecek nesne prefabı (CoinCollect, PowerUpCollectable vs.)")]
        public GameObject prefab;

        [Tooltip("Ağırlıklı rastgele seçim olasılığı (weight >= 0)")]
        [Min(0f)] public float weight = 1f;

        [Tooltip("Düşecek minimum adet")]
        [Min(1)] public int minCount = 1;

        [Tooltip("Düşecek maksimum adet")]
        [Min(1)] public int maxCount = 1;

        [Tooltip("Prefab yoksa veya ek olarak verilecek doğrudan altın miktarı")]
        public int directCoinAmount = 0;

        public LootEntry() { }

        public LootEntry(GameObject prefab, float weight, int minCount = 1, int maxCount = 1, int directCoinAmount = 0)
        {
            this.prefab = prefab;
            this.weight = Mathf.Max(0f, weight);
            this.minCount = Mathf.Max(1, minCount);
            this.maxCount = Mathf.Max(this.minCount, maxCount);
            this.directCoinAmount = directCoinAmount;
        }

        public static implicit operator GameObject(LootEntry entry) => entry?.prefab;

        public int RollCount()
        {
            int min = Mathf.Max(1, minCount);
            int max = Mathf.Max(min, maxCount);
            return UnityEngine.Random.Range(min, max + 1);
        }
    }

    [Header("--- Loot Entries ---")]
    [SerializeField] private List<LootEntry> _entries = new List<LootEntry>();

    [Tooltip("Hiçbir şey düşmeme olasılığı (0 ise her zaman geçerli bir girdi seçilir)")]
    [SerializeField] [Range(0f, 1f)] private float _emptyRollChance = 0f;

    public List<LootEntry> Entries => _entries;
    public float EmptyRollChance { get => _emptyRollChance; set => _emptyRollChance = Mathf.Clamp01(value); }

    public LootEntry RollLoot()
    {
        if (_entries == null || _entries.Count == 0) return null;

        if (_emptyRollChance > 0f && UnityEngine.Random.value < _emptyRollChance)
        {
            return null;
        }

        float totalWeight = 0f;
        for (int i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            if (entry != null && entry.weight > 0f)
            {
                totalWeight += entry.weight;
            }
        }

        if (totalWeight <= 0f) return null;

        float randomVal = UnityEngine.Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        for (int i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            if (entry == null || entry.weight <= 0f) continue;

            currentWeight += entry.weight;
            if (randomVal <= currentWeight)
            {
                return entry;
            }
        }

        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i] != null && _entries[i].weight > 0f)
                return _entries[i];
        }

        return null;
    }

    public GameObject RollPrefab() => RollLoot()?.prefab;

    public void AddEntry(LootEntry entry)
    {
        if (entry == null) return;
        if (_entries == null) _entries = new List<LootEntry>();
        _entries.Add(entry);
    }

    public void ClearEntries()
    {
        _entries?.Clear();
    }
}

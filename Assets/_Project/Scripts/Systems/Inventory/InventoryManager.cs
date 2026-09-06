using UnityEngine;
using System.Collections.Generic;

namespace BattleBall.Systems.Inventory
{
    /// <summary>
    /// 背包/物品管理器 - 迁移占位
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        // ===== 基础方法 =====
        public virtual List<Dictionary<string, object>> GetItems() { return new List<Dictionary<string, object>>(); }
        public virtual Dictionary<string, int> GetItemCounts() { return new Dictionary<string, int>(); }
        public virtual void AddItem(string itemId, int count) { }
        public virtual void RemoveItem(string itemId, int count) { }
        public virtual Dictionary<string, object> GetItem(string itemId) { return null; }
        public virtual bool EquipItem(string itemId, string slot) { return true; }
        public virtual void UnequipSlot(string slot) { }

        // ===== Godot snake_case 兼容 (NutritionManager 调用) =====
        public virtual int get_item_count(string itemId) { return 0; }
        public virtual bool remove_item(string itemId, int count) { RemoveItem(itemId, count); return true; }

        // ===== UI (PreparationUI) 兼容方法 =====
        public virtual Dictionary<string, object> GetItemDef(string itemId) { return new Dictionary<string, object>(); }
        public virtual Dictionary<string, object> GetItemCountsAsObject() {
            var r = new Dictionary<string, object>();
            foreach (var kv in GetItemCounts()) r[kv.Key] = kv.Value;
            return r;
        }
        public virtual Color GetRarityColor(string rarity) { return Color.white; }
        public virtual List<Dictionary<string, object>> GetBackpackBySlot(string slot) { return new List<Dictionary<string, object>>(); }
        public virtual List<Dictionary<string, object>> GetBackpackByType(string type) { return new List<Dictionary<string, object>>(); }
        public virtual bool EquipToCharacter(string itemId, int playerIdx, string slot) { return true; }
        public virtual bool UnequipFromCharacter(int playerIdx, string slot) { return true; }
// ===== UI 兼容：装备分类 + string 角色 ID 重载 (PreparationUI/BaseSystem 传字符串charId) =====
        public virtual List<Dictionary<string, object>> GetBackpackEquipment() { return new List<Dictionary<string, object>>(); }
        public virtual List<Dictionary<string, object>> GetBackpackConsumables() { return new List<Dictionary<string, object>>(); }
        public virtual bool EquipToCharacter(string charId, string slot, string itemId) { return true; }
        public virtual bool UnequipFromCharacter(string charId, string slot) { return true; }

        // ===== 单例生命周期 =====
        protected virtual void Awake() {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        protected virtual void OnDestroy() { if (Instance == this) Instance = null; }
    }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance == null)
            {
                var go = new GameObject("InventoryManager");
                go.AddComponent<InventoryManager>();
            }
        }
}
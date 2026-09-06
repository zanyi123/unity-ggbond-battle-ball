using UnityEngine;
using System.Collections.Generic;
using System;

namespace BattleBall.Systems
{
    /// <summary>
    /// 玩家存档管理器 - 迁移占位
    /// 负责存档槽位、玩家数据持久化、货币/装备/训练数据读写
    /// </summary>
    public class PlayerSaveManager : MonoBehaviour
    {
        public static PlayerSaveManager Instance { get; private set; }

        /// <summary>UI 兼容: 各稀有度的最大耐久度(字典)</summary>
        public static readonly Dictionary<string, object> RARITY_MAX_DURABILITY = new Dictionary<string, object>
        {
            { "common", 50 }, { "uncommon", 75 }, { "rare", 100 }, { "epic", 150 }, { "legendary", 200 }
        };

        // ===== 槽位管理 =====
        public int CurrentSlot { get; set; } = 0;
        public List<string> SlotNames { get; set; } = new List<string> { "玩家1", "", "", "" };

        public virtual void LoadSlot(int slot) {
            CurrentSlot = slot;
            Debug.Log("[PlayerSaveMgr] 加载存档槽: " + slot);
        }

        // ===== 货币系统 =====
        public virtual Dictionary<string, float> GetCurrency() {
            return new Dictionary<string, float>
            {
                { "gold", 0f },
                { "spirit_ore", 0f },
                { "spirit_crystal", 0f }
            };
        }
        public virtual float GetCurrency(string type) { return 0f; }
        public virtual void AddCurrency(string type, float amount) { }

        // ===== Godot snake_case 兼容方法 (RewardSystem / NutritionManager 调用) =====
        public virtual void add_currency(string type, float amount) { AddCurrency(type, amount); }
        public virtual void save_slot(int slot = -1) { SaveData(); }
        public virtual void set_active_food(string food_id) { Debug.Log("[PlayerSaveMgr] set_active_food: food=" + food_id); }
        public virtual void set_active_food(int player_idx, string food_id) { set_active_food(food_id); }
        public virtual Dictionary<string, object> get_data() { return new Dictionary<string, object>(); }

        // ===== 装备系统 =====
        public virtual Dictionary<string, Dictionary<string, object>> GetAllEquipped() {
            return new Dictionary<string, Dictionary<string, object>>();
        }

        // ===== UI (PreparationUI) 兼容方法 =====
        public virtual Dictionary<string, object> GetCharacterTrain(string charId) { return new Dictionary<string, object>(); }
        public virtual Dictionary<string, object> GetEquippedItem(int playerIdx, string slot) { return new Dictionary<string, object>(); }
        public virtual Dictionary<string, object> GetEquippedDurability(int playerIdx, string slot) { return new Dictionary<string, object>(); }
        public virtual Dictionary<string, object> GetCurrencyAsObject() {
            var r = new Dictionary<string, object>();
            foreach (var kv in GetCurrency()) r[kv.Key] = kv.Value;
            return r;
        }
        public virtual Dictionary<string, object> GetEquipmentBonuses(int playerIdx) { return new Dictionary<string, object>(); }

        // ===== UI 兼容: string charId 版本 (PreparationUI 传角色字符串ID不是int) =====
        public virtual Dictionary<string, object> GetEquippedItem(string charId, string slot) { return new Dictionary<string, object>(); }
        public virtual Dictionary<string, object> GetEquippedDurability(string charId, string slot) { return new Dictionary<string, object>(); }
        public virtual Dictionary<string, object> GetEquipmentBonuses(string charId) { return new Dictionary<string, object>(); }

        // ===== 持久化 =====
        public virtual void SaveData() { Debug.Log("[PlayerSaveMgr] SaveData 调用"); }

        /// <summary>UI兼容: 取全部存档数据(供BaseSystem调用避免void错误)</summary>
        public virtual Dictionary<string, object> GetData() {
            var d = new Dictionary<string, object>();
            d["unlocked_characters"] = new List<object>();
            return d;
        }
        public virtual void LoadData() { Debug.Log("[PlayerSaveMgr] LoadData 调用"); }

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
                var go = new GameObject("PlayerSaveManager");
                go.AddComponent<PlayerSaveManager>();
            }
        }
}
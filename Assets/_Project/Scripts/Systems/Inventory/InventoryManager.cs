using UnityEngine;
using System.Collections.Generic;
using System;
using BattleBall.Core;

namespace BattleBall.Systems.Inventory
{
    /// <summary>
    /// 背包/装备管理器 - Autoload 单例
    /// 道具静态数据 + 玩家背包增删改查 + 装备穿脱
    /// 背包数据存在 PlayerSaveManager.saveData["inventory"]
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        public const int MAX_SLOTS = 50;

        protected Dictionary<string, Dictionary<string, object>> _itemDefs = new Dictionary<string, Dictionary<string, object>>();
        protected List<Dictionary<string, object>> _itemList = new List<Dictionary<string, object>>();

        public event Action inventory_changed;
        public event Action<string, int> item_added;
        public event Action<string, int> item_removed;

        public static InventoryManager Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        protected virtual void Start()
        {
            _load_item_definitions();
            _ensure_inventory_structure();
        }

        protected virtual void OnDestroy() { if (Instance == this) Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance == null)
            {
                var go = new GameObject("InventoryManager");
                go.AddComponent<InventoryManager>();
            }
        }

        protected virtual void _load_item_definitions()
        {
            _itemDefs.Clear();
            _itemList.Clear();
            var dm = DataManager.Instance;
            if (dm == null) { Debug.LogWarning("[InventoryManager] DataManager 不存在"); return; }
            var items = dm.GetAllItems();
            if (items == null || items.Count == 0) { Debug.LogWarning("[InventoryManager] 道具数据为空"); return; }
            foreach (var item in items)
            {
                string id = GetStr(item, "id", "");
                if (id == "") continue;
                _itemDefs[id] = item;
                _itemList.Add(item);
            }
            Debug.Log("[InventoryManager] 加载道具定义: " + _itemList.Count + " 个");
        }

        public virtual void reload_item_defs() { _load_item_definitions(); }

        protected virtual void _ensure_inventory_structure()
        {
            var psm = PlayerSaveManager.Instance;
            if (psm == null) return;
            var save = psm.saveData;
            if (save == null) return;
            if (!save.ContainsKey("inventory"))
            {
                save["inventory"] = new Dictionary<string, object>
                {
                    { "items", new List<object>() },
                    { "max_slots", MAX_SLOTS }
                };
                return;
            }
            var inv = save["inventory"] as Dictionary<string, object>;
            if (inv == null)
            {
                save["inventory"] = new Dictionary<string, object>
                {
                    { "items", new List<object>() },
                    { "max_slots", MAX_SLOTS }
                };
                return;
            }
            if (!inv.ContainsKey("items")) inv["items"] = new List<object>();
            if (!inv.ContainsKey("max_slots")) inv["max_slots"] = MAX_SLOTS;
        }

        protected virtual List<object> _get_backpack_list()
        {
            _ensure_inventory_structure();
            var psm = PlayerSaveManager.Instance;
            if (psm == null) return new List<object>();
            var inv = psm.saveData["inventory"] as Dictionary<string, object>;
            if (inv == null) return new List<object>();
            return inv["items"] as List<object> ?? new List<object>();
        }

        public virtual Dictionary<string, object> GetItemDef(string itemId)
        {
            if (_itemDefs.ContainsKey(itemId)) return _itemDefs[itemId];
            return new Dictionary<string, object>();
        }
        public virtual Dictionary<string, object> get_item_def(string itemId) => GetItemDef(itemId);

        public virtual List<Dictionary<string, object>> GetAllItems() => new List<Dictionary<string, object>>(_itemList);
        public virtual List<Dictionary<string, object>> get_all_items() => GetAllItems();

        public virtual List<Dictionary<string, object>> GetItemsByType(string itemType)
        {
            var result = new List<Dictionary<string, object>>();
            foreach (var item in _itemList)
                if (GetStr(item, "type", "") == itemType) result.Add(item);
            return result;
        }
        public virtual List<Dictionary<string, object>> get_items_by_type(string t) => GetItemsByType(t);

        public virtual List<Dictionary<string, object>> GetItemsBySubType(string subType)
        {
            var result = new List<Dictionary<string, object>>();
            foreach (var item in _itemList)
                if (GetStr(item, "sub_type", "") == subType) result.Add(item);
            return result;
        }

        public virtual List<Dictionary<string, object>> GetItemsByRarity(string rarity)
        {
            var result = new List<Dictionary<string, object>>();
            foreach (var item in _itemList)
                if (GetStr(item, "rarity", "") == rarity) result.Add(item);
            return result;
        }

        public virtual List<object> GetBackpackItems() => _get_backpack_list();
        public virtual List<object> get_backpack_items() => GetBackpackItems();

        public virtual int GetItemCount(string itemId)
        {
            var items = _get_backpack_list();
            int total = 0;
            foreach (var entry in items)
            {
                var d = entry as Dictionary<string, object>;
                if (d == null) continue;
                if (GetStr(d, "item_id", "") == itemId) total += GetInt(d, "count", 0);
            }
            return total;
        }
        public virtual int get_item_count(string itemId) => GetItemCount(itemId);

        public virtual bool HasItem(string itemId, int count = 1) => GetItemCount(itemId) >= count;
        public virtual bool has_item(string itemId, int count = 1) => HasItem(itemId, count);

        public virtual int GetMaxSlots()
        {
            _ensure_inventory_structure();
            var psm = PlayerSaveManager.Instance;
            if (psm == null) return MAX_SLOTS;
            var inv = psm.saveData["inventory"] as Dictionary<string, object>;
            if (inv == null) return MAX_SLOTS;
            return GetInt(inv, "max_slots", MAX_SLOTS);
        }

        public virtual int GetUsedSlots() => _get_backpack_list().Count;
        public virtual bool IsFull() => GetUsedSlots() >= GetMaxSlots();

        public virtual bool AddItem(string itemId, int count = 1, float initialDurability = -1f)
        {
            if (count <= 0) return false;
            var def = GetItemDef(itemId);
            if (def.Count == 0) { Debug.LogError("[InventoryManager] 未知道具ID: " + itemId); return false; }

            _ensure_inventory_structure();
            var psm = PlayerSaveManager.Instance;
            var inv = psm.saveData["inventory"] as Dictionary<string, object>;
            var items = inv["items"] as List<object>;

            bool isEquip = GetStr(def, "type", "") == "equipment";
            int stackMax = GetInt(def, "stack_max", 1);
            if (isEquip) stackMax = 1;
            int remaining = count;

            float defaultDur = 0f;
            if (isEquip) defaultDur = GetFloat(def, "max_durability", 50f);
            if (initialDurability >= 0) defaultDur = initialDurability;

            if (stackMax > 1 && !isEquip)
            {
                foreach (var entry in items)
                {
                    var d = entry as Dictionary<string, object>;
                    if (d == null) continue;
                    if (GetStr(d, "item_id", "") == itemId)
                    {
                        int current = GetInt(d, "count", 0);
                        int canAdd = stackMax - current;
                        if (canAdd > 0)
                        {
                            int add = Math.Min(canAdd, remaining);
                            d["count"] = current + add;
                            remaining -= add;
                            if (remaining <= 0) break;
                        }
                    }
                }
            }

            while (remaining > 0)
            {
                if (items.Count >= GetMaxSlots())
                {
                    if (remaining < count)
                    {
                        psm.save_slot();
                        inventory_changed?.Invoke();
                        item_added?.Invoke(itemId, count - remaining);
                    }
                    Debug.LogWarning("[InventoryManager] 背包已满");
                    return false;
                }
                int add = remaining;
                if (stackMax > 0) add = Math.Min(remaining, stackMax);
                var newEntry = new Dictionary<string, object>
                {
                    { "item_id", itemId },
                    { "count", add }
                };
                if (isEquip) newEntry["durability"] = defaultDur;
                items.Add(newEntry);
                remaining -= add;
            }

            psm.save_slot();
            inventory_changed?.Invoke();
            item_added?.Invoke(itemId, count);
            return true;
        }
        public virtual bool add_item(string itemId, int count = 1, float initialDurability = -1f) => AddItem(itemId, count, initialDurability);

        public virtual bool RemoveItem(string itemId, int count = 1)
        {
            if (count <= 0) return false;
            if (!HasItem(itemId, count)) return false;

            _ensure_inventory_structure();
            var psm = PlayerSaveManager.Instance;
            var inv = psm.saveData["inventory"] as Dictionary<string, object>;
            var items = inv["items"] as List<object>;
            int remaining = count;

            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (remaining <= 0) break;
                var d = items[i] as Dictionary<string, object>;
                if (d == null) continue;
                if (GetStr(d, "item_id", "") == itemId)
                {
                    int entryCount = GetInt(d, "count", 0);
                    int remove = Math.Min(entryCount, remaining);
                    d["count"] = entryCount - remove;
                    remaining -= remove;
                    if (GetInt(d, "count", 0) <= 0) items.RemoveAt(i);
                }
            }

            psm.save_slot();
            inventory_changed?.Invoke();
            item_removed?.Invoke(itemId, count);
            return true;
        }
        public virtual bool remove_item(string itemId, int count = 1) => RemoveItem(itemId, count);

        public virtual List<object> GetBackpackByType(string itemType)
        {
            var result = new List<object>();
            var items = GetBackpackItems();
            foreach (var entry in items)
            {
                var d = entry as Dictionary<string, object>;
                if (d == null) continue;
                var def = GetItemDef(GetStr(d, "item_id", ""));
                if (GetStr(def, "type", "") == itemType) result.Add(entry);
            }
            return result;
        }
        public virtual List<object> get_backpack_by_type(string t) => GetBackpackByType(t);

        public virtual List<Dictionary<string, object>> GetBackpackEquipment() { var r = new List<Dictionary<string, object>>(); foreach (var e in GetBackpackByType("equipment")) { var d = e as Dictionary<string, object>; if (d != null) r.Add(d); } return r; }
        public virtual List<Dictionary<string, object>> get_backpack_equipment() => GetBackpackEquipment();

        public virtual List<Dictionary<string, object>> GetBackpackConsumables() { var r = new List<Dictionary<string, object>>(); foreach (var e in GetBackpackByType("consumable")) { var d = e as Dictionary<string, object>; if (d != null) r.Add(d); } return r; }
        public virtual List<Dictionary<string, object>> get_backpack_consumables() => GetBackpackConsumables();

        public virtual List<Dictionary<string, object>> GetBackpackBySlot(string slot)
        {
            var result = new List<Dictionary<string, object>>();
            var items = GetBackpackItems();
            foreach (var entry in items)
            {
                var d = entry as Dictionary<string, object>;
                if (d == null) continue;
                string iid = GetStr(d, "item_id", "");
                var def = GetItemDef(iid);
                if (def.Count == 0) continue;
                if (GetStr(def, "type", "") != "equipment") continue;
                if (GetStr(def, "sub_type", "") != slot) continue;
                result.Add(new Dictionary<string, object>
                {
                    { "item_id", iid },
                    { "count", GetInt(d, "count", 0) },
                    { "def", def }
                });
            }
            return result;
        }
        public virtual List<Dictionary<string, object>> get_backpack_by_slot(string slot) => GetBackpackBySlot(slot);

        protected virtual float _remove_one_equipment(string itemId)
        {
            _ensure_inventory_structure();
            var psm = PlayerSaveManager.Instance;
            var inv = psm.saveData["inventory"] as Dictionary<string, object>;
            var items = inv["items"] as List<object>;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var d = items[i] as Dictionary<string, object>;
                if (d == null) continue;
                if (GetStr(d, "item_id", "") == itemId)
                {
                    float dur = GetFloat(d, "durability", 0f);
                    int count = GetInt(d, "count", 0);
                    if (count <= 1) items.RemoveAt(i);
                    else d["count"] = count - 1;
                    return dur;
                }
            }
            return -1f;
        }

        public virtual bool EquipToCharacter(string charId, string slot, string itemId)
        {
            var def = GetItemDef(itemId);
            if (def.Count == 0) return false;
            if (GetStr(def, "sub_type", "") != slot) return false;
            if (!HasItem(itemId, 1)) return false;

            float dur = _remove_one_equipment(itemId);
            if (dur < 0) return false;

            var psm = PlayerSaveManager.Instance;
            string oldItemId = psm.get_equipped_item(charId, slot);
            float oldDur = 0f;
            if (oldItemId != "") oldDur = psm.get_equipped_durability(charId, slot);

            psm.equip_item(charId, slot, itemId, dur);
            if (oldItemId != "") AddItem(oldItemId, 1, oldDur);

            psm.save_slot();
            inventory_changed?.Invoke();
            Debug.Log("[Inventory] " + charId + " 穿戴 " + slot + " " + itemId + " 耐久:" + dur.ToString("F1"));
            return true;
        }
        public virtual bool equip_to_character(string charId, string slot, string itemId) => EquipToCharacter(charId, slot, itemId);

        public virtual bool UnequipFromCharacter(string charId, string slot)
        {
            var psm = PlayerSaveManager.Instance;
            string itemId = psm.get_equipped_item(charId, slot);
            if (itemId == "") return false;
            float dur = psm.get_equipped_durability(charId, slot);
            psm.unequip_item(charId, slot);
            AddItem(itemId, 1, dur);
            inventory_changed?.Invoke();
            Debug.Log("[Inventory] " + charId + " 卸下 " + slot + " " + itemId);
            return true;
        }
        public virtual bool unequip_from_character(string charId, string slot) => UnequipFromCharacter(charId, slot);

        public virtual Dictionary<string, object> GetEquipmentBonuses(string charId)
        {
            var psm = PlayerSaveManager.Instance;
            if (psm == null) return new Dictionary<string, object>();
            return psm.get_equipment_bonuses(charId);
        }
        public virtual Dictionary<string, object> get_equipment_bonuses(string charId) => GetEquipmentBonuses(charId);

        public virtual Color GetRarityColor(string rarity)
        {
            switch (rarity)
            {
                case "common": return new Color(0.85f, 0.85f, 0.85f);
                case "good": return new Color(0.3f, 0.9f, 0.3f);
                case "rare": return new Color(0.3f, 0.5f, 1.0f);
                case "epic": return new Color(0.8f, 0.3f, 1.0f);
                case "legendary": return new Color(1.0f, 0.85f, 0.2f);
                default: return Color.white;
            }
        }
        public virtual Color get_rarity_color(string rarity) => GetRarityColor(rarity);

        public virtual string GetRarityName(string rarity)
        {
            switch (rarity)
            {
                case "common": return "普通";
                case "good": return "良好";
                case "rare": return "稀有";
                case "epic": return "史诗";
                case "legendary": return "传说";
                default: return "未知";
            }
        }
        public virtual string get_rarity_name(string rarity) => GetRarityName(rarity);

        public virtual List<string> GetAllRarities() => new List<string> { "common", "good", "rare", "epic", "legendary" };

        public virtual void ClearAll()
        {
            _ensure_inventory_structure();
            var psm = PlayerSaveManager.Instance;
            var inv = psm.saveData["inventory"] as Dictionary<string, object>;
            inv["items"] = new List<object>();
            psm.save_slot();
            inventory_changed?.Invoke();
        }
        public virtual void clear_all() => ClearAll();

        public virtual Dictionary<string, object> GetItemCounts()
        {
            var result = new Dictionary<string, object>();
            foreach (var entry in GetBackpackItems())
            {
                var d = entry as Dictionary<string, object>;
                if (d == null) continue;
                string id = GetStr(d, "item_id", "");
                if (!result.ContainsKey(id)) result[id] = 0; result[id] = (int)result[id] + GetInt(d, "count", 0);
            }
            return result;
        }
        public virtual Dictionary<string, object> GetItemCountsAsObject()
        {
            var r = new Dictionary<string, object>();
            foreach (var kv in GetItemCounts()) r[kv.Key] = kv.Value;
            return r;
        }
        public virtual Dictionary<string, object> GetItems()
        {
            var r = new Dictionary<string, object>();
            r["items"] = GetBackpackItems();
            return r;
        }

        protected static string GetStr(Dictionary<string, object> d, string key, string def)
        {
            if (d == null) return def;
            object v;
            return d.TryGetValue(key, out v) && v != null ? v.ToString() : def;
        }

        protected static int GetInt(Dictionary<string, object> d, string key, int def)
        {
            if (d == null) return def;
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return def;
            if (v is int) return (int)v;
            if (v is long) return (int)(long)v;
            if (v is double) return (int)(double)v;
            if (v is float) return (int)(float)v;
            int r;
            return int.TryParse(v.ToString(), out r) ? r : def;
        }

        protected static float GetFloat(Dictionary<string, object> d, string key, float def)
        {
            if (d == null) return def;
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return def;
            if (v is float) return (float)v;
            if (v is double) return (float)(double)v;
            if (v is int) return (float)(int)v;
            if (v is long) return (float)(long)v;
            float r;
            return float.TryParse(v.ToString(), out r) ? r : def;
        }
    }
}
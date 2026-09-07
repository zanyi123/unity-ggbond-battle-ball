using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BattleBall.Core
{
    /// <summary>
    /// 玩家存档管理器 - 本地持久化存储
    /// 挂载为单例，全局访问
    /// 支持3个存档位、版本迁移、坏档备份、自动保存
    /// </summary>
    public class PlayerSaveManager : MonoBehaviour
    {
        public const int SAVE_VERSION = 2;

        // 装备耐久消耗常量
        public const float DURABILITY_LOSS_PER_HIT = 0.1f;
        public const float DURABILITY_LOSS_PER_CATCH = 0.1f;

        // 稀有度对应的耐久衰减阈值（低于此值属性减半）
        public static readonly Dictionary<string, int> RARITY_DURABILITY_THRESHOLD = new Dictionary<string, int>
        {
            {"common", 30}, {"good", 40}, {"rare", 50}, {"epic", 60}, {"legendary", 75}
        };

        // 稀有度对应的最大耐久
        public static readonly Dictionary<string, int> RARITY_MAX_DURABILITY = new Dictionary<string, int>
        {
            {"common", 50}, {"good", 80}, {"rare", 100}, {"epic", 120}, {"legendary", 150}
        };

        public const string SAVE_DIR_NAME = "saves";
        public const string SAVE_FILE_PREFIX = "save_";
        public const string SAVE_FILE_SUFFIX = ".json";

        public static readonly string[] EQUIP_SLOTS = { "glove", "jersey", "shoes" };

        public int currentSlot = 1;
        public Dictionary<string, object> saveData = new Dictionary<string, object>();

        public event Action<int> save_loaded;
        public event Action<int> save_saved;
        public event Action currency_changed;

        public static PlayerSaveManager Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        protected virtual void OnDestroy() { if (Instance == this) Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance == null)
            {
                var go = new GameObject("PlayerSaveManager");
                go.AddComponent<PlayerSaveManager>();
            }
        }

        protected virtual void Start()
        {
            _ensure_save_dir();
            load_slot(currentSlot);
        }

        // ==================== 内部工具 ====================

        private static string _save_dir()
        {
            return Path.Combine(Application.persistentDataPath, SAVE_DIR_NAME);
        }

        private void _ensure_save_dir()
        {
            try
            {
                var dir = _save_dir();
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            }
            catch (Exception e)
            {
                Debug.LogError("[PlayerSaveManager] 创建存档目录失败: " + e.Message);
            }
        }

        private string _get_save_path(int slot)
        {
            return Path.Combine(_save_dir(), SAVE_FILE_PREFIX + slot + SAVE_FILE_SUFFIX);
        }

        private string _get_backup_path(int slot)
        {
            return Path.Combine(_save_dir(), SAVE_FILE_PREFIX + slot + "_corrupted_backup.json");
        }

        // 字典取值助手（带默认值）
        private static object _get(Dictionary<string, object> d, string key, object def)
        {
            if (d == null) return def;
            object v;
            return d.TryGetValue(key, out v) ? v : def;
        }

        private static Dictionary<string, object> _get_dict(Dictionary<string, object> d, string key)
        {
            var v = _get(d, key, null);
            if (v is Dictionary<string, object> dd) return dd;
            if (v is JObject jo) return jo.ToObject<Dictionary<string, object>>();
            return new Dictionary<string, object>();
        }

        private static List<object> _get_list(Dictionary<string, object> d, string key)
        {
            var v = _get(d, key, null);
            if (v is List<object> li) return li;
            if (v is JArray ja) return ja.ToObject<List<object>>();
            return new List<object>();
        }

        private static int _int(object v) { if (v == null) return 0; if (v is int i) return i; if (v is long l) return (int)l; int r; return int.TryParse(v.ToString(), out r) ? r : 0; }
        private static float _float(object v) { if (v == null) return 0f; if (v is float f) return f; if (v is double dd) return (float)dd; if (v is int i) return (float)i; if (v is long l) return (float)l; float r; return float.TryParse(v.ToString(), out r) ? r : 0f; }
        private static string _str(object v) { return v == null ? "" : v.ToString(); }

        // 反射访问 InventoryManager.Instance.get_item_def(item_id)
        // 避免 Core 程序集对 Systems 程序集的循环引用
        private static System.Type _inventoryMgrType;
        private static bool _inventoryMgrTypeResolved;
        private static Dictionary<string, object> _get_item_def(string item_id)
        {
            if (!_inventoryMgrTypeResolved)
            {
                _inventoryMgrTypeResolved = true;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("BattleBall.Systems.Inventory.InventoryManager");
                    if (t != null) { _inventoryMgrType = t; break; }
                }
            }
            if (_inventoryMgrType == null) return new Dictionary<string, object>();
            try
            {
                var instProp = _inventoryMgrType.GetProperty("Instance");
                var instance = instProp != null ? instProp.GetValue(null, null) : null;
                if (instance == null) return new Dictionary<string, object>();
                var method = _inventoryMgrType.GetMethod("get_item_def");
                if (method == null) return new Dictionary<string, object>();
                var result = method.Invoke(instance, new object[] { item_id });
                if (result is Dictionary<string, object> d) return d;
                return new Dictionary<string, object>();
            }
            catch (Exception)
            {
                return new Dictionary<string, object>();
            }
        }

        // ==================== 公共 API ====================

        public bool has_save(int slot)
        {
            return File.Exists(_get_save_path(slot));
        }

        public void load_slot(int slot)
        {
            currentSlot = slot;
            var path = _get_save_path(slot);

            if (!File.Exists(path))
            {
                saveData = _create_default_save();
                _save_to_file();
                if (save_loaded != null) save_loaded(slot);
                return;
            }

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Debug.LogError("[PlayerSaveManager] 无法打开存档: " + path + " err=" + e.Message);
                _backup_corrupted(slot, "");
                saveData = _create_default_save();
                _save_to_file();
                if (save_loaded != null) save_loaded(slot);
                return;
            }

            Dictionary<string, object> data;
            try
            {
                data = JsonConvert.DeserializeObject<Dictionary<string, object>>(text);
                if (data == null) data = new Dictionary<string, object>();
            }
            catch (Exception)
            {
                Debug.LogError("[PlayerSaveManager] 存档" + slot + "解析失败，备份后重置");
                _backup_corrupted(slot, text);
                saveData = _create_default_save();
                _save_to_file();
                if (save_loaded != null) save_loaded(slot);
                return;
            }

            int version = _int(_get(data, "version", 0));
            if (version < SAVE_VERSION)
            {
                data = _migrate_save(data, version);
            }

            saveData = data;
            if (save_loaded != null) save_loaded(slot);
            Debug.Log("[PlayerSaveManager] 存档" + slot + "加载完成，版本" + _int(_get(saveData, "version", 0)));
        }

        public void save_slot()
        {
            _save_to_file();
            if (save_saved != null) save_saved(currentSlot);
        }

        private void _save_to_file()
        {
            var path = _get_save_path(currentSlot);
            try
            {
                saveData["last_save_time"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var json = JsonConvert.SerializeObject(saveData, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch (Exception e)
            {
                Debug.LogError("[PlayerSaveManager] 无法写入存档: " + path + " err=" + e.Message);
            }
        }

        private void _backup_corrupted(int slot, string text)
        {
            try
            {
                var backup_path = _get_backup_path(slot);
                File.WriteAllText(backup_path, text);
                Debug.Log("[PlayerSaveManager] 坏档已备份到: " + backup_path);
            }
            catch (Exception e)
            {
                Debug.LogError("[PlayerSaveManager] 备份坏档失败: " + e.Message);
            }
        }

        // ==================== 默认存档 ====================

        private Dictionary<string, object> _create_default_save()
        {
            var character_trains = new Dictionary<string, object>();
            var dm = DataManager.Instance;
            if (dm != null)
            {
                foreach (var c in dm.characters)
                {
                    string char_id = _str(_get(c, "id", ""));
                    if (char_id == "") continue;
                    character_trains[char_id] = new Dictionary<string, object>
                    {
                        {"stamina_bonus", 0}, {"defense_bonus", 0}, {"speed_bonus", 0},
                        {"attack_bonus", 0}, {"resilience_bonus", 0}, {"ball_speed_bonus", 0}
                    };
                }
            }

            var unlocked = new List<object> { "char_004", "char_006", "char_007" };
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            return new Dictionary<string, object>
            {
                {"version", SAVE_VERSION},
                {"create_time", now},
                {"last_save_time", now},
                {"player_id", ""},
                {"player_name", "小虎队"},
                {"is_dev_mode", true},
                {"currencies", new Dictionary<string, object> { {"fairy_coin", 1000}, {"spirit_ore", 50}, {"crystal", 20} }},
                {"training", new Dictionary<string, object> { {"field_level", 1}, {"character_trains", character_trains} }},
                {"equipment", new Dictionary<string, object> { {"equipped", new Dictionary<string, object>()}, {"inventory", new List<object>()} }},
                {"inventory", new Dictionary<string, object> { {"items", new List<object>()}, {"max_slots", 50} }},
                {"nutrition", new Dictionary<string, object> { {"pre_match_food", new List<object>()} }},
                {"unlocked_characters", unlocked}
            };
        }

        // ==================== 迁移 ====================

        private Dictionary<string, object> _migrate_save(Dictionary<string, object> data, int from_version)
        {
            var result = new Dictionary<string, object>(data);

            if (from_version < 1)
            {
                if (!result.ContainsKey("player_id")) result["player_id"] = "";
                if (!result.ContainsKey("is_dev_mode")) result["is_dev_mode"] = true;
                if (!result.ContainsKey("nutrition")) result["nutrition"] = new Dictionary<string, object> { {"pre_match_food", new List<object>()} };
                if (!result.ContainsKey("equipment")) result["equipment"] = new Dictionary<string, object> { {"equipped", new Dictionary<string, object>()}, {"inventory", new List<object>()} };
            }

            if (from_version < 2)
            {
                var equipment = _get_dict(result, "equipment");
                var equipped = _get_dict(equipment, "equipped");
                var keys = new List<string>(equipped.Keys);
                foreach (var char_id in keys)
                {
                    var char_eq_o = equipped.TryGetValue(char_id, out var v) ? v : null;
                    Dictionary<string, object> char_eq;
                    if (char_eq_o is Dictionary<string, object> ce) char_eq = ce;
                    else if (char_eq_o is JObject jo) char_eq = jo.ToObject<Dictionary<string, object>>();
                    else char_eq = new Dictionary<string, object>();
                    foreach (var slot in EQUIP_SLOTS)
                    {
                        var val = char_eq.TryGetValue(slot, out var vv) ? vv : "";
                        if (val is string s && s != "")
                        {
                            var def = _get_item_def(s);
                            string rarity = _str(_get(def, "rarity", "common"));
                            int max_dur = RARITY_MAX_DURABILITY.TryGetValue(rarity, out var md) ? md : 50;
                            char_eq[slot] = new Dictionary<string, object> { {"item_id", s}, {"durability", (float)max_dur} };
                        }
                        else if (val is string ss && ss == "")
                        {
                            char_eq[slot] = new Dictionary<string, object> { {"item_id", ""}, {"durability", 0f} };
                        }
                    }
                    equipped[char_id] = char_eq;
                }
                equipment["equipped"] = equipped;
                result["equipment"] = equipment;
            }

            result["version"] = SAVE_VERSION;
            Debug.Log("[PlayerSaveManager] 存档迁移: v" + from_version + " -> v" + SAVE_VERSION);
            return result;
        }

        // ==================== 状态查询 ====================

        public bool is_dev_mode()
        {
            return Convert.ToBoolean(_get(saveData, "is_dev_mode", false));
        }

        public int get_currency(string currency_type)
        {
            var currencies = _get_dict(saveData, "currencies");
            return _int(_get(currencies, currency_type, 0));
        }

        public void set_currency(string currency_type, int amount)
        {
            var currencies = _get_dict(saveData, "currencies");
            currencies[currency_type] = Mathf.Max(0, amount);
            saveData["currencies"] = currencies;
            if (currency_changed != null) currency_changed();
            save_slot();
        }

        public bool add_currency(string currency_type, int amount)
        {
            if (amount <= 0) return false;
            var currencies = _get_dict(saveData, "currencies");
            int current = _int(_get(currencies, currency_type, 0));
            currencies[currency_type] = current + amount;
            saveData["currencies"] = currencies;
            if (currency_changed != null) currency_changed();
            save_slot();
            return true;
        }

        public bool spend_currency(string currency_type, int amount)
        {
            if (amount <= 0) return false;
            var currencies = _get_dict(saveData, "currencies");
            int current = _int(_get(currencies, currency_type, 0));
            if (current < amount) return false;
            currencies[currency_type] = current - amount;
            saveData["currencies"] = currencies;
            if (currency_changed != null) currency_changed();
            save_slot();
            return true;
        }

        public bool has_character(string char_id)
        {
            var unlocked = _get_list(saveData, "unlocked_characters");
            if (is_dev_mode()) return true;
            foreach (var id in unlocked) if (_str(id) == char_id) return true;
            return false;
        }

        public bool unlock_character(string char_id)
        {
            var unlocked = _get_list(saveData, "unlocked_characters");
            foreach (var id in unlocked) if (_str(id) == char_id) return false;
            unlocked.Add(char_id);
            saveData["unlocked_characters"] = unlocked;
            save_slot();
            return true;
        }

        public List<string> get_unlocked_characters()
        {
            var result = new List<string>();
            if (is_dev_mode())
            {
                var dm = DataManager.Instance;
                if (dm != null) foreach (var c in dm.characters) result.Add(_str(_get(c, "id", "")));
                return result;
            }
            foreach (var id in _get_list(saveData, "unlocked_characters")) result.Add(_str(id));
            return result;
        }

        public int get_field_level()
        {
            var training = _get_dict(saveData, "training");
            return _int(_get(training, "field_level", 1));
        }

        public void set_field_level(int level)
        {
            var training = _get_dict(saveData, "training");
            training["field_level"] = level;
            saveData["training"] = training;
            save_slot();
        }

        public Dictionary<string, object> get_character_train(string char_id)
        {
            var training = _get_dict(saveData, "training");
            var char_trains = _get_dict(training, "character_trains");
            if (char_trains.TryGetValue(char_id, out var v) && v is Dictionary<string, object> ct) return ct;

            var default_train = new Dictionary<string, object>
            {
                {"stamina_bonus", 0}, {"defense_bonus", 0}, {"speed_bonus", 0},
                {"attack_bonus", 0}, {"resilience_bonus", 0}, {"ball_speed_bonus", 0}
            };
            char_trains[char_id] = default_train;
            training["character_trains"] = char_trains;
            saveData["training"] = training;
            return default_train;
        }

        public void set_character_train(string char_id, Dictionary<string, object> train_data)
        {
            var training = _get_dict(saveData, "training");
            var char_trains = _get_dict(training, "character_trains");
            char_trains[char_id] = train_data;
            training["character_trains"] = char_trains;
            saveData["training"] = training;
            save_slot();
        }

        public void set_training_bonus(string char_id, string bonus_key, int value)
        {
            var train = get_character_train(char_id);
            train[bonus_key] = Mathf.Max(0, value);
            set_character_train(char_id, train);
        }

        public Dictionary<string, object> get_data()
        {
            return saveData;
        }

        // ==================== 营养系统 ====================

        public void set_active_food(string food_id)
        {
            if (!saveData.ContainsKey("nutrition"))
                saveData["nutrition"] = new Dictionary<string, object> { {"pre_match_food", ""} };
            var nutrition = _get_dict(saveData, "nutrition");
            nutrition["pre_match_food"] = food_id;
            saveData["nutrition"] = nutrition;
            _save_to_file();
        }

        public string get_active_food()
        {
            var nutrition = _get_dict(saveData, "nutrition");
            var food = _get(nutrition, "pre_match_food", "");
            if (food is List<object> arr)
            {
                if (arr.Count > 0) return _str(arr[0]);
                return "";
            }
            if (food is JArray ja)
            {
                if (ja.Count > 0) return _str(ja[0]);
                return "";
            }
            return _str(food);
        }

        public int get_total_stat(string char_id, string stat_key)
        {
            var dm = DataManager.Instance;
            if (dm == null) return 0;
            var char_data = dm.get_character_by_id(char_id);
            if (char_data == null || char_data.Count == 0) return 0;
            int base_v = _int(_get(char_data, stat_key, 0));
            var train = get_character_train(char_id);
            int bonus = _int(_get(train, stat_key + "_bonus", 0));
            return base_v + bonus;
        }

        // ==================== 装备穿戴系统 ====================

        public Dictionary<string, object> get_equipped(string char_id)
        {
            var equipment = _get_dict(saveData, "equipment");
            var equipped = _get_dict(equipment, "equipped");
            if (!equipped.ContainsKey(char_id))
            {
                var empty_eq = new Dictionary<string, object>();
                foreach (var s in EQUIP_SLOTS)
                    empty_eq[s] = new Dictionary<string, object> { {"item_id", ""}, {"durability", 0f} };
                equipped[char_id] = empty_eq;
                equipment["equipped"] = equipped;
                saveData["equipment"] = equipment;
            }
            var v = equipped.TryGetValue(char_id, out var vv) ? vv : null;
            if (vv is Dictionary<string, object> d) return d;
            if (vv is JObject jo) return jo.ToObject<Dictionary<string, object>>();
            return new Dictionary<string, object>();
        }

        public string get_equipped_item(string char_id, string slot)
        {
            var eq = get_equipped(char_id);
            var slot_data = eq.TryGetValue(slot, out var v) ? v : null;
            if (slot_data is string s) return s;
            if (slot_data is Dictionary<string, object> d) return _str(_get(d, "item_id", ""));
            if (slot_data is JObject jo) return _str(_get(jo.ToObject<Dictionary<string, object>>(), "item_id", ""));
            return "";
        }

        public float get_equipped_durability(string char_id, string slot)
        {
            var eq = get_equipped(char_id);
            var slot_data = eq.TryGetValue(slot, out var v) ? v : null;
            if (slot_data is Dictionary<string, object> d) return _float(_get(d, "durability", 0f));
            if (slot_data is JObject jo) return _float(_get(jo.ToObject<Dictionary<string, object>>(), "durability", 0f));
            return 0f;
        }

        public void set_equipped_durability(string char_id, string slot, float new_dur)
        {
            var equipment = _get_dict(saveData, "equipment");
            var equipped = _get_dict(equipment, "equipped");
            if (!equipped.ContainsKey(char_id)) return;
            var char_eq_o = equipped.TryGetValue(char_id, out var v) ? v : null;
            if (!(v is Dictionary<string, object> char_eq)) return;
            char_eq = (Dictionary<string, object>)v;
            var slot_data = char_eq.TryGetValue(slot, out var sd) ? sd : null;
            if (slot_data is Dictionary<string, object> sdd)
            {
                sdd["durability"] = Mathf.Clamp(new_dur, 0f, 9999f);
                char_eq[slot] = sdd;
                equipped[char_id] = char_eq;
                equipment["equipped"] = equipped;
                saveData["equipment"] = equipment;
                save_slot();
            }
        }

        public void repair_all_equipment_max(string char_id)
        {
            var eq = get_equipped(char_id);
            foreach (var slot in EQUIP_SLOTS)
            {
                var slot_data = eq.TryGetValue(slot, out var v) ? v : null;
                if (slot_data is Dictionary<string, object> sd)
                {
                    string item_id = _str(_get(sd, "item_id", ""));
                    if (item_id != "")
                    {
                        var def = _get_item_def(item_id);
                        string rarity = _str(_get(def, "rarity", "common"));
                        int max_dur = RARITY_MAX_DURABILITY.TryGetValue(rarity, out var md) ? md : 50;
                        sd["durability"] = (float)max_dur;
                        eq[slot] = sd;
                    }
                }
            }
            var equipment = _get_dict(saveData, "equipment");
            var equipped = _get_dict(equipment, "equipped");
            equipped[char_id] = eq;
            save_slot();
        }

        public float get_backpack_item_durability(string item_id)
        {
            var inv = _get_dict(saveData, "inventory");
            var items = _get_list(inv, "items");
            foreach (var entry in items)
            {
                Dictionary<string, object> e;
                if (entry is Dictionary<string, object> de) e = de;
                else if (entry is JObject jo) e = jo.ToObject<Dictionary<string, object>>();
                else continue;
                if (_str(_get(e, "item_id", "")) == item_id)
                    return _float(_get(e, "durability", 0f));
            }
            return 0f;
        }

        public Dictionary<string, object> get_all_equipped()
        {
            var equipment = _get_dict(saveData, "equipment");
            var equipped = _get_dict(equipment, "equipped");
            var result = new Dictionary<string, object>();
            foreach (var kv in equipped) result[kv.Key] = kv.Value;
            return result;
        }

        public void clear_all_equipment()
        {
            var equipment = _get_dict(saveData, "equipment");
            var equipped = _get_dict(equipment, "equipped");
            var keys = new List<string>(equipped.Keys);
            foreach (var char_id in keys)
            {
                var empty_eq = new Dictionary<string, object>();
                foreach (var s in EQUIP_SLOTS)
                    empty_eq[s] = new Dictionary<string, object> { {"item_id", ""}, {"durability", 0f} };
                equipped[char_id] = empty_eq;
            }
            equipment["equipped"] = equipped;
            saveData["equipment"] = equipment;
            save_slot();
        }

        // 穿戴装备：只更新存档里的装备记录，不操作背包
        // 返回被替换下来的旧装备ID（空字符串=原本没穿）
        // initial_durability >= 0 时使用指定耐久值，否则使用最大值
        public string equip_item(string char_id, string slot, string item_id, float initial_durability = -1f)
        {
            var equipment = _get_dict(saveData, "equipment");
            var equipped = _get_dict(equipment, "equipped");
            if (!equipped.ContainsKey(char_id))
            {
                var empty_eq = new Dictionary<string, object>();
                foreach (var s in EQUIP_SLOTS)
                    empty_eq[s] = new Dictionary<string, object> { {"item_id", ""}, {"durability", 0f} };
                equipped[char_id] = empty_eq;
            }
            var char_eq_o = equipped.TryGetValue(char_id, out var v) ? v : null;
            Dictionary<string, object> char_eq;
            if (v is Dictionary<string, object> ce) char_eq = ce;
            else char_eq = new Dictionary<string, object>();

            var old_slot = char_eq.TryGetValue(slot, out var osv) ? osv : null;
            string old_id = "";
            if (old_slot is string oss) old_id = oss;
            else if (old_slot is Dictionary<string, object> osd) old_id = _str(_get(osd, "item_id", ""));
            else if (old_slot is JObject osj) old_id = _str(_get(osj.ToObject<Dictionary<string, object>>(), "item_id", ""));

            float dur = 0f;
            if (item_id != "")
            {
                if (initial_durability >= 0) dur = initial_durability;
                else
                {
                    var def = _get_item_def(item_id);
                    string rarity = _str(_get(def, "rarity", "common"));
                    dur = (float)(RARITY_MAX_DURABILITY.TryGetValue(rarity, out var md) ? md : 50);
                }
            }
            char_eq[slot] = new Dictionary<string, object> { {"item_id", item_id}, {"durability", dur} };
            equipped[char_id] = char_eq;
            equipment["equipped"] = equipped;
            saveData["equipment"] = equipment;
            save_slot();
            return old_id;
        }

        public string unequip_item(string char_id, string slot)
        {
            return equip_item(char_id, slot, "");
        }

        // 消耗装备耐久（接球/被击中时调用）
        // reason: "catch" 或 "hit"
        public void reduce_equipment_durability(string char_id, string reason)
        {
            Debug.Log("[PlayerSaveManager] reduce_equipment_durability 被调用: char=" + char_id + " reason=" + reason);
            var equipment = _get_dict(saveData, "equipment");
            var equipped = _get_dict(equipment, "equipped");
            if (!equipped.ContainsKey(char_id))
            {
                Debug.Log("[PlayerSaveManager] 角色 " + char_id + " 无装备记录，跳过");
                return;
            }
            var char_eq_o = equipped.TryGetValue(char_id, out var v) ? v : null;
            if (!(v is Dictionary<string, object>)) return;
            var char_eq = (Dictionary<string, object>)v;
            float loss = (reason == "hit") ? DURABILITY_LOSS_PER_HIT : DURABILITY_LOSS_PER_CATCH;
            bool changed = false;
            foreach (var slot in EQUIP_SLOTS)
            {
                var slot_data = char_eq.TryGetValue(slot, out var sd) ? sd : null;
                if (slot_data is Dictionary<string, object> sdd)
                {
                    string item_id = _str(_get(sdd, "item_id", ""));
                    if (item_id == "") continue;
                    float cur_dur = _float(_get(sdd, "durability", 0f));
                    float new_dur = Mathf.Max(0f, cur_dur - loss);
                    Debug.Log(string.Format("[PlayerSaveManager] 装备损耗: {0}[{1}] {2}: {3:F2} -> {4:F2}", char_id, slot, item_id, cur_dur, new_dur));
                    if (new_dur <= 0f)
                    {
                        Debug.Log("[PlayerSaveManager] 装备 " + item_id + " 耐久归零，已消失 (char=" + char_id + " slot=" + slot + ")");
                        char_eq[slot] = new Dictionary<string, object> { {"item_id", ""}, {"durability", 0f} };
                    }
                    else
                    {
                        char_eq[slot] = new Dictionary<string, object> { {"item_id", item_id}, {"durability", new_dur} };
                    }
                    changed = true;
                }
            }
            if (changed)
            {
                equipped[char_id] = char_eq;
                equipment["equipped"] = equipped;
                saveData["equipment"] = equipment;
                save_slot();
                Debug.Log("[PlayerSaveManager] 装备耐久已保存到存档");
            }
            else
            {
                Debug.Log("[PlayerSaveManager] 角色 " + char_id + " 无装备损耗");
            }
        }

        // 获取某角色的装备总加成 {stat_key: total_bonus}
        // 衰减阈值仅在开局判断：低于阈值的装备属性减半
        public Dictionary<string, object> get_equipment_bonuses(string char_id)
        {
            var result = new Dictionary<string, object>
            {
                {"stamina_bonus", 0}, {"defense_bonus", 0}, {"speed_bonus", 0},
                {"attack_bonus", 0}, {"resilience_bonus", 0}, {"ball_speed_bonus", 0}
            };
            var eq = get_equipped(char_id);
            foreach (var slot in EQUIP_SLOTS)
            {
                var slot_data = eq.TryGetValue(slot, out var v) ? v : null;
                string item_id = "";
                float durability = 0f;
                if (slot_data is string s) item_id = s;
                else if (slot_data is Dictionary<string, object> sd)
                {
                    item_id = _str(_get(sd, "item_id", ""));
                    durability = _float(_get(sd, "durability", 0f));
                }
                if (item_id == "") continue;
                var def = _get_item_def(item_id);
                if (def == null || def.Count == 0) continue;
                var stats = _get_dict(def, "stats");
                string rarity = _str(_get(def, "rarity", "common"));
                int threshold = RARITY_DURABILITY_THRESHOLD.TryGetValue(rarity, out var th) ? th : 30;
                float multiplier = 1f;
                if (durability < threshold) multiplier = 0.5f;
                foreach (var kv in stats)
                {
                    if (result.ContainsKey(kv.Key))
                    {
                        int cur = _int(result[kv.Key]);
                        result[kv.Key] = cur + (int)(_float(kv.Value) * multiplier);
                    }
                }
            }
            return result;
        }
        // ==================== PascalCase 兼容方法（UI 调用） ====================

        /// <summary>UI兼容: 获取货币（float版本）</summary>
        public float GetCurrency(string type)
        {
            return (float)get_currency(type);
        }

        /// <summary>UI兼容: 获取货币（Dictionary版本）</summary>
        public Dictionary<string, float> GetCurrency()
        {
            var r = new Dictionary<string, float>();
            r["gold"] = (float)get_currency("gold");
            r["spirit_ore"] = (float)get_currency("spirit_ore");
            r["spirit_crystal"] = (float)get_currency("spirit_crystal");
            r["fairy_coin"] = (float)get_currency("fairy_coin");
            return r;
        }

        /// <summary>UI兼容: 货币字典转object</summary>
        public Dictionary<string, object> GetCurrencyAsObject()
        {
            var r = new Dictionary<string, object>();
            foreach (var kv in GetCurrency()) r[kv.Key] = kv.Value;
            return r;
        }

        /// <summary>UI兼容: 增加货币</summary>
        public void AddCurrency(string type, float amount)
        {
            add_currency(type, (int)amount);
        }

        /// <summary>UI兼容: 获取所有装备</summary>
        public Dictionary<string, Dictionary<string, object>> GetAllEquipped()
        {
            var r = new Dictionary<string, Dictionary<string, object>>();
            var all = get_all_equipped();
            if (all != null)
            {
                foreach (var kv in all)
                {
                    if (kv.Value is Dictionary<string, object> d) r[kv.Key] = d;
                }
            }
            return r;
        }

        /// <summary>UI兼容: 获取角色训练数据</summary>
        public Dictionary<string, object> GetCharacterTrain(string charId)
        {
            return get_character_train(charId);
        }

        /// <summary>UI兼容: 获取装备物品（返回含item_id的字典）</summary>
        public Dictionary<string, object> GetEquippedItem(string charId, string slot)
        {
            var r = new Dictionary<string, object>();
            r["item_id"] = get_equipped_item(charId, slot);
            return r;
        }

        /// <summary>UI兼容: 获取装备耐久（返回含current的字典）</summary>
        public Dictionary<string, object> GetEquippedDurability(string charId, string slot)
        {
            var r = new Dictionary<string, object>();
            r["current"] = get_equipped_durability(charId, slot);
            var def = get_equipped_item(charId, slot);
            r["max"] = 100f;
            return r;
        }

        /// <summary>UI兼容: 获取装备加成</summary>
        public Dictionary<string, object> GetEquipmentBonuses(string charId)
        {
            return get_equipment_bonuses(charId);
        }

        /// <summary>UI兼容: 获取存档数据</summary>
        public Dictionary<string, object> GetData()
        {
            return get_data();
        }

        /// <summary>UI兼容: 保存存档</summary>
        public void SaveData()
        {
            save_slot();
        }
    }
}
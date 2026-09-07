using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BattleBall.Core;
using BattleBall.Systems.Inventory;

namespace BattleBall.Systems.Nutrition
{
    /// <summary>
    /// 营养系统管理器
    /// 赛前吃1种食物，消耗1个，全队获得固定值加成，整场有效
    /// 整场只能吃1种1次，不叠加
    /// </summary>
    public class NutritionManager : MonoBehaviour
    {
        public event Action<string> food_consumed;
        public event Action food_cleared;

        // 食物数据缓存 {food_id: food_data}
        private Dictionary<string, object> _foods_cache = new Dictionary<string, object>();
        // 当前已吃的食物ID（空字符串=没吃）
        private string _active_food_id = "";

        // 属性映射：食物effect.stat -> player属性名
        public static readonly Dictionary<string, string> STAT_MAP = new Dictionary<string, string>
        {
            {"stamina", "stamina"}, {"defense", "defense"}, {"speed", "speed"},
            {"attack", "attack"}, {"resilience", "resilience"}, {"ball_speed", "ball_speed"},
        };

        public static NutritionManager Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        protected virtual void OnDestroy() { if (Instance == this) Instance = null; }

        protected virtual void Start()
        {
            _load_foods_data();
        }

        // ===== 内部工具 =====
        private static object _get(Dictionary<string, object> d, string k, object def)
        {
            if (d == null) return def;
            object v; return d.TryGetValue(k, out v) ? v : def;
        }
        private static float _float(object v) { if (v == null) return 0f; if (v is float f) return f; if (v is double dd) return (float)dd; if (v is int i) return (float)i; if (v is long l) return (float)l; float r; return float.TryParse(v.ToString(), out r) ? r : 0f; }
        private static string _str(object v) { return v == null ? "" : v.ToString(); }

        private static Dictionary<string, object> _as_dict(object v)
        {
            if (v is Dictionary<string, object> d) return d;
            if (v is JObject jo) return jo.ToObject<Dictionary<string, object>>();
            return new Dictionary<string, object>();
        }

        private void _load_foods_data()
        {
            _foods_cache.Clear();

            // 1. 先从 items.json 加载（管理员维护的数据源，优先级高）
            var ta = Resources.Load<TextAsset>("Data/items/items");
            if (ta != null)
            {
                try
                {
                    var ijson = JsonConvert.DeserializeObject<Dictionary<string, object>>(ta.text);
                    if (ijson != null)
                    {
                        var items_o = _get(ijson, "items", null);
                        List<object> items = null;
                        if (items_o is List<object> li) items = li;
                        else if (items_o is JArray ja) items = ja.ToObject<List<object>>();
                        if (items != null)
                        {
                            foreach (var item in items)
                            {
                                var d = _as_dict(item);
                                if (_str(_get(d, "sub_type", "")) == "food")
                                {
                                    string fid = _str(_get(d, "id", ""));
                                    if (fid != "") _foods_cache[fid] = d;
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("[NutritionManager] items.json 解析失败: " + e.Message);
                }
            }

            // 2. 再从 foods.json 补充（兼容旧数据，不覆盖 items.json 已有的）
            var ta2 = Resources.Load<TextAsset>("Data/items/foods");
            if (ta2 == null)
            {
                Debug.LogWarning("[NutritionManager] 食物数据文件不存在: Data/items/foods");
                return;
            }

            try
            {
                var json = JsonConvert.DeserializeObject<Dictionary<string, object>>(ta2.text);
                if (json == null)
                {
                    Debug.LogError("[NutritionManager] 食物数据JSON解析失败");
                    return;
                }
                var foods_o = _get(json, "foods", null);
                List<object> foods = null;
                if (foods_o is List<object> lf) foods = lf;
                else if (foods_o is JArray jaf) foods = jaf.ToObject<List<object>>();
                else foods = new List<object>();

                foreach (var food in foods)
                {
                    var d = _as_dict(food);
                    string fid = _str(_get(d, "id", ""));
                    if (fid != "" && !_foods_cache.ContainsKey(fid))
                        _foods_cache[fid] = d;
                }
                Debug.Log("[NutritionManager] 食物数据加载完成: " + _foods_cache.Count + " 个");
            }
            catch (Exception e)
            {
                Debug.LogError("[NutritionManager] 食物数据JSON解析失败: " + e.Message);
            }
        }

        // 重新加载食物数据（管理员修改数据后调用）
        public void reload_foods_data()
        {
            _load_foods_data();
        }

        // 获取所有食物数据
        public List<object> get_all_foods()
        {
            return new List<object>(_foods_cache.Values);
        }

        // 按稀有度获取食物
        public List<object> get_foods_by_rarity(string rarity)
        {
            var result = new List<object>();
            foreach (var food in _foods_cache.Values)
            {
                var d = _as_dict(food);
                if (_str(_get(d, "rarity", "")) == rarity) result.Add(d);
            }
            return result;
        }

        // 获取单个食物数据
        public Dictionary<string, object> get_food(string food_id)
        {
            return _foods_cache.TryGetValue(food_id, out var v) ? _as_dict(v) : new Dictionary<string, object>();
        }

        // 吃食物：消耗1个，全队生效，整场1种1次
        // 返回 true=成功，false=失败
        public bool consume_food(string food_id)
        {
            // 整场只能吃1种1次
            if (_active_food_id != "")
            {
                Debug.LogWarning("[NutritionManager] 本场已吃过食物: " + _active_food_id + "，不能再吃");
                return false;
            }

            // 验证食物ID有效
            if (!_foods_cache.ContainsKey(food_id))
            {
                Debug.LogWarning("[NutritionManager] 无效的食物ID: " + food_id);
                return false;
            }

            // 从背包扣减1个
            if (InventoryManager.Instance == null)
            {
                Debug.LogError("[NutritionManager] InventoryManager未就绪");
                return false;
            }
            int before_count = InventoryManager.Instance.get_item_count(food_id);
            Debug.Log("[NutritionManager] 吃食物前背包数量: " + food_id + " = " + before_count);
            bool removed = InventoryManager.Instance.remove_item(food_id, 1);
            if (!removed)
            {
                Debug.LogWarning("[NutritionManager] 背包中食物不足或不存在: " + food_id);
                return false;
            }
            int after_count = InventoryManager.Instance.get_item_count(food_id);
            Debug.Log("[NutritionManager] 吃食物后背包数量: " + food_id + " = " + after_count + " (扣减" + (before_count - after_count) + ")");

            _active_food_id = food_id;
            if (PlayerSaveManager.Instance != null) PlayerSaveManager.Instance.set_active_food(food_id);
            if (food_consumed != null) food_consumed(food_id);
            return true;
        }

        // 获取当前已激活的食物ID
        public string get_active_food_id()
        {
            return _active_food_id;
        }

        // 获取当前已激活的食物加成（返回 {stat: value} 或空字典）
        public Dictionary<string, object> get_active_bonus()
        {
            if (_active_food_id == "") return new Dictionary<string, object>();
            var food = get_food(_active_food_id);
            if (food == null || food.Count == 0) return new Dictionary<string, object>();
            var effect = _as_dict(_get(food, "effect", null));
            string stat = _str(_get(effect, "stat", ""));
            float value = _float(_get(effect, "value", 0));
            if (stat == "" || value == 0f) return new Dictionary<string, object>();
            var r = new Dictionary<string, object>();
            r[stat] = value;
            return r;
        }

        // 获取全队食物加成（给player用，返回6项属性的加成字典）
        public Dictionary<string, object> get_team_bonuses()
        {
            var bonus = get_active_bonus();
            if (bonus.Count == 0) return new Dictionary<string, object>();
            // 取第一项
            string stat = null; object val = null;
            foreach (var kv in bonus) { stat = kv.Key; val = kv.Value; break; }
            if (stat == null) return new Dictionary<string, object>();
            float value = _float(val);
            string mapped = STAT_MAP.TryGetValue(stat, out var m) ? m : stat;
            var r = new Dictionary<string, object>();
            r[mapped + "_bonus"] = value;
            return r;
        }

        // 清空已吃食物（比赛结束后调用）
        public void clear_active_food()
        {
            _active_food_id = "";
            if (PlayerSaveManager.Instance != null) PlayerSaveManager.Instance.set_active_food("");
            if (food_cleared != null) food_cleared();
        }

        // 比赛开始时从存档恢复已吃食物状态
        public void load_from_save()
        {
            if (PlayerSaveManager.Instance == null) return;
            var save_data = PlayerSaveManager.Instance.get_data();
            var nutrition = _as_dict(_get(save_data, "nutrition", null));
            var raw = _get(nutrition, "pre_match_food", "");
            if (raw is List<object> arr)
            {
                if (arr.Count > 0) _active_food_id = _str(arr[0]);
                else _active_food_id = "";
            }
            else if (raw is JArray ja)
            {
                if (ja.Count > 0) _active_food_id = _str(ja[0]);
                else _active_food_id = "";
            }
            else
            {
                _active_food_id = _str(raw);
            }
        }

        // ===== UI (PreparationUI) 兼容方法 =====
        public virtual void LoadFromSave(object saveData) { }
        public virtual Dictionary<string, object> GetTeamBonuses(string team) { return new Dictionary<string, object>(); }
        public virtual Dictionary<string, object> GetFood(string foodId) { return new Dictionary<string, object>(); }
        public virtual string GetActiveFoodId(int playerIdx) { return ""; }
        public virtual bool ConsumeFood(int playerIdx, string foodId) { return true; }

        // ===== UI 兼容：少参/无参重载 =====
        public virtual void LoadFromSave() { }
        public virtual string GetActiveFoodId() { return ""; }
        public virtual Dictionary<string, object> GetTeamBonuses() { return new Dictionary<string, object>(); }
        public virtual bool ConsumeFood(string foodId) { return true; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance == null)
            {
                var go = new GameObject("NutritionManager");
                go.AddComponent<NutritionManager>();
            }
        }
    }
}

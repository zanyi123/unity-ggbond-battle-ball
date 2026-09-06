using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BattleBall.Core
{
    /// <summary>
    /// 全局数据管理器（单例）。
    /// 使用 Newtonsoft.Json 从 Resources/data/ 加载所有 JSON 配置到内存。
    /// </summary>
    public class DataManager : MonoBehaviour
    {
        public static DataManager Instance { get; private set; }

        // ============================================================
        // 数据缓存（私有）
        // ============================================================
        private List<Dictionary<string, object>> _characters;
        private List<Dictionary<string, object>> _spirits;
        private List<Dictionary<string, object>> _skills;          // data/skills/skills.json
        private List<Dictionary<string, object>> _items;
        private List<Dictionary<string, object>> _foods;
        private Dictionary<string, object> _growthCurves;
        private Dictionary<string, object> _elements;
        private List<Dictionary<string, object>> _tags;
        private List<Dictionary<string, object>> _spiritSkills;    // data/spirits/skills.json（向后兼容）

        // ============================================================
        // 向后兼容字段（旧代码直接访问这些字段）
        // ============================================================
        public List<Dictionary<string, object>> characters { get { return _characters; } }
        public List<Dictionary<string, object>> spirits { get { return _spirits; } }
        public List<Dictionary<string, object>> skills { get { return _spiritSkills; } }
        public List<Dictionary<string, object>> tags { get { return _tags; } }
        public Dictionary<string, object> elements { get { return _elements; } }

        public event System.Action data_loaded;

        /// <summary>UI 兼容：元灵列表属性。</summary>
        public List<Dictionary<string, object>> Spirits { get { return _spirits ?? new List<Dictionary<string, object>>(); } }

        // ============================================================
        // 生命周期
        // ============================================================
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadAllData();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance == null)
            {
                var go = new GameObject("DataManager");
                go.AddComponent<DataManager>();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>加载全部 JSON 数据到内存。可重复调用以刷新。</summary>
        public void LoadAllData()
        {
            _characters = LoadArray("data/characters/characters");
            _spirits = LoadObjectArray("data/spirits/spirits", "spirits");
            _skills = LoadArray("data/skills/skills");
            _items = LoadObjectArray("data/items/items", "items");
            _foods = LoadObjectArray("data/items/foods", "foods");
            _growthCurves = LoadObject("data/characters/growth_curves");
            _elements = LoadObject("data/spirits/elements");
            _tags = LoadObjectArray("data/spirits/tags_registry", "tags");
            _spiritSkills = LoadObjectArray("data/spirits/skills", "skills");

            data_loaded?.Invoke();
            Debug.Log(string.Format(
                "[DataManager] 数据加载完成: {0} 角色, {1} 元灵, {2} 技能(skills), " +
                "{3} 元灵技能, {4} 道具, {5} 食物, {6} 标签",
                _characters.Count, _spirits.Count, _skills.Count, _spiritSkills.Count,
                _items.Count, _foods.Count, _tags.Count));
        }

        // ============================================================
        // 公共 API（需求定义）
        // ============================================================
        public List<Dictionary<string, object>> GetAllCharacters()
        {
            return _characters ?? new List<Dictionary<string, object>>();
        }

        public Dictionary<string, object> GetCharacterById(string id)
        {
            return FindById(_characters, id);
        }

        public List<Dictionary<string, object>> GetAllSpirits()
        {
            return _spirits ?? new List<Dictionary<string, object>>();
        }

        public Dictionary<string, object> GetSpiritById(string id)
        {
            return FindById(_spirits, id);
        }

        public List<Dictionary<string, object>> GetAllSkills()
        {
            return _skills ?? new List<Dictionary<string, object>>();
        }

        public Dictionary<string, object> GetSkillById(string id)
        {
            // 优先在 skills.json 中查找，未命中再回退到元灵技能，兼容旧调用。
            var found = FindById(_skills, id);
            if (found.Count > 0) return found;
            return FindById(_spiritSkills, id);
        }

        public List<Dictionary<string, object>> GetAllItems()
        {
            return _items ?? new List<Dictionary<string, object>>();
        }

        public Dictionary<string, object> GetItemById(string id)
        {
            return FindById(_items, id);
        }

        public List<Dictionary<string, object>> GetAllFoods()
        {
            return _foods ?? new List<Dictionary<string, object>>();
        }

        public Dictionary<string, object> GetFoodById(string id)
        {
            return FindById(_foods, id);
        }

        // ============================================================
        // 向后兼容方法
        // ============================================================
        public void load_all_data() { LoadAllData(); }
        public void reload_all() { LoadAllData(); }

        public Dictionary<string, object> get_character_by_id(string char_id) { return GetCharacterById(char_id); }
        public Dictionary<string, object> get_spirit_by_id(string spirit_id) { return GetSpiritById(spirit_id); }

        /// <summary>旧版技能查找：在元灵技能（spirits/skills.json）中查找。</summary>
        public Dictionary<string, object> get_skill_by_id(string skill_id)
        {
            return FindById(_spiritSkills, skill_id);
        }

        public Dictionary<string, object> get_tag_by_id(string tag_id)
        {
            return FindById(_tags, tag_id);
        }

        public List<Dictionary<string, object>> get_skills_for_spirit(string spirit_id)
        {
            var result = new List<Dictionary<string, object>>();
            if (_spiritSkills == null) return result;
            foreach (var s in _spiritSkills)
            {
                if (s != null && s.TryGetValue("spirit_id", out var v) && v != null && v.ToString() == spirit_id)
                    result.Add(s);
            }
            return result;
        }

        public List<Dictionary<string, object>> get_skills_by_tag(string tag)
        {
            var result = new List<Dictionary<string, object>>();
            if (_spiritSkills == null) return result;
            foreach (var s in _spiritSkills)
            {
                if (s != null && s.TryGetValue("tag", out var v) && v != null && v.ToString() == tag)
                    result.Add(s);
            }
            return result;
        }

        /// <summary>计算属性克制倍率。</summary>
        public float get_counter_multiplier(string attacker_element, string defender_element)
        {
            if (_elements == null || _elements.Count == 0) return 1.0f;

            float mult = 1.3f;
            if (_elements.TryGetValue("counter_multiplier", out var mObj) && mObj != null)
            {
                if (float.TryParse(mObj.ToString(), out var mv)) mult = mv;
            }

            if (_elements.TryGetValue("counters", out var cObj) && cObj != null)
            {
                IEnumerable counters = null;
                if (cObj is JArray ja) counters = ja;
                else if (cObj is IList il) counters = il;

                if (counters != null)
                {
                    foreach (var counter in counters)
                    {
                        string atk = null;
                        string def = null;
                        if (counter is JObject jo)
                        {
                            atk = jo["attacker"] != null ? jo["attacker"].ToString() : null;
                            def = jo["defender"] != null ? jo["defender"].ToString() : null;
                        }
                        else if (counter is IDictionary<string, object> cd)
                        {
                            atk = cd.TryGetValue("attacker", out var a) ? a?.ToString() : null;
                            def = cd.TryGetValue("defender", out var d) ? d?.ToString() : null;
                        }
                        if (atk != null && def != null && atk == attacker_element && def == defender_element)
                            return mult;
                    }
                }
            }
            return 1.0f;
        }

        // ============================================================
        // 私有加载辅助
        // ============================================================
        private string LoadText(string resourcePath)
        {
            var ta = Resources.Load<TextAsset>(resourcePath);
            if (ta == null)
            {
                Debug.LogError("[DataManager] 资源不存在: " + resourcePath);
                return null;
            }
            return ta.text;
        }

        /// <summary>加载根节点为数组的 JSON，转为 List&lt;Dictionary&lt;string, object&gt;&gt;。</summary>
        private List<Dictionary<string, object>> LoadArray(string resourcePath)
        {
            var result = new List<Dictionary<string, object>>();
            string text = LoadText(resourcePath);
            if (text == null) return result;
            try
            {
                var arr = JArray.Parse(text);
                foreach (var item in arr)
                {
                    if (item is JObject jo)
                        result.Add(jo.ToObject<Dictionary<string, object>>());
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[DataManager] JSON 解析错误 " + resourcePath + ": " + e.Message);
            }
            return result;
        }

        /// <summary>加载根节点为对象、内部含数组字段的 JSON，提取指定数组。</summary>
        private List<Dictionary<string, object>> LoadObjectArray(string resourcePath, string arrayKey)
        {
            var result = new List<Dictionary<string, object>>();
            string text = LoadText(resourcePath);
            if (text == null) return result;
            try
            {
                var obj = JObject.Parse(text);
                var token = obj[arrayKey];
                if (token is JArray arr)
                {
                    foreach (var item in arr)
                    {
                        if (item is JObject jo)
                            result.Add(jo.ToObject<Dictionary<string, object>>());
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[DataManager] JSON 解析错误 " + resourcePath + ": " + e.Message);
            }
            return result;
        }

        /// <summary>加载根节点为对象的 JSON，转为 Dictionary&lt;string, object&gt;。</summary>
        private Dictionary<string, object> LoadObject(string resourcePath)
        {
            var result = new Dictionary<string, object>();
            string text = LoadText(resourcePath);
            if (text == null) return result;
            try
            {
                var obj = JObject.Parse(text);
                result = obj.ToObject<Dictionary<string, object>>();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[DataManager] JSON 解析错误 " + resourcePath + ": " + e.Message);
            }
            return result;
        }

        /// <summary>在列表中按 id 字段查找，未找到返回空字典。</summary>
        private Dictionary<string, object> FindById(List<Dictionary<string, object>> list, string id)
        {
            if (list == null || id == null) return new Dictionary<string, object>();
            foreach (var item in list)
            {
                if (item != null && item.TryGetValue("id", out var v) && v != null && v.ToString() == id)
                    return item;
            }
            return new Dictionary<string, object>();
        }
    }
}
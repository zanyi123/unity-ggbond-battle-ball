using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace BattleBall.Core
{
    public class DataManager : MonoBehaviour
    {
        public List<Dictionary<string, object>> characters = new List<Dictionary<string, object>>();
        public List<Dictionary<string, object>> spirits = new List<Dictionary<string, object>>();
        public List<Dictionary<string, object>> skills = new List<Dictionary<string, object>>();
        public List<Dictionary<string, object>> tags = new List<Dictionary<string, object>>();
        public Dictionary<string, object> elements = new Dictionary<string, object>();

        public event System.Action data_loaded;

        public static DataManager Instance { get; private set; }

        /// <summary>UI 兼容: 按ID获取角色配置</summary>
        /// <summary>UI 兼容: 按ID获取角色配置</summary>
        public virtual Dictionary<string, object> GetCharacterById(string id) {
            if (characters == null) return new Dictionary<string, object>();
            foreach (var c in characters) { if (c is Dictionary<string, object> cd && cd.ContainsKey("id") && cd["id"]?.ToString() == id) return cd; }
            return new Dictionary<string, object>();
        }

        /// <summary>UI 兼容: 图鉴Spirits</summary>
        public virtual List<Dictionary<string, object>> Spirits { get { return _spirits; } }
        protected List<Dictionary<string, object>> _spirits = new List<Dictionary<string, object>>();

        /// <summary>UI 兼容: 按ID获取技能定义</summary>
        public virtual Dictionary<string, object> GetSkillById(string skillId) {
            if (skills == null) return new Dictionary<string, object>();
            foreach (var s in skills) { if (s is Dictionary<string, object> sd && sd.ContainsKey("id") && sd["id"]?.ToString() == skillId) return sd; }
            return new Dictionary<string, object>();
        }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }
        protected virtual void OnDestroy() { if (Instance == this) Instance = null; }

        public virtual void Start() { load_all_data(); }

        public virtual void load_all_data()
        {
            characters = _load_json_array("Data/characters/characters");
            spirits = _load_spirits_array();
            skills = _load_spirits_skills();
            tags = _load_tags();
            elements = _load_json_dict("Data/spirits/elements");
            data_loaded?.Invoke();
            Debug.Log(string.Format(
                "[DataManager] 数据加载完成: {0} 角色, {1} 元灵, {2} 技能, {3} 标签",
                characters.Count, spirits.Count, skills.Count, tags.Count));
        }

        public virtual void reload_all() { load_all_data(); }

        // 实际加载 JSON: Resources.Load<TextAsset> + JObject.Parse
        protected virtual object _load_json_raw(string path)
        {
            var ta = Resources.Load<TextAsset>(path);
            if (ta == null)
            {
                Debug.LogWarning("[DataManager] 文件不存在: " + path);
                return null;
            }
            try
            {
                var text = ta.text.Trim();
                if (text.StartsWith("["))
                    return JArray.Parse(text).ToObject<List<object>>();
                else
                    return JObject.Parse(text).ToObject<Dictionary<string, object>>();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[DataManager] JSON解析错误 " + path + ": " + e.Message);
                return null;
            }
        }

        protected virtual List<Dictionary<string, object>> _load_json_array(string path)
        {
            var result = _load_json_raw(path);
            if (result is IList list)
            {
                var typed = new List<Dictionary<string, object>>();
                foreach (var item in list)
                {
                    if (item is IDictionary<string, object> d)
                        typed.Add(new Dictionary<string, object>(d));
                    else if (item is JObject jo)
                        typed.Add(jo.ToObject<Dictionary<string, object>>());
                }
                return typed;
            }
            return new List<Dictionary<string, object>>();
        }

        protected virtual Dictionary<string, object> _load_json_dict(string path)
        {
            var result = _load_json_raw(path);
            if (result is IDictionary<string, object> d)
                return new Dictionary<string, object>(d);
            if (result is JObject jo)
                return jo.ToObject<Dictionary<string, object>>();
            return new Dictionary<string, object>();
        }

        protected virtual List<Dictionary<string, object>> _load_spirits_array()
        {
            var result = _load_json_raw("Data/spirits/spirits");
            if (result is IDictionary<string, object> d && d.TryGetValue("spirits", out var tObj) && tObj is IList list)
            {
                var typed = new List<Dictionary<string, object>>();
                foreach (var item in list)
                {
                    if (item is IDictionary<string, object> dd) typed.Add(new Dictionary<string, object>(dd));
                    else if (item is JObject jo) typed.Add(jo.ToObject<Dictionary<string, object>>());
                }
                return typed;
            }
            return new List<Dictionary<string, object>>();
        }

        protected virtual List<Dictionary<string, object>> _load_spirits_skills()
        {
            var result = _load_json_raw("Data/spirits/skills");
            if (result is IDictionary<string, object> d && d.TryGetValue("skills", out var tObj) && tObj is IList list)
            {
                var typed = new List<Dictionary<string, object>>();
                foreach (var item in list)
                {
                    if (item is IDictionary<string, object> dd) typed.Add(new Dictionary<string, object>(dd));
                    else if (item is JObject jo) typed.Add(jo.ToObject<Dictionary<string, object>>());
                }
                return typed;
            }
            return new List<Dictionary<string, object>>();
        }

        protected virtual List<Dictionary<string, object>> _load_tags()
        {
            var result = _load_json_raw("Data/spirits/tags_registry");
            if (result is IDictionary<string, object> d && d.TryGetValue("tags", out var tObj) && tObj is IList list)
            {
                var typed = new List<Dictionary<string, object>>();
                foreach (var item in list)
                {
                    if (item is IDictionary<string, object> dd) typed.Add(new Dictionary<string, object>(dd));
                    else if (item is JObject jo) typed.Add(jo.ToObject<Dictionary<string, object>>());
                }
                return typed;
            }
            return new List<Dictionary<string, object>>();
        }

        // ===== 查询方法 =====
        public virtual Dictionary<string, object> get_character_by_id(string char_id)
        {
            foreach (var c in characters)
                if (c.TryGetValue("id", out var v) && v != null && v.ToString() == char_id) return c;
            return new Dictionary<string, object>();
        }

        public virtual Dictionary<string, object> get_spirit_by_id(string spirit_id)
        {
            foreach (var s in spirits)
                if (s.TryGetValue("id", out var v) && v != null && v.ToString() == spirit_id) return s;
            return new Dictionary<string, object>();
        }

        public virtual List<Dictionary<string, object>> get_skills_for_spirit(string spirit_id)
        {
            var result = new List<Dictionary<string, object>>();
            foreach (var s in skills)
                if (s.TryGetValue("spirit_id", out var v) && v != null && v.ToString() == spirit_id) result.Add(s);
            return result;
        }

        public virtual Dictionary<string, object> get_skill_by_id(string skill_id)
        {
            foreach (var s in skills)
                if (s.TryGetValue("id", out var v) && v != null && v.ToString() == skill_id) return s;
            return new Dictionary<string, object>();
        }

        public virtual Dictionary<string, object> get_tag_by_id(string tag_id)
        {
            foreach (var t in tags)
                if (t.TryGetValue("id", out var v) && v != null && v.ToString() == tag_id) return t;
            return new Dictionary<string, object>();
        }

        public virtual List<Dictionary<string, object>> get_skills_by_tag(string tag)
        {
            var result = new List<Dictionary<string, object>>();
            foreach (var s in skills)
                if (s.TryGetValue("tag", out var v) && v != null && v.ToString() == tag) result.Add(s);
            return result;
        }

        public virtual float get_counter_multiplier(string attacker_element, string defender_element)
        {
            if (elements.Count == 0) return 1.0f;
            if (elements.TryGetValue("counters", out var cObj) && cObj is IList counters)
            {
                float mult = 1.3f;
                if (elements.TryGetValue("counter_multiplier", out var mObj) && mObj != null)
                    if (float.TryParse(mObj.ToString(), out var mv)) mult = mv;
                foreach (var counter in counters)
                {
                    if (counter is IDictionary<string, object> cd)
                    {
                        if (cd.TryGetValue("attacker", out var atk) && cd.TryGetValue("defender", out var def) &&
                            atk != null && def != null &&
                            atk.ToString() == attacker_element && def.ToString() == defender_element)
                            return mult;
                    }
                }
            }
            return 1.0f;
        }
    }
}

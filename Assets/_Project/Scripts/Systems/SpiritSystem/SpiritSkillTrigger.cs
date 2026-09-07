using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using BattleBall.Core;
using BattleBall.Battle;
using BattleBall.Systems.SpiritSystem;

namespace BattleBall.Systems.Spirit
{
    /// <summary>
    /// 元灵技能触发器
    /// 负责检测技能使用并调用标签效果
    /// 数据源：DataManager.GetAllSkills() + DataManager.tags
    /// </summary>
    public class SpiritSkillTrigger : MonoBehaviour
    {
        // ===== 事件（GD signal 转 C# event）=====
        public event Action<string, int, Dictionary<string, object>> skill_triggered;
        public event Action<string, string, Dictionary<string, object>> skill_effect_applied;
        public event Action<string, Dictionary<string, object>> skill_ui_feedback;

        // ===== 标签注册表 =====
        protected Dictionary<string, Dictionary<string, object>> _tagsRegistry = new Dictionary<string, Dictionary<string, object>>();

        // ===== 标签效果处理器 =====
        protected SpiritTagEffectHandler _effectHandler;

        // ===== 玩家技能映射 {player_id: [skill_ids]} =====
        protected Dictionary<int, List<string>> _playerSkills = new Dictionary<int, List<string>>();

        // ===== 技能冷却 {player_id: {skill_id: remaining}} =====
        protected Dictionary<int, Dictionary<string, float>> _skillCooldowns = new Dictionary<int, Dictionary<string, float>>();

        // ===== 技能数据缓存 =====
        protected Dictionary<string, Dictionary<string, object>> _skillsCache = new Dictionary<string, Dictionary<string, object>>();
        protected bool _skillsLoaded = false;

        // ===== 战斗引用 =====
        public BattleManager battle_manager;
        public List<PlayerController> players = new List<PlayerController>();
        public Transform ball_node;

        protected virtual void Start()
        {
            _load_tags_registry();
            _load_skills_data();

            // 创建效果处理器（如果场景中没有）
            _effectHandler = FindObjectOfType<SpiritTagEffectHandler>();
            if (_effectHandler == null)
            {
                var go = new GameObject("SpiritTagEffectHandler");
                go.transform.SetParent(transform, false);
                _effectHandler = go.AddComponent<SpiritTagEffectHandler>();
            }
            _effectHandler.effect_applied += _on_effect_applied;
            _effectHandler.effect_finished += _on_effect_finished;

            Debug.Log("[SpiritSkillTrigger] 初始化完成, 标签=" + _tagsRegistry.Count + " 技能=" + _skillsCache.Count);
        }

        protected virtual void Update()
        {
            // 更新冷却时间
            float dt = Time.deltaTime;
            foreach (var pid in _skillCooldowns.Keys.ToList())
            {
                var cd = _skillCooldowns[pid];
                foreach (var sid in cd.Keys.ToList())
                {
                    if (cd[sid] > 0) cd[sid] = Mathf.Max(0f, cd[sid] - dt);
                }
            }
        }

        // ============================================================
        // 数据加载
        // ============================================================
        protected virtual void _load_tags_registry()
        {
            _tagsRegistry.Clear();
            var dm = DataManager.Instance;
            if (dm == null) { Debug.LogWarning("[SpiritSkillTrigger] DataManager 不存在"); return; }
            var tags = dm.tags;
            if (tags == null) return;
            foreach (var tag in tags)
            {
                string id = GetStr(tag, "id", "");
                if (id != "") _tagsRegistry[id] = tag;
            }
            Debug.Log("[SpiritSkillTrigger] 加载标签注册表: " + _tagsRegistry.Count + " 个");
        }

        protected virtual void _load_skills_data()
        {
            if (_skillsLoaded) return;
            _skillsCache.Clear();
            var dm = DataManager.Instance;
            if (dm == null) return;
            // 合并两套技能数据: skills/skills.json(通用技能) + spirits/skills.json(元灵技能)
            // 元灵的 skills 列表指向 spirits/skills.json 中的 id(如 skill_金刚_1)，
            // 仅缓存 GetAllSkills() 会导致元灵技能数据查不到、trigger_skill 恒失败。
            foreach (var s in dm.GetAllSkills())
            {
                string id = GetStr(s, "id", "");
                if (id != "") _skillsCache[id] = s;
            }
            foreach (var s in dm.GetAllSpiritSkills())
            {
                string id = GetStr(s, "id", "");
                if (id != "") _skillsCache[id] = s;
            }
            _skillsLoaded = true;
        }

        protected virtual Dictionary<string, object> _get_skill_data(string skillId)
        {
            _load_skills_data();
            if (_skillsCache.ContainsKey(skillId)) return _skillsCache[skillId];
            return new Dictionary<string, object>();
        }

        // ============================================================
        // 战斗引用设置
        // ============================================================
        public virtual void setup_battle_refs(BattleManager bm, List<PlayerController> playerNodes, Transform ball)
        {
            battle_manager = bm;
            players = playerNodes ?? new List<PlayerController>();
            ball_node = ball;
            if (_effectHandler != null)
            {
                _effectHandler.battle_manager = bm;
                if (ball != null) _effectHandler.ball_node = ball.GetComponent<BallController>();
                _effectHandler.players = players;
            }
        }

        public virtual void Bind(Component eh, BattleManager bm, Transform ball)
        {
            if (eh is SpiritTagEffectHandler) _effectHandler = (SpiritTagEffectHandler)eh;
            battle_manager = bm;
            ball_node = ball;
        }

        // ============================================================
        // 玩家技能
        // ============================================================
        public virtual void set_player_skills(int playerId, List<string> skillIds)
        {
            _playerSkills[playerId] = skillIds ?? new List<string>();
            if (!_skillCooldowns.ContainsKey(playerId))
                _skillCooldowns[playerId] = new Dictionary<string, float>();
            foreach (var sid in _playerSkills[playerId])
            {
                if (!_skillCooldowns[playerId].ContainsKey(sid))
                    _skillCooldowns[playerId][sid] = 0f;
            }
        }

        public virtual List<string> get_player_skills(int playerId)
        {
            if (_playerSkills.ContainsKey(playerId)) return _playerSkills[playerId];
            return new List<string>();
        }

        // ============================================================
        // 技能触发
        // ============================================================
        public virtual bool trigger_skill(int playerId, string skillId, Dictionary<string, object> targetData = null)
        {
            if (targetData == null) targetData = new Dictionary<string, object>();

            // 检查玩家是否有该技能
            if (!_playerSkills.ContainsKey(playerId))
            {
                Debug.Log("[SpiritSkillTrigger] 玩家无上场技能: " + playerId);
                return false;
            }
            if (!_playerSkills[playerId].Contains(skillId))
            {
                Debug.Log("[SpiritSkillTrigger] 玩家未上场该技能: " + skillId);
                return false;
            }

            // 检查冷却
            if (_skillCooldowns.ContainsKey(playerId) && _skillCooldowns[playerId].ContainsKey(skillId))
            {
                if (_skillCooldowns[playerId][skillId] > 0)
                {
                    Debug.Log("[SpiritSkillTrigger] 技能冷却中: " + _skillCooldowns[playerId][skillId]);
                    return false;
                }
            }

            // 获取技能数据
            var skillData = _get_skill_data(skillId);
            if (skillData.Count == 0)
            {
                Debug.LogError("[SpiritSkillTrigger] 技能数据不存在: " + skillId);
                return false;
            }

            // 检查能量消耗
            int energyCost = GetInt(skillData, "energy_cost", 0);
            if (!_consume_energy(playerId, energyCost))
            {
                Debug.Log("[SpiritSkillTrigger] 能量不足");
                return false;
            }

            // 发送技能触发信号
            skill_triggered?.Invoke(skillId, playerId, targetData);

            // 重置球修饰符
            if (_effectHandler != null) _effectHandler.reset_ball_mods();

            // 执行技能标签效果
            _execute_skill_tags(skillId, playerId, targetData);

            // 设置冷却
            float cooldown = GetFloat(skillData, "cooldown", 0f);
            _set_skill_cooldown(playerId, skillId, cooldown);

            return true;
        }

        protected virtual void _execute_skill_tags(string skillId, int playerId, Dictionary<string, object> targetData)
        {
            var skillData = _get_skill_data(skillId);
            // "tags" 可能是 JArray(Newtonsoft 反序列化) 或 List<object>，统一按 IEnumerable 处理，避免 as 转换得 null 导致 NPE
            var tagsObj = skillData.ContainsKey("tags") ? skillData["tags"] : null;
            System.Collections.IEnumerable tagIds = null;
            if (tagsObj is Newtonsoft.Json.Linq.JArray jarr) tagIds = jarr;
            else if (tagsObj is List<object> lst) tagIds = lst;

            if (tagIds != null)
            foreach (var tagObj in tagIds)
            {
                string tagId = tagObj != null ? tagObj.ToString() : "";
                if (!_tagsRegistry.ContainsKey(tagId))
                {
                    Debug.LogError("[SpiritSkillTrigger] 标签不存在: " + tagId);
                    continue;
                }
                var tagData = _tagsRegistry[tagId];
                var tagParams = _build_tag_params(tagData, skillData, playerId, targetData);

                Dictionary<string, object> result = null;
                if (_effectHandler != null)
                    result = _effectHandler.apply_tag_effect(tagId, tagParams, playerId);

                skill_effect_applied?.Invoke(skillId, tagId, result ?? new Dictionary<string, object>());
                _send_ui_feedback(tagData, result);
            }
        }

        protected virtual Dictionary<string, object> _build_tag_params(
            Dictionary<string, object> tagData, Dictionary<string, object> skillData,
            int playerId, Dictionary<string, object> targetData)
        {
            var tagParams = new Dictionary<string, object>();
            string tagId = GetStr(tagData, "id", "");
            if (skillData.ContainsKey("tag_params"))
            {
                // "tag_params" 可能是 JObject(Newtonsoft) 或 Dictionary<string,object>，统一转换
                Dictionary<string, object> allTagParams = null;
                var tpObj = skillData["tag_params"];
                if (tpObj is Newtonsoft.Json.Linq.JObject jobj)
                    allTagParams = jobj.ToObject<Dictionary<string, object>>();
                else
                    allTagParams = tpObj as Dictionary<string, object>;
                if (allTagParams != null && allTagParams.ContainsKey(tagId))
                {
                    var tp = allTagParams[tagId];
                    Dictionary<string, object> tpDict = null;
                    if (tp is Newtonsoft.Json.Linq.JObject jtp)
                        tpDict = jtp.ToObject<Dictionary<string, object>>();
                    else
                        tpDict = tp as Dictionary<string, object>;
                    if (tpDict != null) foreach (var kv in tpDict) tagParams[kv.Key] = kv.Value;
                }
            }
            tagParams["_caster_id"] = playerId;
            tagParams["_skill_id"] = GetStr(skillData, "id", "");
            tagParams["_element"] = GetStr(skillData, "element", "");
            tagParams["_target_data"] = targetData;
            return tagParams;
        }

        protected virtual bool _consume_energy(int playerId, int amount)
        {
            // TODO: 从玩家获取当前能量并扣除
            return true;
        }

        protected virtual void _set_skill_cooldown(int playerId, string skillId, float cooldown)
        {
            if (!_skillCooldowns.ContainsKey(playerId))
                _skillCooldowns[playerId] = new Dictionary<string, float>();
            _skillCooldowns[playerId][skillId] = cooldown;
        }

        // ============================================================
        // 查询
        // ============================================================
        public virtual float get_skill_cooldown(int playerId, string skillId)
        {
            if (_skillCooldowns.ContainsKey(playerId) && _skillCooldowns[playerId].ContainsKey(skillId))
                return _skillCooldowns[playerId][skillId];
            return 0f;
        }

        public virtual bool has_tag(string tagId) => _tagsRegistry.ContainsKey(tagId);

        public virtual Dictionary<string, object> get_tag_data(string tagId)
        {
            if (_tagsRegistry.ContainsKey(tagId)) return _tagsRegistry[tagId];
            return new Dictionary<string, object>();
        }

        // ============================================================
        // 回调
        // ============================================================
        protected virtual void _on_effect_applied(string tagId, Dictionary<string, object> effectData)
        {
            Debug.Log("[SpiritSkillTrigger] 效果已应用: " + tagId);
        }

        protected virtual void _on_effect_finished(string tagId, Dictionary<string, object> effectData)
        {
            Debug.Log("[SpiritSkillTrigger] 效果已结束: " + tagId);
        }

        protected virtual void _send_ui_feedback(Dictionary<string, object> tagData, Dictionary<string, object> effectResult)
        {
            var feedback = new Dictionary<string, object>
            {
                { "category", GetStr(tagData, "category", "") },
                { "sub_category", GetStr(tagData, "sub_category", "") },
                { "name", GetStr(tagData, "name", "") },
                { "target_type", GetStr(tagData, "target_type", "") },
                { "success", effectResult != null && effectResult.ContainsKey("success") && (bool)effectResult["success"] }
            };
            skill_ui_feedback?.Invoke("effect_applied", feedback);
        }

        // ============================================================
        // 工具
        // ============================================================
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
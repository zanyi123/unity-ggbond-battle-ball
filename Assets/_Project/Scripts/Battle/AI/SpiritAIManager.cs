// ================================================================
// 决竞球 Godot→Unity 翻译: SpiritAIManager.cs [BattleBall.Battle.AI]
// 源: E:/项目储存/决竞球battle-ball/scripts/battle/spirit_ai_manager.gd
// 集成备注:
//   1) 现有 AiProfile.Profile 缺少 skill_* 字段, 需后续补充:
//      skillThinkInterval, skillEnergyMin, skillUseThreshold,
//      skillSelectionTemperature, skillMistakeChance,
//      skillAttackIntentWeight, skillDefenseIntentWeight, skillSupportIntentWeight,
//      skillLateGameBonus, skillReserveWeight, skillExpectedFutureScore,
//      skillUncertaintyDiscount, skillOutnumberedBonus, skillLosingBonus,
//      skillLeadingPenalty, role
//   2) PlayerController 缺少 spirit_energy / max_spirit_energy 字段, 需后续补充
//   3) SpiritSystemManager 缺少 get_player_skills / get_skill_cooldown / use_skill 方法, 需后续补充
//   4) AiCommunication 缺少 try_send_message / record_message / has_need_buff / get_need_buff_sender / has_buff_on_you 方法, 需后续补充
//   5) InputManager 缺少 controlled_player 字段, 需后续补充
// ================================================================
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using BattleBall.Core;
using BattleBall.Battle;
using Random = UnityEngine.Random;
using BattleBall.Battle.AI;
// 跨程序集引用通过反射访问 SpiritSystemManager (Battle 程序集不引用 Systems 程序集)

namespace BattleBall.Battle.AI
{
    public class SpiritAIManager : MonoBehaviour
    {
        public static readonly Vector2 GOAL_A = new Vector2(3.0f, 0.0f);
        public static readonly Vector2 GOAL_B = new Vector2(-3.0f, 0.0f);

        public BattleManager battle_manager;
        public MonoBehaviour spirit_system;
        public AiManager ai_manager;
        public BallController ball_node;

        public List<Dictionary<string, object>> spirit_ai_data = new List<Dictionary<string, object>>();

        public Dictionary<string, List<string>> element_counters = new Dictionary<string, List<string>>();
        public float counter_multiplier = 1.3f;

        // ===== 数值/类型转换辅助 =====
        protected static float _F(object o) { if (o == null) return 0f; float r; return float.TryParse(o.ToString(), out r) ? r : 0f; }
        protected static int _I(object o) { if (o == null) return 0; int r; return int.TryParse(o.ToString(), out r) ? r : 0; }
        protected static string _S(object o) { return o == null ? "" : o.ToString(); }
        protected static bool _B(object o) { if (o == null) return false; if (o is bool b) return b; bool r; return bool.TryParse(o.ToString(), out r) ? r : false; }
        protected static List<string> _StrList(object o) {
            var r = new List<string>();
            if (o is IList l) { foreach (var x in l) if (x != null && !string.IsNullOrEmpty(x.ToString())) r.Add(x.ToString()); }
            else if (o is string s && !string.IsNullOrEmpty(s)) r.Add(s);
            return r;
        }
        protected static Dictionary<string, object> _Dict(object o) {
            if (o is IDictionary<string, object> d) return new Dictionary<string, object>(d);
            return new Dictionary<string, object>();
        }
        // Vector2 与 Unity XZ 平面互转 (Unity 用 XZ 平面做场地)
        protected static Vector2 _XZ(Transform t) { return t == null ? Vector2.zero : new Vector2(t.position.x, t.position.z); }
        protected static Vector3 _FromXZ(Vector2 v) { return new Vector3(v.x, 0f, v.y); }

        public virtual void initialize(BattleManager battle_mgr, MonoBehaviour spirit_sys, AiManager ai_mgr)
        {
            battle_manager = battle_mgr;
            spirit_system = spirit_sys;
            ai_manager = ai_mgr;
            _load_element_counters();
            Debug.Log("[SpiritAI] 初始化完成");
        }

        public virtual void _load_element_counters()
        {
            var dm = DataManager.Instance;
            if (dm == null) return;
            var elements_data = dm.elements;
            if (elements_data == null || elements_data.Count == 0) return;

            if (elements_data.TryGetValue("counter_multiplier", out var cmObj) && cmObj != null)
            {
                if (float.TryParse(cmObj.ToString(), out var cm)) counter_multiplier = cm;
            }

            if (elements_data.TryGetValue("counters", out var cObj) && cObj is IList counters)
            {
                foreach (var entry in counters)
                {
                    if (entry is IDictionary<string, object> cd)
                    {
                        string attacker = cd.TryGetValue("attacker", out var a) && a != null ? a.ToString() : "";
                        string defender = cd.TryGetValue("defender", out var d) && d != null ? d.ToString() : "";
                        if (!string.IsNullOrEmpty(attacker) && !string.IsNullOrEmpty(defender))
                        {
                            if (!element_counters.ContainsKey(attacker))
                                element_counters[attacker] = new List<string>();
                            element_counters[attacker].Add(defender);
                        }
                    }
                }
            }
        }

        public virtual void register_player(PlayerController player, AiProfile.Profile profile)
        {
            var player_analysis = _analyze_player_attributes(player);
            var skills_analysis = new List<Dictionary<string, object>>();
            spirit_ai_data.Add(new Dictionary<string, object> {
                { "player", player },
                { "profile", profile },
                { "skill_think_timer", UnityEngine.Random.value * profile.skillThinkInterval },
                { "last_skill_use_time", 0.0f },
                { "player_analysis", player_analysis },
                { "skills_analysis", skills_analysis },
            });
            var weakness = player_analysis.TryGetValue("weaknesses", out var w) ? w : new List<string>();
            Debug.Log("[SpiritAI] 注册球员: " + player.name + ", 弱点: " + (weakness == null ? "[]" : weakness.ToString()));
        }

        public virtual void refresh_all_skills_analysis()
        {
            if (battle_manager != null)
            {
                var sm = GameObject.FindObjectOfType<MonoBehaviour>();
                if (sm != null) spirit_system = sm;
            }
            if (spirit_system == null)
            {
                Debug.Log("[SpiritAI] 警告: spirit_system 为空，无法分析技能");
                return;
            }
            foreach (var sad in spirit_ai_data)
            {
                var p = sad["player"] as PlayerController;
                sad["skills_analysis"] = _analyze_skills_for_player(p, _Dict(sad["player_analysis"]));
                var sa = sad["skills_analysis"] as List<Dictionary<string, object>>;
                Debug.Log("[SpiritAI] " + p.name + " 技能分析完成，技能数: " + (sa != null ? sa.Count : 0));
            }
        }

        public virtual Dictionary<string, object> _analyze_player_attributes(PlayerController player)
        {
            var result = new Dictionary<string, object> {
                { "weaknesses", new List<string>() },
                { "strengths", new List<string>() },
                { "risk_level", "medium" },
                { "risk_score", 0.0f },
            };

            float attack = player.attackPower;
            float defense = player.defense;
            float speed = player.moveSpeed;
            float stamina = player.stamina;
            float resilience = player.resilience;

            float max_attr = Mathf.Max(attack, Mathf.Max(defense, Mathf.Max(speed, Mathf.Max(stamina, resilience))));

            float risk_score = 0f;
            var weaknesses = result["weaknesses"] as List<string>;
            var strengths = result["strengths"] as List<string>;

            if (defense < max_attr * 0.5f) { weaknesses.Add("defense_low"); risk_score += 0.4f; }
            if (stamina < max_attr * 0.5f)  { weaknesses.Add("stamina_low");  risk_score += 0.3f; }
            if (resilience < max_attr * 0.5f) { weaknesses.Add("resilience_low"); risk_score += 0.3f; }

            if (attack  > max_attr * 0.8f) strengths.Add("attack_high");
            if (defense > max_attr * 0.8f) strengths.Add("defense_high");
            if (speed   > max_attr * 0.8f) strengths.Add("speed_high");

            risk_score = Mathf.Clamp(risk_score, 0.0f, 1.0f);
            result["risk_score"] = risk_score;

            if (risk_score > 0.6f) result["risk_level"] = "high";
            else if (risk_score < 0.3f) result["risk_level"] = "low";

            result["base_attack"] = attack;
            result["base_defense"] = defense;
            result["base_speed"] = speed;
            result["base_stamina"] = stamina;
            result["base_resilience"] = resilience;

            return result;
        }

        public virtual List<Dictionary<string, object>> _analyze_skills_for_player(PlayerController player, Dictionary<string, object> player_analysis)
        {
            var result = new List<Dictionary<string, object>>();
            if (spirit_system == null) return result;
            var player_skills = _CallSpiritMethod("get_player_skills", player.GetInstanceID()) as System.Collections.IEnumerable;
            if (player_skills == null) return result;
            foreach (var skill_id in player_skills)
            {
                var dm = DataManager.Instance;
                if (dm == null) continue;
                var skill_data = dm.get_skill_by_id(skill_id == null ? "" : skill_id.ToString());
                if (skill_data == null || skill_data.Count == 0) continue;
                var analysis = _analyze_single_skill(skill_data, player_analysis);
                analysis["skill_id"] = skill_id;
                analysis["skill_data"] = skill_data;
                result.Add(analysis);
            }

            _analyze_skill_combinations(result);

            return result;
        }

        public virtual void _analyze_skill_combinations(List<Dictionary<string, object>> skills_analysis)
        {
            for (int i = 0; i < skills_analysis.Count; i++)
            {
                var skill_a = skills_analysis[i];
                skill_a["combos"] = new List<Dictionary<string, object>>();

                for (int j = 0; j < skills_analysis.Count; j++)
                {
                    if (i == j) continue;
                    var skill_b = skills_analysis[j];
                    string combo_type = _detect_combo_type(skill_a, skill_b);
                    if (!string.IsNullOrEmpty(combo_type))
                    {
                        (skill_a["combos"] as List<Dictionary<string, object>>).Add(new Dictionary<string, object> {
                            { "skill_id", skill_b["skill_id"] },
                            { "combo_type", combo_type },
                            { "bonus", _get_combo_bonus(combo_type) },
                        });
                    }
                }
            }
        }

        public virtual string _detect_combo_type(Dictionary<string, object> skill_a, Dictionary<string, object> skill_b)
        {
            var tags_a = _StrList(skill_a.TryGetValue("tags", out var ta) ? ta : null);
            var tags_b = _StrList(skill_b.TryGetValue("tags", out var tb) ? tb : null);
            var intents_a = _Dict(skill_a.TryGetValue("intents", out var ia) ? ia : null);
            var intents_b = _Dict(skill_b.TryGetValue("intents", out var ib) ? ib : null);

            if (_F(intents_a.TryGetValue("control", out var ca) ? ca : 0) > 0.5f && _F(intents_b.TryGetValue("attack", out var ab) ? ab : 0) > 0.5f)
            {
                if (tags_a.Contains("on_player") && tags_b.Contains("on_ball")) return "combo_control_attack";
                if (tags_a.Contains("on_field") && tags_b.Contains("on_ball")) return "combo_control_attack";
            }

            if (_F(intents_a.TryGetValue("support", out var sa) ? sa : 0) > 0.5f && _F(intents_b.TryGetValue("attack", out var ab2) ? ab2 : 0) > 0.5f)
                return "combo_buff_attack";

            if (_F(intents_a.TryGetValue("defense", out var da) ? da : 0) > 0.5f && _F(intents_b.TryGetValue("control", out var cb2) ? cb2 : 0) > 0.5f)
                return "combo_defense_control";

            return "";
        }

        public virtual float _get_combo_bonus(string combo_type)
        {
            switch (combo_type)
            {
                case "combo_control_attack": return 0.4f;
                case "combo_buff_attack": return 0.3f;
                case "combo_defense_control": return 0.25f;
            }
            return 0.0f;
        }

        public virtual Dictionary<string, object> _analyze_single_skill(Dictionary<string, object> skill_data, Dictionary<string, object> player_analysis)
        {
            var result = new Dictionary<string, object>();
            var tags = _normalize_tags(skill_data.TryGetValue("tags", out var td) ? td : null);
            var tag_params = _Dict(skill_data.TryGetValue("tag_params", out var tp) ? tp : null);
            float energy_cost = _F(skill_data.TryGetValue("energy_cost", out var ec) ? ec : 20);
            float cooldown = _F(skill_data.TryGetValue("cooldown", out var cd) ? cd : 5.0f);

            result["tags"] = tags;
            result["tag_count"] = tags.Count;
            result["has_ball_tag"] = tags.Contains("on_ball");
            result["has_player_tag"] = tags.Contains("on_player");
            result["has_field_tag"] = tags.Contains("on_field");
            result["base_value"] = _compute_base_value(tags, tag_params, (int)energy_cost, cooldown);
            result["intents"] = _determine_intents(tags, tag_params);
            result["primary_intent"] = _get_primary_intent(_Dict(result["intents"]));
            result["synergy_level"] = _compute_synergy_level(tags, tag_params, player_analysis);
            result["synergy_bonus"] = _compute_synergy_bonus(_S(result["synergy_level"]));
            return result;
        }

        public virtual string _compute_synergy_level(List<string> tags, Dictionary<string, object> values, Dictionary<string, object> player_analysis)
        {
            var weaknesses = _StrList(player_analysis.TryGetValue("weaknesses", out var w) ? w : null);
            var strengths = _StrList(player_analysis.TryGetValue("strengths", out var s) ? s : null);

            float synergy_score = 0.0f;

            foreach (var tag in tags)
            {
                switch (tag)
                {
                    case "on_player":
                        if (values.ContainsKey("defense_bonus") || values.ContainsKey("shield_hp") || values.ContainsKey("damage_reduction"))
                        {
                            if (weaknesses.Contains("defense_low")) synergy_score += 0.8f;
                            else if (strengths.Contains("defense_high")) synergy_score += 0.4f;
                            else synergy_score += 0.2f;
                        }
                        if (values.ContainsKey("slow_percent") || values.ContainsKey("root_duration"))
                        {
                            if (strengths.Contains("speed_high")) synergy_score += 0.3f;
                        }
                        break;
                    case "on_ball":
                        if (values.ContainsKey("damage_bonus"))
                        {
                            if (strengths.Contains("attack_high")) synergy_score += 0.5f;
                            else synergy_score += 0.2f;
                        }
                        if (values.ContainsKey("speed_multiplier"))
                        {
                            if (strengths.Contains("speed_high")) synergy_score += 0.4f;
                        }
                        break;
                    case "on_field":
                        if (values.ContainsKey("wall_width"))
                        {
                            if (weaknesses.Contains("defense_low")) synergy_score += 0.6f;
                            else synergy_score += 0.2f;
                        }
                        break;
                }
            }

            synergy_score = Mathf.Clamp(synergy_score, 0.0f, 1.0f);

            if (synergy_score >= 0.7f) return "critical";
            else if (synergy_score >= 0.4f) return "high";
            else if (synergy_score >= 0.2f) return "medium";
            else return "low";
        }

        public virtual float _compute_synergy_bonus(string synergy_level)
        {
            switch (synergy_level)
            {
                case "critical": return 1.5f;
                case "high": return 1.25f;
                case "medium": return 1.0f;
                case "low": return 0.8f;
            }
            return 1.0f;
        }

        public virtual List<string> _normalize_tags(object tag_data)
        {
            var raw_tags = new List<string>();

            if (tag_data is string s && !string.IsNullOrEmpty(s))
            {
                raw_tags.Add(s);
            }
            else if (tag_data is IList l)
            {
                foreach (var t in l)
                {
                    if (t is string ts && !string.IsNullOrEmpty(ts)) raw_tags.Add(ts);
                    else if (t != null && !string.IsNullOrEmpty(t.ToString())) raw_tags.Add(t.ToString());
                }
            }

            return _map_tags_to_categories(raw_tags);
        }

        public virtual List<string> _map_tags_to_categories(List<string> raw_tags)
        {
            var result = new List<string>();
            bool has_ball = false, has_player = false, has_field = false;

            foreach (var tag in raw_tags)
            {
                string category = _get_tag_category(tag);
                switch (category)
                {
                    case "BALL": has_ball = true; break;
                    case "PLAYER": has_player = true; break;
                    case "FIELD": has_field = true; break;
                }
            }

            if (has_ball) result.Add("on_ball");
            if (has_player) result.Add("on_player");
            if (has_field) result.Add("on_field");

            if (result.Count == 0 && raw_tags.Count > 0)
            {
                foreach (var tag in raw_tags)
                {
                    if (!result.Contains(tag)) result.Add(tag);
                }
            }

            return result;
        }

        public virtual string _get_tag_category(string tag_id)
        {
            var dm = DataManager.Instance;
            if (dm != null && dm.tags != null)
            {
                foreach (var tag_entry in dm.tags)
                {
                    if (tag_entry.TryGetValue("id", out var id) && id != null && id.ToString() == tag_id)
                    {
                        return tag_entry.TryGetValue("category", out var cat) && cat != null ? cat.ToString() : "";
                    }
                }
            }

            if (tag_id.StartsWith("ball_")) return "BALL";
            else if (tag_id.StartsWith("player_")) return "PLAYER";
            else if (tag_id.StartsWith("field_")) return "FIELD";

            return "";
        }

        public virtual Dictionary<string, object> _extract_values_from_tag_params(Dictionary<string, object> tag_params)
        {
            var values = new Dictionary<string, object>();
            foreach (var kv in tag_params)
            {
                var @params = _Dict(kv.Value);
                foreach (var pk in @params)
                {
                    if (!values.ContainsKey(pk.Key))
                    {
                        values[pk.Key] = pk.Value;
                    }
                    else
                    {
                        var existing = _F(values[pk.Key]);
                        var incoming = _F(pk.Value);
                        if (incoming > existing) values[pk.Key] = incoming;
                    }
                }
            }
            return values;
        }

        public virtual float _compute_base_value(List<string> tags, Dictionary<string, object> tag_params, int energy_cost, float cooldown)
        {
            if (tags.Count == 0) return 10.0f;

            float total_value = 0.0f;
            int tag_count = tags.Count;

            float tag_weight;
            switch (tag_count)
            {
                case 1: tag_weight = 1.0f; break;
                case 2: tag_weight = 0.7f; break;
                case 3: tag_weight = 0.5f; break;
                default: tag_weight = 0.4f; break;
            }

            foreach (var tag in tags)
            {
                float tag_value = 0.0f;
                switch (tag)
                {
                    case "on_ball": tag_value = _compute_ball_value(tag_params); break;
                    case "on_player": tag_value = _compute_player_value(tag_params); break;
                    case "on_field": tag_value = _compute_field_value(tag_params); break;
                    default: tag_value = 10.0f; break;
                }
                total_value += tag_value;
            }

            float combined_value = total_value * tag_weight;
            float efficiency = combined_value / Mathf.Max(energy_cost, 1);
            float cd_factor = Mathf.Clamp(10.0f / Mathf.Max(cooldown, 1.0f), 0.3f, 2.0f);

            return Mathf.Clamp(combined_value * cd_factor * 0.5f + efficiency * 5.0f, 10.0f, 100.0f);
        }

        public virtual float _compute_ball_value(Dictionary<string, object> tag_params)
        {
            float value = 0.0f;

            foreach (var kv in tag_params)
            {
                var tag_id = kv.Key;
                var @params = _Dict(kv.Value);
                float tag_value = _F(@params.TryGetValue("value", out var v) ? v : 0);
                float multiplier = _F(@params.TryGetValue("multiplier", out var m) ? m : 0);
                float duration = _F(@params.TryGetValue("duration", out var d) ? d : 0);

                if (tag_id.StartsWith("ball_dmg_up")) value += tag_value * 1.5f;
                else if (tag_id.StartsWith("ball_speed_up")) value += multiplier * 1.0f;
                else if (tag_id.StartsWith("ball_range_up")) value += tag_value * 0.5f;
                else if (tag_id.StartsWith("ball_dmg_down")) value += tag_value * 0.8f;
                else if (tag_id.StartsWith("ball_slow")) { value += multiplier * 0.8f; value += duration * 5.0f; }
                else if (tag_id.StartsWith("ball_knockback")) value += tag_value * 0.5f;
                else if (tag_id.StartsWith("ball_burn")) value += duration * 8.0f;
                else if (tag_id.StartsWith("ball_deception")) value += 25.0f;
                else if (tag_id.StartsWith("ball_split")) value += tag_value * 20.0f;
            }

            return Mathf.Max(value, 15.0f);
        }

        public virtual float _compute_player_value(Dictionary<string, object> tag_params)
        {
            float value = 0.0f;

            foreach (var kv in tag_params)
            {
                var tag_id = kv.Key;
                var @params = _Dict(kv.Value);
                float tag_value = _F(@params.TryGetValue("value", out var v) ? v : 0);
                float multiplier = _F(@params.TryGetValue("multiplier", out var m) ? m : 0);
                float duration = _F(@params.TryGetValue("duration", out var d) ? d : 0);

                if (tag_id.StartsWith("player_def_up")) { value += tag_value * 1.2f; value += duration * 2.0f; }
                else if (tag_id.StartsWith("player_hp_regen")) { value += tag_value * 3.0f; value += duration * 3.0f; }
                else if (tag_id.StartsWith("player_spd_up")) { value += tag_value * 0.8f; value += duration * 2.0f; }
                else if (tag_id.StartsWith("player_move_slow")) { value += multiplier * 1.0f; value += duration * 8.0f; }
                else if (tag_id.StartsWith("player_root")) value += duration * 20.0f;
                else if (tag_id.StartsWith("player_damage_reduction")) value += tag_value * 1.5f;
                else if (tag_id.StartsWith("player_stealth")) { value += 30.0f; value += duration * 5.0f; }
                else if (tag_id.StartsWith("player_shield")) value += tag_value * 1.8f;
            }

            return Mathf.Max(value, 15.0f);
        }

        public virtual float _compute_field_value(Dictionary<string, object> tag_params)
        {
            float value = 0.0f;

            foreach (var kv in tag_params)
            {
                var tag_id = kv.Key;
                var @params = _Dict(kv.Value);
                float tag_value = _F(@params.TryGetValue("value", out var v) ? v : 0);
                float multiplier = _F(@params.TryGetValue("multiplier", out var m) ? m : 0);
                float duration = _F(@params.TryGetValue("duration", out var d) ? d : 0);
                float width = _F(@params.TryGetValue("width", out var w) ? w : 0);
                float height = _F(@params.TryGetValue("height", out var h) ? h : 0);
                float radius = _F(@params.TryGetValue("radius", out var r) ? r : 0);
                float hp = _F(@params.TryGetValue("hp", out var hpv) ? hpv : 0);

                if (tag_id.StartsWith("field_obs_add")) { value += width * 0.3f; value += height * 0.2f; value += hp * 0.1f; value += duration * 3.0f; }
                else if (tag_id.StartsWith("field_slow_zone")) { value += radius * 0.4f; value += multiplier * 0.8f; value += duration * 4.0f; }
                else if (tag_id.StartsWith("field_stun")) { value += radius * 0.5f; value += duration * 25.0f; }
                else if (tag_id.StartsWith("field_clone")) value += tag_value * 30.0f;
            }

            return Mathf.Max(value, 15.0f);
        }

        public virtual Dictionary<string, object> _determine_intents(List<string> tags, Dictionary<string, object> tag_params)
        {
            var intents = new Dictionary<string, object> {
                { "attack", 0.0f },
                { "defense", 0.0f },
                { "support", 0.0f },
                { "control", 0.0f },
            };

            if (tags.Count == 0) return intents;

            int tag_count = tags.Count;
            float tag_weight;
            switch (tag_count)
            {
                case 1: tag_weight = 1.0f; break;
                case 2: tag_weight = 0.7f; break;
                case 3: tag_weight = 0.5f; break;
                default: tag_weight = 0.4f; break;
            }

            foreach (var tag in tags)
            {
                var sub_intents = _compute_tag_intents(tag, tag_params);
                foreach (var kv in sub_intents)
                {
                    float existing = _F(intents.TryGetValue(kv.Key, out var e) ? e : 0);
                    intents[kv.Key] = existing + _F(kv.Value) * tag_weight;
                }
            }

            float total = _F(intents["attack"]) + _F(intents["defense"]) + _F(intents["support"]) + _F(intents["control"]);
            if (total > 0)
            {
                intents["attack"] = _F(intents["attack"]) / total;
                intents["defense"] = _F(intents["defense"]) / total;
                intents["support"] = _F(intents["support"]) / total;
                intents["control"] = _F(intents["control"]) / total;
            }

            return intents;
        }

        public virtual Dictionary<string, object> _compute_tag_intents(string tag, Dictionary<string, object> tag_params)
        {
            var intents = new Dictionary<string, object> {
                { "attack", 0.0f },
                { "defense", 0.0f },
                { "support", 0.0f },
                { "control", 0.0f },
            };

            foreach (var kv in tag_params)
            {
                string tag_id = kv.Key;
                if (tag_id.StartsWith("ball_dmg_up") || tag_id.StartsWith("ball_speed_up") || tag_id.StartsWith("ball_range_up"))
                    intents["attack"] = Mathf.Max(_F(intents["attack"]), 0.8f);
                else if (tag_id.StartsWith("ball_dmg_down") || tag_id.StartsWith("ball_slow"))
                    intents["control"] = Mathf.Max(_F(intents["control"]), 0.5f);
                else if (tag_id.StartsWith("ball_deception"))
                    intents["control"] = Mathf.Max(_F(intents["control"]), 0.5f);
                else if (tag_id.StartsWith("ball_split"))
                    intents["attack"] = Mathf.Max(_F(intents["attack"]), 1.0f);
                else if (tag_id.StartsWith("player_def_up") || tag_id.StartsWith("player_shield"))
                    intents["defense"] = Mathf.Max(_F(intents["defense"]), 0.8f);
                else if (tag_id.StartsWith("player_hp_regen"))
                    intents["support"] = Mathf.Max(_F(intents["support"]), 0.8f);
                else if (tag_id.StartsWith("player_spd_up"))
                    intents["support"] = Mathf.Max(_F(intents["support"]), 0.5f);
                else if (tag_id.StartsWith("player_move_slow") || tag_id.StartsWith("player_root"))
                    intents["control"] = Mathf.Max(_F(intents["control"]), 0.7f);
                else if (tag_id.StartsWith("player_stealth"))
                {
                    intents["control"] = Mathf.Max(_F(intents["control"]), 0.5f);
                    intents["defense"] = Mathf.Max(_F(intents["defense"]), 0.3f);
                }
                else if (tag_id.StartsWith("field_obs_add"))
                    intents["defense"] = Mathf.Max(_F(intents["defense"]), 0.9f);
                else if (tag_id.StartsWith("field_slow"))
                {
                    intents["control"] = Mathf.Max(_F(intents["control"]), 0.6f);
                    intents["defense"] = Mathf.Max(_F(intents["defense"]), 0.5f);
                }
                else if (tag_id.StartsWith("field_stun"))
                    intents["control"] = Mathf.Max(_F(intents["control"]), 0.9f);
                else if (tag_id.StartsWith("field_clone"))
                {
                    intents["attack"] = Mathf.Max(_F(intents["attack"]), 0.5f);
                    intents["support"] = Mathf.Max(_F(intents["support"]), 0.3f);
                }
            }

            if (_F(intents["attack"]) == 0 && _F(intents["defense"]) == 0 && _F(intents["support"]) == 0 && _F(intents["control"]) == 0)
            {
                switch (tag)
                {
                    case "on_ball": intents["attack"] = 0.8f; break;
                    case "on_player": intents["support"] = 0.5f; break;
                    case "on_field": intents["control"] = 0.6f; break;
                }
            }

            return intents;
        }

        public virtual string _get_primary_intent(Dictionary<string, object> intents)
        {
            float max_val = -1.0f;
            string primary = "attack";
            foreach (var kv in intents)
            {
                float v = _F(kv.Value);
                if (v > max_val) { max_val = v; primary = kv.Key; }
            }
            return primary;
        }

        protected virtual void FixedUpdate()
        {
            float delta = Time.fixedDeltaTime;
            if (battle_manager == null || !battle_manager.match_started) return;

            if (ball_node == null)
            {
                var bc = battle_manager.ballNode != null ? battle_manager.ballNode.GetComponent<BallController>() : null;
                if (bc != null) ball_node = bc;
            }
            if (ball_node == null) return;

            foreach (var sad in spirit_ai_data)
            {
                if (!_is_valid(sad)) continue;

                float timer = _F(sad.TryGetValue("skill_think_timer", out var t) ? t : 0);
                var profile = sad["profile"] as AiProfile.Profile;
                timer += delta;
                sad["skill_think_timer"] = timer;
                if (timer >= profile.skillThinkInterval)
                {
                    sad["skill_think_timer"] = 0.0f;
                    _decide_skill(sad);
                    _try_send_need_buff(sad);
                }
            }
        }

        public virtual bool _is_valid(Dictionary<string, object> sad)
        {
            var p = sad["player"] as PlayerController;
            if (p == null) return false;
            if (ai_manager != null && ai_manager.input_manager != null && ai_manager.input_manager.controlled_player == p.transform) return false;
            if (p.state == PlayerState.Defeated) return false;
            return true;
        }

        public virtual void _decide_skill(Dictionary<string, object> sad)
        {
            var p = sad["player"] as PlayerController;
            var profile = sad["profile"] as AiProfile.Profile;

            if (p.spirit_energy < profile.skillEnergyMin) return;

            if (!_should_think_about_skills(sad)) return;

            var available_skills = _get_available_skills(sad);
            if (available_skills.Count == 0) return;

            var scored_skills = new List<Dictionary<string, object>>();
            foreach (var skill_info in available_skills)
            {
                float score = _compute_skill_score(sad, skill_info);
                if (score > 0)
                {
                    scored_skills.Add(new Dictionary<string, object> {
                        { "skill", skill_info },
                        { "score", score },
                    });
                }
            }

            if (scored_skills.Count == 0) return;

            var best_skill = _select_skill_with_softmax(scored_skills, profile.skillSelectionTemperature);

            if (best_skill != null && _F(best_skill["score"]) >= profile.skillUseThreshold)
                _execute_skill(sad, _Dict(best_skill["skill"]));
        }

        public virtual Dictionary<string, object> _select_skill_with_softmax(List<Dictionary<string, object>> scored_skills, float temperature)
        {
            if (scored_skills.Count == 1) return scored_skills[0];

            if (temperature <= 0.01f)
            {
                var best = scored_skills[0];
                foreach (var s in scored_skills)
                {
                    if (_F(s["score"]) > _F(best["score"])) best = s;
                }
                return best;
            }

            float max_score = -float.MaxValue;
            foreach (var s in scored_skills)
            {
                if (_F(s["score"]) > max_score) max_score = _F(s["score"]);
            }

            var exp_scores = new List<float>();
            float total_exp = 0.0f;
            foreach (var s in scored_skills)
            {
                float exp_val = Mathf.Exp((_F(s["score"]) - max_score) / temperature);
                exp_scores.Add(exp_val);
                total_exp += exp_val;
            }

            float r = UnityEngine.Random.value * total_exp;
            float cum = 0.0f;
            for (int i = 0; i < scored_skills.Count; i++)
            {
                cum += exp_scores[i];
                if (r <= cum) return scored_skills[i];
            }

            return scored_skills[0];
        }

        public virtual bool _should_think_about_skills(Dictionary<string, object> sad)
        {
            var p = sad["player"] as PlayerController;
            var profile = sad["profile"] as AiProfile.Profile;

            if (p.HasBall()) return true;

            if (ball_node != null && ball_node.owner != null)
            {
                var owner_pc = ball_node.owner.GetComponent<PlayerController>();
                if (owner_pc != null && owner_pc.team == p.team) return true;
            }

            if (ball_node != null && ball_node.isFlying) return true;

            if (profile.role == "defender") return true;

            var sa = sad.TryGetValue("skills_analysis", out var s) ? s as List<Dictionary<string, object>> : null;
            if (sa != null)
            {
                foreach (var analysis in sa)
                {
                    if (_B(analysis.TryGetValue("has_field_tag", out var hf) ? hf : false)) return true;
                }
            }

            return false;
        }

        public virtual List<Dictionary<string, object>> _get_available_skills(Dictionary<string, object> sad)
        {
            var result = new List<Dictionary<string, object>>();
            var p = sad["player"] as PlayerController;
            var sa = sad.TryGetValue("skills_analysis", out var s) ? s as List<Dictionary<string, object>> : null;
            if (sa == null) return result;
            foreach (var analysis in sa)
            {
                string skill_id = _S(analysis["skill_id"]);
                float cd = _CallSpiritMethodFloat("get_skill_cooldown", p.GetInstanceID(), skill_id);
                var skill_data = _Dict(analysis.TryGetValue("skill_data", out var sd) ? sd : null);
                float energy_cost = _F(skill_data.TryGetValue("energy_cost", out var ec) ? ec : 20);
                if (cd <= 0.0f && p.spirit_energy >= energy_cost) result.Add(analysis);
            }
            return result;
        }

        public virtual float _compute_skill_score(Dictionary<string, object> sad, Dictionary<string, object> skill_info)
        {
            float base_value = _F(skill_info["base_value"]);
            float situation_factor = _compute_situation_factor(sad, skill_info);
            float intent_match = _compute_intent_match(sad, skill_info);
            float synergy_bonus = _F(skill_info.TryGetValue("synergy_bonus", out var sb) ? sb : 1.0f);
            float stamina_factor = _compute_stamina_factor(sad, skill_info);
            float time_factor = _compute_time_factor(sad, skill_info);
            float comm_factor = _compute_communication_factor(sad, skill_info);
            float element_factor = _compute_element_factor(sad, skill_info);
            float combo_factor = _compute_combo_factor(sad, skill_info);
            float team_factor = _compute_team_factor(sad, skill_info);

            float raw_score = base_value * situation_factor * intent_match * synergy_bonus * stamina_factor * time_factor * comm_factor * element_factor * combo_factor * team_factor;

            if (!_should_use_energy(sad, skill_info, raw_score)) return 0.0f;

            return raw_score;
        }

        public virtual float _compute_team_factor(Dictionary<string, object> sad, Dictionary<string, object> skill_info)
        {
            var p = sad["player"] as PlayerController;

            var team_weaknesses = _get_team_weaknesses(p.team);
            if (team_weaknesses.Count == 0) return 1.0f;

            var intents = _Dict(skill_info["intents"]);
            float factor = 1.0f;

            if (_F(intents.TryGetValue("defense", out var d) ? d : 0) > 0.3f || _F(intents.TryGetValue("support", out var su) ? su : 0) > 0.3f)
            {
                if (team_weaknesses.Contains("team_defense_low")) factor *= 1.2f;
                if (team_weaknesses.Contains("team_stamina_low")) factor *= 1.15f;
            }

            if (_F(intents.TryGetValue("attack", out var a) ? a : 0) > 0.3f)
            {
                if (team_weaknesses.Contains("team_attack_low")) factor *= 1.1f;
            }

            return factor;
        }

        public virtual List<string> _get_team_weaknesses(string team)
        {
            var result = new List<string>();
            if (ai_manager == null) return result;

            float attack_sum = 0, defense_sum = 0, stamina_sum = 0;
            int count = 0;

            foreach (var ap in ai_manager.ai_players)
            {
                var member = ap.player != null ? ap.player.GetComponent<PlayerController>() : null;
                if (member == null || member.team != team) continue;
                attack_sum += member.attackPower;
                defense_sum += member.defense;
                stamina_sum += member.stamina;
                count += 1;
            }

            if (count == 0) return result;

            float avg_attack = attack_sum / count;
            float avg_defense = defense_sum / count;
            float avg_stamina = stamina_sum / count;

            float max_attr = Mathf.Max(avg_attack, Mathf.Max(avg_defense, avg_stamina));

            if (avg_defense < max_attr * 0.6f) result.Add("team_defense_low");
            if (avg_stamina < max_attr * 0.6f) result.Add("team_stamina_low");
            if (avg_attack < max_attr * 0.6f) result.Add("team_attack_low");

            return result;
        }

        public virtual float _compute_combo_factor(Dictionary<string, object> sad, Dictionary<string, object> skill_info)
        {
            var p = sad["player"] as PlayerController;
            var combos = skill_info.TryGetValue("combos", out var c) ? c as List<Dictionary<string, object>> : null;
            if (combos == null || combos.Count == 0) return 1.0f;

            float factor = 1.0f;
            foreach (var combo in combos)
            {
                string combo_skill_id = _S(combo["skill_id"]);
                float cd = _CallSpiritMethodFloat("get_skill_cooldown", p.GetInstanceID(), combo_skill_id);
                if (cd <= 0.0f) factor += _F(combo["bonus"]);
            }

            return factor;
        }

        public virtual float _compute_element_factor(Dictionary<string, object> sad, Dictionary<string, object> skill_info)
        {
            var p = sad["player"] as PlayerController;
            var skill_data = _Dict(skill_info.TryGetValue("skill_data", out var sd) ? sd : null);
            string skill_element = _S(skill_data.TryGetValue("element", out var el) ? el : "");

            if (string.IsNullOrEmpty(skill_element) || element_counters.Count == 0) return 1.0f;

            var target = _select_player_target(sad, skill_info);
            if (target == null || target == p) return 1.0f;

            var dm = DataManager.Instance;
            if (dm == null) return 1.0f;
            var target_spirit_data = dm.get_spirit_by_id(target.charId);
            if (target_spirit_data == null || target_spirit_data.Count == 0) return 1.0f;

            string target_element = _S(target_spirit_data.TryGetValue("element", out var te) ? te : "");
            if (string.IsNullOrEmpty(target_element)) return 1.0f;

            if (element_counters.TryGetValue(skill_element, out var counters))
            {
                if (counters.Contains(target_element)) return counter_multiplier;
            }

            return 1.0f;
        }

        public virtual float _compute_communication_factor(Dictionary<string, object> sad, Dictionary<string, object> skill_info)
        {
            var p = sad["player"] as PlayerController;

            if (battle_manager == null || battle_manager.comm_system == null) return 1.0f;

            var intents = _Dict(skill_info["intents"]);
            float factor = 1.0f;
            var comm = battle_manager.comm_system;

            if (_F(intents.TryGetValue("support", out var su) ? su : 0) > 0.3f)
            {
                if (comm.has_need_buff(p.team))
                {
                    var need_buff_sender = comm.get_need_buff_sender(p.team);
                    if (need_buff_sender != null && Vector2.Distance(_XZ(p.transform), _XZ(need_buff_sender)) < 1.5f)
                        factor *= 1.4f;
                }
            }

            if (_F(intents.TryGetValue("attack", out var a) ? a : 0) > 0.5f)
            {
                if (comm.has_buff_on_you(p.transform)) factor *= 1.25f;
            }

            return factor;
        }

        public virtual float _compute_time_factor(Dictionary<string, object> sad, Dictionary<string, object> skill_info)
        {
            var profile = sad["profile"] as AiProfile.Profile;

            float remaining_time = 0.0f;
            float total_time = 180.0f;

            var gm = GameManager.Instance;
            if (gm != null)
            {
                remaining_time = gm.match_time;
                if (gm.match_phase == BattleBall.Core.MatchPhase.FIRST_HALF) total_time = gm.get_first_half_duration();
                else if (gm.match_phase == BattleBall.Core.MatchPhase.SECOND_HALF) total_time = gm.get_second_half_duration();
            }

            float time_ratio = 1.0f - remaining_time / Mathf.Max(total_time, 1);
            time_ratio = Mathf.Clamp(time_ratio, 0.0f, 1.0f);

            if (time_ratio < 0.3f) return 0.85f;
            else if (time_ratio < 0.7f) return 1.0f;
            else
            {
                float remaining_ratio = 1.0f - time_ratio;
                return 1.0f + (1.0f - remaining_ratio) * (profile.skillLateGameBonus - 1.0f);
            }
        }

        public virtual bool _should_use_energy(Dictionary<string, object> sad, Dictionary<string, object> skill_info, float current_score)
        {
            var p = sad["player"] as PlayerController;
            var profile = sad["profile"] as AiProfile.Profile;

            var skill_data = _Dict(skill_info.TryGetValue("skill_data", out var sd) ? sd : null);
            float energy_cost = _F(skill_data.TryGetValue("energy_cost", out var ec) ? ec : 20);
            float current_energy = p.spirit_energy;
            float energy_ratio = (current_energy - energy_cost) / Mathf.Max(p.max_spirit_energy, 1);

            float reserve_weight = profile.skillReserveWeight;

            float future_value = profile.skillExpectedFutureScore * (1.0f - energy_ratio * reserve_weight);
            float current_value = current_score;

            if (current_value >= future_value) return true;

            float uncertainty_discount = profile.skillUncertaintyDiscount;
            if (current_value * (1.0f - uncertainty_discount) >= future_value * uncertainty_discount) return true;

            return false;
        }

        public virtual float _compute_situation_factor(Dictionary<string, object> sad, Dictionary<string, object> skill_info)
        {
            float possession_factor = _compute_possession_factor(sad, skill_info);
            float score_factor = _compute_score_factor(sad);
            float numbers_factor = _compute_numbers_factor(sad, skill_info);

            return possession_factor * 0.4f + score_factor * 0.3f + numbers_factor * 0.3f;
        }

        public virtual float _compute_numbers_factor(Dictionary<string, object> sad, Dictionary<string, object> skill_info)
        {
            var p = sad["player"] as PlayerController;
            var profile = sad["profile"] as AiProfile.Profile;
            var intents = _Dict(skill_info["intents"]);

            int alive_team = 0, alive_enemy = 0;

            if (battle_manager != null)
            {
                var team_members = (p.team == "A") ? battle_manager.teamA : battle_manager.teamB;
                var enemy_members = (p.team == "A") ? battle_manager.teamB : battle_manager.teamA;
                foreach (var t in team_members)
                {
                    var pc = t != null ? t.GetComponent<PlayerController>() : null;
                    if (pc != null && pc.state != PlayerState.Defeated) alive_team += 1;
                }
                foreach (var t in enemy_members)
                {
                    var pc = t != null ? t.GetComponent<PlayerController>() : null;
                    if (pc != null && pc.state != PlayerState.Defeated) alive_enemy += 1;
                }
            }

            int diff = alive_team - alive_enemy;
            float normalized_diff = diff / 3.0f;
            normalized_diff = Mathf.Clamp(normalized_diff, -1.0f, 1.0f);

            bool is_defensive = _F(intents.TryGetValue("defense", out var d) ? d : 0) > _F(intents.TryGetValue("attack", out var a) ? a : 0);

            if (is_defensive)
            {
                if (normalized_diff < 0) return 1.0f + Mathf.Abs(normalized_diff) * (profile.skillOutnumberedBonus - 1.0f);
                else return 1.0f - normalized_diff * 0.3f;
            }
            else
            {
                if (normalized_diff > 0) return 1.0f + normalized_diff * 0.3f;
                else return 1.0f + Mathf.Abs(normalized_diff) * 0.2f;
            }
        }

        public virtual float _compute_possession_factor(Dictionary<string, object> sad, Dictionary<string, object> skill_info)
        {
            var p = sad["player"] as PlayerController;
            var intents = _Dict(skill_info["intents"]);

            if (ball_node == null) return 0.5f;

            if (p.HasBall())
            {
                float factor = 0.0f;
                factor += _F(intents.TryGetValue("attack", out var a) ? a : 0) * 1.5f;
                factor += _F(intents.TryGetValue("control", out var c) ? c : 0) * 1.2f;
                factor += _F(intents.TryGetValue("defense", out var d) ? d : 0) * 0.6f;
                factor += _F(intents.TryGetValue("support", out var su) ? su : 0) * 0.8f;
                return Mathf.Clamp(factor, 0.5f, 1.5f);
            }

            if (ball_node.owner != null)
            {
                var owner_pc = ball_node.owner.GetComponent<PlayerController>();
                if (owner_pc != null && owner_pc.team == p.team)
                {
                    float factor = 0.0f;
                    factor += _F(intents.TryGetValue("support", out var su) ? su : 0) * 1.3f;
                    factor += _F(intents.TryGetValue("defense", out var d) ? d : 0) * 0.9f;
                    factor += _F(intents.TryGetValue("attack", out var a) ? a : 0) * 0.8f;
                    factor += _F(intents.TryGetValue("control", out var c) ? c : 0) * 1.0f;
                    return Mathf.Clamp(factor, 0.5f, 1.5f);
                }

                if (owner_pc != null && owner_pc.team != p.team)
                {
                    float factor = 0.0f;
                    factor += _F(intents.TryGetValue("defense", out var d) ? d : 0) * 1.4f;
                    factor += _F(intents.TryGetValue("control", out var c) ? c : 0) * 1.1f;
                    factor += _F(intents.TryGetValue("attack", out var a) ? a : 0) * 0.5f;
                    factor += _F(intents.TryGetValue("support", out var su) ? su : 0) * 0.7f;
                    return Mathf.Clamp(factor, 0.5f, 1.5f);
                }
            }

            return 0.7f;
        }

        public virtual float _compute_score_factor(Dictionary<string, object> sad)
        {
            var p = sad["player"] as PlayerController;
            var profile = sad["profile"] as AiProfile.Profile;

            int my_score = 0, enemy_score = 0;
            var gm = GameManager.Instance;
            if (gm != null)
            {
                if (p.team == "A") { my_score = gm.score_team_a; enemy_score = gm.score_team_b; }
                else { my_score = gm.score_team_b; enemy_score = gm.score_team_a; }
            }

            int diff = my_score - enemy_score;
            int max_score = Mathf.Max(my_score, enemy_score);
            float normalized_diff = diff / Mathf.Max(max_score, 3);
            normalized_diff = Mathf.Clamp(normalized_diff, -1.0f, 1.0f);

            if (normalized_diff < 0)
            {
                float losing_magnitude = Mathf.Abs(normalized_diff);
                return 1.0f + losing_magnitude * (profile.skillLosingBonus - 1.0f);
            }
            else if (normalized_diff > 0.3f)
            {
                float leading_magnitude = normalized_diff;
                return profile.skillLeadingPenalty + (1.0f - leading_magnitude) * (1.0f - profile.skillLeadingPenalty);
            }
            else return 1.0f;
        }

        public virtual float _compute_intent_match(Dictionary<string, object> sad, Dictionary<string, object> skill_info)
        {
            var profile = sad["profile"] as AiProfile.Profile;
            var intents = _Dict(skill_info["intents"]);

            float match_score = 0.0f;
            match_score += _F(intents.TryGetValue("attack", out var a) ? a : 0) * profile.skillAttackIntentWeight;
            match_score += _F(intents.TryGetValue("defense", out var d) ? d : 0) * profile.skillDefenseIntentWeight;
            match_score += _F(intents.TryGetValue("support", out var su) ? su : 0) * profile.skillSupportIntentWeight;
            match_score += _F(intents.TryGetValue("control", out var c) ? c : 0) * (profile.skillAttackIntentWeight + profile.skillDefenseIntentWeight) * 0.5f;

            float max_possible = Mathf.Max(profile.skillAttackIntentWeight, Mathf.Max(profile.skillDefenseIntentWeight, profile.skillSupportIntentWeight));

            return Mathf.Clamp(match_score / max_possible, 0.5f, 1.5f);
        }

        public virtual float _compute_stamina_factor(Dictionary<string, object> sad, Dictionary<string, object> skill_info)
        {
            var p = sad["player"] as PlayerController;

            float current_stamina = p.stamina;
            float max_stamina = p.maxStamina;
            float current_energy = p.spirit_energy;
            float max_energy = p.max_spirit_energy;

            float stamina_ratio = current_stamina / Mathf.Max(max_stamina, 1);
            float energy_ratio = current_energy / Mathf.Max(max_energy, 1);

            float avg_ratio = (stamina_ratio + energy_ratio) * 0.5f;

            var intents = _Dict(skill_info["intents"]);
            bool has_defense = _F(intents.TryGetValue("defense", out var d) ? d : 0) > 0.3f || _F(intents.TryGetValue("support", out var su) ? su : 0) > 0.3f;

            if (has_defense)
            {
                if (avg_ratio < 0.3f) return 2.0f;
                else if (avg_ratio < 0.5f) return 1.5f;
                else if (avg_ratio < 0.7f) return 1.1f;
                else return 1.0f;
            }
            else
            {
                if (avg_ratio < 0.2f) return 0.5f;
                else if (avg_ratio < 0.4f) return 0.8f;
                else return 1.0f;
            }
        }

        public virtual PlayerController _select_player_target(Dictionary<string, object> sad, Dictionary<string, object> skill_info)
        {
            var p = sad["player"] as PlayerController;
            bool has_player_tag = _B(skill_info.TryGetValue("has_player_tag", out var hpt) ? hpt : false);

            if (!has_player_tag) return null;

            var intents = _Dict(skill_info["intents"]);
            string primary_intent = _S(skill_info["primary_intent"]);

            if (primary_intent == "support" || (_F(intents.TryGetValue("defense", out var d) ? d : 0) > 0.5f && _F(intents.TryGetValue("support", out var su) ? su : 0) > 0.3f))
                return _select_support_target(sad);
            else if (primary_intent == "attack" || _F(intents.TryGetValue("control", out var c) ? c : 0) > 0.5f)
                return _select_attack_target(sad);
            else return p;
        }

        public virtual PlayerController _select_support_target(Dictionary<string, object> sad)
        {
            var p = sad["player"] as PlayerController;

            var team_members = _get_team_members(p);
            if (team_members.Count == 0) return p;

            var best_target = p;
            float best_score = -float.MaxValue;

            foreach (var member in team_members)
            {
                if (member == null) continue;

                float stamina_ratio = member.stamina / Mathf.Max(member.maxStamina, 1);
                float energy_ratio = member.spirit_energy / Mathf.Max(member.max_spirit_energy, 1);
                float distance = Vector2.Distance(_XZ(p.transform), _XZ(member.transform));

                float score = 0.0f;
                score += (1.0f - stamina_ratio) * 3.0f;
                score += (1.0f - energy_ratio) * 2.0f;
                score += 1.0f / Mathf.Max(distance, 0.1f);

                if (score > best_score) { best_score = score; best_target = member; }
            }

            return best_target;
        }

        public virtual PlayerController _select_attack_target(Dictionary<string, object> sad)
        {
            var p = sad["player"] as PlayerController;

            var enemies = _get_enemies(p);
            if (enemies.Count == 0) return null;

            PlayerController best_target = null;
            float best_score = -float.MaxValue;

            foreach (var enemy in enemies)
            {
                if (enemy == null) continue;

                float stamina_ratio = enemy.stamina / Mathf.Max(enemy.maxStamina, 1);
                float distance = Vector2.Distance(_XZ(p.transform), _XZ(enemy.transform));
                bool is_closest_to_ball = false;

                if (ball_node != null && ball_node.owner != null)
                {
                    float enemy_to_ball = Vector2.Distance(_XZ(enemy.transform), _XZ(ball_node.transform));
                    float min_dist = float.MaxValue;
                    foreach (var e in enemies)
                    {
                        float d = Vector2.Distance(_XZ(e.transform), _XZ(ball_node.transform));
                        if (d < min_dist) min_dist = d;
                    }
                    is_closest_to_ball = Mathf.Abs(enemy_to_ball - min_dist) < 0.05f;
                }

                float score = 0.0f;
                score += (1.0f - stamina_ratio) * 2.0f;
                score += 1.0f / Mathf.Max(distance, 0.1f);
                if (is_closest_to_ball) score += 1.5f;

                if (score > best_score) { best_score = score; best_target = enemy; }
            }

            return best_target;
        }

        public virtual List<PlayerController> _get_team_members(PlayerController player)
        {
            var result = new List<PlayerController>();
            if (ai_manager == null) return result;
            foreach (var ap in ai_manager.ai_players)
            {
                var member = ap.player != null ? ap.player.GetComponent<PlayerController>() : null;
                if (member != null && member != player && member.team == player.team) result.Add(member);
            }
            return result;
        }

        public virtual List<PlayerController> _get_enemies(PlayerController player)
        {
            var result = new List<PlayerController>();
            if (ai_manager == null) return result;
            foreach (var ap in ai_manager.ai_players)
            {
                var enemy = ap.player != null ? ap.player.GetComponent<PlayerController>() : null;
                if (enemy != null && enemy.team != player.team) result.Add(enemy);
            }
            return result;
        }

        public virtual Vector2 _select_field_position(Dictionary<string, object> sad, Dictionary<string, object> skill_info)
        {
            var p = sad["player"] as PlayerController;
            bool has_field_tag = _B(skill_info.TryGetValue("has_field_tag", out var hft) ? hft : false);

            if (!has_field_tag) return _XZ(p.transform);

            var skill_data = _Dict(skill_info.TryGetValue("skill_data", out var sd) ? sd : null);
            var values = _Dict(skill_data.TryGetValue("values", out var vv) ? vv : null);
            var intents = _Dict(skill_info["intents"]);

            bool is_defensive = _F(intents.TryGetValue("defense", out var d) ? d : 0) > _F(intents.TryGetValue("attack", out var a) ? a : 0)
                && _F(intents.TryGetValue("defense", out var d2) ? d2 : 0) > _F(intents.TryGetValue("control", out var c) ? c : 0);
            bool has_wall = values.ContainsKey("wall_width");
            bool has_radius = values.ContainsKey("radius");
            bool has_stun = values.ContainsKey("stun_duration");

            if (has_wall) return _select_wall_position(sad, is_defensive);
            else if (has_stun) return _select_aoe_position(sad, is_defensive);
            else if (has_radius) return _select_area_position(sad, is_defensive);
            else return _XZ(p.transform);
        }

        public virtual Vector2 _select_wall_position(Dictionary<string, object> sad, bool is_defensive)
        {
            var p = sad["player"] as PlayerController;
            var our_goal = _get_our_goal_position(p);
            var enemy_goal = _get_enemy_goal_position(p);

            if (ball_node == null) return _XZ(p.transform);

            if (is_defensive)
            {
                var ball_dir = _XZ(ball_node.transform) - our_goal;
                float dist_to_goal = ball_dir.magnitude;
                var wall_pos = our_goal + ball_dir.normalized * Mathf.Min(dist_to_goal * 0.6f, 0.8f);
                return wall_pos;
            }
            else
            {
                var enemy_ball_holder = _get_enemy_ball_holder(p);
                if (enemy_ball_holder != null)
                {
                    var to_enemy = _XZ(enemy_ball_holder.transform) - our_goal;
                    var wall_pos = our_goal + to_enemy.normalized * Mathf.Min(to_enemy.magnitude * 0.4f, 0.6f);
                    return wall_pos;
                }
                else
                {
                    return _XZ(p.transform) + (enemy_goal - _XZ(p.transform)).normalized * 0.3f;
                }
            }
        }

        public virtual Vector2 _select_aoe_position(Dictionary<string, object> sad, bool is_defensive)
        {
            var p = sad["player"] as PlayerController;

            var enemies = _get_enemies(p);
            if (enemies.Count == 0) return _XZ(p.transform);

            if (is_defensive)
            {
                PlayerController closest_enemy = null;
                float min_dist = float.MaxValue;
                foreach (var enemy in enemies)
                {
                    float d = Vector2.Distance(_XZ(p.transform), _XZ(enemy.transform));
                    if (d < min_dist) { min_dist = d; closest_enemy = enemy; }
                }
                if (closest_enemy != null) return _XZ(closest_enemy.transform);
            }
            else
            {
                PlayerController lowest_stamina_enemy = null;
                float min_stamina_ratio = float.MaxValue;
                foreach (var enemy in enemies)
                {
                    float stamina_ratio = enemy.stamina / Mathf.Max(enemy.maxStamina, 1);
                    if (stamina_ratio < min_stamina_ratio) { min_stamina_ratio = stamina_ratio; lowest_stamina_enemy = enemy; }
                }
                if (lowest_stamina_enemy != null) return _XZ(lowest_stamina_enemy.transform);
            }

            return _XZ(p.transform);
        }

        public virtual Vector2 _select_area_position(Dictionary<string, object> sad, bool is_defensive)
        {
            var p = sad["player"] as PlayerController;
            var our_goal = _get_our_goal_position(p);

            if (is_defensive) return our_goal + new Vector2(0f, 0.2f);
            else return _XZ(p.transform) + new Vector2(0f, -0.2f);
        }

        public virtual Vector2 _get_our_goal_position(PlayerController player)
        {
            return player.team == "A" ? GOAL_A : GOAL_B;
        }

        public virtual Vector2 _get_enemy_goal_position(PlayerController player)
        {
            return player.team == "A" ? GOAL_B : GOAL_A;
        }

        public virtual PlayerController _get_enemy_ball_holder(PlayerController player)
        {
            if (ball_node == null || ball_node.owner == null) return null;
            var owner_pc = ball_node.owner.GetComponent<PlayerController>();
            if (owner_pc != null && owner_pc.team != player.team) return owner_pc;
            return null;
        }

        public virtual void _execute_skill(Dictionary<string, object> sad, Dictionary<string, object> skill_info)
        {
            var p = sad["player"] as PlayerController;
            var profile = sad["profile"] as AiProfile.Profile;

            if (UnityEngine.Random.value < profile.skillMistakeChance)
            {
                var skill_data = _Dict(skill_info.TryGetValue("skill_data", out var sd) ? sd : null);
                string mistake_type = _decide_mistake_type(sad, skill_info);
                Debug.Log("[SpiritAI] " + p.name + " 技能失误: " + _S(skill_data.TryGetValue("name", out var n) ? n : "unknown") + " (失误类型: " + mistake_type + ")");
                return;
            }

            var target = _select_player_target(sad, skill_info);
            var field_pos = _select_field_position(sad, skill_info);

            var target_data = new Dictionary<string, object>();
            if (target != null)
            {
                target_data["target_player_id"] = target.GetInstanceID();
                target_data["target_position"] = _XZ(target.transform);
            }
            if (_B(skill_info.TryGetValue("has_field_tag", out var hft) ? hft : false))
            {
                target_data["field_position"] = field_pos;
            }

            object _result = _CallSpiritMethod("use_skill", p.GetInstanceID(), _S(skill_info["skill_id"]), target_data);
            bool success = (_result != null);

            if (success)
            {
                sad["last_skill_use_time"] = Time.time;
                var skill_data = _Dict(skill_info.TryGetValue("skill_data", out var sd) ? sd : null);
                string target_name = (target == p) ? "自己" : (target != null ? target.name : "无");
                string pos_str = _B(skill_info.TryGetValue("has_field_tag", out var hft2) ? hft2 : false) ? "场地" : "无";
                Debug.Log("[SpiritAI] " + p.name + " 使用技能: " + _S(skill_data.TryGetValue("name", out var n) ? n : "unknown")
                          + " (评分: " + _F(skill_info.TryGetValue("base_value", out var bv) ? bv : 0).ToString("F1")
                          + ", 目标: " + target_name + ", 位置: " + pos_str + ")");

                _send_skill_message(sad, skill_info, target);
            }
        }

        public virtual void _send_skill_message(Dictionary<string, object> sad, Dictionary<string, object> skill_info, PlayerController target)
        {
            var p = sad["player"] as PlayerController;
            var intents = _Dict(skill_info["intents"]);

            if (battle_manager == null || battle_manager.comm_system == null) return;
            var comm = battle_manager.comm_system;

            if (_F(intents.TryGetValue("support", out var su) ? su : 0) > 0.5f && target != null && target != p)
            {
                comm.try_send_message(p.transform, (int)AiMsgType.BUFF_ON_YOU, p.team);
                comm.record_message(p.transform, (int)AiMsgType.BUFF_ON_YOU, p.team);
            }

            if (_F(intents.TryGetValue("attack", out var a) ? a : 0) > 0.7f && _F(intents.TryGetValue("control", out var c) ? c : 0) > 0.3f)
            {
                if (UnityEngine.Random.value < 0.3f)
                {
                    comm.try_send_message(p.transform, (int)AiMsgType.SKILL_READY, p.team);
                    comm.record_message(p.transform, (int)AiMsgType.SKILL_READY, p.team);
                }
            }
        }

        public virtual void _try_send_need_buff(Dictionary<string, object> sad)
        {
            var p = sad["player"] as PlayerController;

            if (battle_manager == null || battle_manager.comm_system == null) return;
            var comm = battle_manager.comm_system;

            if (!comm.can_send(p.transform)) return;

            bool has_support_skill = false;
            var sa = sad.TryGetValue("skills_analysis", out var s) ? s as List<Dictionary<string, object>> : null;
            if (sa != null)
            {
                foreach (var analysis in sa)
                {
                    if (_S(analysis.TryGetValue("synergy_level", out var sl) ? sl : "low") == "critical")
                    {
                        has_support_skill = true;
                        break;
                    }
                }
            }

            float stamina_ratio = p.stamina / Mathf.Max(p.maxStamina, 1);
            float energy_ratio = p.spirit_energy / Mathf.Max(p.max_spirit_energy, 1);

            if ((stamina_ratio < 0.2f || energy_ratio < 0.2f) && !has_support_skill)
            {
                if (UnityEngine.Random.value < 0.2f)
                {
                    comm.try_send_message(p.transform, (int)AiMsgType.NEED_BUFF, p.team);
                    comm.record_message(p.transform, (int)AiMsgType.NEED_BUFF, p.team);
                }
            }
        }

        public virtual string _decide_mistake_type(Dictionary<string, object> sad, Dictionary<string, object> skill_info)
        {
            float r = UnityEngine.Random.value;
            if (r < 0.4f) return "时机失误";
            else if (r < 0.7f) return "目标失误";
            else return "技能失误";
        }
        private object _CallSpiritMethod(string methodName, params object[] args) {
            if (spirit_system == null) return null;
            var mi = spirit_system.GetType().GetMethod(methodName);
            if (mi != null) return mi.Invoke(spirit_system, args);
            return null;
        }
        private float _CallSpiritMethodFloat(string methodName, params object[] args) {
            var r = _CallSpiritMethod(methodName, args);
            if (r is float f) return f;
            if (r is double d) return (float)d;
            if (r is int i) return (float)i;
            return 0f;
        }
    }
}

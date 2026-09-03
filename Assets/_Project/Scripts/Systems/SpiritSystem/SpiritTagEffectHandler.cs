// ================================================================
// 决竞球 Godot→Unity 翻译: SpiritTagEffectHandler.cs [BattleBall.Systems.SpiritSystem]
// 源: E:/项目储存/决竞球battle-ball/scripts/systems/spirit_system/spirit_tag_effect_handler.gd
// 集成备注:
//   1) PlayerController 缺少方法, 需后续补充:
//      GetAndConsumeNextSkillMult(), AddBuff(6参数), TurnOnLight(status, duration),
//      TurnOffLight(status), AddSkillCostMult, AddSkillCdMult, AddSkillBonusUses,
//      AddNextSkillMult, AddTickEffect, equipped_skills, TeleportTo, ReturnToPrevious,
//      _OnDefeated, max_spirit_energy, spirit_energy
//   2) ObstacleManager/FieldZoneManager/IllusionManager 节点查找方式:
//      Unity 中通过 Transform.Find / GameObject.Find 查找
//   3) 战术队列优先级使用 Dictionary<string,int>, 按数字升序排序执行
// ================================================================
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using BattleBall.Core;
using BattleBall.Battle;

namespace BattleBall.Systems.SpiritSystem
{
    public class SpiritTagEffectHandler : MonoBehaviour
    {
        // ===== 信号 =====
        public event Action<string, Dictionary<string, object>> effect_applied;
        public event Action<string, Dictionary<string, object>> effect_finished;

        // ===== 引用 =====
        public BattleManager battle_manager;
        public BallController ball_node;
        public Transform field_node;
        public List<PlayerController> players = new List<PlayerController>();

        // ===== 活跃效果堆栈 =====
        // _active_effects: {effect_uuid: {tag_id, params, duration, remaining, on_tick, on_expire}}
        protected Dictionary<string, Dictionary<string, object>> _active_effects = new Dictionary<string, Dictionary<string, object>>();
        protected int _effect_counter = 0;

        // ===== 优先级队列 =====
        // 标签优先级字典: tag_id → 优先级数字 (越小越先执行)
        protected Dictionary<string, int> _tag_priority = new Dictionary<string, int>();

        // 待执行队列: List<{tag_id, params, caster_id, priority}>
        protected List<Dictionary<string, object>> _pending_tags = new List<Dictionary<string, object>>();

        // 累积窗口计时器 (秒)
        protected float _flush_timer = 0.0f;

        // 窗口时长 (秒)
        public const float FLUSH_WINDOW = 0.1f;

        // 是否启用优先级队列
        public bool priority_queue_enabled = true;

        // ===== 球的临时修饰符 (发球时生效) =====
        protected Dictionary<string, object> _ball_mods = new Dictionary<string, object> {
            { "dmg_mult", 1.0f },
            { "dmg_flat", 0.0f },
            { "speed_mult", 1.0f },
            { "speed_flat", 0.0f },
            { "range_mult", 1.0f },
            { "range_flat", 0.0f },
            { "penetrate", false },
            { "armor", 0.0f },
            { "tracking_target", null },
            { "tracking_turn_speed", 0.0f },
            { "boomerang", false },
            { "boomerang_triggered", false },
            { "boomerang_return_dir", Vector2.zero },
            { "boomerang_dist", 0.0f },
            { "lock_straight", false },
            { "spread_done", false },
            { "aoe_radius", 0.0f },
            { "aoe_damage_pct", 0.5f },
            { "lockon_target", null },
        };

        // ===== 数值/类型转换辅助 =====
        protected static float _F(object o) { if (o == null) return 0f; float r; return float.TryParse(o.ToString(), out r) ? r : 0f; }
        protected static int _I(object o) { if (o == null) return 0; int r; return int.TryParse(o.ToString(), out r) ? r : 0; }
        protected static string _S(object o) { return o == null ? "" : o.ToString(); }
        protected static bool _B(object o) { if (o == null) return false; if (o is bool b) return b; bool r; return bool.TryParse(o.ToString(), out r) ? r : false; }
        protected static Dictionary<string, object> _Dict(object o) {
            if (o is IDictionary<string, object> d) return new Dictionary<string, object>(d);
            return new Dictionary<string, object>();
        }
        protected static Vector2 _XZ(Transform t) { return t == null ? Vector2.zero : new Vector2(t.position.x, t.position.z); }

        protected virtual void Awake()
        {
            // battle_manager 通过外部注入或单例获取
            if (battle_manager == null) battle_manager = BattleManager.Instance;
            _init_priority_table();
        }

        // ==================== 优先级表初始化 ====================

        protected virtual void _init_priority_table()
        {
            // === BALL 类 ===
            // B-10 数值修改层
            _tag_priority["ball_dmg_up_pct"] = 11;
            _tag_priority["ball_dmg_down_pct"] = 12;
            _tag_priority["ball_dmg_up_flat"] = 13;
            _tag_priority["ball_dmg_down_flat"] = 14;
            _tag_priority["ball_speed_up_pct"] = 15;
            _tag_priority["ball_speed_down_pct"] = 16;
            _tag_priority["ball_speed_up_flat"] = 17;
            _tag_priority["ball_speed_down_flat"] = 18;
            // B-20 飞行行为层
            _tag_priority["ball_tracking"] = 21;
            _tag_priority["ball_avoid"] = 22;
            _tag_priority["ball_boomerang"] = 23;
            _tag_priority["ball_straight"] = 24;
            _tag_priority["ball_lockon"] = 25;
            _tag_priority["ball_spread"] = 26;
            // B-30 穿透/范围层
            _tag_priority["ball_penetrate"] = 31;
            _tag_priority["ball_range_up"] = 32;
            _tag_priority["ball_range_down"] = 33;

            // === FIELD 类 ===
            // F-10 障碍层
            _tag_priority["field_obs_add"] = 101;
            _tag_priority["field_obs_clear"] = 102;
            // F-20 地形层
            _tag_priority["field_terra_change"] = 121;
            _tag_priority["field_terra_revert"] = 122;
            _tag_priority["field_zone_mark"] = 123;
            _tag_priority["field_zone_clear"] = 124;
            // F-30 区域效果层
            _tag_priority["field_zone_boost"] = 131;
            _tag_priority["field_zone_slow"] = 132;
            _tag_priority["field_zone_danger"] = 133;
            _tag_priority["field_zone_safe"] = 134;
            // F-40 视觉层
            _tag_priority["field_illusion_add"] = 141;
            _tag_priority["field_illusion_clear"] = 142;

            // === PLAYER 类 ===
            // P-10 规则修改层
            _tag_priority["player_spirit_cost_down"] = 201;
            _tag_priority["player_spirit_cost_up"] = 202;
            _tag_priority["player_spirit_cd_down"] = 203;
            _tag_priority["player_spirit_cd_up"] = 204;
            _tag_priority["player_spirit_double"] = 205;
            _tag_priority["player_spirit_half"] = 206;
            _tag_priority["player_spirit_uses_up"] = 207;
            // P-20 属性 Buff 层
            _tag_priority["player_atk_up_pct"] = 221;
            _tag_priority["player_atk_down_pct"] = 222;
            _tag_priority["player_atk_up_flat"] = 223;
            _tag_priority["player_atk_down_flat"] = 224;
            _tag_priority["player_def_up_pct"] = 225;
            _tag_priority["player_def_down_pct"] = 226;
            _tag_priority["player_def_up_flat"] = 227;
            _tag_priority["player_def_down_flat"] = 228;
            _tag_priority["player_spd_up_pct"] = 229;
            _tag_priority["player_spd_down_pct"] = 230;
            _tag_priority["player_spd_up_flat"] = 231;
            _tag_priority["player_spd_down_flat"] = 232;
            _tag_priority["player_res_up_pct"] = 233;
            _tag_priority["player_res_down_pct"] = 234;
            _tag_priority["player_res_up_flat"] = 235;
            _tag_priority["player_res_down_flat"] = 236;
            _tag_priority["player_energy_max_up_pct"] = 237;
            _tag_priority["player_energy_max_down_pct"] = 238;
            _tag_priority["player_energy_max_up_flat"] = 239;
            _tag_priority["player_energy_max_down_flat"] = 240;
            // P-30 状态灯层
            _tag_priority["player_invincible"] = 301;
            _tag_priority["player_vulnerable"] = 302;
            _tag_priority["player_stealth"] = 303;
            _tag_priority["player_reveal"] = 304;
            // P-40 运动控制层
            _tag_priority["player_move_slow"] = 401;
            _tag_priority["player_move_boost"] = 402;
            _tag_priority["player_root"] = 403;
            _tag_priority["player_unroot"] = 404;
            // P-50 控制层
            _tag_priority["player_stun"] = 501;
            _tag_priority["player_cc_immune"] = 502;
            _tag_priority["player_silence"] = 503;
            _tag_priority["player_disarm"] = 504;
            // P-60 即时效果层
            _tag_priority["player_hp_heal_pct"] = 601;
            _tag_priority["player_hp_damage_pct"] = 602;
            _tag_priority["player_hp_heal_flat"] = 603;
            _tag_priority["player_hp_damage_flat"] = 604;
            _tag_priority["player_hp_regen"] = 605;
            _tag_priority["player_hp_dot"] = 606;
            _tag_priority["player_energy_gain_pct"] = 607;
            _tag_priority["player_energy_cost_pct"] = 608;
            _tag_priority["player_energy_gain_flat"] = 609;
            _tag_priority["player_energy_cost_flat"] = 610;
            // P-70 交互层
            _tag_priority["player_teleport"] = 701;
            _tag_priority["player_return"] = 702;
        }

        public virtual int get_tag_priority(string tag_id)
        {
            return _tag_priority.TryGetValue(tag_id, out var pri) ? pri : 999;
        }

        public virtual void queue_tag_effect(string tag_id, Dictionary<string, object> parameters, int caster_id)
        {
            int pri = get_tag_priority(tag_id);
            _pending_tags.Add(new Dictionary<string, object> {
                { "tag_id", tag_id },
                { "params", parameters },
                { "caster_id", caster_id },
                { "priority", pri },
            });
            _flush_timer = FLUSH_WINDOW;
            Debug.Log("[TagQueue] 排队: " + tag_id + " (优先级=" + pri + ", 队列=" + _pending_tags.Count + ")");
        }

        protected virtual void _flush_pending_tags()
        {
            if (_pending_tags.Count == 0) return;
            // 按优先级升序排序 (小数字先执行)
            var sorted = _pending_tags.OrderBy(e => _I(e.TryGetValue("priority", out var p) ? p : 999)).ToList();
            _pending_tags = sorted;
            Debug.Log("[TagQueue] 开始按优先级执行 " + _pending_tags.Count + " 个标签");
            foreach (var entry in _pending_tags)
            {
                _do_apply_tag(_S(entry["tag_id"]), _Dict(entry["params"]), _I(entry["caster_id"]));
            }
            _pending_tags.Clear();
            _flush_timer = 0.0f;
        }

        // ==================== 球修饰符接口 ====================

        // 发球前重置所有修饰符
        public virtual void reset_ball_mods()
        {
            _ball_mods = new Dictionary<string, object> {
                { "dmg_mult", 1.0f },
                { "dmg_flat", 0.0f },
                { "speed_mult", 1.0f },
                { "speed_flat", 0.0f },
                { "range_mult", 1.0f },
                { "range_flat", 0.0f },
                { "penetrate", false },
                { "armor", 0.0f },
                { "tracking_target", null },
                { "tracking_turn_speed", 0.0f },
                { "boomerang", false },
                { "boomerang_triggered", false },
                { "boomerang_return_dir", Vector2.zero },
                { "boomerang_dist", 0.0f },
                { "lock_straight", false },
                { "spread_done", false },
                { "aoe_radius", 0.0f },
                { "aoe_damage_pct", 0.5f },
                { "lockon_target", null },
            };
        }

        public virtual float get_modified_ball_damage(float base_damage)
        {
            float dmg_mult = _F(_ball_mods.TryGetValue("dmg_mult", out var dm) ? dm : 1.0f);
            float dmg_flat = _F(_ball_mods.TryGetValue("dmg_flat", out var df) ? df : 0.0f);
            float armor = _F(_ball_mods.TryGetValue("armor", out var ar) ? ar : 0.0f);
            float result = (base_damage + dmg_flat) * dmg_mult;
            result = Mathf.Max(0.0f, result - armor);
            return result;
        }

        public virtual float get_modified_ball_speed(float base_speed)
        {
            float speed_mult = _F(_ball_mods.TryGetValue("speed_mult", out var sm) ? sm : 1.0f);
            float speed_flat = _F(_ball_mods.TryGetValue("speed_flat", out var sf) ? sf : 0.0f);
            return (base_speed + speed_flat) * speed_mult;
        }

        public virtual float get_modified_ball_range(float base_range)
        {
            float range_mult = _F(_ball_mods.TryGetValue("range_mult", out var rm) ? rm : 1.0f);
            float range_flat = _F(_ball_mods.TryGetValue("range_flat", out var rf) ? rf : 0.0f);
            return (base_range + range_flat) * range_mult;
        }

        public virtual bool is_ball_penetrating()
        {
            return _B(_ball_mods.TryGetValue("penetrate", out var p) ? p : false);
        }

        public virtual bool is_ball_boomerang()
        {
            bool boomerang = _B(_ball_mods.TryGetValue("boomerang", out var b) ? b : false);
            bool lock_straight = _B(_ball_mods.TryGetValue("lock_straight", out var ls) ? ls : false);
            return boomerang && !lock_straight;
        }

        public virtual bool is_ball_tracking()
        {
            var target = _ball_mods.TryGetValue("tracking_target", out var t) ? t : null;
            bool lock_straight = _B(_ball_mods.TryGetValue("lock_straight", out var ls) ? ls : false);
            return target != null && !lock_straight;
        }

        public virtual Transform get_tracking_target()
        {
            return _ball_mods.TryGetValue("tracking_target", out var t) ? t as Transform : null;
        }

        public virtual float get_tracking_turn_speed()
        {
            return _F(_ball_mods.TryGetValue("tracking_turn_speed", out var ts) ? ts : 0.0f);
        }

        public virtual bool has_ball_aoe()
        {
            return _ball_mods.ContainsKey("aoe_radius") && _F(_ball_mods["aoe_radius"]) > 0.0f;
        }

        public virtual float get_ball_aoe_radius()
        {
            return _F(_ball_mods.TryGetValue("aoe_radius", out var r) ? r : 0.0f);
        }

        public virtual float get_ball_aoe_damage_pct()
        {
            return _F(_ball_mods.TryGetValue("aoe_damage_pct", out var p) ? p : 0.5f);
        }

        public virtual Vector2 trigger_boomerang(Vector2 current_dir)
        {
            bool triggered = _B(_ball_mods.TryGetValue("boomerang_triggered", out var bt) ? bt : false);
            if (triggered) return Vector2.zero;
            _ball_mods["boomerang_triggered"] = true;
            Vector2 return_dir = -current_dir;
            _ball_mods["boomerang_return_dir"] = return_dir;
            return return_dir;
        }

        // ==================== 主入口 ====================

        public virtual Dictionary<string, object> apply_tag_effect(string tag_id, Dictionary<string, object> parameters, int caster_id)
        {
            if (priority_queue_enabled && _pending_tags.Count > 0)
            {
                queue_tag_effect(tag_id, parameters, caster_id);
                return new Dictionary<string, object> { { "success", true }, { "tag_id", tag_id }, { "queued", true } };
            }
            else if (priority_queue_enabled)
            {
                queue_tag_effect(tag_id, parameters, caster_id);
                return new Dictionary<string, object> { { "success", true }, { "tag_id", tag_id }, { "queued", true } };
            }
            else
            {
                return _do_apply_tag(tag_id, parameters, caster_id);
            }
        }

        public virtual Dictionary<string, object> _do_apply_tag(string tag_id, Dictionary<string, object> parameters, int caster_id)
        {
            Debug.Log("[TagEffect] 执行标签: " + tag_id + " params=" + _DictToString(parameters));

            bool success = false;

            // === 对球效果 ===
            switch (tag_id)
            {
                // 数值类 (01-08)
                case "ball_dmg_up_pct":
                    _apply_ball_dmg_up(parameters);
                    success = true;
                    break;
                case "ball_dmg_down_pct":
                    _apply_ball_dmg_down(parameters);
                    success = true;
                    break;
                case "ball_dmg_up_flat":
                    {
                        var flat_params_up = new Dictionary<string, object>(parameters);
                        flat_params_up["value_type"] = "flat";
                        _apply_ball_dmg_up(flat_params_up);
                        success = true;
                        break;
                    }
                case "ball_dmg_down_flat":
                    {
                        var flat_params_down = new Dictionary<string, object>(parameters);
                        flat_params_down["value_type"] = "flat";
                        _apply_ball_dmg_down(flat_params_down);
                        success = true;
                        break;
                    }
                case "ball_speed_up_pct":
                    _apply_ball_speed_up(parameters);
                    success = true;
                    break;
                case "ball_speed_down_pct":
                    _apply_ball_speed_down(parameters);
                    success = true;
                    break;
                case "ball_speed_up_flat":
                    _apply_ball_speed_up(parameters);
                    success = true;
                    break;
                case "ball_speed_down_flat":
                    _apply_ball_speed_down(parameters);
                    success = true;
                    break;
                // 飞行行为类 (09-14)
                case "ball_tracking":
                    _apply_ball_tracking(parameters, caster_id);
                    success = true;
                    break;
                case "ball_avoid":
                    success = true;  // 避障待场地系统
                    break;
                case "ball_boomerang":
                    _apply_ball_boomerang(parameters);
                    success = true;
                    break;
                case "ball_straight":
                    _apply_ball_straight(parameters);
                    success = true;
                    break;
                case "ball_lockon":
                    _apply_ball_lockon(parameters, caster_id);
                    success = true;
                    break;
                case "ball_spread":
                    success = true;  // 扩散在球碰撞时处理
                    break;
                // 穿透/范围类 (15-17)
                case "ball_penetrate":
                    _apply_ball_penetrate(parameters);
                    success = true;
                    break;
                case "ball_range_up":
                    _apply_ball_range_up(parameters);
                    success = true;
                    break;
                case "ball_range_down":
                    _apply_ball_range_down(parameters);
                    success = true;
                    break;
                // 对场地标签
                case "field_obs_add":
                    _apply_field_obs_add(parameters);
                    success = true;
                    break;
                case "field_obs_clear":
                    _apply_field_obs_clear(parameters);
                    success = true;
                    break;
                // === 场地标签 - 区域效果 ===
                case "field_zone_boost":
                    _apply_field_zone_effect(parameters, 0);
                    success = true;
                    break;
                case "field_zone_slow":
                    _apply_field_zone_effect(parameters, 1);
                    success = true;
                    break;
                case "field_zone_danger":
                    _apply_field_zone_effect(parameters, 2);
                    success = true;
                    break;
                case "field_zone_safe":
                    _apply_field_zone_effect(parameters, 3);
                    success = true;
                    break;
                // === 球员标签 - 属性 ===
                case "player_atk_up_pct":
                    _apply_player_stat_buff(parameters, caster_id, "attack", 1.0f + _F(parameters.TryGetValue("value", out var v1) ? v1 : 30) / 100.0f, 0.0f);
                    success = true;
                    break;
                case "player_atk_down_pct":
                    _apply_player_stat_buff(parameters, caster_id, "attack", 1.0f / Mathf.Max(0.01f, 1.0f + _F(parameters.TryGetValue("value", out var v2) ? v2 : 30) / 100.0f), 0.0f);
                    success = true;
                    break;
                case "player_atk_up_flat":
                    _apply_player_stat_buff(parameters, caster_id, "attack", 1.0f, _F(parameters.TryGetValue("value", out var v3) ? v3 : 10));
                    success = true;
                    break;
                case "player_atk_down_flat":
                    _apply_player_stat_buff(parameters, caster_id, "attack", 1.0f, -_F(parameters.TryGetValue("value", out var v4) ? v4 : 10));
                    success = true;
                    break;
                case "player_def_up_pct":
                    _apply_player_stat_buff(parameters, caster_id, "defense", 1.0f + _F(parameters.TryGetValue("value", out var v5) ? v5 : 30) / 100.0f, 0.0f);
                    success = true;
                    break;
                case "player_def_down_pct":
                    _apply_player_stat_buff(parameters, caster_id, "defense", 1.0f / Mathf.Max(0.01f, 1.0f + _F(parameters.TryGetValue("value", out var v6) ? v6 : 30) / 100.0f), 0.0f);
                    success = true;
                    break;
                case "player_def_up_flat":
                    _apply_player_stat_buff(parameters, caster_id, "defense", 1.0f, _F(parameters.TryGetValue("value", out var v7) ? v7 : 10));
                    success = true;
                    break;
                case "player_def_down_flat":
                    _apply_player_stat_buff(parameters, caster_id, "defense", 1.0f, -_F(parameters.TryGetValue("value", out var v8) ? v8 : 10));
                    success = true;
                    break;
                case "player_spd_up_pct":
                    _apply_player_stat_buff(parameters, caster_id, "speed", 1.0f + _F(parameters.TryGetValue("value", out var v9) ? v9 : 30) / 100.0f, 0.0f);
                    success = true;
                    break;
                case "player_spd_down_pct":
                    _apply_player_stat_buff(parameters, caster_id, "speed", 1.0f / Mathf.Max(0.01f, 1.0f + _F(parameters.TryGetValue("value", out var v10) ? v10 : 30) / 100.0f), 0.0f);
                    success = true;
                    break;
                case "player_spd_up_flat":
                    _apply_player_stat_buff(parameters, caster_id, "speed", 1.0f, _F(parameters.TryGetValue("value", out var v11) ? v11 : 30));
                    success = true;
                    break;
                case "player_spd_down_flat":
                    _apply_player_stat_buff(parameters, caster_id, "speed", 1.0f, -_F(parameters.TryGetValue("value", out var v12) ? v12 : 30));
                    success = true;
                    break;
                case "player_res_up_pct":
                    _apply_player_stat_buff(parameters, caster_id, "resilience", 1.0f + _F(parameters.TryGetValue("value", out var v13) ? v13 : 30) / 100.0f, 0.0f);
                    success = true;
                    break;
                case "player_res_down_pct":
                    _apply_player_stat_buff(parameters, caster_id, "resilience", 1.0f / Mathf.Max(0.01f, 1.0f + _F(parameters.TryGetValue("value", out var v14) ? v14 : 30) / 100.0f), 0.0f);
                    success = true;
                    break;
                case "player_res_up_flat":
                    _apply_player_stat_buff(parameters, caster_id, "resilience", 1.0f, _F(parameters.TryGetValue("value", out var v15) ? v15 : 10));
                    success = true;
                    break;
                case "player_res_down_flat":
                    _apply_player_stat_buff(parameters, caster_id, "resilience", 1.0f, -_F(parameters.TryGetValue("value", out var v16) ? v16 : 10));
                    success = true;
                    break;
                // === 球员标签 - 状态 ===
                case "player_invincible":
                    _apply_player_status(parameters, caster_id, "invincible");
                    success = true;
                    break;
                case "player_vulnerable":
                    _apply_player_vulnerable(parameters, caster_id);
                    success = true;
                    break;
                case "player_stealth":
                    _apply_player_status(parameters, caster_id, "stealthed");
                    success = true;
                    break;
                case "player_reveal":
                    _apply_player_reveal(parameters, caster_id);
                    success = true;
                    break;
                // === 球员标签 - 体力 ===
                case "player_hp_heal_pct":
                    _apply_player_hp_heal_pct(parameters, caster_id);
                    success = true;
                    break;
                case "player_hp_damage_pct":
                    _apply_player_hp_damage_pct(parameters, caster_id);
                    success = true;
                    break;
                case "player_hp_heal_flat":
                    _apply_player_hp_heal_flat(parameters, caster_id);
                    success = true;
                    break;
                case "player_hp_damage_flat":
                    _apply_player_hp_damage_flat(parameters, caster_id);
                    success = true;
                    break;
                case "player_hp_regen":
                    _apply_player_hp_regen(parameters, caster_id);
                    success = true;
                    break;
                case "player_hp_dot":
                    _apply_player_hp_dot(parameters, caster_id);
                    success = true;
                    break;
                // === 球员标签 - 运动 ===
                case "player_move_slow":
                    _apply_player_stat_buff(parameters, caster_id, "speed", 1.0f / Mathf.Max(0.01f, _F(parameters.TryGetValue("multiplier", out var ms1) ? ms1 : 1.5f)), 0.0f);
                    success = true;
                    break;
                case "player_move_boost":
                    _apply_player_stat_buff(parameters, caster_id, "speed", _F(parameters.TryGetValue("multiplier", out var ms2) ? ms2 : 1.5f), 0.0f);
                    success = true;
                    break;
                case "player_root":
                    _apply_player_status(parameters, caster_id, "rooted");
                    success = true;
                    break;
                case "player_unroot":
                    _apply_player_unroot(parameters, caster_id);
                    success = true;
                    break;
                // === 球员标签 - 能量 ===
                case "player_energy_gain_pct":
                    _apply_player_energy_pct(parameters, caster_id, true);
                    success = true;
                    break;
                case "player_energy_cost_pct":
                    _apply_player_energy_pct(parameters, caster_id, false);
                    success = true;
                    break;
                case "player_energy_gain_flat":
                    _apply_player_energy_flat(parameters, caster_id, true);
                    success = true;
                    break;
                case "player_energy_cost_flat":
                    _apply_player_energy_flat(parameters, caster_id, false);
                    success = true;
                    break;
                case "player_energy_max_up_pct":
                    _apply_player_stat_buff(parameters, caster_id, "max_energy", 1.0f + _F(parameters.TryGetValue("value", out var v17) ? v17 : 30) / 100.0f, 0.0f);
                    success = true;
                    break;
                case "player_energy_max_down_pct":
                    _apply_player_stat_buff(parameters, caster_id, "max_energy", 1.0f / Mathf.Max(0.01f, 1.0f + _F(parameters.TryGetValue("value", out var v18) ? v18 : 30) / 100.0f), 0.0f);
                    success = true;
                    break;
                case "player_energy_max_up_flat":
                    _apply_player_stat_buff(parameters, caster_id, "max_energy", 1.0f, _F(parameters.TryGetValue("value", out var v19) ? v19 : 20));
                    success = true;
                    break;
                case "player_energy_max_down_flat":
                    _apply_player_stat_buff(parameters, caster_id, "max_energy", 1.0f, -_F(parameters.TryGetValue("value", out var v20) ? v20 : 20));
                    success = true;
                    break;
                // === 球员标签 - 元灵 ===
                case "player_spirit_cost_down":
                    _apply_player_spirit_cost(parameters, caster_id, true);
                    success = true;
                    break;
                case "player_spirit_cost_up":
                    _apply_player_spirit_cost(parameters, caster_id, false);
                    success = true;
                    break;
                case "player_spirit_uses_up":
                    _apply_player_spirit_uses(parameters, caster_id);
                    success = true;
                    break;
                case "player_spirit_cd_down":
                    _apply_player_spirit_cd(parameters, caster_id, true);
                    success = true;
                    break;
                case "player_spirit_cd_up":
                    _apply_player_spirit_cd(parameters, caster_id, false);
                    success = true;
                    break;
                case "player_spirit_double":
                    _apply_player_spirit_double(parameters, caster_id);
                    success = true;
                    break;
                case "player_spirit_half":
                    _apply_player_spirit_half(parameters, caster_id);
                    success = true;
                    break;
                // === 球员标签 - 控制 ===
                case "player_stun":
                    _apply_player_status(parameters, caster_id, "stunned");
                    success = true;
                    break;
                case "player_cc_immune":
                    _apply_player_status(parameters, caster_id, "cc_immune");
                    success = true;
                    break;
                case "player_silence":
                    _apply_player_status(parameters, caster_id, "silenced");
                    success = true;
                    break;
                case "player_disarm":
                    _apply_player_status(parameters, caster_id, "disarmed");
                    success = true;
                    break;
                // === 球员标签 - 交互 ===
                case "player_teleport":
                    _apply_player_teleport(parameters, caster_id);
                    success = true;
                    break;
                case "player_return":
                    _apply_player_return(parameters, caster_id);
                    success = true;
                    break;
                default:
                    Debug.Log("[TagEffect] 标签未实现: " + tag_id);
                    break;
            }

            var effect_data = new Dictionary<string, object> {
                { "tag_id", tag_id },
                { "params", parameters },
                { "caster_id", caster_id },
            };

            if (success)
            {
                effect_applied?.Invoke(tag_id, effect_data);
            }

            return new Dictionary<string, object> { { "success", success }, { "tag_id", tag_id } };
        }

        protected virtual string _DictToString(Dictionary<string, object> d)
        {
            if (d == null) return "{}";
            var parts = new List<string>();
            foreach (var kv in d) parts.Add(kv.Key + "=" + (kv.Value == null ? "null" : kv.Value.ToString()));
            return "{" + string.Join(", ", parts) + "}";
        }

        // ==================== 效果堆栈 ====================

        public virtual string _register_timed_effect(string tag_id, Dictionary<string, object> parameters, float duration, Action<float> on_tick = null, Action on_expire = null)
        {
            _effect_counter += 1;
            string eid = "eff_" + _effect_counter;
            _active_effects[eid] = new Dictionary<string, object> {
                { "tag_id", tag_id },
                { "params", parameters },
                { "duration", duration },
                { "remaining", duration },
                { "on_tick", on_tick },
                { "on_expire", on_expire },
            };
            return eid;
        }

        public virtual void remove_tag_effect(string effect_id)
        {
            if (!_active_effects.ContainsKey(effect_id)) return;
            var effect = _active_effects[effect_id];
            var on_expire = effect.TryGetValue("on_expire", out var oe) ? oe as Action : null;
            if (on_expire != null) on_expire.Invoke();
            _active_effects.Remove(effect_id);
            var tag_id = _S(effect.TryGetValue("tag_id", out var ti) ? ti : "");
            effect_finished?.Invoke(tag_id, effect);
        }

        protected virtual void Update()
        {
            float delta = Time.deltaTime;
            // 优先级队列窗口计时
            if (_flush_timer > 0.0f)
            {
                _flush_timer -= delta;
                if (_flush_timer <= 0.0f) _flush_pending_tags();
            }

            // 活跃效果倒计时
            var to_remove = new List<string>();
            // 复制键集合避免修改时迭代
            var keys = new List<string>(_active_effects.Keys);
            foreach (var eid in keys)
            {
                var effect = _active_effects[eid];
                float remaining = _F(effect.TryGetValue("remaining", out var r) ? r : 0f);
                remaining -= delta;
                effect["remaining"] = remaining;
                // 每帧 tick
                var on_tick = effect.TryGetValue("on_tick", out var ot) ? ot as Action<float> : null;
                if (on_tick != null) on_tick.Invoke(delta);
                if (remaining <= 0.0f) to_remove.Add(eid);
            }
            foreach (var eid in to_remove) remove_tag_effect(eid);
        }

        // ==================== 辅助函数 ====================

        public virtual PlayerController _get_caster(int caster_id)
        {
            foreach (var p in players)
            {
                if (p != null && p.GetInstanceID() == caster_id) return p;
            }
            // 备用：通过 GameObject.FindObjectsOfType 查找
            var all = UnityEngine.Object.FindObjectsOfType<PlayerController>();
            foreach (var p in all)
            {
                if (p != null && p.GetInstanceID() == caster_id) return p;
            }
            return null;
        }

        public virtual List<PlayerController> _get_enemies(PlayerController caster)
        {
            var result = new List<PlayerController>();
            if (caster == null) return result;
            string enemy_team = (caster.team == "a" || caster.team == "A") ? "B" : "A";
            foreach (var p in players)
            {
                if (p == null) continue;
                if (p.team != enemy_team) continue;
                if (p.state == PlayerState.Defeated) continue;
                // 隐身者不在敌方索敌列表中
                if (p.IsStatusActive("stealthed")) continue;
                result.Add(p);
            }
            return result;
        }

        public virtual PlayerController _get_nearest_enemy(PlayerController caster)
        {
            var enemies = _get_enemies(caster);
            PlayerController nearest = null;
            float min_dist = float.MaxValue;
            foreach (var e in enemies)
            {
                float dist = Vector2.Distance(_XZ(caster.transform), _XZ(e.transform));
                if (dist < min_dist) { min_dist = dist; nearest = e; }
            }
            return nearest;
        }

        // ==================== 对球效果实现 ====================

        // 01 增伤
        public virtual void _apply_ball_dmg_up(Dictionary<string, object> parameters)
        {
            float val = _F(parameters.TryGetValue("value", out var v) ? v : 0);
            string vtype = _S(parameters.TryGetValue("value_type", out var vt) ? vt : "percentage");

            float dmg_mult = _F(_ball_mods.TryGetValue("dmg_mult", out var dm) ? dm : 1.0f);
            float dmg_flat = _F(_ball_mods.TryGetValue("dmg_flat", out var df) ? df : 0.0f);

            if (vtype == "percentage") dmg_mult += val / 100.0f;
            else dmg_flat += val;

            _ball_mods["dmg_mult"] = dmg_mult;
            _ball_mods["dmg_flat"] = dmg_flat;
            Debug.Log("[TagEffect] 增伤: type=" + vtype + " val=" + val.ToString("F1") + " mult=" + dmg_mult.ToString("F2") + " flat=" + dmg_flat.ToString("F1"));
        }

        // 02 减伤
        public virtual void _apply_ball_dmg_down(Dictionary<string, object> parameters)
        {
            float val = _F(parameters.TryGetValue("value", out var v) ? v : 0);
            string vtype = _S(parameters.TryGetValue("value_type", out var vt) ? vt : "percentage");

            float dmg_mult = _F(_ball_mods.TryGetValue("dmg_mult", out var dm) ? dm : 1.0f);
            float dmg_flat = _F(_ball_mods.TryGetValue("dmg_flat", out var df) ? df : 0.0f);

            if (vtype == "percentage") dmg_mult -= val / 100.0f;
            else dmg_flat -= val;

            dmg_mult = Mathf.Max(0.0f, dmg_mult);
            _ball_mods["dmg_mult"] = dmg_mult;
            _ball_mods["dmg_flat"] = dmg_flat;
            Debug.Log("[TagEffect] 减伤: mult=" + dmg_mult.ToString("F2") + " flat=" + dmg_flat.ToString("F1"));
        }

        // 03 穿透
        public virtual void _apply_ball_penetrate(Dictionary<string, object> parameters)
        {
            _ball_mods["penetrate"] = true;
            Debug.Log("[TagEffect] 穿透: 启用");
        }

        // 04 护甲
        public virtual void _apply_ball_armor(Dictionary<string, object> parameters)
        {
            float val = _F(parameters.TryGetValue("value", out var v) ? v : 0);
            float armor = _F(_ball_mods.TryGetValue("armor", out var ar) ? ar : 0.0f);
            armor += val;
            _ball_mods["armor"] = armor;
            Debug.Log("[TagEffect] 护甲: armor=" + armor.ToString("F1"));
        }

        // 05 加速
        public virtual void _apply_ball_speed_up(Dictionary<string, object> parameters)
        {
            float mult = _F(parameters.TryGetValue("multiplier", out var m) ? m : 0);
            // 兼容 registry 的 value 字段 (ball_speed_up_flat 用 value)
            float fixed_val = 0f;
            if (parameters.TryGetValue("value", out var v)) fixed_val = _F(v);
            else if (parameters.TryGetValue("fixed_value", out var fv)) fixed_val = _F(fv);

            float speed_mult = _F(_ball_mods.TryGetValue("speed_mult", out var sm) ? sm : 1.0f);
            float speed_flat = _F(_ball_mods.TryGetValue("speed_flat", out var sf) ? sf : 0.0f);

            if (mult > 0) speed_mult *= mult;
            if (fixed_val != 0) speed_flat += fixed_val;

            _ball_mods["speed_mult"] = speed_mult;
            _ball_mods["speed_flat"] = speed_flat;
            Debug.Log("[TagEffect] 球加速: mult=" + speed_mult.ToString("F2") + " flat=" + speed_flat.ToString("F1"));
        }

        // 06 减速
        public virtual void _apply_ball_speed_down(Dictionary<string, object> parameters)
        {
            float mult = _F(parameters.TryGetValue("multiplier", out var m) ? m : 0);
            float fixed_val = 0f;
            if (parameters.TryGetValue("value", out var v)) fixed_val = _F(v);
            else if (parameters.TryGetValue("fixed_value", out var fv)) fixed_val = _F(fv);

            float speed_mult = _F(_ball_mods.TryGetValue("speed_mult", out var sm) ? sm : 1.0f);
            float speed_flat = _F(_ball_mods.TryGetValue("speed_flat", out var sf) ? sf : 0.0f);

            if (mult > 0) speed_mult /= mult;
            if (fixed_val != 0) speed_flat -= fixed_val;

            speed_mult = Mathf.Max(0.1f, speed_mult);
            _ball_mods["speed_mult"] = speed_mult;
            _ball_mods["speed_flat"] = speed_flat;
            Debug.Log("[TagEffect] 球减速: mult=" + speed_mult.ToString("F2") + " flat=" + speed_flat.ToString("F1"));
        }

        // 07 范围扩大/AOE
        public virtual void _apply_ball_range_up(Dictionary<string, object> parameters)
        {
            float radius = _F(parameters.TryGetValue("radius", out var r) ? r : 0);
            float dmg_pct = _F(parameters.TryGetValue("damage_pct", out var dp) ? dp : 0);
            float mult = _F(parameters.TryGetValue("multiplier", out var m) ? m : 0);

            float range_mult = _F(_ball_mods.TryGetValue("range_mult", out var rm) ? rm : 1.0f);
            float aoe_radius = _F(_ball_mods.TryGetValue("aoe_radius", out var ar) ? ar : 0.0f);
            float aoe_damage_pct = _F(_ball_mods.TryGetValue("aoe_damage_pct", out var adp) ? adp : 0.5f);

            if (radius > 0) aoe_radius = radius;
            if (dmg_pct > 0) aoe_damage_pct = dmg_pct;
            if (mult > 0) range_mult *= mult;

            _ball_mods["range_mult"] = range_mult;
            _ball_mods["aoe_radius"] = aoe_radius;
            _ball_mods["aoe_damage_pct"] = aoe_damage_pct;
            Debug.Log("[TagEffect] 范围扩大/AOE: radius=" + aoe_radius.ToString("F1") + " dmg_pct=" + aoe_damage_pct.ToString("F2") + " mult=" + range_mult.ToString("F2"));
        }

        // 08 范围缩小
        public virtual void _apply_ball_range_down(Dictionary<string, object> parameters)
        {
            float mult = _F(parameters.TryGetValue("multiplier", out var m) ? m : 0);
            float range_mult = _F(_ball_mods.TryGetValue("range_mult", out var rm) ? rm : 1.0f);
            if (mult > 0) range_mult /= mult;
            range_mult = Mathf.Max(0.1f, range_mult);
            _ball_mods["range_mult"] = range_mult;
            Debug.Log("[TagEffect] 范围缩小: mult=" + range_mult.ToString("F2"));
        }

        // 09 精准锁定
        public virtual void _apply_ball_lockon(Dictionary<string, object> parameters, int caster_id)
        {
            var caster = _get_caster(caster_id);
            if (caster != null)
            {
                var target = _get_nearest_enemy(caster);
                if (target != null)
                {
                    _ball_mods["lockon_target"] = target.transform;
                    Debug.Log("[TagEffect] 精准锁定: 目标=" + _S(caster.charData.TryGetValue("name", out var n) ? n : "?"));
                }
                else
                {
                    Debug.Log("[TagEffect] 精准锁定: 无目标");
                }
            }
            else
            {
                Debug.Log("[TagEffect] 精准锁定: 找不到施法者");
            }
        }

        // 11 追踪
        public virtual void _apply_ball_tracking(Dictionary<string, object> parameters, int caster_id)
        {
            var caster = _get_caster(caster_id);
            if (caster == null)
            {
                Debug.Log("[TagEffect] 追踪: 找不到施法者");
                return;
            }
            var target = _get_nearest_enemy(caster);
            if (target != null)
            {
                _ball_mods["tracking_target"] = target.transform;
                _ball_mods["tracking_turn_speed"] = _F(parameters.TryGetValue("turn_speed", out var ts) ? ts : 3.0f);
                Debug.Log("[TagEffect] 追踪: 目标=" + _S(caster.charData.TryGetValue("name", out var n) ? n : "?")
                          + " 转速=" + _F(_ball_mods["tracking_turn_speed"]).ToString("F1"));
            }
            else
            {
                Debug.Log("[TagEffect] 追踪: 无目标");
            }
        }

        // 13 回旋
        public virtual void _apply_ball_boomerang(Dictionary<string, object> parameters)
        {
            _ball_mods["boomerang"] = true;
            _ball_mods["boomerang_dist"] = _F(parameters.TryGetValue("return_distance", out var rd) ? rd : 0.5f);
            Debug.Log("[TagEffect] 回旋: 启用 返回点=" + (_F(_ball_mods["boomerang_dist"]) * 100).ToString("F0") + "%");
        }

        // 14 直行
        public virtual void _apply_ball_straight(Dictionary<string, object> parameters)
        {
            _ball_mods["lock_straight"] = true;
            // 清除追踪和回旋
            _ball_mods["tracking_target"] = null;
            _ball_mods["boomerang"] = false;
            Debug.Log("[TagEffect] 直行: 启用，禁用追踪/回旋");
        }

        // ==================== 对场地效果 ====================

        public virtual void _apply_field_obs_add(Dictionary<string, object> parameters)
        {
            var manager = _get_obstacle_manager();
            if (manager == null)
            {
                Debug.LogError("[TagEffectHandler] 找不到 ObstacleManager");
                return;
            }

            // 补充元素颜色
            if (!parameters.ContainsKey("element_color"))
            {
                string element = _S(parameters.TryGetValue("element", out var e) ? e : "");
                parameters["element_color"] = _get_element_color(element);
            }

            // 补充来源技能
            if (!parameters.ContainsKey("source_skill"))
            {
                parameters["source_skill"] = _S(parameters.TryGetValue("skill_id", out var sid) ? sid : "");
            }

            // 补充释放球员位置
            int cid = _I(parameters.TryGetValue("caster_id", out var cidObj) ? cidObj : 0);
            var caster_node = _get_caster(cid);
            if (caster_node != null)
            {
                parameters["caster_position"] = _XZ(caster_node.transform);
            }

            int mouse_ops = _I(parameters.TryGetValue("mouse_ops", out var mo) ? mo : 1);
            // C# ObstacleManager 接口待定，暂用 SendMessage 占位
            manager.SendMessage("StartPlacing", new object[] { parameters, mouse_ops }, SendMessageOptions.DontRequireReceiver);

            Debug.Log("[TagEffectHandler] 创造障碍: shape=" + _S(parameters.TryGetValue("shape", out var sh) ? sh : "rect")
                      + " hp=" + _F(parameters.TryGetValue("hp", out var hp) ? hp : 50.0).ToString("F0")
                      + " atk_consume=" + _F(parameters.TryGetValue("attack_consume_rate", out var acr) ? acr : 20.0).ToString("F0") + "/s"
                      + " spd_consume=" + _F(parameters.TryGetValue("speed_consume_rate", out var scr) ? scr : 20.0).ToString("F0") + "px/s"
                      + " mouse_ops=" + mouse_ops);
        }

        public virtual void _apply_field_obs_clear(Dictionary<string, object> parameters)
        {
            var manager = _get_obstacle_manager();
            if (manager == null)
            {
                Debug.LogError("[TagEffectHandler] 找不到 ObstacleManager");
                return;
            }

            int clear_count = _I(parameters.TryGetValue("clear_count", out var cc) ? cc : 1);
            int mouse_ops = _I(parameters.TryGetValue("mouse_ops", out var mo) ? mo : 1);
            manager.SendMessage("StartClearing", new object[] { clear_count, mouse_ops }, SendMessageOptions.DontRequireReceiver);

            Debug.Log("[TagEffectHandler] 清除障碍: clear_count=" + clear_count + " mouse_ops=" + mouse_ops);
        }

        public virtual void _apply_field_obs_move(Dictionary<string, object> parameters) { }
        public virtual void _apply_field_obs_lock(Dictionary<string, object> parameters) { }
        public virtual void _apply_field_terra_change(Dictionary<string, object> parameters) { }
        public virtual void _apply_field_terra_revert(Dictionary<string, object> parameters) { }
        public virtual void _apply_field_zone_mark(Dictionary<string, object> parameters) { }
        public virtual void _apply_field_zone_clear(Dictionary<string, object> parameters) { }

        public virtual void _apply_field_zone_effect(Dictionary<string, object> parameters, int zone_type)
        {
            var manager = _get_field_zone_manager();
            if (manager == null)
            {
                Debug.LogError("[TagEffectHandler] 找不到 FieldZoneManager");
                return;
            }

            // 构建区域参数
            var zone_params = new Dictionary<string, object>();
            zone_params["zone_type"] = zone_type;
            zone_params["width"] = _F(parameters.TryGetValue("width", out var w) ? w : 120.0f) / 100.0f;
            zone_params["height"] = _F(parameters.TryGetValue("height", out var h) ? h : 120.0f) / 100.0f;
            zone_params["duration"] = _F(parameters.TryGetValue("duration", out var d) ? d : 10.0f);

            // 效果值
            switch (zone_type)
            {
                case 0:  // 加速
                    zone_params["effect_value"] = _F(parameters.TryGetValue("boost_multiplier", out var bm) ? bm : 1.5f);
                    break;
                case 1:  // 减速
                    zone_params["effect_value"] = _F(parameters.TryGetValue("slow_multiplier", out var sm) ? sm : 1.5f);
                    break;
                case 2:  // 危险
                    zone_params["effect_value"] = _F(parameters.TryGetValue("damage_value", out var dv) ? dv : 10.0f);
                    break;
                case 3:  // 安全
                    zone_params["effect_value"] = 0.0f;
                    break;
            }

            // 补充来源技能
            if (!parameters.ContainsKey("source_skill"))
            {
                zone_params["source_skill"] = _S(parameters.TryGetValue("skill_id", out var sid) ? sid : "");
            }

            int mouse_ops = _I(parameters.TryGetValue("mouse_ops", out var mo) ? mo : 1);
            manager.SendMessage("StartPlacing", new object[] { zone_params, mouse_ops }, SendMessageOptions.DontRequireReceiver);

            string[] type_names = { "加速区", "减速区", "危险区", "安全区" };
            Debug.Log("[TagEffectHandler] 区域效果: " + type_names[zone_type]
                      + " size=" + _F(zone_params["width"]).ToString("F0") + "x" + _F(zone_params["height"]).ToString("F0")
                      + " dur=" + _F(zone_params["duration"]).ToString("F1") + "s"
                      + " mouse_ops=" + mouse_ops);
        }

        public virtual void _apply_field_illusion_add(Dictionary<string, object> parameters)
        {
            var manager = _get_illusion_manager();
            if (manager == null)
            {
                Debug.LogError("[TagEffectHandler] 找不到 IllusionManager");
                return;
            }

            var source = parameters.TryGetValue("source_player", out var sp) ? sp as PlayerController : null;
            if (source == null)
            {
                Debug.LogError("[TagEffectHandler] 幻象缺少 source_player");
                return;
            }

            int mouse_ops = _I(parameters.TryGetValue("count", out var c) ? c : 1);
            manager.SendMessage("StartPlacing", new object[] { parameters, mouse_ops }, SendMessageOptions.DontRequireReceiver);

            Debug.Log("[TagEffectHandler] 幻象生成: mode=" + _S(parameters.TryGetValue("place_mode", out var pm) ? pm : "any")
                      + " count=" + mouse_ops
                      + " stamina=" + _F(parameters.TryGetValue("stamina", out var st) ? st : source.maxStamina).ToString("F0")
                      + " dur=" + _F(parameters.TryGetValue("duration", out var d) ? d : 10.0f).ToString("F1") + "s"
                      + " ai=" + _S(parameters.TryGetValue("ai_mode", out var am) ? am : false));
        }

        public virtual void _apply_field_illusion_clear(Dictionary<string, object> parameters)
        {
            var manager = _get_illusion_manager();
            if (manager == null)
            {
                Debug.LogError("[TagEffectHandler] 找不到 IllusionManager");
                return;
            }
            manager.SendMessage("ClearAllIllusions", SendMessageOptions.DontRequireReceiver);
            Debug.Log("[TagEffectHandler] 幻象破除: 清除场上所有幻象");
        }

        // ==================== 辅助方法 ====================

        public virtual Component _get_obstacle_manager()
        {
            if (battle_manager != null)
            {
                var t = battle_manager.transform.Find("ObstacleManager");
                if (t != null) return t;
            }
            // 独立测试 fallback：通过 GameObject.Find 查找
            var go = GameObject.Find("ObstacleManager");
            return go != null ? go.transform : null;
        }

        public virtual Component _get_field_zone_manager()
        {
            if (battle_manager != null)
            {
                var t = battle_manager.transform.Find("FieldZoneManager");
                if (t != null) return t;
            }
            var go = GameObject.Find("FieldZoneManager");
            return go != null ? go.transform : null;
        }

        public virtual Component _get_illusion_manager()
        {
            if (battle_manager != null)
            {
                var t = battle_manager.transform.Find("IllusionManager");
                if (t != null) return t;
            }
            var go = GameObject.Find("IllusionManager");
            return go != null ? go.transform : null;
        }

        public virtual Color _get_element_color(string element)
        {
            var colors = new Dictionary<string, Color> {
                { "金刚", new Color(0.85f, 0.75f, 0.3f) },
                { "大地", new Color(0.7f, 0.55f, 0.35f) },
                { "雷火", new Color(1.0f, 0.4f, 0.2f) },
                { "冰雪", new Color(0.4f, 0.8f, 1.0f) },
                { "草木", new Color(0.3f, 0.8f, 0.3f) },
                { "梦幻", new Color(0.7f, 0.5f, 0.9f) },
            };
            return colors.TryGetValue(element, out var c) ? c : new Color(1.0f, 1.0f, 0.5f);
        }

        // ==================== 球员标签通用函数 ====================

        // 获取目标球员列表
        public virtual List<PlayerController> _get_player_targets(Dictionary<string, object> parameters, int caster_id)
        {
            string target_mode = _S(parameters.TryGetValue("target", out var t) ? t : "self");
            var caster = _get_caster(caster_id);
            var result = new List<PlayerController>();
            if (caster == null) return result;
            switch (target_mode)
            {
                case "self":
                    result.Add(caster);
                    break;
                case "enemies":
                    result = _get_enemies(caster);
                    break;
                case "allies":
                    foreach (var p in players)
                    {
                        if (p != null && p.team == caster.team && p.state != PlayerState.Defeated)
                            result.Add(p);
                    }
                    break;
                case "nearest_enemy":
                    var nearest = _get_nearest_enemy(caster);
                    if (nearest != null) result.Add(nearest);
                    else result = _get_enemies(caster);
                    break;
                default:
                    result.Add(caster);
                    break;
            }
            return result;
        }

        // === ①属性类通用 ===
        public virtual void _apply_player_stat_buff(Dictionary<string, object> parameters, int caster_id, string stat, float mult, float flat)
        {
            var targets = _get_player_targets(parameters, caster_id);
            float duration = _F(parameters.TryGetValue("duration", out var d) ? d : 5.0f);
            string source_tag = _S(parameters.TryGetValue("_tag_id", out var ti) ? ti : "");
            foreach (var target in targets)
            {
                // 读取并消费双倍/减半倍率 (来自 player_spirit_double/half 标签)
                float skill_mult = target.GetAndConsumeNextSkillMult();
                float final_mult = 1.0f + (mult - 1.0f) * skill_mult;
                float final_flat = flat * skill_mult;
                _effect_counter += 1;
                string buff_id = "stat_" + _effect_counter + "_" + stat + "_" + target.GetInstanceID();
                // 调用 PlayerController.AddBuff 6 参数版本 (待补充)
                target.AddBuff(buff_id, stat, final_mult, final_flat, duration, source_tag);
                if (skill_mult != 1.0f)
                {
                    Debug.Log("[TagEffect] 双倍/减半生效: skill_mult=" + skill_mult.ToString("F2")
                              + " -> mult=" + final_mult.ToString("F2") + " flat=" + final_flat.ToString("F1"));
                }
                Debug.Log("[TagEffect] 属性buff: stat=" + stat + " mult=" + final_mult.ToString("F2")
                          + " flat=" + final_flat.ToString("F1") + " dur=" + duration.ToString("F1") + "s"
                          + " target=" + _S(target.charData.TryGetValue("name", out var n) ? n : "?"));
            }
        }

        // === ②状态类通用 ===
        public virtual void _apply_player_status(Dictionary<string, object> parameters, int caster_id, string status)
        {
            var targets = _get_player_targets(parameters, caster_id);
            float duration = _F(parameters.TryGetValue("duration", out var d) ? d : 3.0f);
            foreach (var target in targets)
            {
                bool ok = target.TurnOnLight(status, duration);
                if (!ok)
                {
                    Debug.Log("[TagEffect] " + status + " 被免控挡住: target="
                              + _S(target.charData.TryGetValue("name", out var n) ? n : "?"));
                }
            }
            Debug.Log("[TagEffect] 状态: " + status + " dur=" + duration.ToString("F1") + "s targets=" + targets.Count);
        }

        // === 易伤 ===
        public virtual void _apply_player_vulnerable(Dictionary<string, object> parameters, int caster_id)
        {
            var targets = _get_player_targets(parameters, caster_id);
            float duration = _F(parameters.TryGetValue("duration", out var d) ? d : 3.0f);
            float mult = _F(parameters.TryGetValue("multiplier", out var m) ? m : 1.5f);
            foreach (var target in targets)
            {
                target.TurnOnLight("vulnerable", duration, new Dictionary<string, object> { { "multiplier", mult } });
            }
            Debug.Log("[TagEffect] 易伤: mult=" + mult.ToString("F1") + " dur=" + duration.ToString("F1") + "s targets=" + targets.Count);
        }

        // === 显形 ===
        public virtual void _apply_player_reveal(Dictionary<string, object> parameters, int caster_id)
        {
            var caster = _get_caster(caster_id);
            if (caster == null) return;
            var enemies = _get_all_enemies(caster);
            int count = 0;
            foreach (var e in enemies)
            {
                if (e.IsStatusActive("stealthed"))
                {
                    e.TurnOffLight("stealthed");
                    count += 1;
                }
            }
            Debug.Log("[TagEffect] 显形: " + count + "个隐身目标");
        }

        // === 体力恢复/扣除 (%) ===
        public virtual void _apply_player_hp_heal_pct(Dictionary<string, object> parameters, int caster_id)
        {
            var targets = _get_player_targets(parameters, caster_id);
            float pct = _F(parameters.TryGetValue("value", out var v) ? v : 20) / 100.0f;
            foreach (var target in targets)
            {
                float skill_mult = target.GetAndConsumeNextSkillMult();
                float heal = target.maxStamina * pct * skill_mult;
                target.stamina = Mathf.Min(target.maxStamina, target.stamina + heal);
            }
        }

        public virtual void _apply_player_hp_damage_pct(Dictionary<string, object> parameters, int caster_id)
        {
            var targets = _get_player_targets(parameters, caster_id);
            float pct = _F(parameters.TryGetValue("value", out var v) ? v : 20) / 100.0f;
            foreach (var target in targets)
            {
                if (target.IsStatusActive("invincible")) continue;
                float skill_mult = target.GetAndConsumeNextSkillMult();
                float dmg = target.maxStamina * pct * skill_mult;
                target.stamina = Mathf.Max(0.0f, target.stamina - dmg);
                if (target.stamina <= 0.0f && target.state != PlayerState.Defeated) target._OnDefeated();
            }
        }

        // === 体力恢复/扣除 (固定) ===
        public virtual void _apply_player_hp_heal_flat(Dictionary<string, object> parameters, int caster_id)
        {
            var targets = _get_player_targets(parameters, caster_id);
            float val = _F(parameters.TryGetValue("value", out var v) ? v : 30);
            foreach (var target in targets)
            {
                float skill_mult = target.GetAndConsumeNextSkillMult();
                target.stamina = Mathf.Min(target.maxStamina, target.stamina + val * skill_mult);
            }
        }

        public virtual void _apply_player_hp_damage_flat(Dictionary<string, object> parameters, int caster_id)
        {
            var targets = _get_player_targets(parameters, caster_id);
            float val = _F(parameters.TryGetValue("value", out var v) ? v : 30);
            foreach (var target in targets)
            {
                if (target.IsStatusActive("invincible")) continue;
                float skill_mult = target.GetAndConsumeNextSkillMult();
                target.stamina = Mathf.Max(0.0f, target.stamina - val * skill_mult);
                if (target.stamina <= 0.0f && target.state != PlayerState.Defeated) target._OnDefeated();
            }
        }

        // === ③持续类 ===
        public virtual void _apply_player_hp_regen(Dictionary<string, object> parameters, int caster_id)
        {
            var targets = _get_player_targets(parameters, caster_id);
            float rate = _F(parameters.TryGetValue("value", out var v) ? v : 5);
            float duration = _F(parameters.TryGetValue("duration", out var d) ? d : 5.0f);
            foreach (var target in targets)
            {
                float skill_mult = target.GetAndConsumeNextSkillMult();
                target.AddTickEffect("hp_regen_" + target.GetInstanceID(), "regen", rate * skill_mult, duration);
                if (skill_mult != 1.0f)
                {
                    Debug.Log("[TagEffect] 持续恢复双倍/减半生效: rate=" + rate.ToString("F1")
                              + " × " + skill_mult.ToString("F2") + " = " + (rate * skill_mult).ToString("F1") + "/s");
                }
            }
            Debug.Log("[TagEffect] 持续恢复: rate=" + rate.ToString("F1") + "/s dur=" + duration.ToString("F1") + "s");
        }

        public virtual void _apply_player_hp_dot(Dictionary<string, object> parameters, int caster_id)
        {
            var targets = _get_player_targets(parameters, caster_id);
            float rate = _F(parameters.TryGetValue("value", out var v) ? v : 5);
            float duration = _F(parameters.TryGetValue("duration", out var d) ? d : 5.0f);
            foreach (var target in targets)
            {
                float skill_mult = target.GetAndConsumeNextSkillMult();
                target.AddTickEffect("hp_dot_" + target.GetInstanceID(), "dot", rate * skill_mult, duration, new List<string> { "attack" });
                if (skill_mult != 1.0f)
                {
                    Debug.Log("[TagEffect] 持续掉血双倍/减半生效: rate=" + rate.ToString("F1")
                              + " × " + skill_mult.ToString("F2") + " = " + (rate * skill_mult).ToString("F1") + "/s");
                }
            }
            Debug.Log("[TagEffect] 持续掉血: rate=" + rate.ToString("F1") + "/s dur=" + duration.ToString("F1") + "s");
        }

        // === 解控 ===
        public virtual void _apply_player_unroot(Dictionary<string, object> parameters, int caster_id)
        {
            var targets = _get_player_targets(parameters, caster_id);
            foreach (var target in targets)
            {
                target.TurnOffLight("rooted");
            }
            Debug.Log("[TagEffect] 解控: targets=" + targets.Count);
        }

        // === 能量恢复/消耗 (%) ===
        public virtual void _apply_player_energy_pct(Dictionary<string, object> parameters, int caster_id, bool is_gain)
        {
            var targets = _get_player_targets(parameters, caster_id);
            float pct = _F(parameters.TryGetValue("value", out var v) ? v : 20) / 100.0f;
            foreach (var target in targets)
            {
                float skill_mult = target.GetAndConsumeNextSkillMult();
                float amt = target.max_spirit_energy * pct * skill_mult;
                if (is_gain)
                {
                    target.spirit_energy = Mathf.Min(target._GetEffectiveValue("max_energy", target.max_spirit_energy), target.spirit_energy + amt);
                }
                else
                {
                    target.spirit_energy = Mathf.Max(0.0f, target.spirit_energy - amt);
                }
            }
        }

        // === 能量恢复/消耗 (固定) ===
        public virtual void _apply_player_energy_flat(Dictionary<string, object> parameters, int caster_id, bool is_gain)
        {
            var targets = _get_player_targets(parameters, caster_id);
            float val = _F(parameters.TryGetValue("value", out var v) ? v : 20);
            foreach (var target in targets)
            {
                float skill_mult = target.GetAndConsumeNextSkillMult();
                if (is_gain)
                {
                    target.spirit_energy = Mathf.Min(target._GetEffectiveValue("max_energy", target.max_spirit_energy), target.spirit_energy + val * skill_mult);
                }
                else
                {
                    target.spirit_energy = Mathf.Max(0.0f, target.spirit_energy - val * skill_mult);
                }
            }
        }

        // === ④折扣类 ===
        public virtual void _apply_player_spirit_cost(Dictionary<string, object> parameters, int caster_id, bool is_down)
        {
            var targets = _get_player_targets(parameters, caster_id);
            float mult = _F(parameters.TryGetValue("multiplier", out var m) ? m : (is_down ? 0.8f : 1.2f));
            float duration = _F(parameters.TryGetValue("duration", out var d) ? d : 5.0f);
            foreach (var target in targets)
            {
                target.AddSkillCostMult("spirit_cost_" + target.GetInstanceID(), Mathf.Max(0.1f, mult), duration);
            }
            Debug.Log("[TagEffect] 消耗" + (is_down ? "减少" : "增加") + ": mult=" + mult.ToString("F2") + " dur=" + duration.ToString("F1") + "s");
        }

        public virtual void _apply_player_spirit_cd(Dictionary<string, object> parameters, int caster_id, bool is_down)
        {
            var targets = _get_player_targets(parameters, caster_id);
            float mult = _F(parameters.TryGetValue("multiplier", out var m) ? m : (is_down ? 0.8f : 1.2f));
            float duration = _F(parameters.TryGetValue("duration", out var d) ? d : 5.0f);
            foreach (var target in targets)
            {
                target.AddSkillCdMult("spirit_cd_" + target.GetInstanceID(), Mathf.Max(0.1f, mult), duration);
            }
            Debug.Log("[TagEffect] CD" + (is_down ? "缩短" : "延长") + ": mult=" + mult.ToString("F2") + " dur=" + duration.ToString("F1") + "s");
        }

        public virtual void _apply_player_spirit_uses(Dictionary<string, object> parameters, int caster_id)
        {
            var targets = _get_player_targets(parameters, caster_id);
            int bonus = _I(parameters.TryGetValue("bonus_uses", out var bu) ? bu : 1);
            string skill_id = _S(parameters.TryGetValue("skill_id", out var sid) ? sid : "");
            foreach (var target in targets)
            {
                if (!string.IsNullOrEmpty(skill_id))
                {
                    target.AddSkillBonusUses(skill_id, bonus);
                }
                else
                {
                    foreach (var sid2 in target.equipped_skills) target.AddSkillBonusUses(_S(sid2), bonus);
                }
            }
        }

        public virtual void _apply_player_spirit_double(Dictionary<string, object> parameters, int caster_id)
        {
            var targets = _get_player_targets(parameters, caster_id);
            foreach (var target in targets) target.AddNextSkillMult(2.0f);
            Debug.Log("[TagEffect] 下次技能效果翻倍");
        }

        public virtual void _apply_player_spirit_half(Dictionary<string, object> parameters, int caster_id)
        {
            var targets = _get_player_targets(parameters, caster_id);
            foreach (var target in targets) target.AddNextSkillMult(0.5f);
            Debug.Log("[TagEffect] 下次技能效果减半");
        }

        // === ⑤交互类 ===
        public virtual void _apply_player_teleport(Dictionary<string, object> parameters, int caster_id)
        {
            var targets = _get_player_targets(parameters, caster_id);
            float pos_x = _F(parameters.TryGetValue("pos_x", out var x) ? x : 0) / 100.0f;
            float pos_y = _F(parameters.TryGetValue("pos_y", out var y) ? y : 0) / 100.0f;
            foreach (var target in targets)
            {
                target.TeleportTo(new Vector2(pos_x, pos_y));
            }
        }

        public virtual void _apply_player_return(Dictionary<string, object> parameters, int caster_id)
        {
            var targets = _get_player_targets(parameters, caster_id);
            foreach (var target in targets) target.ReturnToPrevious();
        }

        // 获取所有敌方 (含隐身)
        public virtual List<PlayerController> _get_all_enemies(PlayerController caster)
        {
            var result = new List<PlayerController>();
            if (caster == null) return result;
            string enemy_team = (caster.team == "a" || caster.team == "A") ? "B" : "A";
            foreach (var p in players)
            {
                if (p != null && p.team == enemy_team && p.state != PlayerState.Defeated)
                    result.Add(p);
            }
            return result;
        }
    }
}

// ================================================================
// 决竞球 Godot→Unity 翻译: SkillVisualManager.cs [BattleBall.Systems.SpiritSystem]
// 源: E:/项目储存/决竞球battle-ball/scripts/systems/spirit_system/skill_visual_manager.gd
// 集成备注:
//   1) PlayerController 缺少 GetVisualRadius() 方法, 需后续补充
//   2) BallController 缺少 GetVisualRadius() / ball_caught 事件, 需后续补充
//      现使用 OnBallHitPlayer + OnBallReturned 作为近似事件
//   3) SkillOutlineNode 在 Unity 中是 MonoBehaviour, 用 new GameObject + AddComponent 创建
//   4) 标签注册表/技能数据 通过 DataManager 加载, 不再读 JSON 文件
// ================================================================
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using BattleBall.Core;
using BattleBall.Battle;

namespace BattleBall.Systems.SpiritSystem
{
    public class SkillVisualManager : MonoBehaviour
    {
        // === 引用 (由 battle_manager.setup() 注入) ===
        public BattleManager battle_manager = null;
        public BallController ball_node = null;
        public List<PlayerController> players = new List<PlayerController>();  // 全部球员 (含双方)

        // 活跃轮廓堆栈: { outline_key: SkillOutlineNode }
        // outline_key 规则:
        //   - 球类: "ball"
        //   - 球员类: "player:<player_id>"
        //   - 场地类: "field:<player_id>"
        protected Dictionary<string, SkillOutlineNode> _active_outlines = new Dictionary<string, SkillOutlineNode>();

        // 标签注册表缓存
        protected Dictionary<string, Dictionary<string, object>> _tags_registry = new Dictionary<string, Dictionary<string, object>>();

        // 技能缓存
        protected Dictionary<string, Dictionary<string, object>> _skills_cache = new Dictionary<string, Dictionary<string, object>>();
        protected bool _skills_loaded = false;

        // ===== 数值/类型转换辅助 =====
        protected static float _F(object o) { if (o == null) return 0f; float r; return float.TryParse(o.ToString(), out r) ? r : 0f; }
        protected static int _I(object o) { if (o == null) return 0; int r; return int.TryParse(o.ToString(), out r) ? r : 0; }
        protected static string _S(object o) { return o == null ? "" : o.ToString(); }
        protected static bool _B(object o) { if (o == null) return false; if (o is bool b) return b; bool r; return bool.TryParse(o.ToString(), out r) ? r : false; }
        protected static Dictionary<string, object> _Dict(object o) {
            if (o is IDictionary<string, object> d) return new Dictionary<string, object>(d);
            return new Dictionary<string, object>();
        }

        protected virtual void Awake()
        {
            _load_tags_registry();
        }

        // 加载标签注册表 (target_type 字段从这里读)
        protected virtual void _load_tags_registry()
        {
            var dm = DataManager.Instance;
            if (dm == null)
            {
                Debug.LogError("[SkillVisual] DataManager 未就绪");
                return;
            }
            if (dm.tags == null) return;
            foreach (var tag in dm.tags)
            {
                string id = _S(tag.TryGetValue("id", out var i) ? i : "");
                if (!string.IsNullOrEmpty(id)) _tags_registry[id] = tag;
            }
            Debug.Log("[SkillVisual] 加载 " + _tags_registry.Count + " 个标签");
        }

        // 注入战斗引用 (由 battle_manager 调用)
        public virtual void setup(BattleManager battle_mgr, BallController ball, List<PlayerController> all_players)
        {
            battle_manager = battle_mgr;
            ball_node = ball;
            players = all_players;

            // 连接球击中信号 (球类轮廓在击中人后清除)
            if (ball_node != null)
            {
                ball_node.OnBallHitPlayer -= _on_ball_hit_player;
                ball_node.OnBallHitPlayer += _on_ball_hit_player;
                ball_node.OnBallReturned -= _on_ball_caught;
                ball_node.OnBallReturned += _on_ball_caught;
            }

            Debug.Log("[SkillVisual] 初始化完成，监听 " + players.Count + " 个球员 registry=" + _tags_registry.Count);
        }

        // ==================== 外部入口 ====================

        // 技能激活时调用: 根据技能包含的标签, 渲染对应轮廓
        public virtual void on_skill_triggered(string skill_id, PlayerController caster, List<string> tag_ids)
        {
            if (caster == null) return;

            // 2026-06-20 诊断: 定位轮廓不显示的断点
            Debug.Log("[诊断轮廓] on_skill_triggered skill=" + skill_id + " tags=" + (tag_ids != null ? tag_ids.Count : 0) + " registry大小=" + _tags_registry.Count);

            // 收集该技能所有标签涉及的目标类型 (去重)
            var skill_data = _get_skill_data(skill_id);
            string element = _S(skill_data.TryGetValue("element", out var el) ? el : "");
            var outline_color = _get_element_color(element);

            // 遍历标签, 按 target_type 分发
            if (tag_ids == null) return;
            foreach (var tag_id in tag_ids)
            {
                var tag_data = _tags_registry.TryGetValue(tag_id, out var td) ? td : null;
                if (tag_data == null || tag_data.Count == 0)
                {
                    Debug.Log("[诊断轮廓] ✗ tag '" + tag_id + "' 不在注册表（跳过）");
                    continue;
                }
                string target_type = _S(tag_data.TryGetValue("target_type", out var tt) ? tt : "");
                Debug.Log("[诊断轮廓] tag '" + tag_id + " target_type='" + target_type + "' → 渲染");
                switch (target_type)
                {
                    case "ball":
                        _apply_ball_outline(outline_color);
                        break;
                    case "player":
                        _apply_player_outline(caster, outline_color);
                        break;
                    case "field":
                        _apply_field_outline(caster, outline_color);
                        break;
                    default:
                        Debug.Log("[诊断轮廓] ✗ 未知 target_type='" + target_type + "'（不渲染）");
                        break;
                }
            }
        }

        // 效果结束时调用: 清除指定球员关联的轮廓
        public virtual void on_effect_finished(int caster_id)
        {
            // 清除该施法者关联的球员轮廓和场地轮廓
            string player_key = "player:" + caster_id;
            string field_key = "field:" + caster_id;
            _remove_outline(player_key);
            _remove_outline(field_key);
        }

        // 清空所有轮廓 (比赛结束/重置时调用)
        public virtual void clear_all()
        {
            var keys = new List<string>(_active_outlines.Keys);
            foreach (var key in keys) _remove_outline(key);
            Debug.Log("[SkillVisual] 已清空所有轮廓");
        }

        // ==================== 轮廓渲染 ====================

        // 球类轮廓: 在球节点下加一圈外膜 (大于球本体)
        protected virtual void _apply_ball_outline(Color color)
        {
            if (ball_node == null) return;
            // 球类轮廓全局唯一 (球只有一个), 直接覆盖
            _remove_outline("ball");
            float radius = ball_node.GetVisualRadius();
            var outline = _create_ring_panel(radius, 0.06f, color, 0.55f);  // 6px → 0.06m
            outline.name = "BallSkillOutline";
            outline.transform.SetParent(ball_node.transform, false);
            _active_outlines["ball"] = outline;
            Debug.Log("[SkillVisual] 球类轮廓已显示 color=" + color.ToString());
        }

        // 球员类轮廓: 在球员节点下加一圈外膜
        protected virtual void _apply_player_outline(PlayerController player, Color color)
        {
            if (player == null) return;
            string key = "player:" + player.GetInstanceID();
            _remove_outline(key);
            float radius = player.GetVisualRadius();
            var outline = _create_ring_panel(radius, 0.05f, color, 0.55f);
            outline.name = "PlayerSkillOutline";
            outline.transform.SetParent(player.transform, false);
            _active_outlines[key] = outline;
            Debug.Log("[SkillVisual] 球员轮廓已显示 " + _S(player.charData.TryGetValue("name", out var n) ? n : "?") + " color=" + color.ToString());
        }

        // 场地类轮廓: 球员朝向那一面的边缘条带
        protected virtual void _apply_field_outline(PlayerController player, Color color)
        {
            if (player == null) return;
            string key = "field:" + player.GetInstanceID();
            _remove_outline(key);

            // 根据朝向决定条带位置 (八方向近似为四方向: 上/下/左/右)
            Vector2 facing = new Vector2(player.facingDir.x, player.facingDir.z);
            if (facing == Vector2.zero)
            {
                facing = (player.team == "A") ? Vector2.right : Vector2.left;
            }
            float radius = player.GetVisualRadius();
            var strip = _create_facing_strip(radius, facing, color, 0.75f);
            strip.name = "FieldSkillStrip";
            strip.transform.SetParent(player.transform, false);
            _active_outlines[key] = strip;
            Debug.Log("[SkillVisual] 场地朝向条带已显示 " + _S(player.charData.TryGetValue("name", out var n) ? n : "?") + " 朝向=" + facing.ToString());
        }

        // ==================== 视觉元素构造 ====================

        // 创建一个圆环外膜节点 (SkillOutlineNode 自绘, 避免 UI 坐标系混乱)
        // 半径外缘一圈半透明色 + 实色边框, 形成"外膜"视觉
        protected virtual SkillOutlineNode _create_ring_panel(float base_radius, float ring_width, Color color, float alpha)
        {
            var go = new GameObject("SkillOutlineRing");
            var outline = go.AddComponent<SkillOutlineNode>();
            outline.setup_ring(base_radius, ring_width, color, alpha);
            return outline;
        }

        // 创建球员朝向那一面的边缘条带 (场地标签专用)
        protected virtual SkillOutlineNode _create_facing_strip(float base_radius, Vector2 facing, Color color, float alpha)
        {
            var go = new GameObject("SkillOutlineStrip");
            var outline = go.AddComponent<SkillOutlineNode>();
            outline.setup_strip(base_radius, facing, color, alpha);
            return outline;
        }

        // ==================== 轮廓移除 ====================

        protected virtual void _remove_outline(string key)
        {
            if (!_active_outlines.TryGetValue(key, out var outline)) return;
            if (outline != null) Destroy(outline.gameObject);
            _active_outlines.Remove(key);
        }

        // ==================== 信号回调 ====================

        // 球击中人 → 清除球类轮廓
        protected virtual void _on_ball_hit_player(Transform _player, float _damage, string _effect)
        {
            _remove_outline("ball");
        }

        // 球被接住 → 清除球类轮廓
        protected virtual void _on_ball_caught(Transform _player)
        {
            _remove_outline("ball");
        }

        // ==================== 数据与颜色 ====================

        protected virtual void _load_skills_data()
        {
            if (_skills_loaded) return;
            var dm = DataManager.Instance;
            if (dm != null && dm.skills != null)
            {
                foreach (var s in dm.skills)
                {
                    string id = _S(s.TryGetValue("id", out var i) ? i : "");
                    if (!string.IsNullOrEmpty(id)) _skills_cache[id] = s;
                }
            }
            _skills_loaded = true;
        }

        public virtual Dictionary<string, object> _get_skill_data(string skill_id)
        {
            _load_skills_data();
            return _skills_cache.TryGetValue(skill_id, out var sd) ? sd : new Dictionary<string, object>();
        }

        // 元素颜色映射 (与 battle_hud/handler 统一)
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
            return colors.TryGetValue(element, out var c) ? c : new Color(0.6f, 0.6f, 0.6f);
        }
    }
}

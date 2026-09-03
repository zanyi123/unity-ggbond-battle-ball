using UnityEngine;
using System;
using System.Collections.Generic;

namespace BattleBall.Battle.Physics
{
    /// <summary>
    /// 幻象节点
    /// 复制自真身球员(player)的虚假实体
    /// - 视觉：克隆真身节点，颜色黯淡（异色/降透明度）
    /// - 行为：默认镜像模仿真身移动动作；开启AI智能→被AI接管保护真身
    /// - 挡伤：实体，球击中走防御减伤扣体力，无韧性系统（不弹飞/不僵直），球会被挡住
    /// - 消失：duration 与 stamina<=0 取先到
    /// - AI对接预留：加组 illusions + is_illusion 标记 + source_player 引用
    /// </summary>
    public class Illusion : MonoBehaviour
    {
        // ==================== 配置 ====================

        // 颜色：比真身黯淡（真身蓝/红 → 幻象降饱和+降透明）
        public const float PHANTOM_ALPHA = 0.55f;
        public static readonly Dictionary<string, Color> PHANTOM_TINT = new Dictionary<string, Color>
        {
            {"a", new Color(0.35f, 0.4f, 0.9f, PHANTOM_ALPHA)},   // 我方蓝→黯淡蓝
            {"b", new Color(0.9f, 0.35f, 0.4f, PHANTOM_ALPHA)},   // 敌方红→黯淡红
        };

        // ==================== 核心引用 ====================

        public BattleBall.Battle.PlayerController source_player = null;  // 真身
        public string illusion_id = "";
        public string illusion_team = "";

        // ==================== 运行时状态 ====================

        public float stamina = 100.0f;
        public float max_stamina = 100.0f;
        public float defense = 0.0f;
        public float defense_factor = 0.15f;
        public float speed = 2.0f;   // 原 GD 200 px/s → 2.0 m/s
        public float attack_power = 0.0f;

        public bool is_illusion = true;
        public bool is_defeated = false;
        public Vector3 facing_direction = new Vector3(1f, 0f, 0f);
        public float duration = 10.0f;
        public float remaining = 10.0f;

        // 球员标识字段（与 PlayerController 对齐，供 ball / 碰撞查询）
        public string team = "a";
        public bool is_player_controlled = false;
        public Dictionary<string, object> char_data = new Dictionary<string, object>();

        // 行为模式
        public bool ai_mode = false;

        // Buff堆栈（区域效果会挂 buff，幻象需要自己的栈）
        private Dictionary<string, object> _buffs = new Dictionary<string, object>();

        // 视觉节点引用
        private Transform _visual_root = null;

        // velocity (GD CharacterBody2D.velocity)
        public Vector3 velocity = Vector3.zero;

        // ==================== 信号 ====================

        public event Action<Illusion> illusion_expired;

        // ==================== 初始化 ====================

        public void setup(BattleBall.Battle.PlayerController source, Dictionary<string, object> parameters)
        {
            if (source == null)
            {
                Debug.LogError("[Illusion] 真身无效");
                return;
            }
            source_player = source;
            illusion_team = source.team;
            illusion_id = _str(_get(parameters, "illusion_id", ""));
            team = source.team;

            // 复制数值
            max_stamina = _float(_get(parameters, "stamina", source.maxStamina));
            stamina = max_stamina;
            defense = source.defense;
            defense_factor = source.defenseFactor;
            speed = source.moveSpeed;
            attack_power = source.attackPower;
            is_player_controlled = false;

            duration = _float(_get(parameters, "duration", 10.0f));
            remaining = duration;
            ai_mode = Convert.ToBoolean(_get(parameters, "ai_mode", false));

            // 视觉
            _build_visual(source);

            // tag (Unity 没有组，使用 tag 标记)
            gameObject.tag = "Illusion";
        }

        private void _build_visual(BattleBall.Battle.PlayerController source)
        {
            // 碰撞圆 (半径 28 → 0.28f)
            var collision_go = new GameObject("CollisionCircle");
            collision_go.transform.SetParent(transform, false);
            var circle_collider = collision_go.AddComponent<CircleCollider2D>();
            circle_collider.radius = 0.28f;
            // 等价于 GD collision_layer=1, collision_mask=0：仅作触发器
            circle_collider.isTrigger = false;

            // 背景色圆（黯淡色）
            var bg_go = new GameObject("AvatarBg");
            bg_go.transform.SetParent(transform, false);
            var sr = bg_go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(new Texture2D(1, 1), new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
            var tint = PHANTOM_TINT.TryGetValue(illusion_team, out var t) ? t : PHANTOM_TINT["a"];
            sr.color = tint;
            bg_go.transform.localScale = new Vector3(0.56f, 0.56f, 1f);
            sr.sortingOrder = 50;

            // 数字标签
            var label_go = new GameObject("AvatarLabel");
            label_go.transform.SetParent(transform, false);
            var tm = label_go.AddComponent<TextMesh>();
            string display = "#";
            if (source.charData != null && source.charData.TryGetValue("name", out var nm) && nm != null)
            {
                string ns = nm.ToString();
                if (ns.Length > 0) display = ns.Substring(0, 1);
            }
            tm.text = display;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            tm.fontSize = 24;
            label_go.transform.localPosition = Vector3.zero;
            label_go.transform.localScale = Vector3.one * 0.05f;

            _visual_root = bg_go.transform;
        }

        // ==================== 帧处理 ====================

        protected virtual void FixedUpdate()
        {
            if (is_defeated) return;

            float delta = Time.fixedDeltaTime;

            // 倒计时
            remaining -= delta;
            if (remaining <= 0.0f)
            {
                _expire("duration");
                return;
            }

            // 体力归零
            if (stamina <= 0.0f)
            {
                _expire("stamina");
                return;
            }

            // 移动行为
            if (ai_mode)
            {
                _ai_move(delta);
            }
            else
            {
                _mirror_source(delta);
            }

            // 真正位移（用 transform.position 增量移动）
            transform.position += velocity * delta;

            // buff 倒计时
            _tick_buffs(delta);

            // 朝向更新
            if (velocity.magnitude > 0.01f)
                facing_direction = velocity.normalized;
        }

        private void _mirror_source(float delta)
        {
            // 默认行为：镜像模仿真身的移动动作（速度方向+大小）
            if (source_player == null)
            {
                velocity = Vector3.zero;
                return;
            }
            // 直接复用真身当前速度
            // PlayerController 没有暴露 velocity 字段，用其 facing/moveSpeed 近似
            // 这里尽量贴近真身的运动方向与速率
            velocity = source_player.facingDir * source_player.moveSpeed;
            if (!source_player.isSprinting) velocity *= 0f; // 真身不在冲刺时不动
        }

        private void _ai_move(float _delta)
        {
            // AI接管模式：保护真身（占位，未来由 ai_manager 驱动）
            // 当前简单实现：贴在真身前方阻挡
            if (source_player == null)
            {
                velocity = Vector3.zero;
                return;
            }
            Vector3 target = source_player.transform.position + source_player.facingDir * 0.35f;
            Vector3 to_target = target - transform.position;
            if (to_target.magnitude > 0.04f)
            {
                velocity = to_target.normalized * speed;
            }
            else
            {
                velocity = Vector3.zero;
            }
        }

        // ==================== 挡伤（与真身流程相同，无韧性系统）====================

        public Dictionary<string, object> take_damage(float amount, BattleBall.Battle.PlayerController _attacker = null)
        {
            // 球击中幻象：走防御减伤，无韧性系统（不弹飞/不僵直）
            // 球会被挡住（实体），所以不改变球权——由 ball 处理球停
            if (is_defeated) return new Dictionary<string, object> { {"damage", 0f}, {"effect", "none"} };

            float dmg = amount;
            float def_resist = defense * defense_factor;
            float reduction_rate = Mathf.Min(def_resist / 100.0f, 0.8f);
            dmg *= (1.0f - reduction_rate);
            dmg = Mathf.Max(0.0f, dmg);

            stamina = Mathf.Max(0.0f, stamina - dmg);

            string effect = "blocked";

            if (stamina <= 0.0f) is_defeated = true;

            Debug.Log(string.Format("[Illusion] {0} 挡伤 {1:F1}(减伤后) 剩余体力{2:F0}", illusion_id, dmg, stamina));
            return new Dictionary<string, object> { {"damage", dmg}, {"effect", effect} };
        }

        public bool is_status_active(string _status_name)
        {
            // 幻象不复制真身技能状态，恒返回 false
            return false;
        }

        // ==================== 消失 ====================

        private void _expire(string reason)
        {
            if (is_defeated) return;
            is_defeated = true;
            Debug.Log("[Illusion] " + illusion_id + " 消失(原因:" + reason + ")");
            if (illusion_expired != null) illusion_expired(this);
            Destroy(gameObject);
        }

        public void force_remove()
        {
            _expire("cleared");
        }

        public string get_illusion_info()
        {
            return string.Format("{0} | 队{1} | 体力{2:F0}/{3:F0} | 剩余{4:F1}s | AI{5}",
                illusion_id, illusion_team, stamina, max_stamina, remaining,
                ai_mode ? "开" : "关");
        }

        // ==================== Buff系统（区域效果用）====================

        public void add_buff(string id, string stat, float mult, float flat, float duration, string source = "")
        {
            _buffs[id] = new Dictionary<string, object>
            {
                {"id", id}, {"stat", stat}, {"mult", mult}, {"flat", flat},
                {"source", source}, {"duration", duration}, {"remaining", duration}
            };
        }

        public bool remove_buff(string id) { return _buffs.Remove(id); }

        public float _get_effective_value(string stat, float base_value)
        {
            float m = 1.0f;
            float f = 0.0f;
            foreach (var kv in _buffs)
            {
                var b = _as_dict(kv.Value);
                if (_str(_get(b, "stat", "")) == stat)
                {
                    m *= _float(_get(b, "mult", 1.0f));
                    f += _float(_get(b, "flat", 0.0f));
                }
            }
            m = Mathf.Max(m, 0.01f);
            return base_value * m + f;
        }

        private void _tick_buffs(float delta)
        {
            var expired_ids = new List<string>();
            foreach (var kv in _buffs)
            {
                var b = _as_dict(kv.Value);
                float r = _float(_get(b, "remaining", 0.0f)) - delta;
                b["remaining"] = r;
                if (r <= 0.0f) expired_ids.Add(kv.Key);
            }
            foreach (var id in expired_ids) _buffs.Remove(id);
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
            return new Dictionary<string, object>();
        }
    }
}

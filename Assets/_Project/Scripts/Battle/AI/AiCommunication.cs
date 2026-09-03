// ================================================================
// 决竞球 Godot→Unity 合法骨架: AiCommunication.cs [BattleBall.Battle.AI]
// 源: E:/项目储存/决竞球battle-ball/scripts/battle/ai_communication.gd
// [TODO Step-E] 语义检查: sender 类型绑定、冷却数值、message_sent 事件订阅
// ================================================================
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BattleBall.Core;

namespace BattleBall.Battle.AI
{
    public enum AiMsgType
    {
        DEFEND_ALERT,   // 注意防守
        PASS_TO_ME,     // 传球给我
        DONT_PASS,      // 别传球
        SKILL_READY,    // 技能就绪
        BUFF_ON_YOU,    // 我给你加buff了
        NEED_BUFF       // 我需要buff支援
    }

    public class AiCommunication : MonoBehaviour
    {
        // [TODO Step-E] 原 GD: const AIProfile = preload("res://scripts/battle/ai_profile.gd")
        // C# 无需 preload, 直接用 AiProfile 类型

        // 消息文本（队友可见） 原 GD const MSG_TEXT = { MsgType.DEFEND_ALERT: "防守!", ... }
        public static readonly Dictionary<AiMsgType, string> MSG_TEXT = new Dictionary<AiMsgType, string>
        {
            { AiMsgType.DEFEND_ALERT, "防守!" },
            { AiMsgType.PASS_TO_ME, "传我!" },
            { AiMsgType.DONT_PASS, "别传!" },
            { AiMsgType.SKILL_READY, "技能就绪!" },
            { AiMsgType.BUFF_ON_YOU, "加油!" },
            { AiMsgType.NEED_BUFF, "需要支援!" }
        };

        // 信号：某球员发送了消息
        // [TODO Step-E] GD: CharacterBody2D sender → C# 暂用 Transform 占位, 后续绑定 PlayerController
        public event System.Action<Transform, int, string> message_sent;

        // 冷却与频率限制
        public const float PLAYER_COOLDOWN = 0.5f;         // 单个球员发送冷却（秒）
        public const float TEAM_FREQ_INTERVAL = 2.0f;      // 团队消息最小间隔（秒）
        public const float MESSAGE_DISPLAY_TIME = 0.5f;    // 消息显示时间（秒）

        // 每个球员的冷却计时 (player_id -> remaining_time_sec)
        public Dictionary<string, float> player_cooldowns = new Dictionary<string, float>();

        // 团队频率控制 team_id -> last_send_elapsed_time
        public Dictionary<string, float> team_last_msg_time = new Dictionary<string, float>();
        public float elapsed_time = 0.0f;

        // AI 管理器引用
        // [TODO Step-E] Node ai_manager → AiManager 引用赋值
        public AiManager ai_manager = null;
        // [TODO Step-E] Area2D ball_node → BallController
        public BattleBall.Battle.BallController ball_node = null;

        // 单例 (GD 原 Autoload)
        public static AiCommunication Instance { get; private set; }
        protected virtual void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }
        protected virtual void OnDestroy() { if (Instance == this) Instance = null; }

        // 原 _process(float delta)
        public virtual void Update()
        {
            elapsed_time += Time.deltaTime;
            // 衰减球员冷却
            var to_remove = new List<string>();
            foreach (var key in player_cooldowns.Keys.ToList())
            {
                player_cooldowns[key] -= Time.deltaTime;
                if (player_cooldowns[key] <= 0.0f) to_remove.Add(key);
            }
            foreach (var id in to_remove) player_cooldowns.Remove(id);

            // [TODO Step-E] evaluate_ai_messages 自动评估发送
            evaluate_ai_messages(Time.deltaTime);
        }

        // 球员发送消息（球员主动调用或AI调用）返回是否成功发送
        public virtual bool send_message(Transform sender, int msg_type, string team)
        {
            if (sender == null) return false;
            // [TODO Step-E] GD get_instance_id → Unity GetInstanceID
            string id = sender.GetInstanceID().ToString();

            // 单个球员冷却检查
            if (player_cooldowns.ContainsKey(id)) return false;

            // 团队频率检查
            if (team_last_msg_time.ContainsKey(team))
            {
                if (elapsed_time - team_last_msg_time[team] < TEAM_FREQ_INTERVAL)
                    return false;
            }

            // 通过检查，发送
            player_cooldowns[id] = PLAYER_COOLDOWN;
            team_last_msg_time[team] = elapsed_time;
            message_sent?.Invoke(sender, msg_type, team);
            AiMsgType mt = (AiMsgType)msg_type;
            string txt = MSG_TEXT.TryGetValue(mt, out var v) ? v : "?";
            Debug.Log(string.Format("[Comm] {0}: {1}", _pname(sender), txt));
            return true;
        }

        public virtual bool can_send(Transform sender)
        {
            if (sender == null) return false;
            string id = sender.GetInstanceID().ToString();
            if (player_cooldowns.ContainsKey(id)) return false;
            // [TODO Step-E] sender.team → 从 sender.GetComponent<PlayerController>().team 取
            string team = "unknown";
            if (team_last_msg_time.ContainsKey(team))
            {
                if (elapsed_time - team_last_msg_time[team] < TEAM_FREQ_INTERVAL)
                    return false;
            }
            return true;
        }

        // ==============================
        // ===== AI 自动发送逻辑 ========
        // ==============================
        // [TODO Step-E] 语义：基于 ai_manager 的球员位置 / 视野信息自动触发 send_message
        public virtual void evaluate_ai_messages(float delta)
        {
            // Step-E 占位实现
        }

        // 辅助: 玩家名字 (GD _pname())
        protected virtual string _pname(Transform t)
        {
            return t == null ? "null" : t.name;
        }

        // ========= 迁移兼容辅助 =========
        protected T GetNode<T>(string path) where T : Component
        {
            var t = transform.Find(path); return t == null ? null : t.GetComponent<T>();
        }
        protected GameObject GetNode(string path)
        {
            var t = transform.Find(path); return t == null ? null : t.gameObject;
        }
        protected Coroutine CreateTimer(float seconds, System.Action cb)
        {
            return StartCoroutine(_TimerCo(seconds, cb));
        }
        private IEnumerator _TimerCo(float s, System.Action cb)
        {
            yield return new WaitForSeconds(s); cb?.Invoke();
        }
        protected void CallDeferred(System.Action cb) { StartCoroutine(_CallDeferredCo(cb)); }
        private IEnumerator _CallDeferredCo(System.Action cb)
        {
            yield return null; cb?.Invoke();
        }
        protected void Emit(System.Action h) { h?.Invoke(); }
        protected void Emit<T1>(System.Action<T1> h, T1 a) { h?.Invoke(a); }
        protected void Emit<T1, T2>(System.Action<T1, T2> h, T1 a, T2 b) { h?.Invoke(a, b); }
        // === SpiritAIManager 兼容方法 ===
        public enum MsgType { NONE, NEED_BUFF, BUFF_ON_YOU, SKILL_READY }
        public bool try_send_message(Transform sender, int msg_type, string team) { return send_message(sender, msg_type, team); }
        public void record_message(Transform sender, int msg_type, string team) { }
        public bool has_need_buff(string team) { return false; }
        public Transform get_need_buff_sender(string team) { return null; }
        public bool has_buff_on_you(Transform p) { return false; }
    }
}

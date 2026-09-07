using UnityEngine;
using System.Collections.Generic;
using System;
using BattleBall.Battle;
using BattleBall.Systems.Spirit;
using BattleBall.Systems.SpiritSystem;

namespace BattleBall.Systems
{
    /// <summary>
    /// 元灵技能系统管理器 - Autoload 单例
    /// 整合技能触发器、标签效果处理器，提供统一入口
    /// </summary>
    public class SpiritSystemManager : MonoBehaviour
    {
        // ===== 事件（GD signal 转 C# event）=====
        public event Action<string, int, bool> skill_used;
        public event Action<string, Dictionary<string, object>> effect_applied;
        public event Action<string, Dictionary<string, object>> effect_finished;
        public event Action<string, Dictionary<string, object>> ui_feedback;

        // ===== 子组件 =====
        public SpiritSkillTrigger skill_trigger;
        public SpiritTagEffectHandler tag_effect_handler;

        // ===== 系统状态 =====
        protected bool _initialized = false;

        public static SpiritSystemManager Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        protected virtual void Start()
        {
            Debug.Log("[SpiritSystemManager] 初始化中...");

            // 创建或获取技能触发器
            skill_trigger = GetComponentInChildren<SpiritSkillTrigger>();
            if (skill_trigger == null)
            {
                var go = new GameObject("SpiritSkillTrigger");
                go.transform.SetParent(transform, false);
                skill_trigger = go.AddComponent<SpiritSkillTrigger>();
            }

            // 连接事件
            skill_trigger.skill_triggered += _on_skill_triggered;
            skill_trigger.skill_effect_applied += _on_skill_effect_applied;
            skill_trigger.skill_ui_feedback += _on_ui_feedback;

            // 获取标签效果处理器引用
            tag_effect_handler = FindObjectOfType<SpiritTagEffectHandler>();
            if (tag_effect_handler == null && skill_trigger != null)
            {
                // 触发器会自动创建
                tag_effect_handler = FindObjectOfType<SpiritTagEffectHandler>();
            }

            // 连接效果处理器事件
            if (tag_effect_handler != null)
            {
                tag_effect_handler.effect_applied += _on_effect_applied_handler;
                tag_effect_handler.effect_finished += _on_effect_finished_handler;
            }

            _initialized = true;
            Debug.Log("[SpiritSystemManager] 初始化完成");
        }

        protected virtual void OnDestroy()
        {
            if (skill_trigger != null)
            {
                skill_trigger.skill_triggered -= _on_skill_triggered;
                skill_trigger.skill_effect_applied -= _on_skill_effect_applied;
                skill_trigger.skill_ui_feedback -= _on_ui_feedback;
            }
            if (tag_effect_handler != null)
            {
                tag_effect_handler.effect_applied -= _on_effect_applied_handler;
                tag_effect_handler.effect_finished -= _on_effect_finished_handler;
            }
            if (Instance == this) Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance == null)
            {
                var go = new GameObject("SpiritSystemManager");
                go.AddComponent<SpiritSystemManager>();
            }
        }

        // ============================================================
        // 初始化系统
        // ============================================================
        public virtual void initialize(BattleManager battleManager, List<PlayerController> players, Transform ballNode)
        {
            if (!_initialized)
            {
                Debug.LogError("[SpiritSystemManager] 系统未初始化");
                return;
            }
            if (skill_trigger != null)
                skill_trigger.setup_battle_refs(battleManager, players, ballNode);
            Debug.Log("[SpiritSystemManager] 系统已连接战斗场景");
        }

        // ============================================================
        // 玩家技能
        // ============================================================
        public virtual void set_player_skills(int playerId, List<string> skillIds)
        {
            if (skill_trigger != null)
                skill_trigger.set_player_skills(playerId, skillIds);
            Debug.Log("[SpiritSystemManager] 玩家" + playerId + "上场技能设置完成");
        }

        // ============================================================
        // 使用技能（主入口）
        // ============================================================
        public virtual bool use_skill(int playerId, string skillId, Dictionary<string, object> targetData = null)
        {
            if (targetData == null) targetData = new Dictionary<string, object>();
            Debug.Log("[SpiritSystemManager] 使用技能: player_id=" + playerId + ", skill_id=" + skillId);

            bool success = false;
            if (skill_trigger != null)
                success = skill_trigger.trigger_skill(playerId, skillId, targetData);

            skill_used?.Invoke(skillId, playerId, success);
            return success;
        }

        // ============================================================
        // 查询
        // ============================================================
        public virtual float get_skill_cooldown(int playerId, string skillId)
        {
            if (skill_trigger != null) return skill_trigger.get_skill_cooldown(playerId, skillId);
            return 0f;
        }

        public virtual List<string> get_player_skills(int playerId)
        {
            if (skill_trigger != null) return skill_trigger.get_player_skills(playerId);
            return new List<string>();
        }

        public virtual bool has_tag(string tagId)
        {
            if (skill_trigger != null) return skill_trigger.has_tag(tagId);
            return false;
        }

        public virtual Dictionary<string, object> get_tag_data(string tagId)
        {
            if (skill_trigger != null) return skill_trigger.get_tag_data(tagId);
            return new Dictionary<string, object>();
        }

        // ============================================================
        // 事件回调
        // ============================================================
        protected virtual void _on_skill_triggered(string skillId, int casterId, Dictionary<string, object> targetData)
        {
            Debug.Log("[SpiritSystemManager] 技能已触发: " + skillId + ", 施法者: " + casterId);
        }

        protected virtual void _on_skill_effect_applied(string skillId, string tagId, Dictionary<string, object> effectResult)
        {
            Debug.Log("[SpiritSystemManager] 技能效果已应用: " + skillId + ", 标签: " + tagId);
        }

        protected virtual void _on_ui_feedback(string effectType, Dictionary<string, object> effectData)
        {
            ui_feedback?.Invoke(effectType, effectData);
        }

        protected virtual void _on_effect_applied_handler(string tagId, Dictionary<string, object> effectData)
        {
            effect_applied?.Invoke(tagId, effectData);
        }

        protected virtual void _on_effect_finished_handler(string tagId, Dictionary<string, object> effectData)
        {
            Debug.Log("[SpiritSystemManager] 效果已结束: " + tagId);
            effect_finished?.Invoke(tagId, effectData);
        }
    }
}
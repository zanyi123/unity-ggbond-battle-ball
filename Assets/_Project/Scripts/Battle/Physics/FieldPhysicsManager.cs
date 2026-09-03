// ================================================================
// 决竞球 CLEAN_v2 合法骨架: FieldPhysicsManager.cs [BattleBall.Battle.Physics]
// ================================================================
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using BattleBall.Core;

namespace BattleBall.Battle.Physics
{
    public class FieldPhysicsManager : MonoBehaviour
    {
        // ===== 常量 =====
        public const float DEFAULT_FRICTION  = 1.0f;
        public const float DEFAULT_BOUNCINESS= 0.0f;
        public const float MIN_FRICTION      = 0.3f;
        public const float MAX_FRICTION      = 2.0f;
        public const float MIN_BOUNCINESS    = 0.0f;
        public const float MAX_BOUNCINESS    = 1.0f;

        // ===== 当前值 =====
        protected float current_friction   = DEFAULT_FRICTION;
        protected float current_bounciness = DEFAULT_BOUNCINESS;
        protected string friction_source   = "default";
        protected float  friction_end_time = 0f;
        protected string bounciness_source = "default";

        // ===== 事件 =====
        public event Action<float, string> friction_changed;
        public event Action<float, string> bounciness_changed;
        public event Action                restored_to_defaults;

        protected Coroutine _restoreCheck;

        protected virtual void Start()
        {
            if (_restoreCheck == null)
                _restoreCheck = StartCoroutine(_CheckFrictionRestoreLoop(0.5f));
        }

        IEnumerator _CheckFrictionRestoreLoop(float interval)
        {
            var wait = new WaitForSeconds(interval);
            while (true)
            {
                _check_friction_restore();
                yield return wait;
            }
        }

        public virtual void set_friction(float mu, string source = "unknown", float duration = 0f)
        {
            mu = Mathf.Clamp(mu, MIN_FRICTION, MAX_FRICTION);
            current_friction  = mu;
            friction_source   = source;
            friction_end_time = duration > 0f ? Time.time + duration : 0f;
            friction_changed?.Invoke(mu, source);
        }

        public virtual void set_bounciness(float e, string source = "unknown", float duration = 0f)
        {
            e = Mathf.Clamp(e, MIN_BOUNCINESS, MAX_BOUNCINESS);
            current_bounciness = e;
            bounciness_source  = source;
            bounciness_changed?.Invoke(e, source);
        }

        public virtual void restore_default_friction(string source = "system")
        {
            current_friction = DEFAULT_FRICTION;
            friction_source  = source;
            friction_end_time = 0f;
            friction_changed?.Invoke(DEFAULT_FRICTION, source);
        }

        public virtual void restore_default_bounciness(string source = "system")
        {
            current_bounciness = DEFAULT_BOUNCINESS;
            bounciness_source  = source;
            bounciness_changed?.Invoke(DEFAULT_BOUNCINESS, source);
        }

        public virtual void restore_all_defaults()
        {
            restore_default_friction();
            restore_default_bounciness();
            restored_to_defaults?.Invoke();
        }

        protected virtual void _check_friction_restore()
        {
            if (friction_end_time > 0f && Time.time >= friction_end_time)
                restore_default_friction("timeout");
        }

        public virtual float GetFriction()   => current_friction;
        public virtual float GetBounciness() => current_bounciness;
    }
}

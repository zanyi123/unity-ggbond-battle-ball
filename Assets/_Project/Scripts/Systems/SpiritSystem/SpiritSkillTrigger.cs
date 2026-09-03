// ================================================================
// 决竞球 CLEAN_v2 合法骨架: SpiritSkillTrigger.cs [BattleBall.Systems.Spirit]
// 语义逻辑: Step A~F 大类统一核对; [TODO Step-*] 处待后续补全
// ================================================================
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using BattleBall.Core;
using BattleBall.Battle;

namespace BattleBall.Systems.Spirit
{

    public class SpiritSkillTrigger : MonoBehaviour
    {
        public Component effect_handler_stub;
        public BattleManager battle_manager;
        public Transform ball_node;
        [Serializable] public class TriggerCond { public string onEvent = ""; }
        public List<TriggerCond> conditions = new List<TriggerCond>();

        protected T GetNode<T>(string path) where T : Component { var t = transform.Find(path); return t == null ? null : t.GetComponent<T>(); }
        protected GameObject GetNode(string path) { var t = transform.Find(path); return t == null ? null : t.gameObject; }
        protected Coroutine CreateTimer(float seconds, Action cb) { return StartCoroutine(_TimerCo(seconds, cb)); }
        private IEnumerator _TimerCo(float s, Action cb) { yield return new WaitForSeconds(s); cb?.Invoke(); }
        protected void CallDeferred(Action cb) { StartCoroutine(_CallDeferredCo(cb)); }
        private IEnumerator _CallDeferredCo(Action cb) { yield return null; cb?.Invoke(); }
        protected static float randf() { return UnityEngine.Random.value; }
        protected static float randf_range(float a, float b) { return UnityEngine.Random.Range(a, b); }
        protected void Emit(Action h) { h?.Invoke(); }
        protected void Emit<T1>(Action<T1> h, T1 a) { h?.Invoke(a); }
        protected void Emit<T1,T2>(Action<T1,T2> h, T1 a, T2 b) { h?.Invoke(a, b); }
        protected void Emit<T1,T2,T3>(Action<T1,T2,T3> h, T1 a, T2 b, T3 c) { h?.Invoke(a, b, c); }

        protected virtual void Update() { }
        public virtual void Bind(Component eh, BattleManager bm, Transform ball) { effect_handler_stub=eh; battle_manager=bm; ball_node=ball; }
    }

}

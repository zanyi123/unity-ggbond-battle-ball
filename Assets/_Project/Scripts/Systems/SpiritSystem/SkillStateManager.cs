// ================================================================
// 决竞球 CLEAN_v2 合法骨架: SkillStateManager.cs [BattleBall.Systems.Spirit]
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

    public enum SkillState { Locked, Ready, Casting, Cooldown }
    [Serializable] public class PlayerSkillSlot {
        public int slot=1; public SkillState state=SkillState.Ready;
        public float cooldown=5f, cdTimer=0f, castTime=0.4f, castTimer=0f;
    }
    public class SkillStateManager : MonoBehaviour
    {
        public Dictionary<int, List<PlayerSkillSlot>> all = new Dictionary<int, List<PlayerSkillSlot>>();

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

        public virtual void RegisterPlayer(Transform player, int count=3) {
            if (player==null) return; int id = player.GetInstanceID();
            var slots = new List<PlayerSkillSlot>(count);
            for (int i=1; i<=count; i++) slots.Add(new PlayerSkillSlot{slot=i});
            all[id] = slots;
        }
        protected virtual void Update() {
            foreach (var kv in all) foreach (var s in kv.Value) {
                if (s.state == SkillState.Cooldown) { s.cdTimer -= Time.deltaTime; if (s.cdTimer<=0f) { s.cdTimer=0f; s.state=SkillState.Ready; } }
                else if (s.state == SkillState.Casting) { s.castTimer -= Time.deltaTime; if (s.castTimer<=0f) { s.castTimer=0f; s.state=SkillState.Cooldown; s.cdTimer=s.cooldown; } }
            }
        }
        public virtual bool TryCast(Transform player, int slot) {
            if (player==null) return false;
            if (!all.TryGetValue(player.GetInstanceID(), out var slots)) return false;
            var s = slots.Find(x=>x.slot==slot);
            if (s==null || s.state!=SkillState.Ready) return false;
            s.state = SkillState.Casting; s.castTimer = s.castTime; return true;
        }
        public virtual SkillState GetState(Transform player, int slot, out float ratio) {
            ratio = 0f; if (player==null) return SkillState.Locked;
            if (!all.TryGetValue(player.GetInstanceID(), out var slots)) return SkillState.Locked;
            var s = slots.Find(x=>x.slot==slot); if (s==null) return SkillState.Locked;
            if (s.state==SkillState.Cooldown) ratio = 1f - Mathf.Clamp01(s.cdTimer / Mathf.Max(0.0001f, s.cooldown));
            return s.state;
        }
    }

}

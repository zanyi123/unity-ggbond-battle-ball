// ================================================================
// 决竞球 CLEAN_v2 合法骨架: SpiritSystemManager.cs [BattleBall.Systems.Spirit]
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

    public class SpiritSystemManager : MonoBehaviour
    {
        public Component skill_trigger_stub;
        public Component tag_effect_handler_stub;
        public Dictionary<int, GameObject> playerToSpirit = new Dictionary<int, GameObject>();

        public static SpiritSystemManager Instance { get; protected set; }
        protected virtual void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }
        protected virtual void OnDestroy() { if (Instance == this) Instance = null; }


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

        protected virtual void Start() { if (Instance != this) return; }
        public virtual void BindSpirit(Transform player, GameObject prefab) {
            if (player==null) return; int id = player.GetInstanceID();
            GameObject go = prefab != null ? Instantiate(prefab, player) : new GameObject("Spirit_"+player.name);
            go.transform.SetParent(player, false); playerToSpirit[id] = go;
            Debug.Log("[Spirit] 绑定: "+player.name);
        }
        public virtual GameObject GetSpirit(Transform p) { return p==null?null:(playerToSpirit.TryGetValue(p.GetInstanceID(), out var g)?g:null); }
        public virtual float ElementMultiplier(string atk, string def) {
            if (string.IsNullOrEmpty(atk) || string.IsNullOrEmpty(def)) return 1.0f;
            string[] ring = {"fire","wood","earth","thunder","water"};
            int ai = Array.IndexOf(ring, atk.ToLower()); int di = Array.IndexOf(ring, def.ToLower());
            if (ai<0 || di<0) return 1.0f;
            if ((ai+1)%ring.Length == di) return 1.25f;
            if ((di+1)%ring.Length == ai) return 0.80f;
            return 1.0f;
        }
    }

}

// ================================================================
// 决竞球 CLEAN_v2 合法骨架: FieldZoneManager.cs [BattleBall.Battle.Physics]
// 语义逻辑: Step A~F 大类统一核对; [TODO Step-*] 处待后续补全
// ================================================================
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using BattleBall.Core;

namespace BattleBall.Battle.Physics
{

    public class FieldZoneManager : MonoBehaviour
    {
        public List<FieldZone> zones = new List<FieldZone>();
        public List<FieldEffectZone> effects = new List<FieldEffectZone>();
        public float innerXMin=-3.80f, innerXMax=3.80f, innerZMin=-2.60f, innerZMax=2.60f;

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

        protected virtual void Awake() { BuildDefaultZones(); }
        protected virtual void BuildDefaultZones() {
            zones.Clear();
            var zR = new GameObject("OuterRight_A").AddComponent<FieldZone>(); zR.side = FieldZone.Side.RightTeamA;
            zR.bodyRect=new Rect( 3.80f,-3.25f,1.30f,6.50f); zR.armTopRect=new Rect( 2.50f, 2.60f,1.30f,0.65f); zR.armBotRect=new Rect( 2.50f,-3.25f,1.30f,0.65f); zones.Add(zR);
            var zL = new GameObject("OuterLeft_B").AddComponent<FieldZone>(); zL.side = FieldZone.Side.LeftTeamB;
            zL.bodyRect=new Rect(-5.10f,-3.25f,1.30f,6.50f); zL.armTopRect=new Rect(-3.80f, 2.60f,1.30f,0.65f); zL.armBotRect=new Rect(-3.80f,-3.25f,1.30f,0.65f); zones.Add(zL);
        }
        public virtual bool IsInOuterField(Vector3 p, out FieldZone.Side side) {
            side = FieldZone.Side.InnerField;
            foreach (var z in zones) if (z.Contains(p)) { side = z.side; return true; }
            return false;
        }
    }

}

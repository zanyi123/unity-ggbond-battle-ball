// ================================================================
// 决竞球 CLEAN_v2 合法骨架: BattleHud.cs [BattleBall.Battle]
// 语义逻辑: Step A~F 大类统一核对; [TODO Step-*] 处待后续补全
// ================================================================
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using BattleBall.Core;

namespace BattleBall.Battle
{

    public class BattleHud : MonoBehaviour
    {
        public Canvas canvas;
        public List<Transform> teamPlayers = new List<Transform>();
        public List<Transform> enemyPlayers = new List<Transform>();
        public int scoreA=0, scoreB=0; public float matchTime = 0f;

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

        protected virtual void Update() { matchTime += Time.deltaTime; }
        public virtual void setup_players(List<Transform> team, List<Transform> enemies) { teamPlayers = team ?? new List<Transform>(); enemyPlayers = enemies ?? new List<Transform>(); }
        public virtual void show_skill_toast(string pn, string sn, float d=1.5f) { Debug.Log(string.Format("[HUD] 技能提示 {0}:{1}", pn, sn)); }
        protected virtual void OnGUI() {
            if (canvas != null) return;
            GUILayout.BeginArea(new Rect(10,10,360,100));
            GUILayout.Label(string.Format("时间 {0:00}:{1:00}   A {2} : {3} B", Mathf.FloorToInt(matchTime/60f), Mathf.FloorToInt(matchTime)%60, scoreA, scoreB));
            GUILayout.EndArea();
        }
    }

}

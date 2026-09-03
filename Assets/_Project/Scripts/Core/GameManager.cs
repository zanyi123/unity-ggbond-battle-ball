using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

namespace BattleBall.Core
{
    public enum MatchPhase { PREP, FIRST_HALF, HALF_TIME, SECOND_HALF, RESULTS }

    public class GameManager : MonoBehaviour
    {
        public const float FIRST_HALF_DURATION  = 300f;  // 5min
        public const float HALF_TIME_DURATION   = 60f;   // 1min
        public const float SECOND_HALF_DURATION = 300f;  // 5min
        public static float sim_half_duration_override = 8f; // 测试加速：每半场8秒

        public List<Transform> team_a = new List<Transform>();
        public List<Transform> team_b = new List<Transform>();

        // ===== UI 导航常量 =====
        public const string MODE_SLOT_PLAYER = "SLOT_PLAYER";
        public const string MODE_SLOT_ADMIN  = "SLOT_ADMIN";

        // ===== UI 状态 =====
        public string LastMenuMode { get; set; } = MODE_SLOT_PLAYER;

        public MatchPhase match_phase = MatchPhase.PREP;
        public float match_time = 0f;
        /// <summary>UI 兼容: 以大写属性形式获取 match_time</summary>
        public float MatchTime { get { return match_time; } set { match_time = value; } }
        public int score_team_a = 0, score_team_b = 0;
        public bool is_paused = false;

        public event Action<MatchPhase> phase_changed;
        public event Action<float> match_time_updated;
        public event Action<string, int> score_updated; // team, new_score
        public event Action<int, int, string> match_ended; // score_a, score_b, result(win/lose/draw)
        public event Action match_paused;
        public event Action match_resumed;

        public static GameManager Instance { get; private set; }

        protected virtual void Awake() {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }
        protected virtual void OnDestroy() { if (Instance == this) Instance = null; }

        public virtual float get_first_half_duration()  => sim_half_duration_override > 0f ? sim_half_duration_override : FIRST_HALF_DURATION;
        public virtual float get_second_half_duration() => sim_half_duration_override > 0f ? sim_half_duration_override : SECOND_HALF_DURATION;
        public virtual float get_half_time_duration()   => sim_half_duration_override > 0f ? sim_half_duration_override * 0.2f : HALF_TIME_DURATION;

        protected virtual void Start() {
            if (match_phase == MatchPhase.PREP) {
                // sim模式自动进入比赛（不用手动点备战）
                StartCoroutine(_AutoStartAfterFrame());
            }
        }

        private IEnumerator _AutoStartAfterFrame() {
            yield return new WaitForSeconds(0.3f);
            start_match();
        }

        protected virtual void Update() {
            if (is_paused) return;
            if (match_phase == MatchPhase.FIRST_HALF || match_phase == MatchPhase.HALF_TIME || match_phase == MatchPhase.SECOND_HALF) {
                match_time -= Time.deltaTime;
                match_time_updated?.Invoke(Mathf.Max(0f, match_time));
                if (match_time <= 0f) _advance_phase();
            }
        }

        public virtual void start_match() {
            score_team_a = 0; score_team_b = 0;
            _set_phase(MatchPhase.FIRST_HALF);
            match_time = get_first_half_duration();
            is_paused = false;
            Debug.Log("[GameManager] 比赛开始! 上半场 " + match_time.ToString("F0") + "s");
        }

        protected virtual void _advance_phase() {
            switch (match_phase) {
                case MatchPhase.FIRST_HALF:
                    _set_phase(MatchPhase.HALF_TIME);
                    match_time = get_half_time_duration();
                    Debug.Log("[GameManager] 上半场结束 → 中场休息 " + match_time.ToString("F0") + "s");
                    break;
                case MatchPhase.HALF_TIME:
                    _set_phase(MatchPhase.SECOND_HALF);
                    match_time = get_second_half_duration();
                    Debug.Log("[GameManager] 下半场开始! " + match_time.ToString("F0") + "s");
                    break;
                case MatchPhase.SECOND_HALF:
                    _set_phase(MatchPhase.RESULTS);
                    var res = _determine_result();
                    Debug.Log(string.Format("[GameManager] 比赛结束! 最终比分 A:{0} B:{1} ({2})", score_team_a, score_team_b, res));
                    match_ended?.Invoke(score_team_a, score_team_b, res);
                    break;
            }
        }

        protected virtual string _determine_result() {
            if (score_team_a > score_team_b) return "win";
            if (score_team_a < score_team_b) return "lose";
            return "draw";
        }

        protected virtual void _set_phase(MatchPhase p) {
            if (p == match_phase) return;
            match_phase = p;
            phase_changed?.Invoke(p);
        }

        public virtual void add_score(string team, int points = 1) {
            var t = (team ?? "").ToUpper();
            if (t == "A") { score_team_a += points; score_updated?.Invoke("A", score_team_a); }
            else if (t == "B") { score_team_b += points; score_updated?.Invoke("B", score_team_b); }
            Debug.Log("[GameManager] 得分 " + t + " → A:" + score_team_a + " B:" + score_team_b);
        }

        public virtual void pause_match() {
            if (is_paused) return; is_paused = true; match_paused?.Invoke();
        }
        public virtual void resume_match() {
            if (!is_paused) return; is_paused = false; match_resumed?.Invoke();
        }
    }
}

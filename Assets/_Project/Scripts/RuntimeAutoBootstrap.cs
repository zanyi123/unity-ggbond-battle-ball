using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BattleBall.Battle;
using BattleBall.Battle.AI;

namespace BattleBall.Core
{
    [DefaultExecutionOrder(-100)]
    public class RuntimeAutoBootstrap : MonoBehaviour
    {
        public bool verboseLog = true;
        public float statusRefresh = 0.25f;
        public bool showPanel = true;
        string _hud = ""; float _nextRefresh = 0f; string _lastResult = ""; GUIStyle _boxStyle; bool _stylesInit = false;

        protected virtual void Awake() {
            DoBindings();
            var gm = GameManager.Instance;
            if (gm != null) { gm.match_ended -= OnMatchEnded; gm.match_ended += OnMatchEnded; }
        }
        protected virtual void Start() {
            var bm = BattleManager.Instance;
            if (bm != null && (bm.teamA == null || bm.teamA.Count == 0)) DoBindings();
            var im = InputManager.Instance;
            if (im != null && bm != null) im.AttachBattleManager(bm);
        }
        protected virtual void Update() {
            if (Input.GetKeyDown(KeyCode.F4)) { showPanel = !showPanel; }
            if (Input.GetKeyDown(KeyCode.F5)) {
                var gm = GameManager.Instance;
                if (gm != null) gm.start_match();
                _lastResult = "";
                _EnsureBallAssigned();
            }
            if (Input.GetKeyDown(KeyCode.F3)) {
                var im = InputManager.Instance;
                if (im != null) {
                    im.quickServe = !im.quickServe;
                    Debug.Log("[Bootstrap] 发球模式切换 → " + (im.quickServe ? "瞬发(左键按下即飞)" : "蓄力(按住瞄准松手飞)"));
                }
            }
            if (Input.GetKeyDown(KeyCode.P)) {
                var gm = GameManager.Instance;
                if (gm != null) { gm.is_paused = !gm.is_paused; }
            }
            // 安全网: 主控球员未绑定时重绑
            var im2 = InputManager.Instance;
            if (im2 != null && im2.controlledPlayer == null) {
                var gm2 = GameManager.Instance;
                if (gm2 != null && gm2.team_a != null && gm2.team_a.Count > 0) {
                    var pc = gm2.team_a[0].GetComponent<PlayerController>();
                    if (pc != null) { im2.controlledPlayer = pc; Debug.Log("[Bootstrap] 重绑主控球员: " + pc.name); }
                }
            }
            // 安全网: 比赛进行中但球无主人时分配球权
            _EnsureBallAssigned();
        }

        void DoBindings() {
            var gm = GameManager.Instance;
            var bm = BattleManager.Instance;
            if (gm == null) { Debug.LogWarning("[Bootstrap] 缺 GameManager"); return; }
            var allPCs = Object.FindObjectsOfType<PlayerController>(true).ToList();
            var ta = new List<Transform>();
            var tb = new List<Transform>();
            foreach (var pc in allPCs) {
                string n = pc.gameObject.name.ToLower();
                if (n.Contains("teama_") || n.StartsWith("a")) ta.Add(pc.transform);
                else if (n.Contains("teamb_") || n.StartsWith("b")) tb.Add(pc.transform);
            }
            ta = ta.OrderBy(t => NumInName(t.name)).ToList();
            tb = tb.OrderBy(t => NumInName(t.name)).ToList();
            gm.team_a = ta; gm.team_b = tb;
            Transform ball = null;
            var bc = Object.FindObjectOfType<BallController>(true);
            if (bc != null) ball = bc.transform;
            if (bm != null) { bm.teamA = ta; bm.teamB = tb; if (ball != null) bm.ballNode = ball; }
            var im = InputManager.Instance;
            if (im != null && ta.Count > 0) {
                try { im.SwitchTo(0); } catch {}
                var pc0 = ta[0].GetComponent<PlayerController>();
                if (im.controlledPlayer == null && pc0 != null) im.controlledPlayer = pc0;
            }
            if (verboseLog) Debug.Log(string.Format(
                "[Bootstrap] OK A={0} B={1} Ball={2} IM={3}",
                ta.Count, tb.Count, ball != null ? ball.name : "null",
                im != null && im.controlledPlayer != null ? im.controlledPlayer.name : "null"));
        }
        void _EnsureBallAssigned() {
            var gm = GameManager.Instance;
            if (gm == null || gm.match_phase == MatchPhase.PREP) return;
            var bc = Object.FindObjectOfType<BallController>();
            if (bc == null || bc.owner != null || bc.isFlying) return;
            var bm = BattleManager.Instance;
            if (bm == null) return;
            bm.AssignInitialBall();
            Debug.Log("[Bootstrap] 球权已分配");
        }

        static int NumInName(string s) {
            string n = ""; foreach (char c in s) if (char.IsDigit(c)) n += c;
            return n.Length == 0 ? 999 : int.Parse(n);
        }
        void OnMatchEnded(int a, int b, string res) {
            _lastResult = "比赛结束 : " + res + "  (A " + a + " : " + b + " B) — 按 F5 重开";
        }

        protected virtual void OnGUI() {
            if (!showPanel) return;
            if (!_stylesInit) {
                _boxStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, wordWrap = true, fontSize = 13 };
                _stylesInit = true;
            }
            if (Time.unscaledTime > _nextRefresh) { _hud = BuildHud(); _nextRefresh = Time.unscaledTime + statusRefresh; }
            GUI.Box(new Rect(10, 10, 420, 360), _hud, _boxStyle);
        }

        string BuildHud() {
            var sb = new System.Text.StringBuilder(512);
            sb.AppendLine("=== 决竞球 测试面板 (RuntimeAutoBootstrap) ===");
            var gm = GameManager.Instance;
            var im = InputManager.Instance;
            var ai = AiManager.Instance;
            if (gm == null) { sb.AppendLine("[ERR] 缺 GameManager"); return sb.ToString(); }
            sb.Append("阶段: ").Append(gm.match_phase.ToString());
            if (gm.is_paused) sb.Append("  [PAUSED]");
            sb.Append("    倒计时: ").Append(Mathf.Max(0, gm.match_time).ToString("F1")).Append("s\n");
            sb.Append("比分  A ").Append(gm.score_team_a).Append("  :  ").Append(gm.score_team_b).Append("  B\n");
            sb.Append("主控玩家: ").Append(im != null && im.controlledPlayer != null ? im.controlledPlayer.name : "null");
            if (im != null && im.isAiming) sb.Append("  [AIMING charge=").Append(im.aimCharge.ToString("F2")).Append("]");
            sb.Append("\n");
            string carrier = "none"; float ballSpeed = 0f; Vector3 ballPos = Vector3.zero;
            var bc = Object.FindObjectOfType<BallController>();
            if (bc != null) {
                ballPos = bc.transform.position;
                var rb = bc.GetComponent<Rigidbody>();
                if (rb != null) ballSpeed = rb.velocity.magnitude;
                var all = Object.FindObjectsOfType<PlayerController>();
                foreach (var p in all) if (p.HasBall()) { carrier = p.name + "(" + p.team + ")"; break; }
            }
            sb.Append("持球: ").Append(carrier).Append("\n");
            sb.Append("球 pos=").Append(ballPos.ToString("F1")).Append("  speed=").Append(ballSpeed.ToString("F2")).Append("m/s\n");
            if (bc != null) {
                sb.Append("球 旋转=").Append(bc.spinSpeed.ToString("F1")).Append("rad/s  飞行=").Append(bc.isFlying ? "YES" : "no").Append("\n");
            }
            var allPC = Object.FindObjectsOfType<PlayerController>();
            sb.AppendLine("--- TeamA ---");
            foreach (var p in allPC.Where(x => x.team == "A").OrderBy(x => x.index)) {
                sb.Append("  ").Append(p.name).Append(" HP ").Append(Mathf.Max(0, p.hp).ToString("F0")).Append("/").Append(p.maxHp.ToString("F0"));
                sb.Append("  st=").Append(p.state.ToString());
                if (p.HasBall()) sb.Append(" *BALL*");
                if (im != null && im.controlledPlayer == p) sb.Append(" [YOU]");
                sb.Append("\n");
            }
            sb.AppendLine("--- TeamB (AI) ---");
            foreach (var p in allPC.Where(x => x.team == "B").OrderBy(x => x.index)) {
                sb.Append("  ").Append(p.name).Append(" HP ").Append(Mathf.Max(0, p.hp).ToString("F0")).Append("/").Append(p.maxHp.ToString("F0"));
                sb.Append("  st=").Append(p.state.ToString());
                if (p.HasBall()) sb.Append(" *BALL*");
                sb.Append("\n");
            }
            sb.Append("AI决策员: ").Append(ai != null ? ai.ai_players.Count.ToString() : "0");
            sb.Append("\n--- 控制说明 ---\n");
            sb.Append(" WASD 移动   1/2/3 切人   Tab 循环切\n");
            // 发球模式显示
            var im2 = InputManager.Instance;
            if (im2 != null && im2.quickServe) {
                sb.Append(" 左键: 持球=瞬发投球 / 无球=冲刺\n");
            } else {
                sb.Append(" 左键(按住):持球=瞄准 / 无球=冲刺   左键松开: 投球\n");
            }
            sb.Append(" 右键按下: 接球姿态   4/5/6 技能   C 取消\n");
            sb.Append(" F5 重开比赛   F3 切换发球模式   F4 隐藏/显示面板   P 暂停/继续");
            if (!string.IsNullOrEmpty(_lastResult)) { sb.Append("\n--- ").Append(_lastResult).Append(" ---"); }
            return sb.ToString();
        }
    }
}

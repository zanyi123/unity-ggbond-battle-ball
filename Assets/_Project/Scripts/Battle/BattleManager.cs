using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using BattleBall.Core;
using BattleBall.Battle.AI;

namespace BattleBall.Battle
{
    public class BattleManager : MonoBehaviour
    {
        public Transform ballNode;
        public List<Transform> teamA = new List<Transform>();
        public List<Transform> teamB = new List<Transform>();
        public Transform ballStartA, ballStartB;
        public bool match_started = false;
        public bool auto_simulate = false;
        public AI.AiCommunication comm_system;
        public event Action<string> OnMatchStateChanged;
        public enum MatchState { Preparation, Half1, MidBreak, Half2, Result }
        public MatchState state = MatchState.Preparation;
        public int scoreA = 0, scoreB = 0;
        public List<PlayerController> team_a_players = new List<PlayerController>();
        public List<PlayerController> team_b_players = new List<PlayerController>();
        public BallController ball_node = null;

        public static BattleManager Instance { get; protected set; }
        protected virtual void Awake() {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }
        protected virtual void OnDestroy() { if (Instance == this) Instance = null; }

        protected virtual void Start() {
            Debug.Log("[BM] Start: teamA=" + teamA.Count + " teamB=" + teamB.Count + " ball=" + (ballNode!=null));
            if (teamA.Count == 0) _AutoBindPlayers();
            _InitAllPlayers();
            _InitAI();
            if (!match_started && teamA.Count > 0 && teamB.Count > 0) {
                Kickoff();
            }
        }

        private static readonly string[] CHAR_IDS = new string[] { "char_001","char_002","char_003","char_004","char_005","char_006" };

        void _InitAllPlayers() {
            for (int i=0; i<teamA.Count; i++) {
                if (teamA[i]==null) continue;
                var pc = teamA[i].GetComponent<PlayerController>();
                if (pc==null) continue;
                pc.team = "A"; pc.index = i; pc.playerId = "A"+(i+1);
                string cid = i < CHAR_IDS.Length ? CHAR_IDS[i] : "char_001";
                if (!string.IsNullOrEmpty(pc.charId) && pc.charId != "char_001") cid = pc.charId;
                bool controlled = (i == 0); // 队A[0]玩家控制
                pc.Initialize(cid, "A", controlled);
                Debug.Log("[Init] A#" + (i+1) + " " + teamA[i].name + " charId=" + cid + " controlled=" + controlled);
            }
            for (int i=0; i<teamB.Count; i++) {
                if (teamB[i]==null) continue;
                var pc = teamB[i].GetComponent<PlayerController>();
                if (pc==null) continue;
                pc.team = "B"; pc.index = i; pc.playerId = "B"+(i+1);
                string cid = (i+3) < CHAR_IDS.Length ? CHAR_IDS[i+3] : "char_004";
                if (!string.IsNullOrEmpty(pc.charId) && pc.charId != "char_001") cid = pc.charId;
                pc.Initialize(cid, "B", false);
                Debug.Log("[Init] B#" + (i+1) + " " + teamB[i].name + " charId=" + cid);
            }
        }

        void _InitAI() {
            var ai = AiManager.Instance;
            if (ai == null) return;
            for (int i=0; i<teamA.Count; i++) {
                if (teamA[i]==null) continue;
                var pc = teamA[i].GetComponent<PlayerController>();
                if (pc != null && pc.isPlayerControlled) continue; // 玩家控制的不注册AI
                var prof = AiProfile.ByIndex(i, pc != null ? pc.role.ToString() : "");
                ai.register_player(teamA[i], "A", i, prof);
            }
            for (int i=0; i<teamB.Count; i++) {
                if (teamB[i]==null) continue;
                var pc = teamB[i].GetComponent<PlayerController>();
                var prof = AiProfile.ByIndex(i, pc != null ? pc.role.ToString() : "");
                ai.register_player(teamB[i], "B", i, prof);
            }
            ai.initialize(this, null);
            if (ballNode != null) ai.ball_node = ballNode;
            Debug.Log("[BM] AI注册完成 ai_players=" + ai.ai_players.Count);
        }

        void _AutoBindPlayers() {
            var gm = GameManager.Instance;
            if (gm != null) {
                if (gm.team_a != null) teamA = gm.team_a;
                if (gm.team_b != null) teamB = gm.team_b;
            }
            if (ballNode == null) {
                var ball = GameObject.Find("battleball");
                if (ball != null) ballNode = ball.transform;
            }
            Debug.Log("[BM] 自动绑定: teamA=" + teamA.Count + " teamB=" + teamB.Count + " ball=" + (ballNode!=null));
        }

        public virtual void Initialize(List<Transform> ta, List<Transform> tb, Transform ball, AI.AiCommunication comm) {
            teamA = ta ?? new List<Transform>();
            teamB = tb ?? new List<Transform>();
            ballNode = ball; comm_system = comm;
        }

        public virtual void Kickoff() {
            scoreA = 0; scoreB = 0; match_started = true;
            state = MatchState.Half1;
            OnMatchStateChanged?.Invoke(state.ToString());
            _assign_initial_ball();
            Debug.Log("[BM] Kickoff! state=Half1");
        }

        void _assign_initial_ball() {
            if (ballNode == null) { Debug.LogWarning("[BM] ballNode 为空,无法分配球权"); return; }
            var bc = ballNode.GetComponent<BallController>();
            if (bc == null) { Debug.LogWarning("[BM] BallController 为空"); return; }
            int coin = UnityEngine.Random.Range(0, 2);
            // sim模式下避免给玩家控制位(teamA[0])
            int aIdx = auto_simulate && teamA.Count > 1 ? 1 : 0;
            int bIdx = 0;
            Transform carrier = (coin == 0) ?
                (aIdx < teamA.Count ? teamA[aIdx] : null) :
                (bIdx < teamB.Count ? teamB[bIdx] : null);
            if (carrier != null) {
                bc.ReturnToPlayer(carrier);
                Debug.Log("[Match] 初始球权分配给队" + (coin == 0 ? "A" : "B") + " 位置" + ((coin == 0 ? aIdx : bIdx) + 1));
            }
        }

        public virtual void OnGoal(string scoringTeam) {
            if (scoringTeam == "A") scoreA++; else if (scoringTeam == "B") scoreB++;
            Debug.Log("[BM] 进球! A:" + scoreA + " B:" + scoreB);
            // 重新分配球权
            _assign_initial_ball();
        }
    }
}
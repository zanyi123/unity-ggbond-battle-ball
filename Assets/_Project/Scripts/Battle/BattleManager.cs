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

        // 场地规则常量(已×2，对应GD field_zone.gd)
        // 内场 x=-380~380(*0.01=3.8, *2放大=7.6), 外场 x=-510~510(*0.01*2=10.2)
        public float innerXMin = -7.6f, innerXMax = 7.6f;
        public float innerZMin = -5.2f, innerZMax = 5.2f;
        public float outerXMin = -10.2f, outerXMax = 10.2f;
        public float centerX = 0f; // 中线 x=0
        // 违规状态追踪(防重复触发)
        private Dictionary<PlayerController, int> _violatingPlayers = new Dictionary<PlayerController, int>();

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
            // 监听所有球员被击败事件
            foreach (var pc in _GetAllPlayers())
            {
                pc.defeated += _OnPlayerDefeated;
            }
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

        public void AssignInitialBall() { _assign_initial_ball(); }
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

        protected virtual void Update()
        {
            if (!match_started) return;
            _CheckViolations();
        }

        private List<PlayerController> _GetAllPlayers()
        {
            var all = new List<PlayerController>();
            all.AddRange(team_a_players);
            all.AddRange(team_b_players);
            if (all.Count == 0)
            {
                foreach (var t in teamA) { var pc = t.GetComponent<PlayerController>(); if (pc != null) all.Add(pc); }
                foreach (var t in teamB) { var pc = t.GetComponent<PlayerController>(); if (pc != null) all.Add(pc); }
            }
            return all;
        }

        /// <summary>每帧检查所有球员违规(蓝色禁区/越中线/越内外场边界)</summary>
        private void _CheckViolations()
        {
            var all = _GetAllPlayers();
            var toClear = new List<PlayerController>();
            foreach (var pc in all)
            {
                if (pc == null || pc.state == PlayerState.Defeated) continue;
                var pos = pc.transform.position;
                int violation = _CheckZoneViolation(pc, pos);
                if (violation != 0)
                {
                    if (!_violatingPlayers.ContainsKey(pc))
                    {
                        _HandleViolation(pc, violation);
                        _violatingPlayers[pc] = violation;
                    }
                }
                else
                {
                    if (_violatingPlayers.ContainsKey(pc)) toClear.Add(pc);
                }
            }
            foreach (var pc in toClear) _violatingPlayers.Remove(pc);
        }

        /// <summary>区域违规检测: 0=无违规, 1=蓝色禁区, 2=越中线, 3=越内外场边界</summary>
        private int _CheckZoneViolation(PlayerController pc, Vector3 pos)
        {
            bool inInner = pos.x >= innerXMin && pos.x <= innerXMax && pos.z >= innerZMin && pos.z <= innerZMax;
            bool inLeftOuter = pos.x >= outerXMin && pos.x < innerXMin && pos.z >= innerZMin - 1.3f && pos.z <= innerZMax + 1.3f;
            bool inRightOuter = pos.x > innerXMax && pos.x <= outerXMax && pos.z >= innerZMin - 1.3f && pos.z <= innerZMax + 1.3f;

            // 1. 蓝色禁区: 不在内场也不在外场
            if (!inInner && !inLeftOuter && !inRightOuter) return 1;

            // 2. 越内外场边界: 队A不能进左外场, 队B不能进右外场
            if (pc.team.ToUpper() == "A" && inLeftOuter) return 3;
            if (pc.team.ToUpper() == "B" && inRightOuter) return 3;

            // 3. 越中线: 队A不能进x>0(右半场), 队B不能进x<0(左半场) — 只在内场检查
            if (inInner)
            {
                if (pc.team.ToUpper() == "A" && pos.x > centerX) return 2;
                if (pc.team.ToUpper() == "B" && pos.x < centerX) return 2;
            }
            return 0;
        }

        private void _HandleViolation(PlayerController pc, int violationType)
        {
            string scoringTeam = pc.team.ToUpper() == "A" ? "B" : "A";
            if (scoringTeam == "A") scoreA++; else scoreB++;
            string vText = violationType == 1 ? "越出禁区" : violationType == 2 ? "越中线" : "越内外场边界";
            Debug.Log("[Match] 违规: " + pc.name + " " + vText + "! 队" + scoringTeam + "得分  A:" + scoreA + " B:" + scoreB);
            // 传送到己方外场
            _TransferToOuterField(pc);
        }

        private void _TransferToOuterField(PlayerController pc)
        {
            float targetX = pc.team.ToUpper() == "A" ? (innerXMax + outerXMax) * 0.5f : (innerXMin + outerXMin) * 0.5f;
            pc.transform.position = new Vector3(targetX, 0f, 0f);
        }

        /// <summary>球员被击败: 攻击方得分 + 传送被击败球员到外场</summary>
        private void _OnPlayerDefeated(Transform playerTransform)
        {
            var pc = playerTransform.GetComponent<PlayerController>();
            if (pc == null) return;
            string scoringTeam = pc.team.ToUpper() == "A" ? "B" : "A";
            if (scoringTeam == "A") scoreA++; else scoreB++;
            Debug.Log("[Match] " + pc.name + " 被击败! 队" + scoringTeam + "得分  A:" + scoreA + " B:" + scoreB);
            _TransferToOuterField(pc);
        }

        /// <summary>得分(兼容旧接口，实际得分由击倒/违规触发)</summary>
        public virtual void AddScore(string scoringTeam) {
            if (scoringTeam == "A") scoreA++; else if (scoringTeam == "B") scoreB++;
            Debug.Log("[BM] 得分 A:" + scoreA + " B:" + scoreB);
        }
    }
}

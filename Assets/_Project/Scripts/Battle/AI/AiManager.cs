using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using BattleBall.Core;
using BattleBall.Battle;

namespace BattleBall.Battle.AI
{
    public enum AiState { IDLE, CHASE_BALL, GOTO_BALL, DRIBBLE, ATTACK, PASS, DEFEND, SUPPORT, PENALTY_MOVE, READY_CATCH }

    public class AiPlayerSlot
    {
        public Transform player; public string team; public int index;
        public AiState state=AiState.IDLE, last_state=AiState.IDLE;
        public Vector3 target_pos, home_pos, dribble_target, last_pos;
        public AiProfile.Profile profile;
        public float hold_timer, hold_duration, total_carry_time, stuck_timer, think_timer, awareness_timer;
        public Transform last_shoot_target;
        public Dictionary<string, Vector3> known_positions = new Dictionary<string, Vector3>();
    }

    [DefaultExecutionOrder(-200)]
    public class AiManager : MonoBehaviour
    {
        public const float FIELD_X_MIN=-3.80f, FIELD_X_MAX=3.80f, FIELD_Z_MIN=-2.60f, FIELD_Z_MAX=2.60f;
        public const float LEFT_OUTER_X_MIN=-5.10f, LEFT_OUTER_X_MAX=-2.50f, LEFT_OUTER_Z_MIN=-3.25f, LEFT_OUTER_Z_MAX=3.25f;
        public const float RIGHT_OUTER_X_MIN=2.50f, RIGHT_OUTER_X_MAX=5.10f, RIGHT_OUTER_Z_MIN=-3.25f, RIGHT_OUTER_Z_MAX=3.25f;
        // 决竞球：球门位置(目标半场中心)
        public static readonly Vector3 GOAL_A = new Vector3( 3.00f, 0f, 0f); // 对方半场中心(队A视角攻向右)
        public static readonly Vector3 GOAL_B = new Vector3(-3.00f, 0f, 0f);
        public const float TURN_SPEED = 5.0f;

        public BattleManager battle_manager;
        public InputManager  input_manager;
        public Transform     ball_node;
        public Component     match_stats, spirit_ai_mgr;
        public List<AiPlayerSlot> ai_players = new List<AiPlayerSlot>();

        public static AiManager Instance { get; protected set; }

        protected virtual void Awake() {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }
        protected virtual void OnDestroy() { if (Instance == this) Instance = null; }

        public virtual void initialize(BattleManager bm, InputManager im) {
            battle_manager=bm; input_manager=im;
        }

        protected virtual void FixedUpdate() {
            if (battle_manager==null || !battle_manager.match_started) return;
            if (ball_node==null && battle_manager.ballNode!=null) ball_node=battle_manager.ballNode;
            float dt = Time.fixedDeltaTime;
            foreach (var ap in ai_players) {
                if (ap==null || ap.player==null) continue;
                var pc = ap.player.GetComponent<PlayerController>();
                if (pc != null && pc.state == PlayerState.Defeated) continue;
                _update_awareness(ap, dt);
                if (ap.profile==null) continue;
                ap.think_timer += dt;
                float thinkSec = ap.profile.decisionIntervalMs / 1000f;
                if (ap.think_timer >= thinkSec) { ap.think_timer = 0f; ap.last_state = ap.state; _decide(ap); }
                _update_facing(ap, dt);
                _move(ap, dt);
            }
        }

        public virtual void register_player(Transform player, string team_name, int index, AiProfile.Profile profile) {
            if (player==null || profile==null) return;
            ai_players.Add(new AiPlayerSlot {
                player=player, team=team_name, index=index, profile=profile,
                home_pos=player.position, target_pos=player.position,
                hold_duration=1f + UnityEngine.Random.value * 1.5f,
                last_pos=player.position,
                think_timer=UnityEngine.Random.value * (profile.decisionIntervalMs/1000f)
            });
            Debug.Log(string.Format("[AI] 注册 队{0}#{1} role={2}", team_name, index, profile.roleName));
        }

        protected virtual bool _is_valid(AiPlayerSlot ap) => ap!=null && ap.player!=null;

        // ========== 感知 ==========
        protected virtual void _update_awareness(AiPlayerSlot ap, float dt) {
            ap.awareness_timer += dt;
            var myPos = ap.player.position;
            ap.last_pos = myPos;
            // 所有AI都知道球和玩家位置（无视野遮挡简化版）
            var allP = UnityEngine.Object.FindObjectsOfType<PlayerController>();
            foreach (var p in allP) {
                if (p == null || p.transform == ap.player) continue;
                var k = p.team.ToUpper() + "#" + p.index;
                ap.known_positions[k] = p.transform.position;
            }
        }

        // 决竞球辅助工具
        protected virtual Vector3 _my_goal(AiPlayerSlot ap) { return (ap.team == "A") ? GOAL_A : GOAL_B; } // 己方半场
        protected virtual Vector3 _enemy_goal(AiPlayerSlot ap) { return (ap.team == "A") ? GOAL_B : GOAL_A; } // 敌方半场（攻击目标）
        protected virtual Vector3 _forward(AiPlayerSlot ap) { return (ap.team == "A") ? Vector3.right : Vector3.left; }
        protected virtual BallController _ball() { return ball_node == null ? null : ball_node.GetComponent<BallController>(); }
        protected virtual PlayerController _pc(AiPlayerSlot ap) { return ap.player.GetComponent<PlayerController>(); }

        protected virtual List<PlayerController> _team_players(AiPlayerSlot ap, bool myTeam) {
            var r = new List<PlayerController>();
            var all = UnityEngine.Object.FindObjectsOfType<PlayerController>();
            foreach (var p in all) {
                if (p == null || p.state == PlayerState.Defeated) continue;
                bool mine = (p.team.ToUpper() == ap.team.ToUpper());
                if (myTeam == mine && p.transform != ap.player) r.Add(p);
            }
            return r;
        }

        protected virtual PlayerController _nearest_enemy(AiPlayerSlot ap, Vector3 to) {
            PlayerController best = null; float bd = float.MaxValue;
            foreach (var e in _team_players(ap, false)) {
                float d = Vector3.Distance(e.transform.position, to);
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }

        protected virtual PlayerController _nearest_teammate(AiPlayerSlot ap, Vector3 to, out float dist) {
            PlayerController best = null; dist = float.MaxValue;
            foreach (var t in _team_players(ap, true)) {
                float d = Vector3.Distance(t.transform.position, to);
                if (d < dist) { dist = d; best = t; }
            }
            return best;
        }

        // ========== 决策（效用评分：投球/传球/推进） ==========
        protected virtual void _decide(AiPlayerSlot ap) {
            var pc = _pc(ap); if (pc == null) return;
            var ball = _ball();
            bool hasBall = pc.HasBall();
            Vector3 ballPos = ball != null ? ball.transform.position : ap.player.position;

            if (pc.state == PlayerState.Defeated) { ap.state = AiState.IDLE; return; }

            if (hasBall) { _decide_carrying(ap); return; }

            if (ball != null && ball.owner != null) {
                var opc = ball.owner.GetComponent<PlayerController>();
                bool myTeamOwns = (opc != null && opc.team.ToUpper() == ap.team.ToUpper());
                if (myTeamOwns) _decide_teammate_has_ball(ap);
                else             _decide_enemy_has_ball(ap);
                return;
            }
            // 球在飞或无主
            float myDist = Vector3.Distance(ap.player.position, ballPos);
            var mates = _team_players(ap, true);
            float mateDist = float.MaxValue;
            foreach (var m in mates) { var d = Vector3.Distance(m.transform.position, ballPos); if (d < mateDist) mateDist = d; }
            bool iAmNearest = myDist <= mateDist + 0.1f; // 容忍 10 cm
            if (iAmNearest && myDist < ap.profile.chaseDist) {
                ap.state = AiState.CHASE_BALL; ap.target_pos = ballPos;
            } else {
                _decide_off_ball_role(ap, ballPos);
            }
        }

        // === 持球决策：投球(ATTACK) vs 传球(PASS) vs 推进(DRIBBLE) 效用评分 ===
        protected virtual void _decide_carrying(AiPlayerSlot ap) {
            var pc = _pc(ap); if (pc == null) return;
            ap.total_carry_time += (ap.profile.decisionIntervalMs / 1000f);
            ap.hold_timer += (ap.profile.decisionIntervalMs / 1000f);
            Vector3 myPos = ap.player.position;
            Vector3 goal = _enemy_goal(ap);
            Vector3 fwd = _forward(ap);
            float dist_to_goal = Vector3.Distance(myPos, goal);

            // 强制出手：超 max_carry_time
            const float MAX_CARRY = 8f;
            if (ap.total_carry_time >= MAX_CARRY) {
                float dmy_p, dmy_s;
                var pt = _best_pass_target(ap, out dmy_p);
                var st = _nearest_enemy(ap, myPos);
                if (pt != null && UnityEngine.Random.value < 0.7f) { ap.state = AiState.PASS; ap.target_pos = pt.transform.position; return; }
                if (st != null) { ap.state = AiState.ATTACK; ap.target_pos = st.transform.position; return; }
                // 盲投
                ap.state = AiState.ATTACK; ap.target_pos = myPos + fwd * 4f; return;
            }

            // 未达到 hold_duration，观察（继续 DRIBBLE 推进）
            if (ap.hold_timer < ap.hold_duration) {
                ap.state = AiState.DRIBBLE;
                ap.target_pos = myPos + fwd * 2.5f;
                return;
            }

            // 效用评分
            float passScore = -100f, shootScore = -100f, dribbleScore = 0f;
            PlayerController passT = _best_pass_target(ap, out float passDist);
            if (passT != null) {
                // 传球：近距离高、距离适中
                float d = passDist;
                passScore = 60f - d * 8f;
            }
            PlayerController shootT = _nearest_enemy(ap, myPos);
            if (shootT != null) {
                float d = Vector3.Distance(myPos, shootT.transform.position);
                shootScore = 80f - d * 10f;
                if (d < ap.profile.shootDist) shootScore += 25f;
            }
            // 推进：敌方威胁近，推进分低；离球门还远，推进分高
            float dangerClose = _has_enemy_within(ap, 1.2f) ? 1f : 0f;
            float dangerVeryClose = _has_enemy_within(ap, 0.6f) ? 1f : 0f;
            dribbleScore = 30f + (dist_to_goal > 3f ? 25f : 0f) - dangerClose * 25f - dangerVeryClose * 50f;

            // 角色权重叠加（AiProfile weight: 原 GD weight_pass/shoot/dribble）
            var prof = ap.profile;
            // 映射：Attacker 投球强；Support 传球强；Defender 更倾向推进到安全区出球
            if (prof == AiProfile.Attacker) { shootScore += 20; passScore += 0; dribbleScore += 0; }
            else if (prof == AiProfile.Defender) { shootScore -= 10; passScore += 10; dribbleScore -= 5; }
            else { shootScore -= 5; passScore += 25; dribbleScore += 0; } // Support

            // 被逼抢时紧急处理：传球加成
            if (dangerVeryClose > 0f) { if (passT != null) passScore += 35f; shootScore += 20f; }
            // 随机因子
            float rng = (UnityEngine.Random.value * 16f) - 8f;
            passScore += rng; shootScore += rng * 0.5f; dribbleScore += rng * 0.3f;

            // 滞回容差：当前状态加分
            switch (ap.state) {
                case AiState.ATTACK: shootScore += 10f; break;
                case AiState.PASS:   passScore  += 10f; break;
                case AiState.DRIBBLE:dribbleScore+= 10f; break;
            }

            if (passScore >= shootScore && passScore >= dribbleScore && passT != null) {
                ap.state = AiState.PASS; ap.target_pos = passT.transform.position;
                ap.hold_timer = 0f; ap.hold_duration = 1f + UnityEngine.Random.value * 1.5f;
            } else if (shootScore >= dribbleScore && shootT != null) {
                ap.state = AiState.ATTACK; ap.target_pos = shootT.transform.position;
                ap.hold_timer = 0f; ap.hold_duration = 1f + UnityEngine.Random.value * 1.5f;
            } else {
                ap.state = AiState.DRIBBLE;
                // 推进目标：朝球门，加随机侧向偏移
                Vector3 baseTarget = myPos + fwd * 3.0f;
                float side = (UnityEngine.Random.value - 0.5f) * 2f;
                ap.dribble_target = new Vector3(baseTarget.x, 0, Mathf.Clamp(baseTarget.z + side * 1.5f, FIELD_Z_MIN, FIELD_Z_MAX));
                ap.target_pos = ap.dribble_target;
            }
        }

        protected virtual bool _has_enemy_within(AiPlayerSlot ap, float r) {
            var myPos = ap.player.position;
            foreach (var e in _team_players(ap, false))
                if (Vector3.Distance(e.transform.position, myPos) <= r) return true;
            return false;
        }

        protected virtual PlayerController _best_pass_target(AiPlayerSlot ap, out float dist) {
            dist = float.MaxValue;
            var mates = _team_players(ap, true);
            if (mates.Count == 0) return null;
            // 选一个"靠近敌方半场+距离适中"的队友
            Vector3 goal = _enemy_goal(ap);
            PlayerController best = null; float bestScore = -1000f;
            Vector3 myPos = ap.player.position;
            foreach (var m in mates) {
                float d = Vector3.Distance(m.transform.position, myPos);
                if (d < 0.8f) continue; // 不能传身边太近
                if (d > 8f) continue;   // 太远不传
                float forwardness = (goal - m.transform.position).sqrMagnitude;
                float score = 50f - d * 3f - forwardness * 2f; // 越靠敌方半场越好
                if (score > bestScore) { bestScore = score; best = m; dist = d; }
            }
            return best;
        }

        // === 队友持球：角色跑位 ===
        protected virtual void _decide_teammate_has_ball(AiPlayerSlot ap) {
            Vector3 myPos = ap.player.position;
            Vector3 fwd = _forward(ap);
            // 阵型基准位
            Vector3 formationBase = _formation_hold_pos(ap);
            Vector3 ballPos = ap.player.position;
            var ball = _ball();
            if (ball != null) ballPos = ball.transform.position;

            // Attacker: 前方空位
            if (ap.profile == AiProfile.Attacker) {
                ap.target_pos = _clamp(formationBase + fwd * 2.0f + new Vector3(0,0,(ap.index%2==0?1f:-1f)*1.2f));
                ap.state = AiState.SUPPORT;
            } else if (ap.profile == AiProfile.Support) {
                ap.target_pos = _clamp(formationBase + fwd * 0.5f);
                ap.state = AiState.SUPPORT;
            } else {
                // Defender：回后场守位
                ap.target_pos = _clamp(formationBase);
                ap.state = AiState.DEFEND;
            }
        }

        // === 对手持球：防御/抢截 ===
        protected virtual void _decide_enemy_has_ball(AiPlayerSlot ap) {
            Vector3 myPos = ap.player.position;
            Vector3 fwd = _forward(ap);
            var ball = _ball();
            Vector3 ballPos = ball != null ? ball.transform.position : myPos;
            float d = Vector3.Distance(myPos, ballPos);

            var prof = ap.profile;
            // Defender/距离近 → 冲上去追；否则守家
            if (prof == AiProfile.Defender) {
                ap.target_pos = _formation_hold_pos(ap);
                if (d < 2.5f) { ap.state = AiState.CHASE_BALL; ap.target_pos = ballPos; }
                else ap.state = AiState.DEFEND;
            } else if (prof == AiProfile.Attacker) {
                if (d < prof.chaseDist) { ap.state = AiState.CHASE_BALL; ap.target_pos = ballPos; }
                else { ap.target_pos = _formation_hold_pos(ap); ap.state = AiState.DEFEND; }
            } else {
                // Support：中间策略
                if (d < 3.5f) { ap.state = AiState.CHASE_BALL; ap.target_pos = ballPos; }
                else { ap.target_pos = _formation_hold_pos(ap); ap.state = AiState.DEFEND; }
            }
        }

        // === 无球不追：按角色返回阵型/微调站位 ===
        protected virtual void _decide_off_ball_role(AiPlayerSlot ap, Vector3 ball_pos) {
            var pos = _formation_hold_pos(ap);
            // 球在己方半场则轻微偏移朝球
            bool ballInMyHalf = (ap.team == "A" && ball_pos.x < 0f) || (ap.team == "B" && ball_pos.x > 0f);
            if (ballInMyHalf) {
                var pull = (ball_pos - pos).normalized * 0.3f;
                pos += pull;
            }
            ap.target_pos = _clamp(pos);
            ap.state = AiState.DEFEND;
        }

        // 决竞球阵型站位（简化3人标准）：index 0=主攻 1=中坚/防 2=辅助
        protected virtual Vector3 _formation_hold_pos(AiPlayerSlot ap) {
            Vector3 half = (ap.team == "A") ? new Vector3(-1.9f,0f,0f) : new Vector3(1.9f,0f,0f);
            switch (ap.index % 3) {
                case 0: return half + new Vector3( (ap.team=="A"?1f:-1f)*0.8f, 0f, 0.8f); // 前锋偏上
                case 1: return half + new Vector3( 0f, 0f, 0f);                                // 中位防守
                default:return half + new Vector3( (ap.team=="A"?1f:-1f)*0.8f, 0f,-0.8f); // 辅助偏下
            }
        }

        protected virtual Vector3 _clamp(Vector3 p) {
            return new Vector3(Mathf.Clamp(p.x, FIELD_X_MIN, FIELD_X_MAX), 0f, Mathf.Clamp(p.z, FIELD_Z_MIN, FIELD_Z_MAX));
        }

        // ========== 朝向 ==========
        protected virtual void _update_facing(AiPlayerSlot ap, float dt) {
            var pc = _pc(ap); if (pc == null) return;
            Vector3 look;
            switch (ap.state) {
                case AiState.CHASE_BALL: case AiState.GOTO_BALL:
                    var b = _ball(); look = b == null ? _forward(ap) : (b.transform.position - ap.player.position).normalized; break;
                case AiState.ATTACK: case AiState.DRIBBLE:
                    look = _forward(ap); break;
                case AiState.PASS:
                    look = (ap.target_pos - ap.player.position).normalized; break;
                case AiState.DEFEND: case AiState.SUPPORT: default:
                    // 朝最近敌人或敌方半场
                    var en = _nearest_enemy(ap, ap.player.position);
                    if (en != null) look = (en.transform.position - ap.player.position).normalized;
                    else look = _forward(ap);
                    break;
            }
            if (look.sqrMagnitude < 0.0001f) return;
            look.y = 0f; look.Normalize();
            var q = Quaternion.LookRotation(look, Vector3.up);
            ap.player.rotation = Quaternion.Slerp(ap.player.rotation, q, TURN_SPEED * dt);
        }

        // ========== 移动 + 状态执行（ATTACK/PASS 出手） ==========
        protected virtual void _move(AiPlayerSlot ap, float dt) {
            var pc = _pc(ap); if (pc == null) return;
            var ball = _ball();

            // ===== 状态处理：需出手 ATTACK/PASS =====
            bool carrying = pc.HasBall();
            if (ap.state == AiState.ATTACK && carrying) { _do_shoot(ap); ap.total_carry_time = 0; return; }
            if (ap.state == AiState.PASS   && carrying) { _do_pass(ap);  ap.total_carry_time = 0; return; }

            // ===== 无球追球：进入接球范围就持球 =====
            if (!carrying && ball != null && ball.owner == null) {
                float d = Vector3.Distance(ap.player.position, ball.transform.position);
                if (d < ap.profile.ball_pickup_range) {
                    ball.ReturnToPlayer(ap.player);
                    ap.hold_timer = 0f; ap.hold_duration = 1f + UnityEngine.Random.value * 1.5f;
                    ap.total_carry_time = 0f; ap.state = AiState.DRIBBLE;
                    return;
                }
            }

            // ===== 朝 target_pos 移动 =====
            Vector3 toT = ap.target_pos - ap.player.position; toT.y = 0f;
            float distT = toT.magnitude;
            if (distT < 0.05f) return; // 到了
            Vector3 dir = toT / distT;
            // ===== 队友分离排斥 =====
            Vector3 sep = _separation_repel(ap, ap.profile.separation);
            Vector3 finalDir = (dir * 0.75f + sep * 0.25f).normalized;
            // 速度（角色 profile 倍率 × 持球/无球差异）
            float s = pc._GetEffectiveValue("speed", pc.moveSpeed) * ap.profile.moveSpeedMult;
            if (ap.state == AiState.CHASE_BALL) s *= 1.15f;
            if (carrying) s *= 0.95f;

            var step = finalDir * Mathf.Min(s * dt, distT);
            ap.player.position += step;
            // 边界 clamp
            var p = ap.player.position;
            ap.player.position = new Vector3(Mathf.Clamp(p.x, FIELD_X_MIN, FIELD_X_MAX), 0f, Mathf.Clamp(p.z, FIELD_Z_MIN, FIELD_Z_MAX));
        }

        protected virtual Vector3 _separation_repel(AiPlayerSlot ap, float radius) {
            Vector3 sum = Vector3.zero; int n = 0;
            var myPos = ap.player.position;
            foreach (var other in ai_players) {
                if (other == ap || other == null || other.player == null) continue;
                if (other.team.ToUpper() != ap.team.ToUpper()) continue;
                var d = myPos - other.player.position; d.y = 0f;
                var dd = d.magnitude;
                if (dd > 0.0001f && dd < radius) {
                    // 越近排斥越强
                    sum += (d / dd) * (radius - dd) / radius;
                    n++;
                }
            }
            if (n == 0) return Vector3.zero;
            return sum.normalized;
        }

        // 决竞球AI投球
        protected virtual void _do_shoot(AiPlayerSlot ap) {
            var pc = _pc(ap); var ball = _ball();
            if (pc == null || ball == null || !pc.HasBall()) return;
            Vector3 myPos = ap.player.position;
            Vector3 dir; float dist = 5f;
            if ((ap.target_pos - myPos).sqrMagnitude > 0.001f) {
                var to = ap.target_pos - myPos;
                dir = to.normalized;
                dist = Mathf.Clamp(to.magnitude + 0.8f, 2f, 8f);
            } else {
                var fallback = ap.team == "A" ? new Vector3(-1.9f, 0, 0) : new Vector3(1.9f, 0, 0);
                dir = (fallback - myPos).normalized; dist = 4f;
            }
            // 投球角度误差
            float errDeg = ap.profile == AiProfile.Attacker ? 4f : (ap.profile == AiProfile.Defender ? 8f : 6f);
            float rot = (UnityEngine.Random.value - 0.5f) * 2f * errDeg * Mathf.Deg2Rad;
            float c = Mathf.Cos(rot), s = Mathf.Sin(rot);
            dir = new Vector3(dir.x * c - dir.z * s, 0, dir.x * s + dir.z * c).normalized;

            pc.SetCarryingBall(ball, false);
            var atk = pc._GetEffectiveValue("attack", pc.attackPower);
            ball.Launch(myPos, dir, atk, dist, ap.player, new List<Dictionary<string, object>>());
            ap.state = AiState.DEFEND; ap.target_pos = _formation_hold_pos(ap);
            Debug.Log("[AI] " + ap.player.name + " 投球 → " + dist.ToString("F1") + "m");
        }

        // 决竞球AI传球
        protected virtual void _do_pass(AiPlayerSlot ap) {
            var pc = _pc(ap); var ball = _ball();
            if (pc == null || ball == null || !pc.HasBall()) return;
            Vector3 myPos = ap.player.position;
            Vector3 toT = ap.target_pos - myPos; toT.y = 0f;
            float dist = toT.magnitude;
            Vector3 dir = dist > 0.001f ? (toT / dist) : _forward(ap);
            // 传球误差
            float errDeg = 3f;
            float rot = (UnityEngine.Random.value - 0.5f) * 2f * errDeg * Mathf.Deg2Rad;
            float c = Mathf.Cos(rot), s = Mathf.Sin(rot);
            dir = new Vector3(dir.x * c - dir.z * s, 0, dir.x * s + dir.z * c).normalized;
            pc.SetCarryingBall(ball, false);
            float atk = pc._GetEffectiveValue("attack", pc.attackPower) * 0.5f; // 传球伤害 0.5x
            ball.Launch(myPos, dir, atk, dist + 0.8f, ap.player, new List<Dictionary<string, object>>());
            ap.state = AiState.DEFEND; ap.target_pos = _formation_hold_pos(ap);
            Debug.Log("[AI] " + ap.player.name + " 传球 → " + dist.ToString("F1") + "m");
        }
    }
}

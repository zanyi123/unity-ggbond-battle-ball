using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using BattleBall.Core;

namespace BattleBall.Battle
{
    public class BallController : MonoBehaviour
    {
        public float ballSpeed = 4f;
        public float maxFlightDistance = 5f;
        public float fieldXMin = -6.5f, fieldXMax = 6.5f;
        public float fieldZMin = -3.9f, fieldZMax = 3.9f;
        public float goalZHalf = 1.3f;
        public float outFieldXMin = -8.5f, outFieldXMax = 8.5f;

        public event Action<Transform, float, string> OnBallHitPlayer;
        public event Action<string> OnGoalScored;
        public event Action<string> OnBallOutOfBounds;
        public event Action<Transform> OnBallReturned;
        public event Action<Transform> OnLaunched;

        public Transform owner, lastAttacker;
        public Vector3 flightDir; public float flightTravelled;
        public bool isFlying, isReturning, isTracking;
        public float launchDamage = 20f;
        public string attackerElement = "none";
        public List<Dictionary<string, object>> flightSkills = new List<Dictionary<string, object>>();
        /// <summary>飞行自旋速度(rad/s)，对应GD ball_proxy_3d.gd BALL_SPIN_SPEED=12.0</summary>
        public float spinSpeed = 12f;
        /// <summary>球模型子节点(用于旋转视觉)。null=旋转自身transform</summary>
        public Transform ballMesh;

        private HashSet<int> _hitPlayerIds = new HashSet<int>();

        protected virtual void Awake() {
            // 确保有碰撞体和刚体(对应GD球物理检测)
            var col = GetComponent<Collider>();
            if (col == null) {
                var sc = gameObject.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = 0.3f;
            }
            var rb = GetComponent<Rigidbody>();
            if (rb == null) {
                rb = gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            // 确保球模型可见
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers) r.enabled = true;
        }

        protected virtual void FixedUpdate()
        {
            if (owner != null && !isFlying && !isReturning)
            {
                transform.position = owner.position + new Vector3(0f, 0.5f, 0f);
                var pc = owner.GetComponent<PlayerController>();
                if (pc != null && pc.carriedBall != this) pc.SetCarryingBall(this, true);
                return;
            }
            if (isFlying) _DoFlight(Time.fixedDeltaTime);
        }

        public virtual void Shoot(Vector3 dir, float speed = -1f, Transform attacker = null)
        {
            Launch(transform.position, dir, launchDamage, maxFlightDistance, attacker, new List<Dictionary<string, object>>());
        }

        public virtual void Launch(Vector3 start, Vector3 dir, float damage, float maxDist, Transform attacker, List<Dictionary<string, object>> skills)
        {
            isFlying = true; isReturning = false; isTracking = false;
            if (owner != null) { var opc = owner.GetComponent<PlayerController>(); if (opc != null) opc.SetCarryingBall(this, false); }
            owner = null;
            flightDir = dir.normalized;
            if (flightDir.sqrMagnitude < 0.0001f) flightDir = Vector3.forward;
            flightTravelled = 0f;
            maxFlightDistance = Mathf.Max(1f, maxDist);
            lastAttacker = attacker;
            launchDamage = damage;
            flightSkills = skills ?? new List<Dictionary<string, object>>();
            _hitPlayerIds.Clear();
            transform.position = start;
            attackerElement = "none";
            if (attacker != null)
            {
                var ap = attacker.GetComponent<PlayerController>();
                if (ap != null)
                {
                    attackerElement = ap.element;
                    launchDamage = Mathf.Max(damage, ap._GetEffectiveValue("attack", ap.attackPower));
                }
            }
            OnLaunched?.Invoke(attacker);
            Debug.Log(string.Format("[Ball] Launch dmg={0} elem={1} dir=({2},{3},{4}) from={5}",
                launchDamage, attackerElement, dir.x.ToString("F1"), dir.y.ToString("F1"), dir.z.ToString("F1"),
                attacker != null ? attacker.name : "null"));
        }

        protected virtual void _DoFlight(float dt)
        {
            float step = ballSpeed * dt;
            transform.position += flightDir * step;
            flightTravelled += step;
            // 飞行自旋(对应GD ball_proxy_3d.gd L160: ball_mesh_instance.rotate(Vector3.UP, BALL_SPIN_SPEED * delta))
            var spinTarget = ballMesh != null ? ballMesh : transform;
            spinTarget.Rotate(Vector3.up, spinSpeed * dt * Mathf.Rad2Deg, Space.Self);
            if (flightTravelled >= maxFlightDistance && !isTracking) { _OnBallStopped(); return; }
            _CheckBoundaries();
        }

        public virtual void Stop() { isFlying = false; isReturning = false; isTracking = false; }
        protected virtual void _OnBallStopped() { Stop(); }

        protected virtual void _CheckBoundaries()
        {
            var p = transform.position;
            if (p.x < outFieldXMin) { OnBallOutOfBounds?.Invoke("left"); _ReturnToNearest("A"); return; }
            if (p.x > outFieldXMax) { OnBallOutOfBounds?.Invoke("right"); _ReturnToNearest("B"); return; }
            if (p.z < fieldZMin - 2.0f || p.z > fieldZMax + 2.0f)
            {
                _ReturnToNearest(p.x < 0f ? "A" : "B");
                return;
            }
            if (Mathf.Abs(p.z) > fieldZMax + 0.5f && Mathf.Abs(p.x) < goalZHalf)
            {
                string scoringTeam = p.z > 0 ? "A" : "B";
                OnGoalScored?.Invoke(scoringTeam);
                Stop();
                Debug.Log("[Ball] Goal! team=" + scoringTeam);
            }
        }

        public virtual void _ReturnToNearest(string team)
        {
            Stop();
            Transform nearest = null;
            float nearestDist = float.MaxValue;
            var gm = GameManager.Instance;
            List<Transform> list = null;
            if (gm != null) list = (team == "A") ? gm.team_a : gm.team_b;
            if (list != null && list.Count > 0)
            {
                foreach (var t in list)
                {
                    if (t == null) continue;
                    var pc = t.GetComponent<PlayerController>();
                    if (pc == null || pc.state == PlayerState.Defeated) continue;
                    float d = Vector3.Distance(transform.position, t.position);
                    if (d < nearestDist) { nearestDist = d; nearest = t; }
                }
            }
            if (nearest == null)
            {
                foreach (var pc in UnityEngine.Object.FindObjectsOfType<PlayerController>())
                {
                    if (pc.team.ToUpper() != team.ToUpper() || pc.state == PlayerState.Defeated) continue;
                    float d = Vector3.Distance(transform.position, pc.transform.position);
                    if (d < nearestDist) { nearestDist = d; nearest = pc.transform; }
                }
            }
            if (nearest != null) ReturnToPlayer(nearest);
            else Debug.LogWarning("[Ball] 找不到队" + team + "球员!");
        }

        public virtual void ReturnToPlayer(Transform p)
        {
            Stop();
            if (p == null) return;
            if (owner != null && owner != p)
            {
                var opc = owner.GetComponent<PlayerController>();
                if (opc != null) opc.SetCarryingBall(this, false);
            }
            owner = p;
            var npc = p.GetComponent<PlayerController>();
            if (npc != null) npc.SetCarryingBall(this, true);
            OnBallReturned?.Invoke(p);
            Debug.Log("[Ball] -> " + p.name);
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (other == null || !isFlying) return;
            var t = other.transform;
            if (lastAttacker != null && t == lastAttacker) return;
            var pc = t.GetComponent<PlayerController>();
            if (pc == null || pc.state == PlayerState.Defeated) return;
            int pid = t.GetInstanceID();
            if (_hitPlayerIds.Contains(pid)) return;
            _hitPlayerIds.Add(pid);

            if (lastAttacker != null)
            {
                var atkPc = lastAttacker.GetComponent<PlayerController>();
                if (atkPc != null && atkPc.team.ToUpper() == pc.team.ToUpper())
                {
                    Debug.Log("[Ball] 同队接球: " + t.name);
                    ReturnToPlayer(t);
                    return;
                }
            }

            var res = pc.take_damage(launchDamage, lastAttacker, attackerElement);
            string effect = res.ContainsKey("effect") ? res["effect"].ToString() : "none";
            float dmg = res.ContainsKey("damage") ? (float)Convert.ToDouble(res["damage"]) : 0f;
            if (effect.Contains("knockback") && lastAttacker != null)
            {
                float dist = effect == "knockback2" ? 1.5f : 0.8f;
                pc._apply_knockback(lastAttacker, dist);
            }
            OnBallHitPlayer?.Invoke(t, dmg, effect);
            Stop();
        }
        /// <summary>SkillVisualManager 兼容: 返回球视觉半径(米)</summary>
        public virtual float GetVisualRadius() { return 0.3f; }
    }
}

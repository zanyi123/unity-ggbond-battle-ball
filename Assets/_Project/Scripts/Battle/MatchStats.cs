using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using BattleBall.Core;
using BattleBall.Battle;
using BattleBall.Battle.AI;

namespace BattleBall.Battle
{
    /// <summary>原GD match_stats.gd 简化版：收集进球/击中/传球/持球时间</summary>
    public class MatchStats : MonoBehaviour
    {
        public class PlayerStat
        {
            public string playerId; public string team;
            public int shots, hits, passes, catches, defeats;
            public float carryTime;
        }
        public Dictionary<string, PlayerStat> stats = new Dictionary<string, PlayerStat>();

        public int goalsA, goalsB;
        public int totalShots, totalHits, totalPasses;
        public float matchDuration;

        public static MatchStats Instance { get; private set; }
        protected virtual void Awake() {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }
        protected virtual void OnDestroy() { if (Instance == this) Instance = null; }

        public virtual void RegisterPlayer(string playerId, string team) {
            stats[playerId] = new PlayerStat { playerId = playerId, team = team };
        }

        public virtual void AddGoal(string team) {
            if (team == "A") goalsA++; else if (team == "B") goalsB++;
        }

        public virtual void AddShot(string playerId, string team) {
            totalShots++;
            if (stats.TryGetValue(playerId, out var s)) s.shots++;
        }

        public virtual void AddHit(string playerId) {
            totalHits++;
            if (stats.TryGetValue(playerId, out var s)) s.hits++;
        }

        public virtual void AddPass(string playerId) {
            totalPasses++;
            if (stats.TryGetValue(playerId, out var s)) s.passes++;
        }

        public virtual void AddCatch(string playerId) {
            if (stats.TryGetValue(playerId, out var s)) s.catches++;
        }

        public virtual void AddDefeated(string playerId) {
            if (stats.TryGetValue(playerId, out var s)) s.defeats++;
        }

        public virtual void TickCarry(string playerId, float dt) {
            if (stats.TryGetValue(playerId, out var s)) s.carryTime += dt;
        }

        public virtual string DumpSummary() {
            return string.Format("[MatchStats] A:{0} / B:{1}  totalShots={2} hits={3} passes={4}",
                goalsA, goalsB, totalShots, totalHits, totalPasses);
        }
    }
}

using UnityEngine;
using System;
using System.Collections.Generic;

namespace BattleBall.Battle
{
    /// <summary>
    /// 比赛个人数据采集器
    /// 采集每位球员的击杀/死亡/伤害/接球/截球/技能释放等统计数据
    /// </summary>
    public class MatchPlayerStats : MonoBehaviour
    {
        // 单个球员的统计数据结构
        public class PlayerStats
        {
            public string player_id = "";
            public string player_name = "";
            public string team = "";

            // 进攻数据
            public int kills = 0;
            public float damage_dealt = 0.0f;
            public int skill_hits = 0;

            // 防守数据
            public int deaths = 0;
            public float damage_taken = 0.0f;
            public int balls_caught = 0;
            public int balls_intercepted = 0;

            // 技能数据
            public int skills_used = 0;

            // 生存数据
            public bool survived = true;
            public float stamina_remaining = 0.0f;

            public Dictionary<string, object> to_dict()
            {
                return new Dictionary<string, object>
                {
                    {"player_id", player_id},
                    {"player_name", player_name},
                    {"team", team},
                    {"kills", kills},
                    {"damage_dealt", damage_dealt},
                    {"skill_hits", skill_hits},
                    {"deaths", deaths},
                    {"damage_taken", damage_taken},
                    {"balls_caught", balls_caught},
                    {"balls_intercepted", balls_intercepted},
                    {"skills_used", skills_used},
                    {"survived", survived},
                    {"stamina_remaining", stamina_remaining},
                };
            }
        }

        // 所有球员的统计数据 { player_instance_id: PlayerStats }
        private Dictionary<int, PlayerStats> _stats = new Dictionary<int, PlayerStats>();
        // 是否正在记录
        private bool _recording = false;

        public static MatchPlayerStats Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        protected virtual void OnDestroy() { if (Instance == this) Instance = null; }

        public void start_recording()
        {
            _stats.Clear();
            _recording = true;
            Debug.Log("[MatchPlayerStats] 开始记录");
        }

        public void stop_recording()
        {
            _recording = false;
            Debug.Log("[MatchPlayerStats] 停止记录, 共" + _stats.Count + "名球员");
        }

        public bool is_recording() { return _recording; }

        // 注册球员（比赛开始时调用）
        public void register_player(PlayerController player)
        {
            if (!_recording || player == null) return;
            var stats = new PlayerStats();
            stats.player_id = player.charId;
            object nm;
            if (player.charData != null && player.charData.TryGetValue("name", out nm) && nm != null)
                stats.player_name = nm.ToString();
            else
                stats.player_name = "?";
            stats.team = player.team;
            _stats[player.GetInstanceID()] = stats;
        }

        // 获取球员统计
        public PlayerStats get_stats(PlayerController player)
        {
            if (player == null) return null;
            return _stats.TryGetValue(player.GetInstanceID(), out var s) ? s : null;
        }

        // 获取所有统计
        public Dictionary<int, PlayerStats> get_all_stats() { return _stats; }

        // 获取某队所有统计
        public List<PlayerStats> get_team_stats(string team)
        {
            var result = new List<PlayerStats>();
            foreach (var kv in _stats)
                if (kv.Value.team == team) result.Add(kv.Value);
            return result;
        }

        // 获取比赛报告字典
        public Dictionary<string, object> get_report()
        {
            var players = new List<object>();
            foreach (var kv in _stats) players.Add(kv.Value.to_dict());
            return new Dictionary<string, object> { {"players", players} };
        }

        // 打印报告
        public void print_report()
        {
            Debug.Log("\n========== 比赛个人数据 ==========");
            foreach (var kv in _stats)
            {
                var s = kv.Value;
                string status = s.survived ? "存活" : "被罚下";
                Debug.Log(string.Format("  {0}({1}) 击杀:{2} 死亡:{3} 伤害:{4:F0} 接球:{5} 截球:{6} 技能:{7} [{8}]",
                    s.player_name, s.team, s.kills, s.deaths, s.damage_dealt,
                    s.balls_caught, s.balls_intercepted, s.skills_used, status));
            }
            Debug.Log("===================================");
        }

        // ===== 事件上报接口（由 player / ball 调用）=====

        // 球员被击败（击杀者+被击败者）
        public void report_defeat(PlayerController killer, PlayerController victim)
        {
            if (!_recording) return;
            var killer_stats = get_stats(killer);
            var victim_stats = get_stats(victim);
            if (killer_stats != null) killer_stats.kills += 1;
            if (victim_stats != null)
            {
                victim_stats.deaths += 1;
                victim_stats.survived = false;
            }
        }

        // 球员造成伤害
        public void report_damage_dealt(PlayerController attacker, float amount)
        {
            if (!_recording) return;
            var stats = get_stats(attacker);
            if (stats != null) stats.damage_dealt += amount;
        }

        // 球员承受伤害
        public void report_damage_taken(PlayerController victim, float amount)
        {
            if (!_recording) return;
            var stats = get_stats(victim);
            if (stats != null) stats.damage_taken += amount;
        }

        // 球员接球（同队传球）
        public void report_ball_caught(PlayerController player)
        {
            if (!_recording) return;
            var stats = get_stats(player);
            if (stats != null) stats.balls_caught += 1;
        }

        // 球员截球（敌方球）
        public void report_ball_intercepted(PlayerController player)
        {
            if (!_recording) return;
            var stats = get_stats(player);
            if (stats != null) stats.balls_intercepted += 1;
        }

        // 球员释放技能
        public void report_skill_used(PlayerController player)
        {
            if (!_recording) return;
            var stats = get_stats(player);
            if (stats != null) stats.skills_used += 1;
        }

        // 技能命中
        public void report_skill_hit(PlayerController player)
        {
            if (!_recording) return;
            var stats = get_stats(player);
            if (stats != null) stats.skill_hits += 1;
        }

        // 记录最终体力（比赛结束时调用）
        public void record_final_stamina(PlayerController player)
        {
            var stats = get_stats(player);
            if (stats != null) stats.stamina_remaining = player.stamina;
        }
    }
}

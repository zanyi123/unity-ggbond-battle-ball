using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BattleBall.Core;

namespace BattleBall.Systems
{
    /// <summary>
    /// 比赛奖励系统
    /// 管理奖励开关、连胜、可调数值、货币发放
    /// </summary>
    public class RewardSystem : MonoBehaviour
    {
        // ===== 奖励配置（可通过开发者面板调整并保存到存档）=====
        public Dictionary<string, object> reward_config = new Dictionary<string, object>
        {
            {"reward_enabled", false},
            {"win_fairy_coin", 300}, {"win_spirit_ore", 10}, {"win_crystal", 3},
            {"lose_fairy_coin", 150}, {"lose_spirit_ore", 5}, {"lose_crystal", 1},
            {"draw_fairy_coin", 200}, {"draw_spirit_ore", 7}, {"draw_crystal", 2},
            {"streak_fairy_coin_bonus", 10},
            {"streak_spirit_ore_bonus", 2},
            {"streak_crystal_bonus", 1},
        };

        // 连胜计数
        private int _win_streak = 0;

        // 配置文件路径（相对 Application.persistentDataPath）
        private const string REWARD_CONFIG_PATH = "reward_config.json";
        private const string MATCH_HISTORY_PATH = "match_history.json";
        private const int MAX_HISTORY = 50;

        public event Action<Dictionary<string, object>> rewards_granted;

        public static RewardSystem Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        protected virtual void OnDestroy() { if (Instance == this) Instance = null; }

        protected virtual void Start()
        {
            _load_config();
        }

        // ===== 内部工具 =====
        private static object _get(Dictionary<string, object> d, string k, object def)
        {
            if (d == null) return def;
            object v; return d.TryGetValue(k, out v) ? v : def;
        }
        private static int _int(object v) { if (v == null) return 0; if (v is int i) return i; if (v is long l) return (int)l; int r; return int.TryParse(v.ToString(), out r) ? r : 0; }
        private static float _float(object v) { if (v == null) return 0f; if (v is float f) return f; if (v is double dd) return (float)dd; if (v is int i) return (float)i; if (v is long l) return (float)l; float r; return float.TryParse(v.ToString(), out r) ? r : 0f; }
        private static string _str(object v) { return v == null ? "" : v.ToString(); }
        private static bool _bool(object v) { if (v == null) return false; if (v is bool b) return b; bool r; return bool.TryParse(v.ToString(), out r) ? r : false; }

        private string _config_path() { return Path.Combine(Application.persistentDataPath, REWARD_CONFIG_PATH); }
        private string _history_path() { return Path.Combine(Application.persistentDataPath, MATCH_HISTORY_PATH); }

        // ===== 奖励发放 =====

        // 计算并发放奖励（比赛结束时调用）
        // result: "win" / "lose" / "draw"
        // is_forfeit: 是否中途退出（不发奖励、不更新连胜、不记录历史）
        public Dictionary<string, object> grant_rewards(string result, bool is_forfeit = false)
        {
            var empty_rewards = new Dictionary<string, object>
            {
                {"fairy_coin", 0}, {"spirit_ore", 0}, {"crystal", 0}
            };

            if (is_forfeit)
            {
                Debug.Log("[RewardSystem] 中途退出，不发放奖励，不更新连胜");
                return empty_rewards;
            }

            if (!_bool(_get(reward_config, "reward_enabled", false)))
            {
                Debug.Log("[RewardSystem] 奖励已关闭，不发放");
                return empty_rewards;
            }

            var rewards = new Dictionary<string, object>
            {
                {"fairy_coin", 0}, {"spirit_ore", 0}, {"crystal", 0}
            };

            switch (result)
            {
                case "win":
                    rewards["fairy_coin"] = _int(_get(reward_config, "win_fairy_coin", 0));
                    rewards["spirit_ore"] = _int(_get(reward_config, "win_spirit_ore", 0));
                    rewards["crystal"] = _int(_get(reward_config, "win_crystal", 0));
                    _win_streak += 1;
                    if (_win_streak > 1)
                    {
                        rewards["fairy_coin"] = _int(rewards["fairy_coin"]) + _int(_get(reward_config, "streak_fairy_coin_bonus", 0)) * (_win_streak - 1);
                        rewards["spirit_ore"] = _int(rewards["spirit_ore"]) + _int(_get(reward_config, "streak_spirit_ore_bonus", 0)) * (_win_streak - 1);
                        rewards["crystal"] = _int(rewards["crystal"]) + _int(_get(reward_config, "streak_crystal_bonus", 0)) * (_win_streak - 1);
                    }
                    break;
                case "lose":
                    rewards["fairy_coin"] = _int(_get(reward_config, "lose_fairy_coin", 0));
                    rewards["spirit_ore"] = _int(_get(reward_config, "lose_spirit_ore", 0));
                    rewards["crystal"] = _int(_get(reward_config, "lose_crystal", 0));
                    _win_streak = 0;
                    break;
                case "draw":
                    rewards["fairy_coin"] = _int(_get(reward_config, "draw_fairy_coin", 0));
                    rewards["spirit_ore"] = _int(_get(reward_config, "draw_spirit_ore", 0));
                    rewards["crystal"] = _int(_get(reward_config, "draw_crystal", 0));
                    // 平局不重置连胜也不增加
                    break;
                default:
                    Debug.LogError("[RewardSystem] 未知比赛结果: " + result);
                    return rewards;
            }

            // 发放货币
            if (PlayerSaveManager.Instance != null)
            {
                PlayerSaveManager.Instance.add_currency("fairy_coin", _int(rewards["fairy_coin"]));
                PlayerSaveManager.Instance.add_currency("spirit_ore", _int(rewards["spirit_ore"]));
                PlayerSaveManager.Instance.add_currency("crystal", _int(rewards["crystal"]));
                PlayerSaveManager.Instance.save_slot();
            }

            Debug.Log(string.Format("[RewardSystem] 奖励发放: {0} -> 童话币+{1} 元灵矿石+{2} 水晶+{3} (连胜{4})",
                result, _int(rewards["fairy_coin"]), _int(rewards["spirit_ore"]), _int(rewards["crystal"]), _win_streak));

            if (rewards_granted != null) rewards_granted(rewards);
            return rewards;
        }

        public int get_win_streak() { return _win_streak; }

        public void reset_win_streak() { _win_streak = 0; }

        public void set_reward_enabled(bool enabled)
        {
            reward_config["reward_enabled"] = enabled;
            _save_config();
            Debug.Log("[RewardSystem] 奖励开关: " + (enabled ? "开启" : "关闭"));
        }

        public void update_config(Dictionary<string, object> new_config)
        {
            foreach (var kv in new_config)
            {
                if (reward_config.ContainsKey(kv.Key))
                    reward_config[kv.Key] = kv.Value;
            }
            _save_config();
            Debug.Log("[RewardSystem] 配置已更新");
        }

        private void _save_config()
        {
            try
            {
                var data = new Dictionary<string, object>(reward_config);
                data["win_streak"] = _win_streak;
                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(_config_path(), json);
            }
            catch (Exception e)
            {
                Debug.LogError("[RewardSystem] 保存配置失败: " + e.Message);
            }
        }

        private void _load_config()
        {
            var path = _config_path();
            if (!File.Exists(path)) return;
            try
            {
                var text = File.ReadAllText(path);
                var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(text);
                if (data == null) return;
                foreach (var kv in data)
                {
                    if (reward_config.ContainsKey(kv.Key))
                        reward_config[kv.Key] = kv.Value;
                }
                if (data.TryGetValue("win_streak", out var ws))
                    _win_streak = _int(ws);
                Debug.Log("[RewardSystem] 配置已加载, 奖励开关: " + (_bool(_get(reward_config, "reward_enabled", false)) ? "开启" : "关闭"));
            }
            catch (Exception e)
            {
                Debug.LogError("[RewardSystem] 加载配置失败: " + e.Message);
            }
        }

        // ===== 比赛历史记录 =====

        // 记录比赛历史（比赛正常结束时调用，中途退出不调用）
        // result: "win"/"lose"/"draw"
        // score_a, score_b: 比分
        // duration: 比赛时长（秒）
        // stats_data: player_stats.get_report() 返回的数据
        // rewards: 发放的奖励字典
        public void record_match_history(string result, int score_a, int score_b, float duration,
            Dictionary<string, object> stats_data, Dictionary<string, object> rewards)
        {
            var history = _load_history();

            int total_kills = 0;
            int total_deaths = 0;
            var players_o = _get(stats_data, "players", null);
            List<object> players = null;
            if (players_o is List<object> li) players = li;
            else if (players_o is JArray ja) players = ja.ToObject<List<object>>();

            if (players != null)
            {
                foreach (var p in players)
                {
                    Dictionary<string, object> pstats;
                    if (p is Dictionary<string, object> d) pstats = d;
                    else if (p is JObject jo) pstats = jo.ToObject<Dictionary<string, object>>();
                    else continue;
                    if (_str(_get(pstats, "team", "")) == "a")
                    {
                        total_kills += _int(_get(pstats, "kills", 0));
                        total_deaths += _int(_get(pstats, "deaths", 0));
                    }
                }
            }

            var record = new Dictionary<string, object>
            {
                {"date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")},
                {"result", result},
                {"score", score_a + ":" + score_b},
                {"duration", Math.Round(duration * 10.0) / 10.0},
                {"kills", total_kills},
                {"deaths", total_deaths},
                {"rewards", new Dictionary<string, object>
                    {
                        {"fairy_coin", _int(_get(rewards, "fairy_coin", 0))},
                        {"spirit_ore", _int(_get(rewards, "spirit_ore", 0))},
                        {"crystal", _int(_get(rewards, "crystal", 0))},
                    }
                },
            };

            history.Insert(0, record);

            if (history.Count > MAX_HISTORY)
                history = history.GetRange(0, MAX_HISTORY);

            _save_history(history);
            Debug.Log(string.Format("[RewardSystem] 比赛历史已记录: {0} {1} 击杀{2}/死亡{3} (共{4}条)",
                result, score_a + ":" + score_b, total_kills, total_deaths, history.Count));
        }

        public List<object> get_match_history()
        {
            return _load_history();
        }

        public void clear_match_history()
        {
            _save_history(new List<object>());
            Debug.Log("[RewardSystem] 比赛历史已清空");
        }

        private void _save_history(List<object> history)
        {
            try
            {
                var json = JsonConvert.SerializeObject(history, Formatting.Indented);
                File.WriteAllText(_history_path(), json);
            }
            catch (Exception e)
            {
                Debug.LogError("[RewardSystem] 保存历史失败: " + e.Message);
            }
        }

        private List<object> _load_history()
        {
            var path = _history_path();
            if (!File.Exists(path)) return new List<object>();
            try
            {
                var text = File.ReadAllText(path);
                var data = JsonConvert.DeserializeObject<List<object>>(text);
                return data ?? new List<object>();
            }
            catch (Exception)
            {
                return new List<object>();
            }
        }

        // ===== UI 兼容方法 =====
        public virtual bool GetRewardEnabled() { return true; }
        public virtual void SetRewardEnabled(bool v) { }
        public virtual int GetWinStreak() { return 0; }

        // ===== static 快捷入口供 UI 类名直接调用 (原实例方法 1 行不动) =====
        public static bool RewardEnabledStatic()  => Instance != null && Instance.GetRewardEnabled();
        public static void RewardSetEnabledStatic(bool v) { if (Instance != null) Instance.SetRewardEnabled(v); }
        public static int  WinStreakStatic()       => Instance != null ? Instance.GetWinStreak() : 0;
    }
}

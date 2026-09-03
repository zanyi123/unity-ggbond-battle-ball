using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BattleBall.Core;
using PlayerSaveManager = BattleBall.Systems.PlayerSaveManager;
using BattleBall.Systems.Nutrition;  // NutritionManager
using BattleBall.Systems;            // RewardSystem / PlayerSaveManager
using BattleBall.Systems.Inventory;  // InventoryManager

namespace BattleBall.UI
{
    /// <summary>
    /// 比赛结算界面
    /// 4个Tab: 战报 / 数据面板 / 消耗与奖励 / 确认
    /// </summary>
    public class MatchResultUI : MonoBehaviour
    {
        // 结算数据
        private int _scoreA = 0;
        private int _scoreB = 0;
        private string _result = "draw";  // "win" / "lose" / "draw"
        private float _durationS = 0f;
        private Dictionary<string, object> _rewards = new Dictionary<string, object>();
        private Dictionary<string, object> _playerStats = new Dictionary<string, object>(); // MatchPlayerStats.PlayerStats 数组
        private Dictionary<string, object> _preMatchEquipment = new Dictionary<string, object>(); // 赛前装备状态（用于显示损耗）

        // UI元素
        private List<Button> _tabButtons = new List<Button>();
        private VerticalLayoutGroup _tabContainer = null;
        private int _currentTab = 0;
        private static readonly string[] TabNames = { "战报", "数据", "奖励", "确认" };

        // 颜色方案
        private static readonly Color BgColor = new Color(0.06f, 0.08f, 0.12f, 0.98f);
        private static readonly Color WinColor = new Color(0.2f, 0.8f, 0.3f);
        private static readonly Color LoseColor = new Color(0.8f, 0.2f, 0.2f);
        private static readonly Color DrawColor = new Color(0.8f, 0.8f, 0.2f);
        private static readonly Color CardBg = new Color(0.1f, 0.12f, 0.18f, 0.95f);
        private static readonly Color TextColor = new Color(0.85f, 0.88f, 0.92f);
        private static readonly Color AccentColor = new Color(0.3f, 0.6f, 1.0f);

        public event System.Action ResultConfirmed;

        private const float Pix = 0.01f;

        private RectTransform _rect;

        void Start()
        {
            _rect = GetComponent<RectTransform>();
            if (_rect == null) _rect = gameObject.AddComponent<RectTransform>();
            // 全屏覆盖
            SetFullRect(_rect);

            // 背景（不拦截鼠标）
            var bgGo = new GameObject("BG", typeof(RectTransform), typeof(Image));
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.SetParent(transform, false);
            SetFullRect(bgRt);
            bgGo.GetComponent<Image>().color = BgColor;

            // 主容器
            var mainVboxGo = new GameObject("MainVBox", typeof(RectTransform), typeof(VerticalLayoutGroup));
            var mrt = mainVboxGo.GetComponent<RectTransform>();
            mrt.SetParent(transform, false);
            SetFullRect(mrt);
            var mainVbox = mainVboxGo.GetComponent<VerticalLayoutGroup>();
            mainVbox.spacing = 10 * Pix;
            mainVbox.childControlWidth = true;
            mainVbox.childControlHeight = true;
            mainVbox.childForceExpandWidth = true;
            mainVbox.childForceExpandHeight = false;

            // 顶部留白
            var topSpacer = new GameObject("TopSpacer", typeof(RectTransform));
            var tsrt = topSpacer.GetComponent<RectTransform>();
            tsrt.SetParent(mainVboxGo.transform, false);
            tsrt.sizeDelta = new Vector2(0, 30) * Pix;

            // 结果标题
            AddResultHeader(mainVboxGo.transform);

            // 比分行
            AddScoreDisplay(mainVboxGo.transform);

            // Tab按钮栏
            AddTabBar(mainVboxGo.transform);

            // Tab内容区
            var tabContainerGo = new GameObject("TabContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            var tcrt = tabContainerGo.GetComponent<RectTransform>();
            tcrt.SetParent(mainVboxGo.transform, false);
            tcrt.sizeDelta = new Vector2(0, 400) * Pix;
            _tabContainer = tabContainerGo.GetComponent<VerticalLayoutGroup>();
            _tabContainer.spacing = 0;
            _tabContainer.childControlWidth = true;
            _tabContainer.childControlHeight = true;
            _tabContainer.childForceExpandWidth = true;
            _tabContainer.childForceExpandHeight = false;

            // 默认显示第一个Tab（延迟到布局完成后）
            // Unity 中使用协程或 Invoke 模拟 call_deferred
            Invoke("ShowTab0", 0f);
        }

        private void ShowTab0()
        {
            ShowTab(0);
        }

        /// <summary>初始化结算数据（由 battle_manager 调用）</summary>
        public void Setup(int scoreA, int scoreB, float durationS, string result,
            Dictionary<string, object> rewards, Dictionary<string, object> statsData,
            Dictionary<string, object> preMatchEquipment = null)
        {
            _scoreA = scoreA;
            _scoreB = scoreB;
            _durationS = durationS;
            _result = result;
            _rewards = rewards;
            _playerStats = statsData;
            _preMatchEquipment = preMatchEquipment ?? new Dictionary<string, object>();
        }

        // ===== UI构建 =====

        private void AddResultHeader(Transform parent)
        {
            var header = NewLabel("Header", Vector2.zero, new Vector2(0, 50), "", 36, Color.white, parent);
            header.alignment = TextAnchor.MiddleCenter;

            switch (_result)
            {
                case "win":
                    header.text = "★ 胜利！★";
                    header.color = WinColor;
                    break;
                case "lose":
                    header.text = "惋惜败北";
                    header.color = LoseColor;
                    break;
                default:
                    header.text = "平局";
                    header.color = DrawColor;
                    break;
            }
        }

        private void AddScoreDisplay(Transform parent)
        {
            var scoreBoxGo = new GameObject("ScoreBox", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var sbRt = scoreBoxGo.GetComponent<RectTransform>();
            sbRt.SetParent(parent, false);
            var scoreBox = scoreBoxGo.GetComponent<HorizontalLayoutGroup>();
            scoreBox.spacing = 20 * Pix;
            scoreBox.childControlWidth = true;
            scoreBox.childControlHeight = true;
            scoreBox.childForceExpandWidth = true;
            scoreBox.childAlignment = TextAnchor.MiddleCenter;

            NewLabel("LabelA", Vector2.zero, new Vector2(200, 30), "队A  " + _scoreA, 28, AccentColor, scoreBoxGo.transform).alignment = TextAnchor.MiddleCenter;
            NewLabel("VS", Vector2.zero, new Vector2(40, 30), "vs", 22, Color.gray, scoreBoxGo.transform).alignment = TextAnchor.MiddleCenter;
            NewLabel("LabelB", Vector2.zero, new Vector2(200, 30), _scoreB + "  队B", 28, new Color(1.0f, 0.5f, 0.3f), scoreBoxGo.transform).alignment = TextAnchor.MiddleCenter;

            // 比赛时长
            int minutes = (int)_durationS / 60;
            int seconds = (int)_durationS % 60;
            var timeLabel = NewLabel("Time", Vector2.zero, new Vector2(300, 30), string.Format("比赛时长: {0:00}:{1:00}", minutes, seconds), 16, Color.gray, parent);
            timeLabel.alignment = TextAnchor.MiddleCenter;
        }

        private void AddTabBar(Transform parent)
        {
            var tabBarGo = new GameObject("TabBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var tbRt = tabBarGo.GetComponent<RectTransform>();
            tbRt.SetParent(parent, false);
            var tabBar = tabBarGo.GetComponent<HorizontalLayoutGroup>();
            tabBar.spacing = 5 * Pix;
            tabBar.childControlWidth = true;
            tabBar.childControlHeight = true;
            tabBar.childForceExpandWidth = true;
            tabBar.childAlignment = TextAnchor.MiddleCenter;

            for (int i = 0; i < TabNames.Length; i++)
            {
                int idx = i;
                var btn = NewButton("Tab_" + i, Vector2.zero, new Vector2(100, 36), TabNames[i], 16, Color.white, tabBarGo.transform);
                btn.onClick.AddListener(() => ShowTab(idx));
                _tabButtons.Add(btn);
            }
        }

        // ===== Tab内容 =====

        private void ShowTab(int index)
        {
            _currentTab = index;

            // 清除旧内容（立即释放）
            for (int i = 0; i < _tabContainer.transform.childCount; i++)
            {
                Destroy(_tabContainer.transform.GetChild(i).gameObject);
            }

            // 更新按钮高亮
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                var btn = _tabButtons[i];
                var colors = btn.colors;
                colors.normalColor = (i == index) ? new Color(1.0f, 1.0f, 0.7f) : Color.white;
                btn.colors = colors;
            }

            // 显示对应Tab内容
            switch (index)
            {
                case 0: ShowBattleReport(); break;
                case 1: ShowDataPanel(); break;
                case 2: ShowRewardsPanel(); break;
                case 3: ShowConfirmPanel(); break;
            }
        }

        /// <summary>Tab 1: 战报</summary>
        private void ShowBattleReport()
        {
            var scroll = NewScrollView("Scroll", _tabContainer.transform);
            var vbox = scroll.GetComponent<ScrollRect>().content;

            // 我方阵容
            AddSectionTitle(vbox, "── 我方阵容 ──");
            AddTeamStats(vbox, "a");

            // 对方阵容
            AddSectionTitle(vbox, "── 对方阵容 ──");
            AddTeamStats(vbox, "b");
        }

        /// <summary>Tab 2: 数据面板</summary>
        private void ShowDataPanel()
        {
            var scroll = NewScrollView("Scroll", _tabContainer.transform);
            var vbox = scroll.GetComponent<ScrollRect>().content;

            // 每位球员的数据卡片
            var allStats = GetList(_playerStats, "players", new List<object>());
            foreach (var ps in allStats)
            {
                var psDict = ps as Dictionary<string, object>;
                if (psDict != null)
                    AddPlayerDataCard(vbox, psDict);
            }
        }

        /// <summary>Tab 3: 消耗与奖励</summary>
        private void ShowRewardsPanel()
        {
            var scroll = NewScrollView("Scroll", _tabContainer.transform);
            var vbox = scroll.GetComponent<ScrollRect>().content;

            // 食物消耗
            AddSectionTitle(vbox, "── 食物消耗 ──");
            var foodLabel = NewLabel("FoodLabel", Vector2.zero, new Vector2(0, 20), "", 14, TextColor, vbox);
            foodLabel.alignment = TextAnchor.MiddleLeft;
            string activeFood = NutritionManager.Instance != null ? NutritionManager.Instance.GetActiveFoodId() : "";
            if (activeFood != "")
            {
                var foodData = NutritionManager.Instance.GetFood(activeFood);
                string foodName = GetStr(foodData, "name", activeFood);
                foodLabel.text = "  " + foodName + " x1 已消耗";
            }
            else
            {
                foodLabel.text = "  未食用食物";
            }

            // 装备损耗（赛前→赛后）
            AddSectionTitle(vbox, "── 装备损耗 ──");
            bool hasLoss = false;
            if (PlayerSaveManager.Instance != null && HasMethod(PlayerSaveManager.Instance, "GetAllEquipped"))
            {
                var postEquip = PlayerSaveManager.Instance.GetAllEquipped();
                foreach (string charId in _preMatchEquipment.Keys)
                {
                    var preChar = GetDict(_preMatchEquipment, charId, new Dictionary<string, object>());
            var postChar = GetDict(new Dictionary<string, object>(), charId, new Dictionary<string, object>());
                    foreach (string slot in new[] { "glove", "jersey", "shoes" })
                    {
                        var preData = GetObj(preChar, slot, new Dictionary<string, object> { { "item_id", "" }, { "durability", 0 } });
                        var postData = GetObj(postChar, slot, new Dictionary<string, object> { { "item_id", "" }, { "durability", 0 } });
                        string preItem = "";
                        float preDur = 0f;
                        if (preData is Dictionary<string, object> preD)
                        {
                            preItem = GetStr(preD, "item_id", "");
                            preDur = GetFloat(preD, "durability", 0f);
                        }
                        else
                            preItem = preData == null ? "" : preData.ToString();
                        string postItem = "";
                        float postDur = 0f;
                        if (postData is Dictionary<string, object> postD)
                        {
                            postItem = GetStr(postD, "item_id", "");
                            postDur = GetFloat(postD, "durability", 0f);
                        }
                        else
                            postItem = postData == null ? "" : postData.ToString();
                        if (preItem != "" || postItem != "")
                        {
                            float loss = preDur - postDur;
                            if (loss > 0.001f)
                            {
                                string itemName = "";
                                int maxDur = 0;
                                if (InventoryManager.Instance != null && preItem != "")
                                {
                                    var def = InventoryManager.Instance.GetItemDef(preItem);
                                    itemName = GetStr(def, "name", preItem);
                                    maxDur = GetInt(def, "max_durability", 50);
                                }
                                else if (InventoryManager.Instance != null && postItem != "")
                                {
                                    var def = InventoryManager.Instance.GetItemDef(postItem);
                                    itemName = GetStr(def, "name", postItem);
                                    maxDur = GetInt(def, "max_durability", 50);
                                }
                                if (itemName == "") itemName = "未知装备";
                                var lossLabel = NewLabel("Loss_" + charId + "_" + slot, Vector2.zero, new Vector2(0, 20),
                                    "  " + itemName + ": " + (int)preDur + "/" + maxDur + " → " + (int)postDur + "/" + maxDur + " (-" + loss.ToString("F1") + ")",
                                    14, new Color(1.0f, 0.6f, 0.3f), vbox);
                                lossLabel.alignment = TextAnchor.MiddleLeft;
                                hasLoss = true;
                            }
                        }
                    }
                }
            }
            if (!hasLoss)
            {
                var noLoss = NewLabel("NoLoss", Vector2.zero, new Vector2(0, 20), "  装备无损耗", 14, new Color(0.5f, 0.5f, 0.6f), vbox);
                noLoss.alignment = TextAnchor.MiddleLeft;
            }

            // 比赛奖励
            AddSectionTitle(vbox, "── 比赛奖励 ──");
            var rewardCard = CreateCard(vbox);
            var rewardVboxGo = new GameObject("RewardVBox", typeof(RectTransform), typeof(VerticalLayoutGroup));
            var rvRt = rewardVboxGo.GetComponent<RectTransform>();
            rvRt.SetParent(rewardCard.transform, false);
            rvRt.anchorMin = Vector2.zero;
            rvRt.anchorMax = Vector2.one;
            rvRt.offsetMin = Vector2.zero;
            rvRt.offsetMax = Vector2.zero;
            var rewardVbox = rewardVboxGo.GetComponent<VerticalLayoutGroup>();
            rewardVbox.spacing = 4 * Pix;
            rewardVbox.childControlWidth = true;
            rewardVbox.childControlHeight = true;
            rewardVbox.childForceExpandWidth = true;

            // 奖励结果
            string resultText;
            switch (_result)
            {
                case "win": resultText = "胜利奖励"; break;
                case "lose": resultText = "败北奖励"; break;
                default: resultText = "平局奖励"; break;
            }

            AddRewardRow(rewardVboxGo.transform, resultText, "");
            AddRewardRow(rewardVboxGo.transform, "童话币", "+" + GetInt(_rewards, "fairy_coin", 0));
            AddRewardRow(rewardVboxGo.transform, "元灵矿石", "+" + GetInt(_rewards, "spirit_ore", 0));
            AddRewardRow(rewardVboxGo.transform, "水晶", "+" + GetInt(_rewards, "crystal", 0));

            // 连胜提示
            int streak = RewardSystem.WinStreakStatic();
            if (streak > 1 && _result == "win")
            {
                AddRewardRow(rewardVboxGo.transform, "连胜x" + streak + "加成", "");
            }

            // 奖励未开启提示
            if (!RewardSystem.RewardEnabledStatic())
            {
                var warn = NewLabel("Warn", Vector2.zero, new Vector2(0, 20), "（奖励开关未开启，货币未实际发放）", 12, new Color(1.0f, 0.7f, 0.2f), vbox);
                warn.alignment = TextAnchor.MiddleLeft;
            }
        }

        /// <summary>Tab 4: 确认返回</summary>
        private void ShowConfirmPanel()
        {
            var centerGo = new GameObject("Center", typeof(RectTransform), typeof(VerticalLayoutGroup));
            var cRt = centerGo.GetComponent<RectTransform>();
            cRt.SetParent(_tabContainer.transform, false);
            var center = centerGo.GetComponent<VerticalLayoutGroup>();
            center.childAlignment = TextAnchor.MiddleCenter;
            center.spacing = 10 * Pix;
            center.childControlWidth = true;
            center.childControlHeight = true;
            center.childForceExpandWidth = true;
            center.childForceExpandHeight = false;

            // 状态提示
            string[] lines = { "奖励已结算", "食物状态已清除" };
            foreach (var text in lines)
            {
                var lbl = NewLabel("Line", Vector2.zero, new Vector2(0, 30), text, 18, TextColor, centerGo.transform);
                lbl.alignment = TextAnchor.MiddleCenter;
            }

            // 间距
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            var sRt = spacer.GetComponent<RectTransform>();
            sRt.SetParent(centerGo.transform, false);
            sRt.sizeDelta = new Vector2(0, 20) * Pix;

            // 确认按钮
            var confirmBtn = NewButton("ConfirmBtn", Vector2.zero, new Vector2(220, 48), "确认返回主菜单", 18, Color.white, centerGo.transform);
            confirmBtn.onClick.AddListener(OnConfirmPressed);
        }

        // ===== 辅助UI方法 =====

        private void AddSectionTitle(Transform parent, string title)
        {
            var lbl = NewLabel("Section", Vector2.zero, new Vector2(0, 22), title, 16, AccentColor, parent);
            lbl.alignment = TextAnchor.MiddleLeft;
        }

        private void AddTeamStats(Transform parent, string team)
        {
            var allStats = GetList(_playerStats, "players", new List<object>());
            foreach (var ps in allStats)
            {
                var psDict = ps as Dictionary<string, object>;
                if (psDict == null) continue;
                if (GetStr(psDict, "team", "") != team) continue;
                bool survived = GetBool(psDict, "survived", true);
                string status = survived ? "存活" : "被罚下";
                string line = string.Format("  {0}  击杀:{1}  死亡:{2}  伤害:{3:F0}  [{4}]",
                    GetStr(psDict, "player_name", "?"),
                    GetInt(psDict, "kills", 0),
                    GetInt(psDict, "deaths", 0),
                    GetFloat(psDict, "damage_dealt", 0.0f),
                    status);
                var lbl = NewLabel("StatLine", Vector2.zero, new Vector2(0, 20), line, 14, TextColor, parent);
                lbl.alignment = TextAnchor.MiddleLeft;
            }
        }

        private void AddPlayerDataCard(Transform parent, Dictionary<string, object> ps)
        {
            var card = CreateCard(parent);
            var gridGo = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            var gRt = gridGo.GetComponent<RectTransform>();
            gRt.SetParent(card.transform, false);
            gRt.anchorMin = Vector2.zero;
            gRt.anchorMax = Vector2.one;
            gRt.offsetMin = Vector2.zero;
            gRt.offsetMax = Vector2.zero;
            var grid = gridGo.GetComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.spacing = new Vector2(20, 4) * Pix;
            grid.cellSize = new Vector2(150, 25) * Pix;

            // 标题行
            var nameLbl = NewLabel("Name", Vector2.zero, new Vector2(100, 25), GetStr(ps, "player_name", "?"), 16, AccentColor, gridGo.transform);
            nameLbl.alignment = TextAnchor.MiddleLeft;

            var teamLbl = NewLabel("Team", Vector2.zero, new Vector2(100, 25), "队" + GetStr(ps, "team", "?"), 14, Color.white, gridGo.transform);
            teamLbl.alignment = TextAnchor.MiddleLeft;

            bool survived = GetBool(ps, "survived", true);
            var statusLbl = NewLabel("Status", Vector2.zero, new Vector2(100, 25), survived ? "存活" : "被罚下", 14, survived ? WinColor : LoseColor, gridGo.transform);
            statusLbl.alignment = TextAnchor.MiddleLeft;

            // 数据行
            AddStatRow(gridGo.transform, "击杀", GetInt(ps, "kills", 0).ToString());
            AddStatRow(gridGo.transform, "死亡", GetInt(ps, "deaths", 0).ToString());
            AddStatRow(gridGo.transform, "伤害", GetFloat(ps, "damage_dealt", 0.0f).ToString("F0"));
            AddStatRow(gridGo.transform, "接球", GetInt(ps, "balls_caught", 0).ToString());
            AddStatRow(gridGo.transform, "截球", GetInt(ps, "balls_intercepted", 0).ToString());
            AddStatRow(gridGo.transform, "技能", GetInt(ps, "skills_used", 0).ToString());
        }

        private void AddStatRow(Transform gridParent, string label, string value)
        {
            var lbl = NewLabel("Lbl_" + label, Vector2.zero, new Vector2(100, 25), label, 13, Color.gray, gridParent);
            lbl.alignment = TextAnchor.MiddleLeft;

            var val = NewLabel("Val_" + label, Vector2.zero, new Vector2(100, 25), value, 13, TextColor, gridParent);
            val.alignment = TextAnchor.MiddleLeft;

            // 占位保持3列
            NewLabel("Spacer_" + label, Vector2.zero, new Vector2(100, 25), "", 13, Color.white, gridParent);
        }

        private void AddRewardRow(Transform parent, string label, string value)
        {
            var rowGo = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var rRt = rowGo.GetComponent<RectTransform>();
            rRt.SetParent(parent, false);
            var row = rowGo.GetComponent<HorizontalLayoutGroup>();
            row.spacing = 10 * Pix;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;

            var lbl = NewLabel("Lbl", Vector2.zero, new Vector2(100, 20), "  " + label, 14, TextColor, rowGo.transform);
            lbl.alignment = TextAnchor.MiddleLeft;

            if (value != "")
            {
                var val = NewLabel("Val", Vector2.zero, new Vector2(50, 20), value, 14, WinColor, rowGo.transform);
                val.alignment = TextAnchor.MiddleLeft;
            }
        }

        private GameObject CreateCard(Transform parent)
        {
            var cardGo = new GameObject("Card", typeof(RectTransform), typeof(Image));
            var cRt = cardGo.GetComponent<RectTransform>();
            cRt.SetParent(parent, false);
            cardGo.GetComponent<Image>().color = CardBg;

            // 添加 VerticalLayoutGroup 作为内部容器（等效 GDScript PanelContainer）
            var vg = cardGo.AddComponent<VerticalLayoutGroup>();
            vg.padding = new RectOffset(10, 10, 10, 10);
            vg.childControlWidth = true;
            vg.childControlHeight = true;
            vg.childForceExpandWidth = true;
            vg.childForceExpandHeight = false;

            return cardGo;
        }

        private void OnConfirmPressed()
        {
            Debug.Log("[MatchResultUI] 确认返回主菜单");
            ResultConfirmed?.Invoke();
        }

        // ===== 工具 =====
        private void SetFullRect(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private GameObject NewScrollView(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            SetFullRect(rt);
            go.GetComponent<Image>().color = new Color(0, 0, 0, 0);

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var crt = content.GetComponent<RectTransform>();
            crt.SetParent(rt, false);
            crt.anchorMin = new Vector2(0, 1);
            crt.anchorMax = Vector2.one;
            crt.pivot = new Vector2(0.5f, 1);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            var cvg = content.GetComponent<VerticalLayoutGroup>();
            cvg.spacing = 4 * Pix;
            cvg.childControlWidth = true;
            cvg.childControlHeight = true;
            cvg.childForceExpandWidth = true;
            var csf = content.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sr = go.GetComponent<ScrollRect>();
            sr.content = crt;
            sr.vertical = true;
            sr.horizontal = false;
            return go;
        }

        private Text NewLabel(string name, Vector2 pos, Vector2 size, string text, int fontSize, Color color, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos * Pix;
            rt.sizeDelta = size * Pix;
            var lbl = go.GetComponent<Text>();
            lbl.text = text;
            lbl.fontSize = fontSize;
            lbl.color = color;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.horizontalOverflow = HorizontalWrapMode.Overflow;
            lbl.verticalOverflow = VerticalWrapMode.Overflow;
            return lbl;
        }

        private Button NewButton(string name, Vector2 pos, Vector2 size, string text, int fontSize, Color color, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos * Pix;
            rt.sizeDelta = size * Pix;
            go.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            var txtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var trt = txtGo.GetComponent<RectTransform>();
            trt.SetParent(rt, false);
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var lbl = txtGo.GetComponent<Text>();
            lbl.text = text;
            lbl.fontSize = fontSize;
            lbl.color = color;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.horizontalOverflow = HorizontalWrapMode.Overflow;
            lbl.verticalOverflow = VerticalWrapMode.Overflow;
            return go.GetComponent<Button>();
        }

        // ===== 工具：反射检测方法是否存在 =====
        private static bool HasMethod(object obj, string methodName)
        {
            return obj != null && obj.GetType().GetMethod(methodName) != null;
        }

        // ===== 工具：Dict 取值 =====
        private static string GetStr(Dictionary<string, object> d, string key, string def)
        {
            if (d == null) return def;
            object v;
            return d.TryGetValue(key, out v) && v != null ? v.ToString() : def;
        }

        private static int GetInt(Dictionary<string, object> d, string key, int def)
        {
            if (d == null) return def;
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return def;
            if (v is int) return (int)v;
            int r;
            return int.TryParse(v.ToString(), out r) ? r : def;
        }

        private static float GetFloat(Dictionary<string, object> d, string key, float def)
        {
            if (d == null) return def;
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return def;
            if (v is float) return (float)v;
            if (v is int) return (float)(int)v;
            if (v is double) return (float)(double)v;
            float r;
            return float.TryParse(v.ToString(), out r) ? r : def;
        }

        private static bool GetBool(Dictionary<string, object> d, string key, bool def)
        {
            if (d == null) return def;
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return def;
            if (v is bool) return (bool)v;
            bool r;
            return bool.TryParse(v.ToString(), out r) ? r : def;
        }

        private static object GetObj(Dictionary<string, object> d, string key, object def)
        {
            if (d == null) return def;
            object v;
            return d.TryGetValue(key, out v) ? v : def;
        }

        private static Dictionary<string, object> GetDict(Dictionary<string, object> d, string key, Dictionary<string, object> def)
        {
            if (d == null) return def;
            object v;
            if (!d.TryGetValue(key, out v)) return def;
            return v as Dictionary<string, object> ?? def;
        }

        private static List<object> GetList(Dictionary<string, object> d, string key, List<object> def)
        {
            if (d == null) return def;
            object v;
            if (!d.TryGetValue(key, out v)) return def;
            return v as List<object> ?? def;
        }
    }
}

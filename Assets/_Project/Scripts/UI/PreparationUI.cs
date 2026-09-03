using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using BattleBall.Core;
using PlayerSaveManager = BattleBall.Systems.PlayerSaveManager;

namespace BattleBall.UI
{
    using BattleBall.Battle;             // PlayerController 在 BattleBall.Battle 命名空间
    using BattleBall.UI.Battle;          // AIProfile / AIManager 在嵌套命名空间 BattleBall.UI.Battle
    using BattleBall.Systems;            // RewardSystem / PlayerSaveManager
    using BattleBall.Systems.Nutrition;  // NutritionManager
    using BattleBall.Systems.Training;    // TrainingManager
    using BattleBall.Systems.Inventory;  // InventoryManager

    /// <summary>
    /// 备战界面 - 比赛前和中场休息时使用
    /// 功能：球员替补、元灵切换、战术策略配置
    /// </summary>
    public class PreparationUI : MonoBehaviour
    {
        // 3个自主AI队友的职位显示配置（颜色/名称）
        // 注意：职位不绑死在某个球员上，玩家可在备战面板自由分配（player_roles 数组）
        private static readonly Dictionary<string, RoleDisplay> RoleDisplayMap = new Dictionary<string, RoleDisplay>
        {
            { "attacker", new RoleDisplay { name = "主攻手", color = new Color(1.0f, 0.45f, 0.45f) } },
            { "defender", new RoleDisplay { name = "防御手", color = new Color(0.45f, 0.65f, 1.0f) } },
            { "supporter", new RoleDisplay { name = "辅助手", color = new Color(0.45f, 1.0f, 0.55f) } },
        };
        private static readonly string[] RoleOrder = { "attacker", "defender", "supporter" };

        public event System.Action<int, int> StrategyChanged;
        public event System.Action<int, string> PlayerSubstituted;
        public event System.Action<int, string> SpiritChanged;
        public event System.Action MatchStartedFromPrep;
        public event System.Action BackToMenuRequested;

        // 策略枚举
        public enum PlayerStrategy { Breakthrough, Defense, Passing }
        public enum TeamStrategy { Offensive, Defensive, Balanced }

        // 当前策略
        public int CurrentPlayerStrategy { get; private set; } = (int)PlayerStrategy.Passing;
        public int CurrentTeamStrategy { get; private set; } = (int)TeamStrategy.Balanced;

        // 3个AI队友的职位分配（index→role，玩家可自由调整，不绑死）
        // 默认 主攻/防御/辅助，玩家点击职位按钮可切换
        public string[] PlayerRoles = { "attacker", "defender", "supporter" };

        // AI Profile 映射
        private string _currentRole = "supporter";
        private string _currentTeamStrategyStr = "balanced";
        private string _currentDifficulty = "normal";

        // 战斗数据引用
        private List<PlayerController> _teamAPlayers = new List<PlayerController>();
        private List<Dictionary<string, object>> _availableCharacters = new List<Dictionary<string, object>>();

        // AI管理器引用
        private AIManager _aiManager = null;

        // UI元素引用
        private List<Dictionary<string, object>> _playerWidgets = new List<Dictionary<string, object>>();
        private List<Dictionary<string, object>> _spiritWidgets = new List<Dictionary<string, object>>();
        private List<Dictionary<string, object>> _equipmentWidgets = new List<Dictionary<string, object>>();
        private List<Button> _strategyButtons = new List<Button>();

        // 食物选择UI
        private Dropdown _foodOption;
        private Button _foodEatBtn;
        private Text _foodStatusLabel;

        // 开始按钮引用
        private Button _startBtn;
        // 标题引用（用于中场休息时修改文本）
        private Text _titleLabel;
        // 中场休息倒计时标签
        private Text _halfTimeTimerLabel;
        // 中场休息模式标记
        private bool _isHalfTime = false;

        // 装备槽位信息
        private static readonly Dictionary<string, EquipSlotInfo> EquipSlotInfoMap = new Dictionary<string, EquipSlotInfo>
        {
            { "glove", new EquipSlotInfo { name = "手套", icon = "🧤" } },
            { "jersey", new EquipSlotInfo { name = "球衣", icon = "👕" } },
            { "shoes", new EquipSlotInfo { name = "球鞋", icon = "👟" } },
        };
        private static readonly string[] EquipSlotOrder = { "glove", "jersey", "shoes" };

        private static readonly Dictionary<string, TrainStatInfo> TrainStatsInfoMap = new Dictionary<string, TrainStatInfo>
        {
            { "stamina", new TrainStatInfo { name = "体力", color = Color.green } },
            { "defense", new TrainStatInfo { name = "防御", color = new Color(0.4f, 0.7f, 1.0f) } },
            { "speed", new TrainStatInfo { name = "速度", color = new Color(1.0f, 0.8f, 0.3f) } },
            { "attack", new TrainStatInfo { name = "攻击", color = new Color(1.0f, 0.4f, 0.4f) } },
            { "resilience", new TrainStatInfo { name = "韧性", color = new Color(0.8f, 0.5f, 1.0f) } },
            { "ball_speed", new TrainStatInfo { name = "球速", color = new Color(0.5f, 1.0f, 0.6f) } },
        };
        private static readonly string[] TrainStatsOrder = { "stamina", "defense", "speed", "attack", "resilience", "ball_speed" };

        // 训练弹窗
        private List<Dictionary<string, object>> _trainingWidgets = new List<Dictionary<string, object>>();
        private int _trainPopupPlayerIndex = -1;
        private GameObject _trainPopup = null;

        // 装备选择弹窗
        private int _equipPopupPlayerIndex = -1;
        private GameObject _equipPopup = null;
        private string _equipPopupCurrentSlot = "";

        // 元灵选择弹窗
        private int _spiritPopupPlayerIndex = -1;
        private GameObject _spiritPopup = null;

        private const float Pix = 0.01f;

        private RectTransform _rect;

        void Start()
        {
            _rect = GetComponent<RectTransform>();
            if (_rect == null) _rect = gameObject.AddComponent<RectTransform>();
            BuildUI();
            // 从存档恢复已吃食物状态
            if (NutritionManager.Instance != null)
            {
                NutritionManager.Instance.LoadFromSave();
                RefreshFoodList();
            }
        }

        void Update()
        {
            // 中场休息时更新倒计时显示
            if (_isHalfTime && GameManager.Instance != null)
            {
                float t = Mathf.Max(0f, GameManager.Instance.MatchTime);
                int mins = (int)t / 60;
                int secs = (int)t % 60;
                _halfTimeTimerLabel.text = string.Format("中场休息 {0:00}:{1:00}", mins, secs);
            }
        }

        /// <summary>构建整个备战界面</summary>
        private void BuildUI()
        {
            // 全屏半透明背景
            var bgGo = new GameObject("BG", typeof(RectTransform), typeof(Image));
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.SetParent(transform, false);
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            bgGo.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.92f);

            // 标题
            _titleLabel = NewLabel("Title", new Vector2(0, 15), new Vector2(1200, 35), "⚔ 备战界面 ⚔", 28, new Color(1, 0.9f, 0.3f), transform);
            _titleLabel.alignment = TextAnchor.MiddleCenter;

            // 返回主菜单按钮（左上角）
            var backBtn = NewButton("BackBtn", new Vector2(10, 15), new Vector2(140, 35), "← 返回主菜单", 14, new Color(1.0f, 0.6f, 0.6f), transform);
            backBtn.onClick.AddListener(OnBackToMenu);

            // === 第一行：球员状态 ===
            BuildPlayerRow();

            // === 第二行：元灵选择 ===
            BuildSpiritRow();

            // === 第三行：装备穿戴 ===
            BuildEquipmentRow();

            // === 第四行：训练系统 ===
            BuildTrainingRow();

            // === 第五行：战术策略 ===
            BuildStrategyPanel();

            // === 底部：开始比赛按钮 ===
            _startBtn = NewButton("StartBtn", new Vector2(475, 830), new Vector2(250, 45), "开始比赛!", 22, Color.white, transform);
            _startBtn.onClick.AddListener(OnStartMatch);

            // === 底部：中场休息倒计时（默认隐藏）===
            _halfTimeTimerLabel = NewLabel("HalfTimeTimer", new Vector2(0, 835), new Vector2(1200, 35), "中场休息 01:00", 26, new Color(1, 0.9f, 0.3f), transform);
            _halfTimeTimerLabel.alignment = TextAnchor.MiddleCenter;
            _halfTimeTimerLabel.gameObject.SetActive(false);
        }

        // ===== 第一行：球员状态 =====

        private void BuildPlayerRow()
        {
            var sectionTitle = NewLabel("PlayerSectionTitle", new Vector2(0, 60), new Vector2(1200, 25), "— 球员状态 —", 18, Color.yellow, transform);
            sectionTitle.alignment = TextAnchor.MiddleCenter;

            // 3个卡片横向排列
            for (int i = 0; i < 3; i++)
            {
                float xPos = 50 + i * 390;  // 每卡片370px宽，间隔20px
                BuildPlayerCard(i, xPos, 95);
            }
        }

        private void BuildPlayerCard(int index, float x, float y)
        {
            var cardGo = new GameObject("PlayerCard_" + index, typeof(RectTransform), typeof(Image));
            var crt = cardGo.GetComponent<RectTransform>();
            crt.SetParent(transform, false);
            crt.anchoredPosition = new Vector2(x, y) * Pix;
            crt.sizeDelta = new Vector2(370, 160) * Pix;
            cardGo.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.22f, 0.95f);

            // 位置标签
            var posLabel = NewLabel("PosLabel", new Vector2(10, 8), new Vector2(80, 20), "位置 " + (index + 1), 16, Color.cyan, cardGo.transform);
            posLabel.alignment = TextAnchor.MiddleLeft;

            // 职位标签（玩家可点击切换，不绑死）
            RoleDisplay roleInfo;
            if (!RoleDisplayMap.TryGetValue(PlayerRoles[index], out roleInfo))
                roleInfo = new RoleDisplay { name = "队员", color = Color.white };
            var roleBtn = NewButton("RoleBtn_" + index, new Vector2(75, 6), new Vector2(85, 24), "［" + roleInfo.name + "］", 14, roleInfo.color, cardGo.transform);
            int idx = index;
            roleBtn.onClick.AddListener(() => OnRoleClicked(idx));

            // 球员名称
            var nameLabel = NewLabel("NameLabel", new Vector2(160, 8), new Vector2(120, 20), "未选择", 16, Color.white, cardGo.transform);
            nameLabel.alignment = TextAnchor.MiddleLeft;

            // 体力条
            var staminaLabel = NewLabel("StaminaLabel", new Vector2(10, 38), new Vector2(50, 18), "体力:", 14, new Color(0.8f, 0.8f, 0.8f), cardGo.transform);
            staminaLabel.alignment = TextAnchor.MiddleLeft;
            var staminaBarGo = new GameObject("StaminaBar", typeof(RectTransform), typeof(Slider));
            var sbrt = staminaBarGo.GetComponent<RectTransform>();
            sbrt.SetParent(cardGo.transform, false);
            sbrt.anchoredPosition = new Vector2(60, 38) * Pix;
            sbrt.sizeDelta = new Vector2(200, 18) * Pix;
            var staminaBar = staminaBarGo.GetComponent<Slider>();
            SetupSlider(staminaBar);
            staminaBar.value = 1.0f;

            var staminaVal = NewLabel("StaminaVal", new Vector2(270, 38), new Vector2(40, 18), "100", 14, Color.green, cardGo.transform);
            staminaVal.alignment = TextAnchor.MiddleLeft;

            // 增益色块（体力条右侧，6个属性：体力/防御/速度/攻击/韧性/球速）
            var bonusColors = new List<Image>();
            float bonusXStart = 60 + 200 + 6;
            float bonusSize = 12.0f;
            float bonusGap = 2.0f;
            float bonusY = 38 + 3;
            Color[] statColors = {
                new Color(0.95f, 0.3f, 0.3f),
                new Color(0.3f, 0.6f, 1.0f),
                new Color(1.0f, 0.85f, 0.2f),
                new Color(1.0f, 0.55f, 0.2f),
                new Color(0.75f, 0.45f, 0.95f),
                new Color(0.3f, 0.85f, 0.85f),
            };
            for (int s = 0; s < 6; s++)
            {
                var box = NewColorRect("Bonus_" + s, new Vector2(bonusXStart + s * (bonusSize + bonusGap), bonusY), new Vector2(bonusSize, bonusSize), statColors[s], cardGo.transform);
                box.SetActive(false);
                bonusColors.Add(box.GetComponent<Image>());
            }

            // 速度
            var speedLabel = NewLabel("SpeedLabel", new Vector2(10, 65), new Vector2(120, 18), "速度: --", 14, new Color(0.8f, 0.8f, 0.8f), cardGo.transform);
            speedLabel.alignment = TextAnchor.MiddleLeft;

            // 攻击力
            var attackLabel = NewLabel("AttackLabel", new Vector2(140, 65), new Vector2(120, 18), "攻击: --", 14, new Color(0.8f, 0.8f, 0.8f), cardGo.transform);
            attackLabel.alignment = TextAnchor.MiddleLeft;

            // 防御
            var defenseLabel = NewLabel("DefenseLabel", new Vector2(260, 65), new Vector2(120, 18), "防御: --", 14, new Color(0.8f, 0.8f, 0.8f), cardGo.transform);
            defenseLabel.alignment = TextAnchor.MiddleLeft;

            // 替补按钮
            var subBtn = NewButton("SubBtn", new Vector2(10, 100), new Vector2(80, 30), "替补", 14, Color.white, cardGo.transform);
            subBtn.onClick.AddListener(() => OnSubstitutePlayer(idx));

            // 状态标签
            var stateLabel = NewLabel("StateLabel", new Vector2(110, 105), new Vector2(120, 20), "状态: 正常", 14, Color.green, cardGo.transform);
            stateLabel.alignment = TextAnchor.MiddleLeft;

            _playerWidgets.Add(new Dictionary<string, object>
            {
                { "card", cardGo },
                { "name_label", nameLabel },
                { "role_btn", roleBtn },
                { "stamina_bar", staminaBar },
                { "stamina_val", staminaVal },
                { "speed_label", speedLabel },
                { "attack_label", attackLabel },
                { "defense_label", defenseLabel },
                { "sub_btn", subBtn },
                { "state_label", stateLabel },
                { "bonus_colors", bonusColors }
            });
        }

        // ===== 第二行：元灵选择 =====

        private void BuildSpiritRow()
        {
            var sectionTitle = NewLabel("SpiritSectionTitle", new Vector2(0, 270), new Vector2(1200, 25), "— 元灵选择 —", 18, new Color(1, 0.6f, 0.2f), transform);
            sectionTitle.alignment = TextAnchor.MiddleCenter;

            for (int i = 0; i < 3; i++)
            {
                float xPos = 50 + i * 390;
                BuildSpiritCard(i, xPos, 305);
            }
        }

        private void BuildSpiritCard(int index, float x, float y)
        {
            var cardGo = new GameObject("SpiritCard_" + index, typeof(RectTransform), typeof(Image));
            var crt = cardGo.GetComponent<RectTransform>();
            crt.SetParent(transform, false);
            crt.anchoredPosition = new Vector2(x, y) * Pix;
            crt.sizeDelta = new Vector2(370, 140) * Pix;
            cardGo.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.22f, 0.95f);

            // 位置标签
            var posLabel = NewLabel("PosLabel", new Vector2(10, 8), new Vector2(120, 20), "位置 " + (index + 1) + " 元灵", 15, new Color(1, 0.6f, 0.2f), cardGo.transform);
            posLabel.alignment = TextAnchor.MiddleLeft;

            // 当前元灵名称
            var currentLabel = NewLabel("CurrentLabel", new Vector2(10, 35), new Vector2(200, 20), "当前: 未装备", 14, Color.white, cardGo.transform);
            currentLabel.alignment = TextAnchor.MiddleLeft;

            // 元灵属性
            var attrLabel = NewLabel("AttrLabel", new Vector2(10, 58), new Vector2(250, 20), "加成: 无", 14, new Color(0.7f, 0.7f, 0.7f), cardGo.transform);
            attrLabel.alignment = TextAnchor.MiddleLeft;

            // 更换按钮
            var changeBtn = NewButton("ChangeBtn", new Vector2(10, 88), new Vector2(140, 38), "更换元灵", 16, Color.white, cardGo.transform);
            int idx = index;
            changeBtn.onClick.AddListener(() => OnChangeSpirit(idx));

            // 卸下按钮（默认未装备时禁用）
            var unequipBtn = NewButton("UnequipBtn", new Vector2(160, 88), new Vector2(70, 38), "卸下", 14, new Color(1.0f, 0.4f, 0.4f), cardGo.transform);
            unequipBtn.interactable = false;
            unequipBtn.onClick.AddListener(() => OnUnequipSpirit(idx));

            // 元灵图标（用Image显示元素颜色圆形）
            var iconPanelGo = new GameObject("IconPanel", typeof(RectTransform), typeof(Image));
            var iprt = iconPanelGo.GetComponent<RectTransform>();
            iprt.SetParent(cardGo.transform, false);
            iprt.anchoredPosition = new Vector2(280, 15) * Pix;
            iprt.sizeDelta = new Vector2(70, 70) * Pix;
            iconPanelGo.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.4f);

            var iconLabel = NewLabel("IconLabel", new Vector2(285, 28), new Vector2(60, 40), "未\n装备", 12, new Color(0.8f, 0.8f, 0.8f), cardGo.transform);
            iconLabel.alignment = TextAnchor.MiddleCenter;

            // 技能色块容器（3个，28×28，在元灵图标下方）
            var skillBoxes = new List<GameObject>();
            var skillChars = new List<Text>();
            for (int s = 0; s < 3; s++)
            {
                var sbox = new GameObject("SkillBox_" + s, typeof(RectTransform), typeof(Image));
                var sbrt = sbox.GetComponent<RectTransform>();
                sbrt.SetParent(cardGo.transform, false);
                sbrt.anchoredPosition = new Vector2(280 + s * 30, 90) * Pix;
                sbrt.sizeDelta = new Vector2(28, 28) * Pix;
                var sbg = sbox.GetComponent<Image>();
                sbg.color = new Color(0.2f, 0.2f, 0.2f);

                var schar = NewLabel("SkillChar_" + s, new Vector2(0, 5), new Vector2(28, 18), "", 13, Color.white, sbox.transform);
                schar.alignment = TextAnchor.MiddleCenter;

                skillBoxes.Add(sbox);
                skillChars.Add(schar);
            }

            _spiritWidgets.Add(new Dictionary<string, object>
            {
                { "card", cardGo },
                { "current_label", currentLabel },
                { "attr_label", attrLabel },
                { "change_btn", changeBtn },
                { "unequip_btn", unequipBtn },
                { "icon_panel", iconPanelGo },
                { "icon_label", iconLabel },
                { "skill_boxes", skillBoxes },
                { "skill_chars", skillChars }
            });
        }

        // ===== 第三行：装备穿戴 =====

        private void BuildEquipmentRow()
        {
            var sectionTitle = NewLabel("EquipSectionTitle", new Vector2(0, 460), new Vector2(1200, 25), "— 装备穿戴 —", 18, new Color(0.9f, 0.7f, 0.3f), transform);
            sectionTitle.alignment = TextAnchor.MiddleCenter;

            for (int i = 0; i < 3; i++)
            {
                float xPos = 50 + i * 390;
                BuildEquipmentCard(i, xPos, 490);
            }
        }

        private void BuildEquipmentCard(int index, float x, float y)
        {
            var cardGo = new GameObject("EquipCard_" + index, typeof(RectTransform), typeof(Image));
            var crt = cardGo.GetComponent<RectTransform>();
            crt.SetParent(transform, false);
            crt.anchoredPosition = new Vector2(x, y) * Pix;
            crt.sizeDelta = new Vector2(370, 70) * Pix;
            cardGo.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.22f, 0.95f);

            // 位置标签
            var posLabel = NewLabel("PosLabel", new Vector2(10, 5), new Vector2(120, 20), "位置 " + (index + 1) + " 装备", 14, new Color(0.9f, 0.7f, 0.3f), cardGo.transform);
            posLabel.alignment = TextAnchor.MiddleLeft;

            // 3个槽位标签
            var slotLabels = new List<Text>();
            var slotIcons = new List<Text>();
            for (int s = 0; s < 3; s++)
            {
                string slotKey = EquipSlotOrder[s];
                EquipSlotInfo info;
                if (!EquipSlotInfoMap.TryGetValue(slotKey, out info)) info = new EquipSlotInfo();

                var iconLbl = NewLabel("SlotIcon_" + s, new Vector2(10 + s * 120, 28), new Vector2(20, 20), info.icon ?? "?", 16, Color.white, cardGo.transform);
                iconLbl.alignment = TextAnchor.MiddleLeft;
                slotIcons.Add(iconLbl);

                var slotLbl = NewLabel("Slot_" + s, new Vector2(30 + s * 120, 30), new Vector2(110, 20), (info.name ?? "") + ": 未装备", 12, new Color(0.6f, 0.6f, 0.6f), cardGo.transform);
                slotLbl.alignment = TextAnchor.MiddleLeft;
                slotLabels.Add(slotLbl);
            }

            // 更换按钮
            var changeBtn = NewButton("ChangeBtn", new Vector2(290, 38), new Vector2(70, 26), "更换", 13, Color.white, cardGo.transform);
            int idx = index;
            changeBtn.onClick.AddListener(() => OnChangeEquipment(idx));

            _equipmentWidgets.Add(new Dictionary<string, object>
            {
                { "card", cardGo },
                { "slot_labels", slotLabels },
                { "slot_icons", slotIcons },
                { "change_btn", changeBtn },
            });
        }

        // ===== 第四行：训练系统 =====

        private void BuildTrainingRow()
        {
            var sectionTitle = NewLabel("TrainSectionTitle", new Vector2(0, 580), new Vector2(1200, 25), "— 训练系统 —", 18, new Color(0.5f, 0.9f, 0.6f), transform);
            sectionTitle.alignment = TextAnchor.MiddleCenter;

            for (int i = 0; i < 3; i++)
            {
                float xPos = 50 + i * 390;
                BuildTrainingCard(i, xPos, 600);
            }
        }

        private void BuildTrainingCard(int index, float x, float y)
        {
            var cardGo = new GameObject("TrainCard_" + index, typeof(RectTransform), typeof(Image));
            var crt = cardGo.GetComponent<RectTransform>();
            crt.SetParent(transform, false);
            crt.anchoredPosition = new Vector2(x, y) * Pix;
            crt.sizeDelta = new Vector2(370, 90) * Pix;
            cardGo.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.22f, 0.95f);

            var posLabel = NewLabel("PosLabel", new Vector2(10, 5), new Vector2(120, 20), "位置 " + (index + 1) + " 训练", 14, new Color(0.5f, 0.9f, 0.6f), cardGo.transform);
            posLabel.alignment = TextAnchor.MiddleLeft;

            var trainBtn = NewButton("TrainBtn", new Vector2(290, 5), new Vector2(70, 26), "训练", 13, new Color(0.5f, 0.9f, 0.6f), cardGo.transform);
            int idx = index;
            trainBtn.onClick.AddListener(() => OnOpenTraining(idx));

            var statsDisplay = NewLabel("StatsDisplay", new Vector2(10, 35), new Vector2(350, 20), "训练加成: 无", 13, new Color(0.8f, 0.8f, 0.8f), cardGo.transform);
            statsDisplay.alignment = TextAnchor.MiddleLeft;

            int fieldLevel = TrainingManager.Instance != null ? TrainingManager.Instance.GetFieldLevel() : 1;
            var fieldLbl = NewLabel("FieldLbl", new Vector2(10, 60), new Vector2(200, 20), "场地等级: Lv." + fieldLevel, 12, new Color(0.6f, 0.8f, 1.0f), cardGo.transform);
            fieldLbl.alignment = TextAnchor.MiddleLeft;

            _trainingWidgets.Add(new Dictionary<string, object>
            {
                { "card", cardGo },
                { "pos_label", posLabel },
                { "train_btn", trainBtn },
                { "stats_display", statsDisplay },
                { "field_lbl", fieldLbl },
            });
        }

        private void UpdateTrainingWidget(int index)
        {
            if (index >= _trainingWidgets.Count) return;
            if (index >= _teamAPlayers.Count) return;
            var w = _trainingWidgets[index];
            var player = _teamAPlayers[index];
            if (player == null) return;
            string charId = player.CharacterId;
            var trainData = PlayerSaveManager.Instance.GetCharacterTrain(charId);

            string bonusText = "";
            foreach (var statKey in TrainStatsOrder)
            {
                int bonus = GetInt(trainData, statKey + "_bonus", 0);
                if (bonus > 0)
                {
                    if (bonusText != "") bonusText += " ";
                    TrainStatInfo info;
                    if (TrainStatsInfoMap.TryGetValue(statKey, out info))
                        bonusText += info.name + "+" + bonus;
                }
            }

            ((Text)w["stats_display"]).text = "训练加成: " + (bonusText != "" ? bonusText : "无");

            int fieldLevel = TrainingManager.Instance.GetFieldLevel();
            ((Text)w["field_lbl"]).text = "场地等级: Lv." + fieldLevel;
        }

        // ===== 训练弹窗 =====

        private void OnOpenTraining(int index)
        {
            OpenTrainingPopup(index);
        }

        private void OpenTrainingPopup(int playerIndex)
        {
            CloseTrainingPopup();
            _trainPopupPlayerIndex = playerIndex;

            if (playerIndex >= _teamAPlayers.Count) return;
            var player = _teamAPlayers[playerIndex];
            if (player == null) return;
            string charId = player.CharacterId;
            string charName = GetStr(player.CharData, "name", "?");
            var trainData = PlayerSaveManager.Instance.GetCharacterTrain(charId);
            var charData = DataManager.Instance.GetCharacterById(charId);

            var popup = new GameObject("TrainingPopup", typeof(RectTransform), typeof(Image));
            popup.GetComponent<RectTransform>().SetParent(transform, false);
            SetFullRect(popup.GetComponent<RectTransform>());
            popup.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);
            // 点击遮罩关闭：通过 Button
            var popupBtn = popup.AddComponent<Button>();
            popupBtn.onClick.AddListener(CloseTrainingPopup);
            _trainPopup = popup;

            // 弹窗面板
            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            var prt = panelGo.GetComponent<RectTransform>();
            prt.SetParent(popup.transform, false);
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = new Vector2((200 + 800) / 2 - 720, (60 + 680) / 2 - 360) * Pix; // 简化居中
            prt.sizeDelta = new Vector2(800, 620) * Pix;
            panelGo.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.22f, 0.95f);

            var title = NewLabel("Title", new Vector2(10, 10), new Vector2(400, 30), "训练 - " + charName, 20, new Color(0.5f, 0.9f, 0.6f), panelGo.transform);
            title.alignment = TextAnchor.MiddleLeft;

            int fieldLevel = TrainingManager.Instance.GetFieldLevel();
            var fieldInfo = NewLabel("FieldInfo", new Vector2(10, 40), new Vector2(300, 20), "当前场地等级: Lv." + fieldLevel, 14, new Color(0.6f, 0.8f, 1.0f), panelGo.transform);
            fieldInfo.alignment = TextAnchor.MiddleLeft;

            var _uc = TrainingManager.Instance.GetFieldUpgradeCost(fieldLevel); int upgradeCost = (_uc != null && _uc.ContainsKey("gold")) ? (int)_uc["gold"] : 0;
            var upgradeBtn = NewButton("UpgradeBtn", new Vector2(450, 35), new Vector2(230, 30), upgradeCost > 0 ? "升级场地 (花费 " + upgradeCost + " 童话币)" : "场地已满级", 13, new Color(0.8f, 0.6f, 1.0f), panelGo.transform);
            if (upgradeCost > 0)
                upgradeBtn.interactable = PlayerSaveManager.Instance.GetCurrency("fairy_coin") >= upgradeCost;
            else
                upgradeBtn.interactable = false;
            upgradeBtn.onClick.AddListener(OnUpgradeField);

            var _tc = TrainingManager.Instance.GetTrainCost(); int trainCost = (_tc != null && _tc.ContainsKey("gold")) ? (int)_tc["gold"] : 0;
            var costLbl = NewLabel("CostLbl", new Vector2(10, 70), new Vector2(400, 20), "每次训练花费: " + trainCost + " 童话币，属性+" + TrainingManager.TRAIN_BONUS_PER_TIME, 14, new Color(0.7f, 0.7f, 0.7f), panelGo.transform);
            costLbl.alignment = TextAnchor.MiddleLeft;

            // 属性行
            for (int i = 0; i < TrainStatsOrder.Length; i++)
            {
                string statKey = TrainStatsOrder[i];
                TrainStatInfo info;
                if (!TrainStatsInfoMap.TryGetValue(statKey, out info)) info = new TrainStatInfo();
                float baseVal = GetFloat(charData, statKey, 0f);
                int bonus = GetInt(trainData, statKey + "_bonus", 0);
                float maxVal = TrainingManager.Instance.GetStatMax(charId, statKey);

                float rowY = 100 + i * 40;
                var nameLbl = NewLabel("Name_" + statKey, new Vector2(10, rowY), new Vector2(60, 35), info.name, 15, info.color, panelGo.transform);
                nameLbl.alignment = TextAnchor.MiddleLeft;

                var barGo = new GameObject("Bar_" + statKey, typeof(RectTransform), typeof(Slider));
                var brt = barGo.GetComponent<RectTransform>();
                brt.SetParent(panelGo.transform, false);
                brt.anchoredPosition = new Vector2(80, rowY) * Pix;
                brt.sizeDelta = new Vector2(400, 24) * Pix;
                var bar = barGo.GetComponent<Slider>();
                SetupSlider(bar);
                bar.maxValue = maxVal;
                bar.value = baseVal + bonus;

                var valLbl = NewLabel("Val_" + statKey, new Vector2(490, rowY), new Vector2(90, 35), string.Format("{0:F0} / {1:F0}", baseVal + bonus, maxVal), 13, Color.white, panelGo.transform);
                valLbl.alignment = TextAnchor.MiddleLeft;

                bool canTrain = TrainingManager.Instance.CanTrain(charId, statKey);
                var trainStatBtn = NewButton("TrainBtn_" + statKey, new Vector2(590, rowY), new Vector2(60, 30), canTrain ? "训练" : "已满", 13, Color.white, panelGo.transform);
                if (canTrain)
                    trainStatBtn.interactable = PlayerSaveManager.Instance.GetCurrency("fairy_coin") >= trainCost;
                else
                    trainStatBtn.interactable = false;
                string sk = statKey;
                trainStatBtn.onClick.AddListener(() => OnTrainStat(sk));
            }

            var closeBtn = NewButton("CloseBtn", new Vector2(360, 580), new Vector2(80, 30), "关闭", 14, Color.white, panelGo.transform);
            closeBtn.onClick.AddListener(CloseTrainingPopup);
        }

        private void OnTrainStat(string statKey)
        {
            int idx = _trainPopupPlayerIndex;
            if (idx < 0 || idx >= _teamAPlayers.Count) return;
            var player = _teamAPlayers[idx];
            if (player == null) return;
            string charId = player.CharacterId;

            bool ok = TrainingManager.Instance.TrainStat(charId, statKey);
            if (ok)
            {
                Debug.Log("[训练] " + charId + " 属性 " + statKey + " 训练成功");
                UpdateTrainingWidget(idx);
                CloseTrainingPopup();
                OpenTrainingPopup(idx);
            }
        }

        private void OnUpgradeField()
        {
            bool ok = TrainingManager.Instance.UpgradeField();
            if (ok)
            {
                int newLevel = TrainingManager.Instance.GetFieldLevel();
                Debug.Log("[训练] 场地升级到 Lv." + newLevel);
                foreach (var w in _trainingWidgets)
                {
                    ((Text)w["field_lbl"]).text = "场地等级: Lv." + newLevel;
                }
                CloseTrainingPopup();
            }
        }

        private void CloseTrainingPopup()
        {
            if (_trainPopup != null) Destroy(_trainPopup);
            _trainPopup = null;
            _trainPopupPlayerIndex = -1;
        }

        private void UpdateEquipmentWidget(int index)
        {
            if (index >= _equipmentWidgets.Count) return;
            if (index >= _teamAPlayers.Count) return;
            var w = _equipmentWidgets[index];
            var player = _teamAPlayers[index];
            if (player == null) return;
            string charId = player.CharacterId;

            var slotLabels = (List<Text>)w["slot_labels"];
            for (int s = 0; s < 3; s++)
            {
                string slotKey = EquipSlotOrder[s];
                var _ei682 = PlayerSaveManager.Instance.GetEquippedItem(charId, slotKey); string itemId = (_ei682 != null && _ei682.ContainsKey("item_id")) ? _ei682["item_id"]?.ToString() ?? "" : "";
                var slotLbl = slotLabels[s];
                if (itemId == "")
                {
                    EquipSlotInfo info;
                    if (!EquipSlotInfoMap.TryGetValue(slotKey, out info)) info = new EquipSlotInfo();
                    slotLbl.text = (info.name ?? "") + ": 未装备";
                    slotLbl.color = new Color(0.6f, 0.6f, 0.6f);
                }
                else
                {
                    var def = InventoryManager.Instance.GetItemDef(itemId);
                    string name = GetStr(def, "name", itemId);
                    var _ed695 = PlayerSaveManager.Instance.GetEquippedDurability(charId, slotKey); float curDur = (_ed695 != null && _ed695.ContainsKey("current")) ? System.Convert.ToSingle(_ed695["current"]) : 0f;
                    string rarityKey = GetStr(def, "rarity", "common");
                    int maxDur = GetInt(PlayerSaveManager.RARITY_MAX_DURABILITY, rarityKey, 50);
                    float durPct = maxDur > 0 ? curDur / (float)maxDur : 0f;
                    Color durColor = durPct > 0.5f ? new Color(0.3f, 0.9f, 0.4f) : (durPct > 0.2f ? new Color(0.9f, 0.8f, 0.2f) : new Color(0.9f, 0.3f, 0.3f));
                    slotLbl.text = name + "  [耐久:" + (int)curDur + "/" + maxDur + "]";
                    string rarity = GetStr(def, "rarity", "common");
                    slotLbl.color = InventoryManager.Instance.GetRarityColor(rarity);
                    if (durPct <= 0.3f)
                        slotLbl.color = durColor;
                }
            }
        }

        // ===== 装备选择弹窗 =====

        private void OnChangeEquipment(int index)
        {
            Debug.Log("[备战] 位置" + (index + 1) + "更换装备");
            OpenEquipmentSelectPopup(index);
        }

        private void OpenEquipmentSelectPopup(int playerIndex)
        {
            CloseEquipmentPopup();
            _equipPopupPlayerIndex = playerIndex;

            if (playerIndex >= _teamAPlayers.Count) return;
            var player = _teamAPlayers[playerIndex];
            if (player == null) return;
            string charId = player.CharacterId;
            string charName = GetStr(player.CharData, "name", "?");

            var popup = new GameObject("EquipmentSelectPopup", typeof(RectTransform), typeof(Image));
            popup.GetComponent<RectTransform>().SetParent(transform, false);
            SetFullRect(popup.GetComponent<RectTransform>());
            popup.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);
            var popupBtn = popup.AddComponent<Button>();
            popupBtn.onClick.AddListener(CloseEquipmentPopup);
            _equipPopup = popup;

            // 弹窗面板
            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            var prt = panelGo.GetComponent<RectTransform>();
            prt.SetParent(popup.transform, false);
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = Vector2.zero;
            prt.sizeDelta = new Vector2(800, 620) * Pix;
            panelGo.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.22f, 0.95f);

            var title = NewLabel("Title", new Vector2(10, 10), new Vector2(400, 30), "装备选择 - " + charName, 20, new Color(0.9f, 0.7f, 0.3f), panelGo.transform);
            title.alignment = TextAnchor.MiddleLeft;

            // 当前穿戴状态
            var equippedRowGo = new GameObject("EquippedRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var ert = equippedRowGo.GetComponent<RectTransform>();
            ert.SetParent(panelGo.transform, false);
            ert.anchoredPosition = new Vector2(10, 50) * Pix;
            ert.sizeDelta = new Vector2(780, 30) * Pix;
            var equippedRow = equippedRowGo.GetComponent<HorizontalLayoutGroup>();
            equippedRow.childControlWidth = true;
            equippedRow.childForceExpandWidth = true;

            foreach (string slotKey in EquipSlotOrder)
            {
                EquipSlotInfo info;
                if (!EquipSlotInfoMap.TryGetValue(slotKey, out info)) info = new EquipSlotInfo();
                var _ei764 = PlayerSaveManager.Instance.GetEquippedItem(charId, slotKey); string itemId = (_ei764 != null && _ei764.ContainsKey("item_id")) ? _ei764["item_id"]?.ToString() ?? "" : "";
                var slotLbl = NewLabel("Cur_" + slotKey, Vector2.zero, new Vector2(200, 20), "", 14, Color.white, equippedRowGo.transform);
                if (itemId == "")
                {
                    slotLbl.text = (info.icon ?? "") + " " + (info.name ?? "") + ": 未装备";
                    slotLbl.color = new Color(0.6f, 0.6f, 0.6f);
                }
                else
                {
                    var def = InventoryManager.Instance.GetItemDef(itemId);
                    var _ed774 = PlayerSaveManager.Instance.GetEquippedDurability(charId, slotKey); float curDur = (_ed774 != null && _ed774.ContainsKey("current")) ? System.Convert.ToSingle(_ed774["current"]) : 0f;
                    string rarityStr = GetStr(def, "rarity", "common");
                    int maxDur = GetInt(PlayerSaveManager.RARITY_MAX_DURABILITY, rarityStr, 50);
                    slotLbl.text = (info.icon ?? "") + " " + GetStr(def, "name", itemId) + " [" + (int)curDur + "/" + maxDur + "]";
                    string rarity = GetStr(def, "rarity", "common");
                    slotLbl.color = InventoryManager.Instance.GetRarityColor(rarity);
                }
                slotLbl.alignment = TextAnchor.MiddleLeft;
            }

            // 3个槽位的装备列表
            var scroll = NewScrollView("Scroll", panelGo.transform);
            var scrollRt = scroll.GetComponent<RectTransform>();
            scrollRt.anchoredPosition = new Vector2(10, 90) * Pix;
            scrollRt.sizeDelta = new Vector2(780, 480) * Pix;

            // 按槽位分组显示背包里的装备
            foreach (string slotKey in EquipSlotOrder)
            {
                EquipSlotInfo info;
                if (!EquipSlotInfoMap.TryGetValue(slotKey, out info)) info = new EquipSlotInfo();

                var slotTitle = NewLabel("SlotTitle_" + slotKey, Vector2.zero, new Vector2(780, 20), "=== " + (info.icon ?? "") + " " + (info.name ?? "") + " ===", 16, new Color(0.9f, 0.7f, 0.3f), scroll.GetComponent<ScrollRect>().content);
                slotTitle.alignment = TextAnchor.MiddleLeft;

                // 当前穿戴的装备 - 卸下按钮
                var _ei800 = PlayerSaveManager.Instance.GetEquippedItem(charId, slotKey); string curItemId = (_ei800 != null && _ei800.ContainsKey("item_id")) ? _ei800["item_id"]?.ToString() ?? "" : "";
                if (curItemId != "")
                {
                    var curDef = InventoryManager.Instance.GetItemDef(curItemId);
            var _ed804 = PlayerSaveManager.Instance.GetEquippedDurability(charId, slotKey); float curDur = (_ed804 != null && _ed804.ContainsKey("current")) ? System.Convert.ToSingle(_ed804["current"]) : 0f;
                    string curRarity = GetStr(curDef, "rarity", "common");
                    int curMaxDur = GetInt(PlayerSaveManager.RARITY_MAX_DURABILITY, curRarity, 50);

                    var curName = NewLabel("Cur_" + slotKey + "_Name", Vector2.zero, new Vector2(500, 30), "当前: " + GetStr(curDef, "name", curItemId) + " [耐久:" + (int)curDur + "/" + curMaxDur + "]", 14, InventoryManager.Instance.GetRarityColor(curRarity), scroll.GetComponent<ScrollRect>().content);
                    curName.alignment = TextAnchor.MiddleLeft;

                    var unequipBtn = NewButton("Unequip_" + slotKey, Vector2.zero, new Vector2(60, 30), "卸下", 13, new Color(1.0f, 0.4f, 0.4f), scroll.GetComponent<ScrollRect>().content);
                    string sk1 = slotKey;
                    unequipBtn.onClick.AddListener(() => OnUnequipEquipment(sk1));
                }

                // 背包里该槽位的装备
                var backpackItems = InventoryManager.Instance.GetBackpackBySlot(slotKey);
                if (backpackItems.Count == 0)
                {
                    var emptyLbl = NewLabel("Empty_" + slotKey, Vector2.zero, new Vector2(780, 20), "  （背包没有此类装备）", 13, new Color(0.5f, 0.5f, 0.5f), scroll.GetComponent<ScrollRect>().content);
                    emptyLbl.alignment = TextAnchor.MiddleLeft;
                }
                else
                {
                    foreach (var item in backpackItems)
                    {
                        string iid = GetStr(item, "item_id", "");
                        var idef = GetDict(item, "def", new Dictionary<string, object>());
                        int icount = GetInt(item, "count", 0);
                        string iname = GetStr(idef, "name", iid);
                        string irarity = GetStr(idef, "rarity", "common");
                        var istats = GetDict(idef, "stats", new Dictionary<string, object>());

                        // 跳过当前已穿戴的
                        if (iid == curItemId) continue;

                        var rowGo = new GameObject("Row_" + iid, typeof(RectTransform), typeof(HorizontalLayoutGroup));
                        var rrt = rowGo.GetComponent<RectTransform>();
                        rrt.SetParent(scroll.GetComponent<ScrollRect>().content, false);
                        rrt.sizeDelta = new Vector2(780, 30) * Pix;
                        var row = rowGo.GetComponent<HorizontalLayoutGroup>();
                        row.childControlWidth = true;
                        row.childForceExpandWidth = true;

                        // 稀有度色块
                        var colorBoxGo = new GameObject("ColorBox", typeof(RectTransform), typeof(Image));
                        var cbRt = colorBoxGo.GetComponent<RectTransform>();
                        cbRt.SetParent(rowGo.transform, false);
                        cbRt.sizeDelta = new Vector2(6, 30) * Pix;
                        colorBoxGo.GetComponent<Image>().color = InventoryManager.Instance.GetRarityColor(irarity);

                        var nameLbl = NewLabel("Name", Vector2.zero, new Vector2(200, 30), "  " + iname + " (x" + icount + ")", 14, InventoryManager.Instance.GetRarityColor(irarity), rowGo.transform);
                        nameLbl.alignment = TextAnchor.MiddleLeft;

                        // 属性加成
                        string statsText = "";
                        foreach (var statKey in istats.Keys)
                        {
                            if (statsText != "") statsText += " ";
                            statsText += StatKeyToName(statKey) + "+" + istats[statKey];
                        }
                        var statsLbl = NewLabel("Stats", Vector2.zero, new Vector2(250, 30), statsText, 12, new Color(0.7f, 0.7f, 0.7f), rowGo.transform);
                        statsLbl.alignment = TextAnchor.MiddleLeft;

                        var wearBtn = NewButton("Wear", Vector2.zero, new Vector2(60, 30), "穿戴", 13, Color.white, rowGo.transform);
                        string sk2 = slotKey;
                        string iid2 = iid;
                        wearBtn.onClick.AddListener(() => OnEquipSelected(sk2, iid2));
                    }
                }

                // 分隔
                var sepGo = new GameObject("Sep", typeof(RectTransform), typeof(Image));
                var seprt = sepGo.GetComponent<RectTransform>();
                seprt.SetParent(scroll.GetComponent<ScrollRect>().content, false);
                seprt.sizeDelta = new Vector2(780, 1) * Pix;
                sepGo.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f);
            }

            // 关闭按钮
            var closeBtn = NewButton("CloseBtn", new Vector2(360, 580), new Vector2(80, 30), "关闭", 14, Color.white, panelGo.transform);
            closeBtn.onClick.AddListener(CloseEquipmentPopup);
        }

        private string StatKeyToName(string key)
        {
            switch (key)
            {
                case "attack_bonus": return "攻击";
                case "defense_bonus": return "防御";
                case "speed_bonus": return "速度";
                case "stamina_bonus": return "体力";
                case "resilience_bonus": return "韧性";
                case "ball_speed_bonus": return "球速";
                default: return key;
            }
        }

        private void OnEquipSelected(string slot, string itemId)
        {
            int idx = _equipPopupPlayerIndex;
            if (idx < 0 || idx >= _teamAPlayers.Count)
            {
                CloseEquipmentPopup();
                return;
            }
            var player = _teamAPlayers[idx];
            if (player == null)
            {
                CloseEquipmentPopup();
                return;
            }
            string charId = player.CharacterId;
            bool ok = InventoryManager.Instance.EquipToCharacter(charId, slot, itemId);
            if (ok)
            {
                UpdateEquipmentWidget(idx);
                Debug.Log("[备战] " + charId + " 穿戴 " + slot + ": " + itemId);
            }
            CloseEquipmentPopup();
        }

        private void OnUnequipEquipment(string slot)
        {
            int idx = _equipPopupPlayerIndex;
            if (idx < 0 || idx >= _teamAPlayers.Count)
            {
                CloseEquipmentPopup();
                return;
            }
            var player = _teamAPlayers[idx];
            if (player == null)
            {
                CloseEquipmentPopup();
                return;
            }
            string charId = player.CharacterId;
            bool ok = InventoryManager.Instance.UnequipFromCharacter(charId, slot);
            if (ok)
            {
                UpdateEquipmentWidget(idx);
                Debug.Log("[备战] " + charId + " 卸下 " + slot);
            }
            CloseEquipmentPopup();
        }

        private void CloseEquipmentPopup()
        {
            if (_equipPopup != null) Destroy(_equipPopup);
            _equipPopup = null;
            _equipPopupPlayerIndex = -1;
            _equipPopupCurrentSlot = "";
        }

        // ===== 第五行：战术策略 =====

        private void BuildStrategyPanel()
        {
            var sectionTitle = NewLabel("StrategySectionTitle", new Vector2(0, 700), new Vector2(1200, 25), "— 战术策略 —", 18, new Color(0.4f, 0.8f, 1.0f), transform);
            sectionTitle.alignment = TextAnchor.MiddleCenter;

            // 个人策略
            var personalLabel = NewLabel("PersonalLabel", new Vector2(80, 735), new Vector2(100, 20), "个人策略:", 15, new Color(0.4f, 0.8f, 1.0f), transform);
            personalLabel.alignment = TextAnchor.MiddleLeft;

            CreateStrategyBtn("突破进攻", (int)PlayerStrategy.Breakthrough, 200, 732, 0);
            CreateStrategyBtn("防守反击", (int)PlayerStrategy.Defense, 310, 732, 1);
            CreateStrategyBtn("传球配合", (int)PlayerStrategy.Passing, 420, 732, 2);

            // 团队策略
            var teamLabel = NewLabel("TeamLabel", new Vector2(560, 735), new Vector2(100, 20), "团队策略:", 15, new Color(1.0f, 0.7f, 0.3f), transform);
            teamLabel.alignment = TextAnchor.MiddleLeft;

            CreateStrategyBtn("全力进攻", (int)TeamStrategy.Offensive + 3, 680, 732, 3);
            CreateStrategyBtn("全力防守", (int)TeamStrategy.Defensive + 3, 790, 732, 4);
            CreateStrategyBtn("攻守平衡", (int)TeamStrategy.Balanced + 3, 900, 732, 5);

            // 策略说明
            var desc = NewLabel("StrategyDesc", new Vector2(350, 775), new Vector2(500, 20), "策略影响AI队友的行为模式，可随时切换", 14, new Color(0.5f, 0.5f, 0.5f), transform);
            desc.alignment = TextAnchor.MiddleCenter;

            // === 食物选择（全队，整场1种1次） ===
            var foodLabel = NewLabel("FoodLabel", new Vector2(1020, 735), new Vector2(100, 20), "赛前食物:", 14, new Color(1.0f, 0.8f, 0.5f), transform);
            foodLabel.alignment = TextAnchor.MiddleLeft;

            var foodOptGo = new GameObject("FoodOption", typeof(RectTransform), typeof(Dropdown));
            var fort = foodOptGo.GetComponent<RectTransform>();
            fort.SetParent(transform, false);
            fort.anchoredPosition = new Vector2(1020, 757) * Pix;
            fort.sizeDelta = new Vector2(200, 30) * Pix;
            _foodOption = foodOptGo.GetComponent<Dropdown>();

            _foodEatBtn = NewButton("FoodEatBtn", new Vector2(1230, 757), new Vector2(60, 30), "食用", 13, new Color(0.9f, 0.7f, 0.3f), transform);
            _foodEatBtn.onClick.AddListener(OnEatFood);

            _foodStatusLabel = NewLabel("FoodStatus", new Vector2(1020, 790), new Vector2(270, 20), "", 12, new Color(0.6f, 0.6f, 0.6f), transform);
            _foodStatusLabel.alignment = TextAnchor.MiddleLeft;

            RefreshFoodList();
        }

        private void CreateStrategyBtn(string text, int strategy, float x, float y, int btnIndex)
        {
            var btn = NewButton("StrategyBtn_" + btnIndex, new Vector2(x, y), new Vector2(100, 35), text, 14, Color.white, transform);
            btn.onClick.AddListener(() => OnStrategySelected(strategy));
            // Unity Button 用 Toggle Group 实现互斥选中，简化为保持原结构
            _strategyButtons.Add(btn);
        }

        // ===== 数据加载 =====

        public void LoadBattleData(List<PlayerController> players)
        {
            _teamAPlayers = players;
            int count = Mathf.Min(3, players.Count);
            for (int i = 0; i < count; i++)
            {
                var player = players[i];
                if (player != null && i < _playerWidgets.Count)
                    UpdatePlayerWidget(i, player);
                if (player != null && i < _equipmentWidgets.Count)
                    UpdateEquipmentWidget(i);
                if (player != null && i < _trainingWidgets.Count)
                    UpdateTrainingWidget(i);
            }
        }

        private void UpdatePlayerWidget(int index, PlayerController player)
        {
            var w = _playerWidgets[index];

            if (player.CharData != null && player.CharData.ContainsKey("name"))
            {
                ((Text)w["name_label"]).text = player.CharData["name"].ToString();
            }

            if (player.CharData != null)
            {
                float speedVal = GetFloat(player.CharData, "speed", 100.0f);
                float attackVal = GetFloat(player.CharData, "attack", 100.0f);
                float defenseVal = GetFloat(player.CharData, "defense", 100.0f);
                ((Text)w["speed_label"]).text = string.Format("速度: {0:F0}", speedVal);
                ((Text)w["attack_label"]).text = string.Format("攻击: {0:F0}", attackVal);
                ((Text)w["defense_label"]).text = string.Format("防御: {0:F0}", defenseVal);
            }

            ((Slider)w["stamina_bar"]).value = 1.0f;
            ((Text)w["stamina_val"]).text = "100";

            // 更新增益色块显示（装备+食物）
            if (w.ContainsKey("bonus_colors"))
            {
                var bonuses = CalcPlayerBonuses(player.CharacterId);
                string[] statKeys = { "stamina_bonus", "defense_bonus", "speed_bonus", "attack_bonus", "resilience_bonus", "ball_speed_bonus" };
                var boxes = (List<Image>)w["bonus_colors"];
                for (int s = 0; s < 6; s++)
                {
                    if (s >= boxes.Count) continue;
                    float bonusVal = GetFloat(bonuses, statKeys[s], 0f);
                    boxes[s].gameObject.SetActive(bonusVal > 0.0f);
                }
            }
        }

        /// <summary>计算球员总增益（装备+食物）</summary>
        private Dictionary<string, object> CalcPlayerBonuses(string charId)
        {
            var result = new Dictionary<string, object>
            {
                { "stamina_bonus", 0 },
                { "defense_bonus", 0 },
                { "speed_bonus", 0 },
                { "attack_bonus", 0 },
                { "resilience_bonus", 0 },
                { "ball_speed_bonus", 0 },
            };
            // 装备加成
            if (PlayerSaveManager.Instance != null)
            {
                var equip = PlayerSaveManager.Instance.GetEquipmentBonuses(charId);
                foreach (var key in result.Keys)
                {
                    if (equip.ContainsKey(key))
                        result[key] = GetInt(result, key, 0) + GetInt(equip, key, 0);
                }
            }
            // 食物加成（全队）
            if (NutritionManager.Instance != null)
            {
                var food = NutritionManager.Instance.GetTeamBonuses();
                foreach (var key in result.Keys)
                {
                    if (food.ContainsKey(key))
                        result[key] = GetInt(result, key, 0) + GetInt(food, key, 0);
                }
            }
            return result;
        }

        // ===== 信号处理 =====

        private void OnStrategySelected(int strategy)
        {
            if (strategy < 3)
            {
                CurrentPlayerStrategy = strategy;
                switch (strategy)
                {
                    case 0: _currentRole = "attacker"; break;
                    case 1: _currentRole = "defender"; break;
                    case 2: _currentRole = "supporter"; break;
                }
            }
            else
            {
                CurrentTeamStrategy = strategy - 3;
                switch (strategy - 3)
                {
                    case 0: _currentTeamStrategyStr = "offensive"; break;
                    case 1: _currentTeamStrategyStr = "defensive"; break;
                    case 2: _currentTeamStrategyStr = "balanced"; break;
                }
            }

            // 更新所有AI队友的profile（保持各自角色，只更新团队策略）
            RebuildTeamAProfiles();
            UpdateStrategyButtonStyles();
            StrategyChanged?.Invoke(CurrentPlayerStrategy, CurrentTeamStrategy);
            Debug.Log("[备战] 策略: 个人=" + _currentRole + " 团队=" + _currentTeamStrategyStr);
        }

        /// <summary>个人策略枚举转名称（外场效用计算用）</summary>
        private string PlayerStrategyToName(int s)
        {
            switch (s)
            {
                case 0: return "breakthrough";
                case 1: return "defense";
                case 2: return "passing";
                default: return "passing";
            }
        }

        private void RebuildTeamAProfiles()
        {
            if (_aiManager == null) return;
            // 职位读 PlayerRoles（玩家可自由分配），不再用硬编码顺序
            for (int i = 0; i < 3; i++)
            {
                var profile = AIProfile.GetRolePreset(PlayerRoles[i]);
                AIProfile.ApplyTeamStrategy(profile, _currentTeamStrategyStr);
                AIProfile.ApplyDifficulty(profile, _currentDifficulty);
                profile.PlayerStrategyName = PlayerStrategyToName(CurrentPlayerStrategy);
                _aiManager.UpdatePlayerProfile(i, profile);
            }
        }

        private void UpdateStrategyButtonStyles()
        {
            // Unity UGUI Button 不直接支持 toggle 状态，可使用 Sprite swap 或 Color swap。
            // 这里仅保留原逻辑结构占位，实际项目中应改用 Toggle Group。
            for (int i = 0; i < _strategyButtons.Count; i++)
            {
                var btn = _strategyButtons[i];
                var colors = btn.colors;
                if (i < 3)
                {
                    colors.normalColor = (i == CurrentPlayerStrategy) ? new Color(1.0f, 1.0f, 0.7f) : Color.white;
                }
                else
                {
                    colors.normalColor = ((i - 3) == CurrentTeamStrategy) ? new Color(1.0f, 1.0f, 0.7f) : Color.white;
                }
                btn.colors = colors;
            }
        }

        // ===== 职位分配（玩家可自由给3个AI队友分配职位，不绑死）=====
        private void OnRoleClicked(int index)
        {
            string currentRole = PlayerRoles[index];
            int startIdx = System.Array.IndexOf(RoleOrder, currentRole);
            if (startIdx < 0) startIdx = 0;
            string nextRole = RoleOrder[(startIdx + 1) % RoleOrder.Length];
            if (nextRole == currentRole) return;

            int occupier = FindRoleOccupier(index, nextRole);
            if (occupier >= 0)
            {
                PlayerRoles[occupier] = currentRole;
                RefreshRoleBtn(occupier);
            }
            PlayerRoles[index] = nextRole;
            RefreshRoleBtn(index);
            RebuildTeamAProfiles();
            Debug.Log("[备战] 位置" + (index + 1) + "职位切换为 " + nextRole);
        }

        private int FindRoleOccupier(int myIndex, string role)
        {
            for (int i = 0; i < PlayerRoles.Length; i++)
            {
                if (i == myIndex) continue;
                if (PlayerRoles[i] == role) return i;
            }
            return -1;
        }

        private void RefreshRoleBtn(int index)
        {
            if (index >= _playerWidgets.Count) return;
            var widget = _playerWidgets[index];
            var roleBtn = widget["role_btn"] as Button;
            if (roleBtn == null) return;
            RoleDisplay roleInfo;
            if (!RoleDisplayMap.TryGetValue(PlayerRoles[index], out roleInfo))
                roleInfo = new RoleDisplay { name = "队员", color = Color.white };
            var txt = roleBtn.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text = "［" + roleInfo.name + "］";
                txt.color = roleInfo.color;
            }
        }

        private void OnSubstitutePlayer(int index)
        {
            Debug.Log("[备战] 位置" + (index + 1) + "替补");
            PlayerSubstituted?.Invoke(index, "");
        }

        private void OnChangeSpirit(int index)
        {
            Debug.Log("[备战] 位置" + (index + 1) + "更换元灵");
            OpenSpiritSelectPopup(index);
        }

        private void OpenSpiritSelectPopup(int playerIndex)
        {
            CloseSpiritPopup();
            _spiritPopupPlayerIndex = playerIndex;

            // 先加载元灵数据
            List<Dictionary<string, object>> spirits = new List<Dictionary<string, object>>();
            if (DataManager.Instance != null)
                spirits = DataManager.Instance.Spirits;

            var popup = new GameObject("SpiritSelectPopup", typeof(RectTransform), typeof(Image));
            popup.GetComponent<RectTransform>().SetParent(transform, false);
            SetFullRect(popup.GetComponent<RectTransform>());
            popup.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);
            var popupBtn = popup.AddComponent<Button>();
            popupBtn.onClick.AddListener(CloseSpiritPopup);
            _spiritPopup = popup;

            // 弹窗面板（根据元灵数量调整高度）
            float popupH = 160.0f + spirits.Count * 120.0f + 60.0f;
            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            var prt = panelGo.GetComponent<RectTransform>();
            prt.SetParent(popup.transform, false);
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = Vector2.zero;
            prt.sizeDelta = new Vector2(900, Mathf.Min(popupH + 50, 700)) * Pix;
            panelGo.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.22f, 0.95f);

            // 滚动容器
            var scroll = NewScrollView("Scroll", panelGo.transform);
            var scrollRt = scroll.GetComponent<RectTransform>();
            scrollRt.anchoredPosition = new Vector2(10, 40) * Pix;
            scrollRt.sizeDelta = new Vector2(880, Mathf.Min(popupH + 20, 620)) * Pix;

            // 元灵卡片列表
            var elementColors = new Dictionary<string, Color>
            {
                { "金刚", new Color(0.85f, 0.75f, 0.3f) },
                { "大地", new Color(0.7f, 0.55f, 0.35f) },
                { "雷火", new Color(1.0f, 0.4f, 0.2f) },
                { "冰雪", new Color(0.4f, 0.8f, 1.0f) },
                { "草木", new Color(0.3f, 0.8f, 0.3f) },
                { "梦幻", new Color(0.7f, 0.5f, 0.9f) },
            };

            for (int i = 0; i < spirits.Count; i++)
            {
                var s = spirits[i];
                string sName = GetStr(s, "name", "?");
                string sElem = GetStr(s, "element", "?");
                string sDesc = GetStr(s, "description", "");
                var sSkills = GetList(s, "skills", new List<object>());
                Color elemColor;
                if (!elementColors.TryGetValue(sElem, out elemColor)) elemColor = new Color(0.6f, 0.6f, 0.6f);

                var cardGo = new GameObject("SpiritCard_" + i, typeof(RectTransform), typeof(Image));
                var crt = cardGo.GetComponent<RectTransform>();
                crt.SetParent(scroll.GetComponent<ScrollRect>().content, false);
                crt.sizeDelta = new Vector2(850, 100) * Pix;
                cardGo.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.22f, 0.98f);

                // 左侧：元素颜色圆形头像
                var avatarGo = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
                var avrt = avatarGo.GetComponent<RectTransform>();
                avrt.SetParent(cardGo.transform, false);
                avrt.anchoredPosition = new Vector2(10, 20) * Pix;
                avrt.sizeDelta = new Vector2(60, 60) * Pix;
                // 圆形头像：用 Sprite 或简单 Image 占位
                avatarGo.GetComponent<Image>().color = elemColor;

                var avatarText = NewLabel("AvatarText", new Vector2(14, 35), new Vector2(50, 20), sElem, 13, Color.white, cardGo.transform);
                avatarText.alignment = TextAnchor.MiddleCenter;

                // 中间：名称 + 描述 + 技能列表
                var nameLbl = NewLabel("Name", new Vector2(80, 8), new Vector2(500, 20), sName + "  [" + sElem + "]", 16, elemColor, cardGo.transform);
                nameLbl.alignment = TextAnchor.MiddleLeft;

                var descLbl = NewLabel("Desc", new Vector2(80, 30), new Vector2(500, 20), sDesc, 12, new Color(0.7f, 0.7f, 0.7f), cardGo.transform);
                descLbl.alignment = TextAnchor.MiddleLeft;

                // 技能列表
                string skillNames = "";
                foreach (var sid in sSkills)
                {
                    var sd = DataManager.Instance.GetSkillById(sid.ToString());
                    if (sd != null && sd.Count > 0)
                    {
                        if (skillNames != "") skillNames += " | ";
                        skillNames += GetStr(sd, "name", sid.ToString());
                    }
                    else
                    {
                        if (skillNames != "") skillNames += " | ";
                        skillNames += sid.ToString();
                    }
                }
                var skillsLbl = NewLabel("Skills", new Vector2(80, 52), new Vector2(500, 18), "技能: " + (skillNames != "" ? skillNames : "无"), 12, new Color(0.6f, 0.8f, 1.0f), cardGo.transform);
                skillsLbl.alignment = TextAnchor.MiddleLeft;

                // 右侧：选择按钮
                var selBtn = NewButton("Select", new Vector2(760, 35), new Vector2(80, 32), "选择", 14, Color.white, cardGo.transform);
                var sCopy = s;
                selBtn.onClick.AddListener(() => OnSpiritSelected(sCopy));
            }

            // 关闭按钮
            var closeBtn = NewButton("CloseBtn", new Vector2(410, Mathf.Min(popupH - 20, 620)), new Vector2(80, 30), "关闭", 14, Color.white, panelGo.transform);
            closeBtn.onClick.AddListener(CloseSpiritPopup);
        }

        private void OnSpiritSelected(Dictionary<string, object> spiritData)
        {
            int idx = _spiritPopupPlayerIndex;
            if (idx < 0 || idx >= _teamAPlayers.Count)
            {
                CloseSpiritPopup();
                return;
            }

            var player = _teamAPlayers[idx];
            if (player == null)
            {
                CloseSpiritPopup();
                return;
            }

            player.EquipSpirit(spiritData);
            UpdateSpiritWidget(idx, spiritData);
            SpiritChanged?.Invoke(idx, GetStr(spiritData, "id", ""));
            Debug.Log("[备战] 位置" + (idx + 1) + " 装备元灵: " + GetStr(spiritData, "name", "?"));
            CloseSpiritPopup();
        }

        private void OnUnequipSpirit(int index)
        {
            if (index < 0 || index >= _teamAPlayers.Count) return;
            var player = _teamAPlayers[index];
            if (player == null) return;
            player.UnequipSpirit();
            ResetSpiritWidget(index);
            SpiritChanged?.Invoke(index, "");
            Debug.Log("[备战] 位置" + (index + 1) + " 卸下元灵");
        }

        private void ResetSpiritWidget(int index)
        {
            if (index >= _spiritWidgets.Count) return;
            var w = _spiritWidgets[index];
            ((Text)w["current_label"]).text = "当前: 未装备";
            ((Text)w["attr_label"]).text = "加成: 无";
            var iconPanel = w["icon_panel"] as GameObject;
            if (iconPanel != null)
                iconPanel.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.4f);
            var iconLabel = w["icon_label"] as Text;
            if (iconLabel != null)
                iconLabel.text = "未\n装备";
            // 清空技能色块
            if (w.ContainsKey("skill_boxes"))
            {
                var skillBoxes = (List<GameObject>)w["skill_boxes"];
                var skillChars = (List<Text>)w["skill_chars"];
                for (int slot = 0; slot < 3; slot++)
                {
                    if (slot >= skillBoxes.Count) break;
                    var sbox = skillBoxes[slot];
                    var schar = skillChars[slot];
                    var sbg = sbox.GetComponent<Image>();
                    sbg.color = new Color(0.18f, 0.18f, 0.18f);
                    schar.text = "";
                }
            }
            // 禁用卸下按钮
            if (w.ContainsKey("unequip_btn"))
                ((Button)w["unequip_btn"]).interactable = false;
        }

        private void UpdateSpiritWidget(int index, Dictionary<string, object> spiritData)
        {
            if (index >= _spiritWidgets.Count) return;
            var w = _spiritWidgets[index];
            ((Text)w["current_label"]).text = "当前: " + GetStr(spiritData, "name", "?") + " [" + GetStr(spiritData, "element", "?") + "]";
            var skills = GetList(spiritData, "skills", new List<object>());
            string skillNames = "";
            foreach (var sid in skills)
            {
                var sd = DataManager.Instance.GetSkillById(sid.ToString());
                if (sd != null && sd.Count > 0)
                {
                    if (skillNames != "") skillNames += ", ";
                    skillNames += GetStr(sd, "name", sid.ToString());
                }
            }
            ((Text)w["attr_label"]).text = "技能: " + (skillNames != "" ? skillNames : "无");

            // 更新图标颜色和文字
            string sElem = GetStr(spiritData, "element", "?");
            var elementColors = new Dictionary<string, Color>
            {
                { "金刚", new Color(0.85f, 0.75f, 0.3f) },
                { "大地", new Color(0.7f, 0.55f, 0.35f) },
                { "雷火", new Color(1.0f, 0.4f, 0.2f) },
                { "冰雪", new Color(0.4f, 0.8f, 1.0f) },
                { "草木", new Color(0.3f, 0.8f, 0.3f) },
                { "梦幻", new Color(0.7f, 0.5f, 0.9f) },
            };
            Color elemColor;
            if (!elementColors.TryGetValue(sElem, out elemColor)) elemColor = new Color(0.6f, 0.6f, 0.6f);
            var iconPanel = w["icon_panel"] as GameObject;
            if (iconPanel != null)
                iconPanel.GetComponent<Image>().color = elemColor;
            var iconLabel = w["icon_label"] as Text;
            if (iconLabel != null)
                iconLabel.text = sElem + "\n元灵";

            // 更新技能色块（3个槽）
            if (w.ContainsKey("skill_boxes"))
            {
                var skillBoxes = (List<GameObject>)w["skill_boxes"];
                var skillChars = (List<Text>)w["skill_chars"];
                for (int slot = 0; slot < 3; slot++)
                {
                    if (slot >= skillBoxes.Count) break;
                    var sbox = skillBoxes[slot];
                    var schar = skillChars[slot];
                    var sbg = sbox.GetComponent<Image>();
                    if (slot >= skills.Count)
                    {
                        sbg.color = new Color(0.18f, 0.18f, 0.18f);
                        schar.text = "";
                        continue;
                    }
                    var sd = DataManager.Instance.GetSkillById(skills[slot].ToString());
                    if (sd == null || sd.Count == 0)
                    {
                        sbg.color = new Color(0.12f, 0.12f, 0.12f);
                        schar.text = "?";
                        continue;
                    }
                    // 色块颜色：优先 icon_color，白色/空时用元素色
                    string colorStr = GetStr(sd, "icon_color", "#FFFFFF");
                    Color boxColor = elemColor;
                    if (colorStr != "" && colorStr != "#FFFFFF")
                    {
                        Color c;
                        if (ColorUtility.TryParseHtmlString(colorStr, out c)) boxColor = c;
                    }
                    sbg.color = boxColor;
                    // 首字
                    string sname = GetStr(sd, "name", "");
                    schar.text = sname.Length > 0 ? sname.Substring(0, 1) : "";
                }
            }
            // 启用卸下按钮
            if (w.ContainsKey("unequip_btn"))
                ((Button)w["unequip_btn"]).interactable = true;
        }

        private void CloseSpiritPopup()
        {
            if (_spiritPopup != null) Destroy(_spiritPopup);
            _spiritPopup = null;
            _spiritPopupPlayerIndex = -1;
        }

        /// <summary>设置中场休息模式（隐藏开始按钮，显示倒计时）</summary>
        public void SetHalfTimeMode(bool isHalfTime)
        {
            _isHalfTime = isHalfTime;
            if (isHalfTime)
            {
                if (_titleLabel != null)
                    _titleLabel.text = "⚔ 中场休息 ⚔";
                if (_startBtn != null)
                    _startBtn.gameObject.SetActive(false);
                if (_halfTimeTimerLabel != null)
                    _halfTimeTimerLabel.gameObject.SetActive(true);
            }
            else
            {
                if (_titleLabel != null)
                    _titleLabel.text = "⚔ 备战界面 ⚔";
                if (_startBtn != null)
                    _startBtn.gameObject.SetActive(true);
                if (_halfTimeTimerLabel != null)
                    _halfTimeTimerLabel.gameObject.SetActive(false);
            }
        }

        private void OnStartMatch()
        {
            Debug.Log("[备战] 开始比赛!");
            gameObject.SetActive(false);
            MatchStartedFromPrep?.Invoke();
        }

        private void OnBackToMenu()
        {
            Debug.Log("[备战] 返回主菜单");
            gameObject.SetActive(false);
            BackToMenuRequested?.Invoke();
        }

        // ===== 食物系统 =====

        /// <summary>刷新食物下拉列表（只显示背包里有的食物）</summary>
        private void RefreshFoodList()
        {
            _foodOption.ClearOptions();
            var opts = new List<string> { "（不吃）" };
            if (InventoryManager.Instance == null)
            {
                _foodOption.AddOptions(opts);
                return;
            }
            var backpack = InventoryManager.Instance.GetBackpackByType("consumable");
            foreach (var item in backpack)
            {
                var foodData = NutritionManager.Instance.GetFood(GetStr(item, "item_id", ""));
                if (foodData == null || foodData.Count == 0) continue;
                var effect = GetDict(foodData, "effect", new Dictionary<string, object>());
                var statNameMap = new Dictionary<string, string>
                {
                    { "stamina", "体力" }, { "defense", "防御" }, { "speed", "速度" },
                    { "attack", "攻击" }, { "resilience", "韧性" }, { "ball_speed", "球速" }
                };
                string statName;
                string stat = GetStr(effect, "stat", "");
                if (!statNameMap.TryGetValue(stat, out statName)) statName = "?";
                string displayText = GetStr(foodData, "name", "") + " +" + GetInt(effect, "value", 0) + "（" + statName + "）";
                opts.Add(displayText);
            }
            _foodOption.AddOptions(opts);

            // 如果已经吃过食物，显示状态
            string activeId = NutritionManager.Instance.GetActiveFoodId();
            if (activeId != "")
            {
                var activeFood = NutritionManager.Instance.GetFood(activeId);
                _foodStatusLabel.text = "已吃: " + GetStr(activeFood, "name", "?");
                _foodStatusLabel.color = new Color(0.3f, 0.9f, 0.3f);
                _foodEatBtn.interactable = false;
                _foodOption.interactable = false;
            }
            else
            {
                _foodStatusLabel.text = "整场只能吃1种1次";
                _foodStatusLabel.color = new Color(0.6f, 0.6f, 0.6f);
                _foodEatBtn.interactable = true;
                _foodOption.interactable = true;
            }
        }

        /// <summary>点击食用按钮</summary>
        private void OnEatFood()
        {
            int selected = _foodOption.value;
            if (selected <= 0)
            {
                _foodStatusLabel.text = "请选择食物";
                _foodStatusLabel.color = new Color(0.9f, 0.5f, 0.3f);
                return;
            }

            // 获取选中的食物ID（从背包列表里取）
            var backpack = InventoryManager.Instance.GetBackpackByType("consumable");
            var foodItems = new List<Dictionary<string, object>>();
            foreach (var item in backpack)
            {
                var foodData = NutritionManager.Instance.GetFood(GetStr(item, "item_id", ""));
                if (foodData != null && foodData.Count > 0)
                    foodItems.Add(item);
            }

            if (selected - 1 >= foodItems.Count) return;

            string foodId = GetStr(foodItems[selected - 1], "item_id", "");
            bool ok = NutritionManager.Instance.ConsumeFood(foodId);
            if (ok)
            {
                var foodData = NutritionManager.Instance.GetFood(foodId);
                _foodStatusLabel.text = "已吃: " + GetStr(foodData, "name", "?");
                _foodStatusLabel.color = new Color(0.3f, 0.9f, 0.3f);
                _foodEatBtn.interactable = false;
                _foodOption.interactable = false;
                Debug.Log("[备战] 已食用: " + GetStr(foodData, "name", ""));
                RefreshFoodList();
            }
            else
            {
                _foodStatusLabel.text = "食用失败（背包不足？）";
                _foodStatusLabel.color = new Color(0.9f, 0.3f, 0.3f);
                RefreshFoodList();
            }
        }

        // ===== 公开方法 =====

        public void SetAiManager(AIManager aiMgr)
        {
            _aiManager = aiMgr;
        }

        public int GetPlayerStrategy()
        {
            return CurrentPlayerStrategy;
        }

        public int GetTeamStrategy()
        {
            return CurrentTeamStrategy;
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

        private void SetupSlider(Slider slider)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.direction = Slider.Direction.LeftToRight;
            // Unity Slider 自带 Handle，可禁用交互
            slider.interactable = false;
            // 创建 Fill 与 Background（如未提供）
            if (slider.fillRect == null)
            {
                var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                var frt = fillGo.GetComponent<RectTransform>();
                frt.SetParent(slider.transform, false);
                frt.anchorMin = Vector2.zero;
                frt.anchorMax = Vector2.one;
                frt.offsetMin = Vector2.zero;
                frt.offsetMax = Vector2.zero;
                fillGo.GetComponent<Image>().color = new Color(0.3f, 0.9f, 0.3f);
                slider.fillRect = frt;
            }
            if (slider.targetGraphic == null)
            {
                var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
                var brt = bgGo.GetComponent<RectTransform>();
                brt.SetParent(slider.transform, false);
                brt.anchorMin = new Vector2(0, 0.5f);
                brt.anchorMax = new Vector2(1, 0.5f);
                brt.pivot = new Vector2(0.5f, 0.5f);
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;
                bgGo.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);
                slider.targetGraphic = bgGo.GetComponent<Image>();
            }
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

        private GameObject NewColorRect(string name, Vector2 pos, Vector2 size, Color color, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos * Pix;
            rt.sizeDelta = size * Pix;
            go.GetComponent<Image>().color = color;
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

        // ===== 内部数据结构类 =====
        private class RoleDisplay
        {
            public string name;
            public Color color;
        }

        private class EquipSlotInfo
        {
            public string name;
            public string icon;
        }

        private class TrainStatInfo
        {
            public string name;
            public Color color;
        }
    }

    // ===== 占位类型：项目需在 BattleBall.Battle 命名空间实现真实类型 =====
    namespace Battle
    {

        /// <summary>AI Profile 占位</summary>
        public class AIProfile
        {
            public string PlayerStrategyName { get; set; } = "";
            public static AIProfile GetRolePreset(string role) { return new AIProfile(); }
            public static void ApplyTeamStrategy(AIProfile profile, string strategy) { }
            public static void ApplyDifficulty(AIProfile profile, string difficulty) { }
        }

        /// <summary>AI 管理器占位</summary>
        public class AIManager : MonoBehaviour
        {
            public void UpdatePlayerProfile(int index, AIProfile profile) { }
        }
    }
}

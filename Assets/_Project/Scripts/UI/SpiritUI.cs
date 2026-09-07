using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using BattleBall.Core;

namespace BattleBall.UI
{
    /// <summary>
    /// 元灵技能系统 UI 面板
    /// 左侧：元灵头像列表（可点击选择）
    /// 右侧：选中元灵详情 + 技能装备槽 + 技能库
    /// 数据源：DataManager.Instance.GetAllSpirits() / GetAllSkills()
    /// 若 DataManager 为空则使用硬编码测试数据（阿五/金刚、岩灵/大地）
    /// </summary>
    public class SpiritUI : MonoBehaviour
    {
        // ============================================================
        // 数据
        // ============================================================
        private List<Dictionary<string, object>> spirits_data = new List<Dictionary<string, object>>();
        private List<Dictionary<string, object>> skills_data = new List<Dictionary<string, object>>();
        private Dictionary<string, object> selected_spirit = new Dictionary<string, object>();
        private int selected_spirit_index = -1;

        // 测试货币
        private int spirit_ore = 20;
        private int spirit_crystal = 30;

        // ============================================================
        // UI 引用
        // ============================================================
        private RectTransform spirit_grid_content;
        private Image detail_avatar;
        private Text detail_name;
        private Text detail_desc;
        private Text level_label;
        private List<Image> active_skill_slots = new List<Image>();
        private Image passive_skill_slot;
        private RectTransform skill_library_content;
        private Text ore_label;
        private Text crystal_label;

        private Font _defaultFont;
        private RectTransform _rect;

        public event System.Action close_requested;

        // ============================================================
        // 生命周期
        // ============================================================
        void Start()
        {
            _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _rect = GetComponent<RectTransform>();
            if (_rect == null) _rect = gameObject.AddComponent<RectTransform>();
            SetFullRect(_rect);

            LoadData();
            BuildUI();
            if (spirits_data.Count > 0)
                SelectSpirit(0);
        }

        // ============================================================
        // 数据加载
        // ============================================================
        private void LoadData()
        {
            var dm = DataManager.Instance;
            if (dm != null)
            {
                spirits_data = dm.GetAllSpirits() ?? new List<Dictionary<string, object>>();
                skills_data = dm.GetAllSkills() ?? new List<Dictionary<string, object>>();
            }
            else
            {
                spirits_data = GetTestSpirits();
                skills_data = GetTestSkills();
            }

            // 数据为空时兜底：确保面板始终有内容可显示
            if (spirits_data == null || spirits_data.Count == 0)
            {
                Debug.LogWarning("[SpiritUI] DataManager 未返回元灵数据，使用测试数据兜底");
                spirits_data = GetTestSpirits();
            }
            if (skills_data == null || skills_data.Count == 0)
            {
                Debug.LogWarning("[SpiritUI] DataManager 未返回技能数据，使用测试数据兜底");
                skills_data = GetTestSkills();
            }

            // 测试数据：每个元灵默认解锁第一个技能并装备到主动1槽
            foreach (var s in spirits_data)
            {
                var skills_arr = GetList(s, "skills");
                string first_skill = skills_arr.Count > 0 ? GetStr(skills_arr[0]) : "";
                if (!s.ContainsKey("unlocked_skills"))
                    s["unlocked_skills"] = first_skill != "" ? new List<string> { first_skill } : new List<string>();
                if (!s.ContainsKey("equipped_actives"))
                    s["equipped_actives"] = first_skill != ""
                        ? new List<string> { first_skill, "", "" }
                        : new List<string> { "", "", "" };
                if (!s.ContainsKey("equipped_passive"))
                    s["equipped_passive"] = "";
            }
            Debug.Log("[SpiritUI] 加载 " + spirits_data.Count + " 元灵, " + skills_data.Count + " 技能");
        }

        /// <summary>重新加载数据并刷新UI（供外部调用，如开发面板保存后）</summary>
        public void RefreshData()
        {
            int prev_index = selected_spirit_index;
            LoadData();
            UpdateSkillLibrary();
            if (spirits_data.Count > 0)
                SelectSpirit(Mathf.Clamp(prev_index, 0, spirits_data.Count - 1));
            Debug.Log("[SpiritUI] 数据已刷新");
        }

        // ============================================================
        // 硬编码测试数据
        // ============================================================
        private List<Dictionary<string, object>> GetTestSpirits()
        {
            return new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    { "id", "spirit_jingang" },
                    { "name", "阿五" },
                    { "element", "金刚" },
                    { "icon_color", "#FFD700" },
                    { "level", 1 },
                    { "max_level", 10 },
                    { "skills", new List<string> { "skill_金刚_1", "skill_金刚_2" } },
                    { "description", "金刚元灵，刚猛无比" }
                },
                new Dictionary<string, object>
                {
                    { "id", "spirit_dadi" },
                    { "name", "岩灵" },
                    { "element", "大地" },
                    { "icon_color", "#8B5A2B" },
                    { "level", 1 },
                    { "max_level", 10 },
                    { "skills", new List<string> { "skill_大地_1", "skill_大地_2" } },
                    { "description", "大地元灵，厚重坚实" }
                }
            };
        }

        private List<Dictionary<string, object>> GetTestSkills()
        {
            return new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    { "id", "skill_金刚_1" },
                    { "name", "金刚重击" },
                    { "element", "金刚" },
                    { "damage", 50 },
                    { "cooldown", 5 },
                    { "description", "造成50点伤害" },
                    { "type", "主动" },
                    { "unlock_cost", 0 }
                },
                new Dictionary<string, object>
                {
                    { "id", "skill_金刚_2" },
                    { "name", "金刚护盾" },
                    { "element", "金刚" },
                    { "damage", 0 },
                    { "cooldown", 8 },
                    { "description", "获得护盾吸收伤害" },
                    { "type", "主动" },
                    { "unlock_cost", 10 }
                },
                new Dictionary<string, object>
                {
                    { "id", "skill_大地_1" },
                    { "name", "岩石投掷" },
                    { "element", "大地" },
                    { "damage", 40 },
                    { "cooldown", 4 },
                    { "description", "投掷岩石造成40点伤害" },
                    { "type", "主动" },
                    { "unlock_cost", 0 }
                },
                new Dictionary<string, object>
                {
                    { "id", "skill_大地_2" },
                    { "name", "大地震击" },
                    { "element", "大地" },
                    { "damage", 70 },
                    { "cooldown", 10 },
                    { "description", "震击地面造成范围伤害" },
                    { "type", "主动" },
                    { "unlock_cost", 15 }
                }
            };
        }

        // ============================================================
        // 构建 UI
        // ============================================================
        private void BuildUI()
        {
            // 全屏背景
            NewColorRect("BG", Vector2.zero, new Vector2(1440, 900), new Color(0.05f, 0.05f, 0.12f, 0.95f), transform);

            // 标题
            var title = NewLabel("Title", new Vector2(600, 10), new Vector2(240, 30), "元灵技能系统", 20, Color.yellow, transform);
            title.alignment = TextAnchor.MiddleCenter;

            // 货币栏
            ore_label = NewLabel("OreLabel", new Vector2(1100, 12), new Vector2(120, 24), "矿石:" + spirit_ore, 14, new Color(0.8f, 0.6f, 0.2f), transform);
            ore_label.alignment = TextAnchor.MiddleLeft;
            crystal_label = NewLabel("CrystalLabel", new Vector2(1230, 12), new Vector2(120, 24), "水晶:" + spirit_crystal, 14, new Color(0.4f, 0.7f, 1.0f), transform);
            crystal_label.alignment = TextAnchor.MiddleLeft;

            // 关闭按钮
            var closeBtn = NewButton("CloseBtn", new Vector2(1340, 10), new Vector2(80, 30), "关闭[X]", 14, Color.white, transform);
            closeBtn.onClick.AddListener(OnClose);

            // 竖直分割线
            NewColorRect("Divider", new Vector2(360, 48), new Vector2(2, 740), new Color(0.4f, 0.4f, 0.5f), transform);

            BuildLeftPanel();
            BuildRightPanel();
        }

        // === 左侧面板 ===
        private void BuildLeftPanel()
        {
            // 左侧背景
            NewColorRect("LeftBG", new Vector2(10, 50), new Vector2(345, 735), new Color(0.08f, 0.08f, 0.15f, 0.9f), transform);

            var leftTitle = NewLabel("LeftTitle", new Vector2(130, 55), new Vector2(100, 24), "元灵列表", 16, new Color(0.7f, 0.8f, 1.0f), transform);
            leftTitle.alignment = TextAnchor.MiddleCenter;

            // 滚动容器
            var scrollGo = NewScrollView("SpiritScroll", new Vector2(20, 80), new Vector2(325, 695), transform);
            var scrollRect = scrollGo.GetComponent<ScrollRect>();

            // 网格内容
            spirit_grid_content = NewGridLayout("SpiritGrid", 4, new Vector2(72, 95), new Vector2(8, 12), scrollRect.content);

            for (int i = 0; i < spirits_data.Count; i++)
            {
                var icon = CreateSpiritIcon(spirits_data[i], i);
                icon.transform.SetParent(spirit_grid_content, false);
            }
        }

        private GameObject CreateSpiritIcon(Dictionary<string, object> spirit, int index)
        {
            var go = new GameObject("Spirit_" + index, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(72, 95);
            go.GetComponent<Image>().color = new Color(0, 0, 0, 0);

            // 圆形头像（带 Button 点击）
            Color iconColor = ParseColor(GetStr(spirit, "icon_color", "#FFFFFF"), Color.white);
            var avatarGo = NewColorRect("Avatar", new Vector2(4, 0), new Vector2(64, 64), iconColor, go.transform);
            var btn = avatarGo.AddComponent<Button>();
            int captured = index;
            btn.onClick.AddListener(() => SelectSpirit(captured));

            // 等级
            var lvl = NewLabel("Lvl", new Vector2(4, 66), new Vector2(64, 14), "Lv" + GetInt(spirit, "level", 1), 10, Color.yellow, go.transform);
            lvl.alignment = TextAnchor.MiddleCenter;

            // 名称
            var nameLabel = NewLabel("Name", new Vector2(4, 80), new Vector2(64, 14), GetStr(spirit, "name", "?"), 12, Color.white, go.transform);
            nameLabel.alignment = TextAnchor.MiddleCenter;

            return go;
        }

        // === 右侧面板 ===
        private void BuildRightPanel()
        {
            // 右侧背景
            NewColorRect("RightBG", new Vector2(370, 50), new Vector2(1060, 735), new Color(0.08f, 0.08f, 0.18f, 0.8f), transform);

            // 头像
            detail_avatar = NewColorRect("DetailAvatar", new Vector2(395, 68), new Vector2(80, 80), Color.yellow, transform).GetComponent<Image>();

            // 名称
            detail_name = NewLabel("DetailName", new Vector2(490, 68), new Vector2(400, 28), "", 20, Color.white, transform);
            detail_name.alignment = TextAnchor.MiddleLeft;

            // 等级
            level_label = NewLabel("LevelLabel", new Vector2(490, 98), new Vector2(200, 22), "", 14, Color.yellow, transform);
            level_label.alignment = TextAnchor.MiddleLeft;

            // 描述
            detail_desc = NewLabel("DetailDesc", new Vector2(490, 122), new Vector2(550, 22), "", 12, new Color(0.6f, 0.6f, 0.6f), transform);
            detail_desc.alignment = TextAnchor.UpperLeft;
            detail_desc.horizontalOverflow = HorizontalWrapMode.Wrap;

            // 上场技能标题
            var skillTitle = NewLabel("SkillTitle", new Vector2(395, 160), new Vector2(120, 22), "上场技能", 14, new Color(0.8f, 0.9f, 1.0f), transform);
            skillTitle.alignment = TextAnchor.MiddleLeft;

            // 3个主动技能槽
            float slotX = 395f;
            for (int i = 0; i < 3; i++)
            {
                var slot = NewColorRect("ActiveSlot_" + i, new Vector2(slotX + i * 72, 183), new Vector2(64, 64), new Color(0.15f, 0.15f, 0.25f), transform).GetComponent<Image>();
                active_skill_slots.Add(slot);

                var tag = NewLabel("Tag_" + i, new Vector2(0, 48), new Vector2(64, 14), "主动" + (i + 1), 10, new Color(0.5f, 0.5f, 0.5f), slot.transform);
                tag.alignment = TextAnchor.MiddleCenter;
            }

            // 被动技能槽
            passive_skill_slot = NewColorRect("PassiveSlot", new Vector2(slotX + 216, 183), new Vector2(64, 64), new Color(0.15f, 0.12f, 0.2f), transform).GetComponent<Image>();
            var ptag = NewLabel("PTag", new Vector2(0, 48), new Vector2(64, 14), "被动", 10, new Color(0.5f, 0.5f, 0.5f), passive_skill_slot.transform);
            ptag.alignment = TextAnchor.MiddleCenter;

            // 三个按钮
            float btnY = 260f;
            var btnEdit = NewButton("BtnEdit", new Vector2(395, btnY), new Vector2(130, 32), "修改上场技能", 13, Color.white, transform);
            btnEdit.onClick.AddListener(OnEditSkills);

            var btnConfirm = NewButton("BtnConfirm", new Vector2(535, btnY), new Vector2(80, 32), "确认", 13, Color.white, transform);
            btnConfirm.onClick.AddListener(OnConfirm);

            var btnUpgrade = NewButton("BtnUpgrade", new Vector2(625, btnY), new Vector2(110, 32), "升级元灵", 13, Color.white, transform);
            btnUpgrade.onClick.AddListener(OnUpgradeSpirit);

            // 分隔线
            NewColorRect("SepLine", new Vector2(395, 305), new Vector2(1000, 1), new Color(0.4f, 0.4f, 0.5f), transform);

            // 技能库标题
            var libTitle = NewLabel("LibTitle", new Vector2(395, 312), new Vector2(120, 24), "技能库", 16, new Color(0.9f, 0.85f, 0.7f), transform);
            libTitle.alignment = TextAnchor.MiddleLeft;

            // 技能库滚动区域
            var libScrollGo = NewScrollView("LibScroll", new Vector2(395, 338), new Vector2(1000, 440), transform);
            var libScrollRect = libScrollGo.GetComponent<ScrollRect>();
            skill_library_content = NewVerticalLayout("LibContent", libScrollRect.content);
        }

        // ============================================================
        // 选中元灵 / 刷新
        // ============================================================
        public void SelectSpirit(int index)
        {
            if (index < 0 || index >= spirits_data.Count) return;
            selected_spirit_index = index;
            selected_spirit = spirits_data[index];

            Color iconColor = ParseColor(GetStr(selected_spirit, "icon_color", "#FFFFFF"), Color.white);
            detail_avatar.color = iconColor;

            detail_name.text = GetStr(selected_spirit, "name", "?") + "（" + GetStr(selected_spirit, "element", "") + "）";
            level_label.text = "等级 " + GetInt(selected_spirit, "level", 1) + " / " + GetInt(selected_spirit, "max_level", 10);
            detail_desc.text = GetStr(selected_spirit, "description", "");

            UpdateEquippedSlots();
            UpdateSkillLibrary();
            Debug.Log("[SpiritUI] 选中: " + GetStr(selected_spirit, "name", "?") + "(" + GetStr(selected_spirit, "element", "") + ")");
        }

        private void UpdateEquippedSlots()
        {
            var equippedActives = GetStringList(selected_spirit, "equipped_actives");
            if (equippedActives.Count == 0) equippedActives = new List<string> { "", "", "" };
            string equippedPassive = GetStr(selected_spirit, "equipped_passive", "");

            for (int i = 0; i < 3; i++)
            {
                ClearPanel(active_skill_slots[i]);
                active_skill_slots[i].color = new Color(0.15f, 0.15f, 0.25f);
                if (i < equippedActives.Count && equippedActives[i] != "")
                {
                    var skill = GetSkillById(equippedActives[i]);
                    if (skill.Count > 0)
                        FillSkillSlot(active_skill_slots[i], skill);
                }
                var tag = NewLabel("Tag_" + i, new Vector2(0, 48), new Vector2(64, 14), "主动" + (i + 1), 10, new Color(0.5f, 0.5f, 0.5f), active_skill_slots[i].transform);
                tag.alignment = TextAnchor.MiddleCenter;
            }

            ClearPanel(passive_skill_slot);
            passive_skill_slot.color = new Color(0.15f, 0.12f, 0.2f);
            if (equippedPassive != "")
            {
                var skill = GetSkillById(equippedPassive);
                if (skill.Count > 0)
                    FillSkillSlot(passive_skill_slot, skill);
            }
            var ptag = NewLabel("PTag", new Vector2(0, 48), new Vector2(64, 14), "被动", 10, new Color(0.5f, 0.5f, 0.5f), passive_skill_slot.transform);
            ptag.alignment = TextAnchor.MiddleCenter;
        }

        private void FillSkillSlot(Image slot, Dictionary<string, object> skill)
        {
            Color iconColor = GetSkillIconColor(skill);
            slot.color = iconColor;

            string skillName = GetStr(skill, "name", "");
            string abbr = skillName.Length >= 2 ? skillName.Substring(0, 2) : skillName;
            var abbrLabel = NewLabel("Abbr", new Vector2(0, 20), new Vector2(64, 22), abbr, 16, Color.white, slot.transform);
            abbrLabel.alignment = TextAnchor.MiddleCenter;

            // 右键查看技能详情
            AddRightClickHandler(slot.gameObject, () => ShowSkillPopup(skill, true, ""));
        }

        private void UpdateSkillLibrary()
        {
            if (skill_library_content == null) return;
            for (int i = skill_library_content.childCount - 1; i >= 0; i--)
                Destroy(skill_library_content.GetChild(i).gameObject);

            var spiritSkills = GetList(selected_spirit, "skills");
            var unlocked = GetList(selected_spirit, "unlocked_skills");

            foreach (var sid in spiritSkills)
            {
                string skillId = GetStr(sid);
                var skill = GetSkillById(skillId);
                if (skill.Count == 0) continue;

                bool isUnlocked = false;
                foreach (var u in unlocked)
                {
                    if (GetStr(u) == skillId) { isUnlocked = true; break; }
                }

                var entry = CreateLibraryEntry(skill, isUnlocked, skillId);
                entry.transform.SetParent(skill_library_content, false);
            }
        }

        private GameObject CreateLibraryEntry(Dictionary<string, object> skill, bool isUnlocked, string skillId)
        {
            var row = new GameObject("LibEntry_" + skillId, typeof(RectTransform), typeof(Image));
            var rt = row.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(980, 65);
            row.GetComponent<Image>().color = new Color(0, 0, 0, 0);

            // 方形图标
            Color iconColor = GetSkillIconColor(skill);
            var iconGo = NewColorRect("Icon", new Vector2(0, 5), new Vector2(52, 52), isUnlocked ? iconColor : new Color(0.2f, 0.2f, 0.2f), row.transform);

            if (!isUnlocked)
            {
                var lockLabel = NewLabel("Lock", new Vector2(0, 14), new Vector2(52, 24), "\U0001F512", 18, Color.white, iconGo.transform);
                lockLabel.alignment = TextAnchor.MiddleCenter;
            }

            // 右侧信息
            float infoX = 62f;

            var nameLabel = NewLabel("Name", new Vector2(infoX, 5), new Vector2(900, 18),
                GetStr(skill, "name", "") + "  [" + GetStr(skill, "type", "") + "]",
                13, isUnlocked ? Color.white : new Color(0.5f, 0.5f, 0.5f), row.transform);
            nameLabel.alignment = TextAnchor.UpperLeft;

            var descLabel = NewLabel("Desc", new Vector2(infoX, 24), new Vector2(900, 18),
                GetStr(skill, "description", ""), 11, new Color(0.6f, 0.6f, 0.6f), row.transform);
            descLabel.alignment = TextAnchor.UpperLeft;
            descLabel.horizontalOverflow = HorizontalWrapMode.Wrap;

            if (!isUnlocked)
            {
                var costLabel = NewLabel("Cost", new Vector2(infoX, 43), new Vector2(900, 18),
                    "解锁: " + GetInt(skill, "unlock_cost", 0) + " 元灵水晶", 11, new Color(0.4f, 0.7f, 1.0f), row.transform);
                costLabel.alignment = TextAnchor.UpperLeft;
            }

            // 覆盖整行的透明按钮，接收右键
            var overlayGo = new GameObject("ClickOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
            var overlayRt = overlayGo.GetComponent<RectTransform>();
            overlayRt.SetParent(row.transform, false);
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.offsetMin = Vector2.zero;
            overlayRt.offsetMax = Vector2.zero;
            overlayGo.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            var capturedSkill = skill;
            bool capturedUnlocked = isUnlocked;
            string capturedId = skillId;
            AddRightClickHandler(overlayGo, () => ShowSkillPopup(capturedSkill, capturedUnlocked, capturedId));

            return row;
        }

        // ============================================================
        // 技能详情弹窗
        // ============================================================
        private void ShowSkillPopup(Dictionary<string, object> skill, bool isUnlocked, string skillId)
        {
            var old = transform.Find("SkillPopup");
            if (old != null) Destroy(old.gameObject);

            var popupGo = NewColorRect("SkillPopup", new Vector2(460, 280), new Vector2(520, 340), new Color(0.1f, 0.1f, 0.22f, 0.98f), transform);
            var popupTransform = popupGo.transform;

            // 技能名
            Color nameColor = ParseColor(GetStr(skill, "icon_color", "#FFF"), Color.white);
            var title = NewLabel("PTitle", new Vector2(20, 15), new Vector2(480, 28), GetStr(skill, "name", ""), 18, nameColor, popupTransform);
            title.alignment = TextAnchor.MiddleLeft;

            // 类型+元素
            var typeLabel = NewLabel("PType", new Vector2(20, 45), new Vector2(480, 20),
                "[" + GetStr(skill, "type", "") + "]  元素: " + GetStr(skill, "element", "?"),
                12, new Color(0.6f, 0.6f, 0.6f), popupTransform);
            typeLabel.alignment = TextAnchor.MiddleLeft;

            // 描述
            var desc = NewLabel("PDesc", new Vector2(20, 68), new Vector2(480, 40), GetStr(skill, "description", ""), 13, Color.white, popupTransform);
            desc.alignment = TextAnchor.UpperLeft;
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;

            // 详细效果（伤害/冷却）
            string detail = "伤害: " + GetInt(skill, "damage", 0) + "   冷却: " + GetInt(skill, "cooldown", 0) + " 回合";
            var detailLabel = NewLabel("PDetail", new Vector2(20, 112), new Vector2(480, 120), detail, 12, new Color(0.6f, 0.85f, 0.6f), popupTransform);
            detailLabel.alignment = TextAnchor.UpperLeft;
            detailLabel.horizontalOverflow = HorizontalWrapMode.Wrap;

            if (!isUnlocked)
            {
                int cost = GetInt(skill, "unlock_cost", 0);
                var unlockBtn = NewButton("PUnlockBtn", new Vector2(150, 240), new Vector2(220, 36), "解锁（" + cost + " 元灵水晶）", 14, Color.white, popupTransform);
                unlockBtn.interactable = spirit_crystal >= cost;
                string capturedId = skillId;
                int capturedCost = cost;
                unlockBtn.onClick.AddListener(() => OnUnlockSkill(capturedId, capturedCost));

                if (spirit_crystal < cost)
                {
                    var warn = NewLabel("PWarn", new Vector2(0, 282), new Vector2(520, 20), "水晶不足！", 12, Color.red, popupTransform);
                    warn.alignment = TextAnchor.MiddleCenter;
                }
            }
            else
            {
                var owned = NewLabel("POwned", new Vector2(0, 245), new Vector2(520, 24), "\u2705 已解锁", 14, Color.green, popupTransform);
                owned.alignment = TextAnchor.MiddleCenter;
            }

            // 关闭按钮
            var closeBtn = NewButton("PCloseBtn", new Vector2(220, 295), new Vector2(80, 28), "关闭", 13, Color.white, popupTransform);
            var capturedPopup = popupGo;
            closeBtn.onClick.AddListener(() => { if (capturedPopup != null) Destroy(capturedPopup); });
        }

        // ============================================================
        // 按钮回调
        // ============================================================
        private void OnUnlockSkill(string skillId, int cost)
        {
            if (spirit_crystal < cost || selected_spirit_index < 0) return;
            spirit_crystal -= cost;
            crystal_label.text = "水晶:" + spirit_crystal;

            var unlocked = GetList(selected_spirit, "unlocked_skills");
            bool found = false;
            foreach (var u in unlocked)
            {
                if (GetStr(u) == skillId) { found = true; break; }
            }
            if (!found) unlocked.Add(skillId);
            selected_spirit["unlocked_skills"] = unlocked;

            UpdateSkillLibrary();
            var popup = transform.Find("SkillPopup");
            if (popup != null) Destroy(popup.gameObject);
            Debug.Log("[SpiritUI] 解锁: " + skillId + " (消耗" + cost + "水晶)");
        }

        private void OnEditSkills()
        {
            Debug.Log("[SpiritUI] 修改上场技能 - 待实现");
        }

        private void OnConfirm()
        {
            Debug.Log("[SpiritUI] 确认配置: " + GetStr(selected_spirit, "name", "?"));
        }

        private void OnUpgradeSpirit()
        {
            if (selected_spirit_index < 0) return;
            int level = GetInt(selected_spirit, "level", 1);
            int maxLevel = GetInt(selected_spirit, "max_level", 10);
            if (level >= maxLevel)
            {
                Debug.Log("[SpiritUI] 已满级!");
                return;
            }
            int cost = level * 2;
            if (spirit_ore < cost)
            {
                Debug.Log("[SpiritUI] 矿石不足! 需要" + cost);
                return;
            }
            spirit_ore -= cost;
            selected_spirit["level"] = level + 1;
            ore_label.text = "矿石:" + spirit_ore;
            level_label.text = "等级 " + (level + 1) + " / " + maxLevel;
            Debug.Log("[SpiritUI] " + GetStr(selected_spirit, "name", "?") + " 升级 Lv" + (level + 1) + " (消耗" + cost + "矿石)");
        }

        private void OnClose()
        {
            close_requested?.Invoke();
            gameObject.SetActive(false);
        }

        // ============================================================
        // 辅助方法
        // ============================================================
        private Dictionary<string, object> GetSkillById(string skillId)
        {
            foreach (var s in skills_data)
            {
                if (GetStr(s, "id", "") == skillId) return s;
            }
            return new Dictionary<string, object>();
        }

        /// <summary>技能色块颜色：优先 icon_color，为白色/空时用元素色兜底</summary>
        private Color GetSkillIconColor(Dictionary<string, object> skill)
        {
            string colorStr = GetStr(skill, "icon_color", "#FFFFFF");
            if (colorStr != "" && colorStr != "#FFFFFF")
                return ParseColor(colorStr, Color.gray);
            return GetElementColor(GetStr(skill, "element", ""));
        }

        /// <summary>元素颜色映射</summary>
        private Color GetElementColor(string element)
        {
            switch (element)
            {
                case "金刚": return new Color(0.85f, 0.75f, 0.3f);
                case "大地": return new Color(0.7f, 0.55f, 0.35f);
                case "雷火": return new Color(1.0f, 0.4f, 0.2f);
                case "冰雪": return new Color(0.4f, 0.8f, 1.0f);
                case "草木": return new Color(0.3f, 0.8f, 0.3f);
                case "梦幻": return new Color(0.7f, 0.5f, 0.9f);
                default: return new Color(0.6f, 0.6f, 0.6f);
            }
        }

        private void ClearPanel(Image panel)
        {
            if (panel == null) return;
            for (int i = panel.transform.childCount - 1; i >= 0; i--)
                Destroy(panel.transform.GetChild(i).gameObject);
            var trigger = panel.GetComponent<EventTrigger>();
            if (trigger != null) Destroy(trigger);
        }

        /// <summary>给 GameObject 添加右键点击回调</summary>
        private void AddRightClickHandler(GameObject go, System.Action callback)
        {
            var trigger = go.GetComponent<EventTrigger>();
            if (trigger == null) trigger = go.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener((data) =>
            {
                var ped = data as PointerEventData;
                if (ped != null && ped.button == PointerEventData.InputButton.Right)
                    callback?.Invoke();
            });
            trigger.triggers.Add(entry);
        }

        // ============================================================
        // Dict 取值辅助
        // ============================================================
        private static string GetStr(Dictionary<string, object> d, string key, string def)
        {
            if (d == null) return def;
            object v;
            return d.TryGetValue(key, out v) && v != null ? v.ToString() : def;
        }

        private static string GetStr(object o)
        {
            return o == null ? "" : o.ToString();
        }

        private static int GetInt(Dictionary<string, object> d, string key, int def)
        {
            if (d == null) return def;
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return def;
            if (v is int i) return i;
            if (v is long l) return (int)l;
            int r;
            return int.TryParse(v.ToString(), out r) ? r : def;
        }

        private static List<object> GetList(Dictionary<string, object> d, string key)
        {
            var result = new List<object>();
            if (d == null) return result;
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return result;
            if (v is IList<object> l) return new List<object>(l);
            if (v is System.Collections.IList il)
            {
                foreach (var item in il) result.Add(item);
            }
            return result;
        }

        private static List<string> GetStringList(Dictionary<string, object> d, string key)
        {
            var result = new List<string>();
            var list = GetList(d, key);
            foreach (var v in list) result.Add(GetStr(v));
            return result;
        }

        private static Color ParseColor(string html, Color fallback)
        {
            if (string.IsNullOrEmpty(html)) return fallback;
            if (ColorUtility.TryParseHtmlString(html, out var c)) return c;
            return fallback;
        }

        // ============================================================
        // UI 工厂方法（项目约定：左上角坐标系）
        // ============================================================
        private void SetFullRect(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 1f);
        }

        private GameObject NewColorRect(string name, Vector2 pos, Vector2 size, Color color, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(pos.x, -pos.y);
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return go;
        }

        private Text NewLabel(string name, Vector2 pos, Vector2 size, string text, int fontSize, Color color, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(pos.x, -pos.y);
            rt.sizeDelta = size;
            var lbl = go.GetComponent<Text>();
            lbl.text = text;
            lbl.font = _defaultFont;
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
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(pos.x, -pos.y);
            rt.sizeDelta = size;
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
            lbl.font = _defaultFont;
            lbl.color = color;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.horizontalOverflow = HorizontalWrapMode.Overflow;
            lbl.verticalOverflow = VerticalWrapMode.Overflow;
            return go.GetComponent<Button>();
        }

        private GameObject NewScrollView(string name, Vector2 pos, Vector2 size, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(pos.x, -pos.y);
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0, 0, 0, 0);

            var sr = go.GetComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;

            // Content
            var content = new GameObject("Content", typeof(RectTransform));
            var crt = content.GetComponent<RectTransform>();
            crt.SetParent(rt, false);
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0f, 1f);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            sr.content = crt;

            return go;
        }

        private RectTransform NewGridLayout(string name, int columns, Vector2 cellSize, Vector2 spacing, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = Vector2.zero;

            var gl = go.GetComponent<GridLayoutGroup>();
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = columns;
            gl.cellSize = cellSize;
            gl.spacing = spacing;
            gl.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gl.startAxis = GridLayoutGroup.Axis.Horizontal;
            gl.childAlignment = TextAnchor.UpperLeft;

            var csf = go.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            return rt;
        }

        private RectTransform NewVerticalLayout(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperLeft;

            var csf = go.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            return rt;
        }
    }
}

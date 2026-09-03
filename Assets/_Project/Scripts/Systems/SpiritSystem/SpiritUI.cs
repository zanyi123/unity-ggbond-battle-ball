// ================================================================
// 决竞球 Godot→Unity 翻译: SpiritUI.cs [BattleBall.Systems.SpiritSystem]
// 源: E:/项目储存/决竞球battle-ball/scripts/systems/spirit_system/spirit_ui.gd
// 集成备注:
//   1) extends Control → MonoBehaviour (挂在 Canvas 下的 UI 节点)
//   2) Godot Control UI 组件 → Unity uGUI (UnityEngine.UI)
//   3) 位置/尺寸用 RectTransform.anchoredPosition / sizeDelta (像素单位, 与 Canvas 一致)
//   4) signal close_requested → public event System.Action close_requested
//   5) add_theme_*_override → Text.fontSize / Text.color / LayoutGroup.spacing
//   6) gui_input → IPointerClickHandler (Button.onClick / 自定义 Pointer 事件)
//   7) StyleBoxFlat → Image.sprite + 颜色 (圆角用 Sprite 或 简化为纯色)
//   8) JSON.parse → JsonUtility (但 Dictionary 结构需手动遍历)
//   9) DataManager 是 Singleton (Autoload) → DataManager.Instance
// ================================================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using System;
using BattleBall.Core;

namespace BattleBall.Systems.SpiritSystem
{
    public class SpiritUI : MonoBehaviour, IPointerClickHandler
    {
        // === 数据 ===
        public List<Dictionary<string, object>> spirits_data = new List<Dictionary<string, object>>();
        public List<Dictionary<string, object>> skills_data = new List<Dictionary<string, object>>();
        public Dictionary<string, object> selected_spirit = new Dictionary<string, object>();
        public int selected_spirit_index = -1;

        // 测试货币
        public int spirit_ore = 20;
        public int spirit_crystal = 30;

        // === UI 引用 ===
        protected ScrollRect spirit_scroll;
        protected GridLayoutGroup spirit_grid;
        protected Image detail_avatar;
        protected Text detail_name;
        protected Text detail_desc;
        protected Text level_label;
        protected List<Image> active_skill_slots = new List<Image>();
        protected Image passive_skill_slot;
        protected VerticalLayoutGroup skill_library_container;
        protected Text ore_label;
        protected Text crystal_label;

        // 弹窗中技能详情的右键回调
        protected List<Image> _popup_slots = new List<Image>();

        public event System.Action close_requested;

        // ===== 数值/类型转换辅助 =====
        protected static float _F(object o) { if (o == null) return 0f; float r; return float.TryParse(o.ToString(), out r) ? r : 0f; }
        protected static int _I(object o) { if (o == null) return 0; int r; return int.TryParse(o.ToString(), out r) ? r : 0; }
        protected static string _S(object o) { return o == null ? "" : o.ToString(); }
        protected static bool _B(object o) { if (o == null) return false; if (o is bool b) return b; bool r; return bool.TryParse(o.ToString(), out r) ? r : false; }
        protected static Dictionary<string, object> _Dict(object o) {
            if (o is IDictionary<string, object> d) return new Dictionary<string, object>(d);
            return new Dictionary<string, object>();
        }
        protected static List<object> _List(object o) {
            if (o is IList<object> l) return new List<object>(l);
            return new List<object>();
        }

        protected virtual void Start()
        {
            _set_full_rect(transform as RectTransform);
            (transform as RectTransform).sizeDelta = new Vector2(1440, 900);
            _load_data();
            _build_ui();
            if (spirits_data.Count > 0)
                _select_spirit(0);
        }

        // ============================================================
        // 数据加载
        // ============================================================

        protected virtual void _load_data()
        {
            // 统一数据源: 走 DataManager (Autoload), 确保与开发面板保存的数据一致
            var dm = DataManager.Instance;
            if (dm != null)
            {
                dm.load_all_data();
                spirits_data = dm.spirits != null ? new List<Dictionary<string, object>>(dm.spirits) : new List<Dictionary<string, object>>();
                skills_data = dm.skills != null ? new List<Dictionary<string, object>>(dm.skills) : new List<Dictionary<string, object>>();
            }
            else
            {
                // 兜底: 直接读文件 (不应发生, DataManager 是 Autoload)
                var f1 = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "data/spirits/spirits.json"));
                var j1 = _parse_json(f1);
                if (j1.TryGetValue("spirits", out var sp)) spirits_data = new List<Dictionary<string, object>>(_List(sp).ConvertAll(x => _Dict(x)));
                var f2 = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "data/spirits/skills.json"));
                var j2 = _parse_json(f2);
                if (j2.TryGetValue("skills", out var sk)) skills_data = new List<Dictionary<string, object>>(_List(sk).ConvertAll(x => _Dict(x)));
            }

            // 测试数据: 每个元灵默认解锁第一个技能
            foreach (var s in spirits_data)
            {
                var skills_arr = new List<object>();
                if (s.TryGetValue("skills", out var sa)) skills_arr = _List(sa);
                string first_skill = skills_arr.Count > 0 ? _S(skills_arr[0]) : "";
                if (!s.ContainsKey("unlocked_skills"))
                    s["unlocked_skills"] = first_skill != "" ? new List<string> { first_skill } : new List<string>();
                if (!s.ContainsKey("equipped_actives"))
                    s["equipped_actives"] = first_skill != "" ? new List<string> { first_skill, "", "" } : new List<string> { "", "", "" };
                if (!s.ContainsKey("equipped_passive"))
                    s["equipped_passive"] = "";
            }
            Debug.Log("[SpiritUI] 加载 " + spirits_data.Count + " 元灵, " + skills_data.Count + " 技能");
        }

        // 重新加载数据并刷新UI (供外部调用, 如开发面板保存后)
        public virtual void refresh_data()
        {
            int prev_index = selected_spirit_index;
            _load_data();
            _update_skill_library();
            // 恢复选中状态 (越界保护)
            if (spirits_data.Count > 0)
                _select_spirit(Mathf.Clamp(prev_index, 0, spirits_data.Count - 1));
            Debug.Log("[SpiritUI] 数据已刷新");
        }

        // ============================================================
        // 构建 UI
        // ============================================================

        protected virtual void _build_ui()
        {
            // 全屏背景
            var bg = _create_color_rect(new Color(0.05f, 0.05f, 0.12f, 0.95f), "BG");
            _set_full_rect(bg.rectTransform);
            _add_child(bg.transform);

            // 标题
            var title = _create_label("元灵技能系统", 20, Color.yellow, "Title");
            _set_pos_size(title.rectTransform, new Vector2(600, 10), new Vector2(240, 30));
            title.alignment = TextAnchor.MiddleCenter;
            _add_child(title.transform);

            // 货币
            var cur_bar = _create_hbox("CurBar");
            _set_pos(cur_bar.GetComponent<RectTransform>(), new Vector2(1100, 12));
            _add_child(cur_bar.transform);

            ore_label = _create_label("矿石:" + spirit_ore, 14, new Color(0.8f, 0.6f, 0.2f), "OreLabel");
            cur_bar.transform.Find("OreLabel").SetParent(cur_bar.transform, false);

            var sep = _create_label("   ", 14, Color.white, "Sep");
            sep.transform.SetParent(cur_bar.transform, false);

            crystal_label = _create_label("水晶:" + spirit_crystal, 14, new Color(0.4f, 0.7f, 1.0f), "CrystalLabel");
            crystal_label.transform.SetParent(cur_bar.transform, false);

            // 关闭
            var close_btn = _create_button("关闭[X]", "CloseBtn");
            _set_pos_size(close_btn.GetComponent<RectTransform>(), new Vector2(1340, 10), new Vector2(80, 30));
            close_btn.onClick.AddListener(() => { close_requested?.Invoke(); });
            _add_child(close_btn.transform);

            // 竖直分割线
            var divider = _create_color_rect(new Color(0.4f, 0.4f, 0.5f), "Divider");
            _set_pos_size(divider.rectTransform, new Vector2(360, 48), new Vector2(2, 740));
            _add_child(divider.transform);

            _build_left_panel();
            _build_right_panel();
        }

        // === 左侧面板 ===

        protected virtual void _build_left_panel()
        {
            var left_bg = _create_panel(new Color(0.08f, 0.08f, 0.15f, 0.9f), "LeftBG");
            _set_pos_size(left_bg.rectTransform, new Vector2(10, 50), new Vector2(345, 735));
            _add_child(left_bg.transform);

            var left_title = _create_label("元灵列表", 16, new Color(0.7f, 0.8f, 1.0f), "LeftTitle");
            _set_pos(left_title.rectTransform, new Vector2(130, 55));
            _add_child(left_title.transform);

            // 滚动容器
            var scroll_go = new GameObject("SpiritScroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scroll_go.transform.SetParent(transform, false);
            spirit_scroll = scroll_go.GetComponent<ScrollRect>();
            _set_pos_size(scroll_go.GetComponent<RectTransform>(), new Vector2(20, 80), new Vector2(325, 695));
            spirit_scroll.horizontal = false;

            // Content (GridLayoutGroup)
            var grid_go = new GameObject("SpiritGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            grid_go.transform.SetParent(scroll_go.transform, false);
            spirit_grid = grid_go.GetComponent<GridLayoutGroup>();
            spirit_grid.cellSize = new Vector2(72, 95);
            spirit_grid.spacing = new Vector2(8, 12);
            spirit_grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            spirit_grid.constraintCount = 4;
            spirit_scroll.content = grid_go.GetComponent<RectTransform>();

            for (int i = 0; i < spirits_data.Count; i++)
            {
                var icon = _create_spirit_icon(spirits_data[i], i);
                icon.transform.SetParent(grid_go.transform, false);
            }
        }

        protected virtual VerticalLayoutGroup _create_spirit_icon(Dictionary<string, object> spirit, int index)
        {
            // 创建单个元灵圆形头像 (用 VBox 保证 GridContainer 正确排列)
            var vbox_go = new GameObject("Spirit_" + index, typeof(RectTransform), typeof(VerticalLayoutGroup));
            var vbox = vbox_go.GetComponent<VerticalLayoutGroup>();
            vbox.childAlignment = TextAnchor.MiddleCenter;
            vbox.spacing = 2;
            vbox.childControlWidth = false;
            vbox.childControlHeight = false;
            vbox.childForceExpandWidth = false;
            vbox.childForceExpandHeight = false;
            (vbox_go.transform as RectTransform).sizeDelta = new Vector2(72, 95);

            // 圆形头像 (Image + 圆角)
            Color icon_color = Color.white;
            string color_str = _S(spirit.TryGetValue("icon_color", out var ic) ? ic : "#FFFFFF");
            if (!ColorUtility.TryParseHtmlString(color_str != "" ? color_str : "#FFFFFF", out icon_color)) icon_color = Color.white;

            var avatar_go = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
            var avatar = avatar_go.GetComponent<Image>();
            avatar.color = icon_color;
            (avatar_go.transform as RectTransform).sizeDelta = new Vector2(64, 64);
            avatar_go.transform.SetParent(vbox_go.transform, false);

            // 右键/左键点击事件: 用 EventTrigger
            var trigger = avatar_go.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            int captured_index = index;
            entry.callback.AddListener((data) => { _on_icon_input(data, captured_index); });
            trigger.triggers.Add(entry);

            // 等级
            var lvl = _create_label("Lv" + _I(spirit.TryGetValue("level", out var lv) ? lv : 1), 10, Color.yellow, "Lvl");
            lvl.alignment = TextAnchor.MiddleCenter;
            lvl.transform.SetParent(vbox_go.transform, false);

            // 名称
            var name_label = _create_label(_S(spirit.TryGetValue("name", out var nm) ? nm : "?"), 12, Color.white, "Name");
            name_label.alignment = TextAnchor.MiddleCenter;
            name_label.transform.SetParent(vbox_go.transform, false);

            return vbox;
        }

        protected virtual void _on_icon_input(BaseEventData data, int index)
        {
            // Godot: if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT
            var ped = data as PointerEventData;
            if (ped != null && ped.button == PointerEventData.InputButton.Left)
                _select_spirit(index);
        }

        // === 右侧面板 ===

        protected virtual void _build_right_panel()
        {
            // 右侧背景
            var right_bg = _create_panel(new Color(0.08f, 0.08f, 0.18f, 0.8f), "RightBG");
            _set_pos_size(right_bg.rectTransform, new Vector2(370, 50), new Vector2(1060, 735));
            _add_child(right_bg.transform);

            // --- 头像 (圆形) ---
            detail_avatar = _create_panel(Color.yellow, "DetailAvatar");
            _set_pos_size(detail_avatar.rectTransform, new Vector2(395, 68), new Vector2(80, 80));
            _add_child(detail_avatar.transform);

            // --- 名称 ---
            detail_name = _create_label("", 20, Color.white, "DetailName");
            _set_pos_size(detail_name.rectTransform, new Vector2(490, 68), new Vector2(400, 28));
            _add_child(detail_name.transform);

            // --- 等级 ---
            level_label = _create_label("", 14, Color.yellow, "LevelLabel");
            _set_pos_size(level_label.rectTransform, new Vector2(490, 98), new Vector2(200, 22));
            _add_child(level_label.transform);

            // --- 描述 ---
            detail_desc = _create_label("", 12, new Color(0.6f, 0.6f, 0.6f), "DetailDesc");
            _set_pos_size(detail_desc.rectTransform, new Vector2(490, 122), new Vector2(550, 22));
            detail_desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            _add_child(detail_desc.transform);

            // --- 上场技能标签 ---
            var skill_title = _create_label("上场技能", 14, new Color(0.8f, 0.9f, 1.0f), "SkillTitle");
            _set_pos(skill_title.rectTransform, new Vector2(395, 160));
            _add_child(skill_title.transform);

            // 3个主动技能槽 (方形)
            float slot_x = 395.0f;
            for (int i = 0; i < 3; i++)
            {
                var slot = _create_panel(new Color(0.15f, 0.15f, 0.25f), "ActiveSlot_" + i);
                _set_pos_size(slot.rectTransform, new Vector2(slot_x + i * 72, 183), new Vector2(64, 64));
                active_skill_slots.Add(slot);
                _add_child(slot.transform);

                // 主动标签
                var tag = _create_label("主动" + (i + 1), 10, new Color(0.5f, 0.5f, 0.5f), "Tag_" + i);
                _set_pos(tag.rectTransform, new Vector2(14, 48));
                tag.transform.SetParent(slot.transform, false);
            }

            // 被动技能槽
            passive_skill_slot = _create_panel(new Color(0.15f, 0.12f, 0.2f), "PassiveSlot");
            _set_pos_size(passive_skill_slot.rectTransform, new Vector2(slot_x + 216, 183), new Vector2(64, 64));
            _add_child(passive_skill_slot.transform);
            var ptag = _create_label("被动", 10, new Color(0.5f, 0.5f, 0.5f), "PTag");
            _set_pos(ptag.rectTransform, new Vector2(18, 48));
            ptag.transform.SetParent(passive_skill_slot.transform, false);

            // --- 3个按钮 ---
            float btn_y = 260;
            var btn_edit = _create_button("修改上场技能", "BtnEdit");
            _set_pos_size(btn_edit.GetComponent<RectTransform>(), new Vector2(395, btn_y), new Vector2(130, 32));
            btn_edit.GetComponentInChildren<Text>().fontSize = 13;
            btn_edit.onClick.AddListener(() => _on_edit_skills());
            _add_child(btn_edit.transform);

            var btn_confirm = _create_button("确认", "BtnConfirm");
            _set_pos_size(btn_confirm.GetComponent<RectTransform>(), new Vector2(535, btn_y), new Vector2(80, 32));
            btn_confirm.GetComponentInChildren<Text>().fontSize = 13;
            btn_confirm.onClick.AddListener(() => _on_confirm());
            _add_child(btn_confirm.transform);

            var btn_upgrade = _create_button("升级元灵", "BtnUpgrade");
            _set_pos_size(btn_upgrade.GetComponent<RectTransform>(), new Vector2(625, btn_y), new Vector2(110, 32));
            btn_upgrade.GetComponentInChildren<Text>().fontSize = 13;
            btn_upgrade.onClick.AddListener(() => _on_upgrade_spirit());
            _add_child(btn_upgrade.transform);

            // --- 分隔线 ---
            var sepline = _create_color_rect(new Color(0.4f, 0.4f, 0.5f), "SepLine");
            _set_pos_size(sepline.rectTransform, new Vector2(395, 305), new Vector2(1000, 1));
            _add_child(sepline.transform);

            // --- 技能库标题 ---
            var lib_title = _create_label("技能库", 16, new Color(0.9f, 0.85f, 0.7f), "LibTitle");
            _set_pos(lib_title.rectTransform, new Vector2(395, 312));
            _add_child(lib_title.transform);

            // --- 技能库滚动区域 ---
            var lib_scroll_go = new GameObject("LibScroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            lib_scroll_go.transform.SetParent(transform, false);
            var lib_scroll = lib_scroll_go.GetComponent<ScrollRect>();
            _set_pos_size(lib_scroll_go.GetComponent<RectTransform>(), new Vector2(395, 338), new Vector2(1000, 440));
            lib_scroll.horizontal = false;

            var lib_content_go = new GameObject("LibContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            lib_content_go.transform.SetParent(lib_scroll_go.transform, false);
            skill_library_container = lib_content_go.GetComponent<VerticalLayoutGroup>();
            skill_library_container.spacing = 6;
            skill_library_container.childControlWidth = false;
            skill_library_container.childControlHeight = false;
            skill_library_container.childForceExpandWidth = false;
            skill_library_container.childForceExpandHeight = false;
            lib_scroll.content = lib_content_go.GetComponent<RectTransform>();
        }

        // ============================================================
        // 选中元灵 / 刷新
        // ============================================================

        public virtual void _select_spirit(int index)
        {
            if (index < 0 || index >= spirits_data.Count) return;
            selected_spirit_index = index;
            selected_spirit = spirits_data[index];

            Color icon_color = Color.white;
            string color_str = _S(selected_spirit.TryGetValue("icon_color", out var ic) ? ic : "#FFFFFF");
            if (!ColorUtility.TryParseHtmlString(color_str != "" ? color_str : "#FFFFFF", out icon_color)) icon_color = Color.white;
            detail_avatar.color = icon_color;

            detail_name.text = _S(selected_spirit.TryGetValue("name", out var nm) ? nm : "?") + "（" + _S(selected_spirit.TryGetValue("element", out var el) ? el : "") + "）";
            level_label.text = "等级 " + _I(selected_spirit.TryGetValue("level", out var lv) ? lv : 1) + " / " + _I(selected_spirit.TryGetValue("max_level", out var ml) ? ml : 10);
            detail_desc.text = _S(selected_spirit.TryGetValue("description", out var desc) ? desc : "");

            _update_equipped_slots();
            _update_skill_library();
            Debug.Log("[SpiritUI] 选中: " + _S(selected_spirit.TryGetValue("name", out var n2) ? n2 : "?") + "(" + _S(selected_spirit.TryGetValue("element", out var e2) ? e2 : "") + ")");
        }

        protected virtual void _update_equipped_slots()
        {
            var equipped_actives = new List<string>();
            if (selected_spirit.TryGetValue("equipped_actives", out var ea))
            {
                var lst = _List(ea);
                foreach (var v in lst) equipped_actives.Add(_S(v));
            }
            else { equipped_actives = new List<string> { "", "", "" }; }

            string equipped_passive = _S(selected_spirit.TryGetValue("equipped_passive", out var ep) ? ep : "");

            for (int i = 0; i < 3; i++)
            {
                _clear_panel(active_skill_slots[i]);
                active_skill_slots[i].color = new Color(0.15f, 0.15f, 0.25f);
                if (i < equipped_actives.Count && equipped_actives[i] != "")
                {
                    var skill = _get_skill_by_id(equipped_actives[i]);
                    if (skill.Count > 0)
                        _fill_skill_slot(active_skill_slots[i], skill);
                }
                // 恢复标签
                var tag = _create_label("主动" + (i + 1), 10, new Color(0.5f, 0.5f, 0.5f), "Tag_" + i);
                _set_pos(tag.rectTransform, new Vector2(14, 48));
                tag.transform.SetParent(active_skill_slots[i].transform, false);
            }

            _clear_panel(passive_skill_slot);
            passive_skill_slot.color = new Color(0.15f, 0.12f, 0.2f);
            if (equipped_passive != "")
            {
                var skill = _get_skill_by_id(equipped_passive);
                if (skill.Count > 0)
                    _fill_skill_slot(passive_skill_slot, skill);
            }
            var ptag = _create_label("被动", 10, new Color(0.5f, 0.5f, 0.5f), "PTag");
            _set_pos(ptag.rectTransform, new Vector2(18, 48));
            ptag.transform.SetParent(passive_skill_slot.transform, false);
        }

        protected virtual void _fill_skill_slot(Image slot, Dictionary<string, object> skill)
        {
            // 填充技能槽位 (方形图标+名称缩写)
            Color icon_color = _get_skill_icon_color(skill);
            slot.color = icon_color;

            var abbr = _create_label(_S(skill.TryGetValue("name", out var nm) ? nm : "").Length >= 2 ? _S(nm).Substring(0, 2) : _S(nm), 16, Color.white, "Abbr");
            _set_pos(abbr.rectTransform, new Vector2(8, 20));
            abbr.transform.SetParent(slot.transform, false);

            // 右键查看技能详情
            var trigger = slot.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            var captured_skill = skill;
            entry.callback.AddListener((data) => { _on_slot_right_click(data, captured_skill); });
            trigger.triggers.Add(entry);
        }

        protected virtual void _on_slot_right_click(BaseEventData data, Dictionary<string, object> skill)
        {
            var ped = data as PointerEventData;
            if (ped != null && ped.button == PointerEventData.InputButton.Right)
                _show_skill_popup(skill, true, "");
        }

        protected virtual void _update_skill_library()
        {
            if (skill_library_container == null) return;
            // 清空旧条目
            for (int i = skill_library_container.transform.childCount - 1; i >= 0; i--)
                Destroy(skill_library_container.transform.GetChild(i).gameObject);

            var spirit_skills = new List<object>();
            if (selected_spirit.TryGetValue("skills", out var ss)) spirit_skills = _List(ss);
            var unlocked = new List<object>();
            if (selected_spirit.TryGetValue("unlocked_skills", out var us)) unlocked = _List(us);

            foreach (var sid in spirit_skills)
            {
                string skill_id = _S(sid);
                var skill = _get_skill_by_id(skill_id);
                if (skill.Count == 0) continue;
                bool is_unlocked = false;
                foreach (var u in unlocked) { if (_S(u) == skill_id) { is_unlocked = true; break; } }
                var entry = _create_library_entry(skill, is_unlocked, skill_id);
                entry.transform.SetParent(skill_library_container.transform, false);
            }
        }

        protected virtual RectTransform _create_library_entry(Dictionary<string, object> skill, bool is_unlocked, string skill_id)
        {
            // 技能库中的一行 (Control容器, 叠加覆盖按钮)
            var row_go = new GameObject("LibEntry_" + skill_id, typeof(RectTransform), typeof(Image));
            var row = row_go.GetComponent<RectTransform>();
            row.sizeDelta = new Vector2(950, 65);
            row.gameObject.GetComponent<Image>().color = new Color(0, 0, 0, 0); // 透明

            // 方形图标
            Color icon_color = _get_skill_icon_color(skill);
            var icon_go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var icon = icon_go.GetComponent<Image>();
            icon.color = is_unlocked ? icon_color : new Color(0.2f, 0.2f, 0.2f);
            _set_pos_size(icon_go.GetComponent<RectTransform>(), new Vector2(0, 5), new Vector2(52, 52));
            icon_go.transform.SetParent(row_go.transform, false);

            // 锁
            if (!is_unlocked)
            {
                var lock_label = _create_label("\U0001F512", 18, Color.white, "Lock");
                _set_pos(lock_label.rectTransform, new Vector2(16, 14));
                lock_label.transform.SetParent(icon_go.transform, false);
            }

            // 右侧信息
            float info_x = 62.0f;

            var name_label = _create_label(_S(skill.TryGetValue("name", out var nm) ? nm : "") + "  [" + _S(skill.TryGetValue("type", out var ty) ? ty : "") + "]", 13, is_unlocked ? Color.white : new Color(0.5f, 0.5f, 0.5f), "N");
            _set_pos_size(name_label.rectTransform, new Vector2(info_x, 5), new Vector2(880, 18));
            name_label.transform.SetParent(row_go.transform, false);

            var desc_label = _create_label(_S(skill.TryGetValue("description", out var dsc) ? dsc : ""), 11, new Color(0.6f, 0.6f, 0.6f), "D");
            _set_pos_size(desc_label.rectTransform, new Vector2(info_x, 24), new Vector2(880, 18));
            desc_label.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc_label.transform.SetParent(row_go.transform, false);

            if (!is_unlocked)
            {
                var cost_label = _create_label("解锁: " + _I(skill.TryGetValue("unlock_cost", out var uc) ? uc : 0) + " 元灵水晶", 11, new Color(0.4f, 0.7f, 1.0f), "Cost");
                _set_pos(cost_label.rectTransform, new Vector2(info_x, 43));
                cost_label.transform.SetParent(row_go.transform, false);
            }

            // 覆盖整行的透明按钮 (最上层, 接收右键)
            var click_go = new GameObject("ClickOverlay", typeof(RectTransform), typeof(Image));
            var click_img = click_go.GetComponent<Image>();
            click_img.color = new Color(0, 0, 0, 0); // 透明
            _set_pos_size(click_go.GetComponent<RectTransform>(), Vector2.zero, new Vector2(950, 65));
            click_go.transform.SetParent(row_go.transform, false);

            // 右键事件
            var trigger = click_go.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            var captured_skill = skill;
            bool captured_unlocked = is_unlocked;
            string captured_id = skill_id;
            entry.callback.AddListener((data) => { _on_entry_input(data, captured_skill, captured_unlocked, captured_id); });
            trigger.triggers.Add(entry);

            return row;
        }

        protected virtual void _on_entry_input(BaseEventData data, Dictionary<string, object> skill, bool is_unlocked, string skill_id)
        {
            var ped = data as PointerEventData;
            if (ped != null && ped.button == PointerEventData.InputButton.Right)
                _show_skill_popup(skill, is_unlocked, skill_id);
        }

        // ============================================================
        // 技能详情弹窗
        // ============================================================

        protected virtual void _show_skill_popup(Dictionary<string, object> skill, bool is_unlocked, string skill_id)
        {
            // 清除旧弹窗
            var old = transform.Find("SkillPopup");
            if (old != null) Destroy(old.gameObject);

            var popup_go = new GameObject("SkillPopup", typeof(RectTransform), typeof(Image));
            var popup = popup_go.GetComponent<Image>();
            popup.color = new Color(0.1f, 0.1f, 0.22f, 0.98f);
            _set_pos_size(popup_go.GetComponent<RectTransform>(), new Vector2(460, 280), new Vector2(520, 340));
            popup_go.transform.SetParent(transform, false);

            // 技能名
            Color name_color = Color.white;
            string color_str = _S(skill.TryGetValue("icon_color", out var ic) ? ic : "#FFF");
            if (!ColorUtility.TryParseHtmlString(color_str != "" ? color_str : "#FFF", out name_color)) name_color = Color.white;
            var title = _create_label(_S(skill.TryGetValue("name", out var nm) ? nm : ""), 18, name_color, "PTitle");
            _set_pos_size(title.rectTransform, new Vector2(20, 15), new Vector2(480, 28));
            title.transform.SetParent(popup_go.transform, false);

            // 类型+元素
            var type_label = _create_label("[" + _S(skill.TryGetValue("type", out var ty) ? ty : "") + "]  元素: " + _S(skill.TryGetValue("element", out var el) ? el : "?"), 12, new Color(0.6f, 0.6f, 0.6f), "PType");
            _set_pos(type_label.rectTransform, new Vector2(20, 45));
            type_label.transform.SetParent(popup_go.transform, false);

            // 描述
            var desc = _create_label(_S(skill.TryGetValue("description", out var dsc) ? dsc : ""), 13, Color.white, "PDesc");
            _set_pos_size(desc.rectTransform, new Vector2(20, 68), new Vector2(480, 40));
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.transform.SetParent(popup_go.transform, false);

            // 详细效果
            var detail = _create_label(_S(skill.TryGetValue("detail", out var dt) ? dt : ""), 12, new Color(0.6f, 0.85f, 0.6f), "PDetail");
            _set_pos_size(detail.rectTransform, new Vector2(20, 112), new Vector2(480, 120));
            detail.horizontalOverflow = HorizontalWrapMode.Wrap;
            detail.transform.SetParent(popup_go.transform, false);

            if (!is_unlocked)
            {
                int cost = _I(skill.TryGetValue("unlock_cost", out var uc) ? uc : 0);
                var unlock_btn = _create_button("解锁（" + cost + " 元灵水晶）", "PUnlockBtn");
                _set_pos_size(unlock_btn.GetComponent<RectTransform>(), new Vector2(150, 240), new Vector2(220, 36));
                unlock_btn.GetComponentInChildren<Text>().fontSize = 14;
                unlock_btn.interactable = (spirit_crystal >= cost);
                string captured_id = skill_id;
                int captured_cost = cost;
                unlock_btn.onClick.AddListener(() => _on_unlock_skill(captured_id, captured_cost));
                unlock_btn.transform.SetParent(popup_go.transform, false);

                if (spirit_crystal < cost)
                {
                    var warn = _create_label("水晶不足！", 12, Color.red, "PWarn");
                    _set_pos(warn.rectTransform, new Vector2(210, 282));
                    warn.transform.SetParent(popup_go.transform, false);
                }
            }
            else
            {
                var owned = _create_label("\u2705 已解锁", 14, Color.green, "POwned");
                _set_pos(owned.rectTransform, new Vector2(220, 245));
                owned.transform.SetParent(popup_go.transform, false);
            }

            // 关闭
            var close_btn = _create_button("关闭", "PCloseBtn");
            _set_pos_size(close_btn.GetComponent<RectTransform>(), new Vector2(220, 295), new Vector2(80, 28));
            var captured_popup = popup_go;
            close_btn.onClick.AddListener(() => { if (captured_popup != null) Destroy(captured_popup); });
            close_btn.transform.SetParent(popup_go.transform, false);
        }

        // ============================================================
        // 按钮回调
        // ============================================================

        public virtual void _on_unlock_skill(string skill_id, int cost)
        {
            if (spirit_crystal < cost || selected_spirit_index < 0) return;
            spirit_crystal -= cost;
            crystal_label.text = "水晶:" + spirit_crystal;

            var unlocked = new List<object>();
            if (selected_spirit.TryGetValue("unlocked_skills", out var us)) unlocked = _List(us);
            bool found = false;
            foreach (var u in unlocked) { if (_S(u) == skill_id) { found = true; break; } }
            if (!found) unlocked.Add(skill_id);
            selected_spirit["unlocked_skills"] = unlocked;

            _update_skill_library();
            var popup = transform.Find("SkillPopup");
            if (popup != null) Destroy(popup.gameObject);
            Debug.Log("[SpiritUI] 解锁: " + skill_id + " (消耗" + cost + "水晶)");
        }

        public virtual void _on_edit_skills()
        {
            Debug.Log("[SpiritUI] 修改上场技能 - 待实现");
        }

        public virtual void _on_confirm()
        {
            Debug.Log("[SpiritUI] 确认配置: " + _S(selected_spirit.TryGetValue("name", out var nm) ? nm : "?"));
        }

        public virtual void _on_upgrade_spirit()
        {
            if (selected_spirit_index < 0) return;
            int level = _I(selected_spirit.TryGetValue("level", out var lv) ? lv : 1);
            int max_level = _I(selected_spirit.TryGetValue("max_level", out var ml) ? ml : 10);
            if (level >= max_level)
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
            level_label.text = "等级 " + _I(selected_spirit["level"]) + " / " + max_level;
            Debug.Log("[SpiritUI] " + _S(selected_spirit.TryGetValue("name", out var nm) ? nm : "?") + " 升级 Lv" + _I(selected_spirit["level"]) + " (消耗" + cost + "矿石)");
        }

        public virtual void _on_close()
        {
            gameObject.SetActive(false);
        }

        // ============================================================
        // 辅助
        // ============================================================

        protected virtual Dictionary<string, object> _get_skill_by_id(string skill_id)
        {
            foreach (var s in skills_data)
            {
                if (_S(s.TryGetValue("id", out var i) ? i : "") == skill_id) return s;
            }
            return new Dictionary<string, object>();
        }

        // 获取技能色块颜色: 优先 icon_color, 为白色/空时用技能元素色兜底
        protected virtual Color _get_skill_icon_color(Dictionary<string, object> skill)
        {
            string color_str = _S(skill.TryGetValue("icon_color", out var ic) ? ic : "#FFFFFF");
            if (color_str != "" && color_str != "#FFFFFF")
            {
                if (ColorUtility.TryParseHtmlString(color_str, out var c)) return c;
                return Color.gray;
            }
            // 白色/空 → 元素色兜底
            return _get_element_color(_S(skill.TryGetValue("element", out var el) ? el : ""));
        }

        // 元素颜色映射 (与开发面板/HUD 一致)
        protected virtual Color _get_element_color(string element)
        {
            var colors = new Dictionary<string, Color> {
                { "金刚", new Color(0.85f, 0.75f, 0.3f) },
                { "大地", new Color(0.7f, 0.55f, 0.35f) },
                { "雷火", new Color(1.0f, 0.4f, 0.2f) },
                { "冰雪", new Color(0.4f, 0.8f, 1.0f) },
                { "草木", new Color(0.3f, 0.8f, 0.3f) },
                { "梦幻", new Color(0.7f, 0.5f, 0.9f) },
            };
            return colors.TryGetValue(element, out var c) ? c : new Color(0.6f, 0.6f, 0.6f);
        }

        protected virtual void _clear_panel(Image panel)
        {
            if (panel == null) return;
            for (int i = panel.transform.childCount - 1; i >= 0; i--)
                Destroy(panel.transform.GetChild(i).gameObject);
            // 移除 EventTrigger (防止重复连接)
            var trigger = panel.GetComponent<EventTrigger>();
            if (trigger != null) Destroy(trigger);
        }

        // ============================================================
        // UI 工厂方法 (替代 Godot 的 X.new() 模式)
        // ============================================================

        protected virtual Image _create_color_rect(Color color, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        protected virtual Image _create_panel(Color color, string name)
        {
            return _create_color_rect(color, name);
        }

        protected virtual Text _create_label(string text, int font_size, Color color, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var lbl = go.GetComponent<Text>();
            lbl.text = text;
            lbl.fontSize = font_size;
            lbl.color = color;
            lbl.alignment = TextAnchor.UpperLeft;
            lbl.raycastTarget = false;
            // 默认字体
            lbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return lbl;
        }

        protected virtual Button _create_button(string text, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var btn = go.GetComponent<Button>();
            var img = go.GetComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.3f, 1.0f);
            // 子 Text
            var txt_go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var txt = txt_go.GetComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;
            var txt_rt = txt_go.GetComponent<RectTransform>();
            _set_full_rect(txt_rt);
            txt_go.transform.SetParent(go.transform, false);
            return btn;
        }

        protected virtual HorizontalLayoutGroup _create_hbox(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            return go.GetComponent<HorizontalLayoutGroup>();
        }

        protected virtual void _set_full_rect(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        protected virtual void _set_pos(RectTransform rt, Vector2 pos)
        {
            rt.anchoredPosition = pos;
        }

        protected virtual void _set_pos_size(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        protected virtual void _add_child(Transform child)
        {
            child.SetParent(transform, false);
        }

        // 简易 JSON 解析 (Godot JSON.parse 替代品, 仅支持简单字典/数组)
        // 完整实现走 JsonUtility 或第三方库, 这里提供最小可用版本
        protected virtual Dictionary<string, object> _parse_json(string json)
        {
            var result = new Dictionary<string, object>();
            if (string.IsNullOrEmpty(json)) return result;
            try
            {
                // 简化: 使用 JsonUtility 配合包装类解析
                // 注意: JsonUtility 不直接支持 Dictionary, 此处为兜底解析
                // 实际项目应使用 Newtonsoft.Json 或 litJSON
                var wrapper = JsonUtility.FromJson<JsonWrapper>(json);
                if (wrapper != null)
                {
                    if (wrapper.spirits != null) result["spirits"] = wrapper.spirits;
                    if (wrapper.skills != null) result["skills"] = wrapper.skills;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SpiritUI] JSON 解析失败: " + e.Message);
            }
            return result;
        }

        [Serializable]
        protected class JsonWrapper
        {
            public List<string> spirits;
            public List<string> skills;
        }

        // IPointerClickHandler 接口实现 (SpiritUI 自身)
        public void OnPointerClick(PointerEventData eventData) { /* 顶层不处理点击 */ }
    }
}

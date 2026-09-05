using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BattleBall.UI
{
    /// <summary>
    /// 管理员快捷设置面板（开发者工具）
    /// 参考 GD scripts/dev_tools/dev_settings_main.gd
    /// 主面板包含 7 个子面板入口：账号、球员、元灵、装备、食物、成长、奖励
    /// 每个子面板使用硬编码测试数据，并提供"添加"/"删除"按钮（点击仅打印日志）
    /// </summary>
    public class DevSettings : MonoBehaviour
    {
        public event System.Action Closed;

        private Font _defaultFont;
        private RectTransform _rect;

        // 当前打开的子面板根节点
        private GameObject _currentSubPanel;

        // 子面板数据列表（用于添加/删除演示）
        private Dictionary<string, List<string>> _subData = new Dictionary<string, List<string>>();

        void Start()
        {
            _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _rect = GetComponent<RectTransform>();
            if (_rect == null) _rect = gameObject.AddComponent<RectTransform>();
            SetFullRect(_rect);

            InitTestData();
            BuildMainUI();
        }

        // ============================================================
        // 测试数据
        // ============================================================
        private void InitTestData()
        {
            _subData["account"] = new List<string> { "玩家名: 猪猪侠", "金币: 9999", "等级: 25" };
            _subData["player"] = new List<string> { "猪猪侠", "超人强", "小呆呆" };
            _subData["spirit"] = new List<string> { "阿五", "岩灵", "雷火" };
            _subData["equipment"] = new List<string> { "金刚手套", "钻石壁垒" };
            _subData["food"] = new List<string> { "能量饮料", "高蛋白面包" };
            _subData["growth"] = new List<string> { "体力 Lv5", "防御 Lv3" };
            _subData["reward"] = new List<string> { "连胜 3 场", "金币 +500" };
        }

        // ============================================================
        // 主面板 UI
        // ============================================================
        private void BuildMainUI()
        {
            // 全屏背景
            NewColorRect("BG", Vector2.zero, new Vector2(1440, 900), new Color(0.06f, 0.06f, 0.10f, 0.97f), transform);

            // 标题栏背景
            NewColorRect("TitleBar", Vector2.zero, new Vector2(1440, 70), new Color(0.12f, 0.15f, 0.22f, 1f), transform);

            // 标题
            NewLabel("Title", new Vector2(520, 18), new Vector2(400, 36), "管理员快捷设置", 28, new Color(0.4f, 0.9f, 0.6f), transform);

            // 关闭按钮
            var closeBtn = NewButton("CloseBtn", new Vector2(1340, 15), new Vector2(80, 40), "✕ 关闭", 16, new Color(1f, 0.6f, 0.6f), transform);
            closeBtn.onClick.AddListener(OnClose);

            // 7 个子面板入口按钮
            var entries = new[]
            {
                new { id = "account",   name = "账号管理",         color = new Color(0.4f, 0.8f, 1.0f) },
                new { id = "player",    name = "球员管理",         color = new Color(0.9f, 0.85f, 0.5f) },
                new { id = "spirit",    name = "元灵管理",         color = new Color(0.6f, 0.9f, 1.0f) },
                new { id = "equipment", name = "装备管理",         color = new Color(0.9f, 0.7f, 0.3f) },
                new { id = "food",      name = "食物管理",         color = new Color(0.3f, 0.9f, 0.6f) },
                new { id = "growth",    name = "成长曲线规划",     color = new Color(0.8f, 0.6f, 1.0f) },
                new { id = "reward",    name = "奖励设置",         color = new Color(0.9f, 0.8f, 0.3f) }
            };

            int startX = 520;
            int startY = 130;
            int btnW = 400;
            int btnH = 55;
            int spacing = 20;

            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                int y = startY + i * (btnH + spacing);
                var btn = NewButton("EntryBtn_" + e.id, new Vector2(startX, y), new Vector2(btnW, btnH), e.name, 20, e.color, transform);
                string capturedId = e.id;
                string capturedName = e.name;
                btn.onClick.AddListener(() => OpenSubPanel(capturedId, capturedName));
            }
        }

        // ============================================================
        // 子面板
        // ============================================================
        private void OpenSubPanel(string panelId, string panelName)
        {
            CloseSubPanel();

            var panel = new GameObject("SubPanel_" + panelId, typeof(RectTransform), typeof(Image));
            var prt = panel.GetComponent<RectTransform>();
            prt.SetParent(transform, false);
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;
            prt.pivot = new Vector2(0f, 1f);
            panel.GetComponent<Image>().color = new Color(0.04f, 0.04f, 0.08f, 0.98f);

            _currentSubPanel = panel;

            // 标题
            NewLabel("SubTitle", new Vector2(520, 18), new Vector2(400, 36), panelName, 26, new Color(0.9f, 0.85f, 0.7f), panel.transform);

            // 返回按钮
            var backBtn = NewButton("BackBtn", new Vector2(1340, 15), new Vector2(80, 40), "← 返回", 16, Color.white, panel.transform);
            backBtn.onClick.AddListener(CloseSubPanel);

            // 内容背景
            NewColorRect("ContentBg", new Vector2(420, 80), new Vector2(600, 680), new Color(0.08f, 0.10f, 0.16f, 0.95f), panel.transform);

            // 数据列表
            BuildDataList(panelId, panel.transform);

            // 添加按钮
            var addBtn = NewButton("AddBtn", new Vector2(440, 780), new Vector2(280, 45), "＋ 添加", 18, new Color(0.4f, 0.9f, 0.5f), panel.transform);
            string cid = panelId;
            string cname = panelName;
            addBtn.onClick.AddListener(() => OnAddItem(cid, cname));

            // 删除按钮
            var delBtn = NewButton("DelBtn", new Vector2(730, 780), new Vector2(280, 45), "－ 删除", 18, new Color(0.9f, 0.4f, 0.4f), panel.transform);
            delBtn.onClick.AddListener(() => OnDeleteItem(cid, cname));
        }

        private void BuildDataList(string panelId, Transform parent)
        {
            List<string> data;
            if (!_subData.TryGetValue(panelId, out data) || data == null)
            {
                NewLabel("EmptyHint", new Vector2(540, 120), new Vector2(360, 30), "（暂无数据）", 18, new Color(0.5f, 0.5f, 0.6f), parent);
                return;
            }

            int itemY = 110;
            int itemH = 40;
            int spacing = 8;

            for (int i = 0; i < data.Count; i++)
            {
                int y = itemY + i * (itemH + spacing);
                NewColorRect("ItemBg_" + i, new Vector2(440, y), new Vector2(560, itemH), new Color(0.14f, 0.17f, 0.25f, 1f), parent);
                NewLabel("ItemLbl_" + i, new Vector2(460, y + 6), new Vector2(520, 28), data[i], 18, Color.white, parent);
            }
        }

        private void CloseSubPanel()
        {
            if (_currentSubPanel != null)
            {
                Destroy(_currentSubPanel);
                _currentSubPanel = null;
            }
        }

        // ============================================================
        // 添加 / 删除（仅打印日志）
        // ============================================================
        private void OnAddItem(string panelId, string panelName)
        {
            Debug.Log("[DevSettings] 添加 -> " + panelName + " (" + panelId + ")");
        }

        private void OnDeleteItem(string panelId, string panelName)
        {
            Debug.Log("[DevSettings] 删除 -> " + panelName + " (" + panelId + ")");
        }

        private void OnClose()
        {
            Closed?.Invoke();
            Destroy(gameObject);
        }

        // ============================================================
        // UI 工厂方法（项目约定：左上角坐标系 anchor=pivot=(0,1)，anchoredPosition=(x,-y)）
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
    }
}

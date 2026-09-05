using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BattleBall.Core;

namespace BattleBall.UI
{
    /// <summary>
    /// 角色系统界面 - 展示所有球员的详细属性
    /// 布局：左侧圆形头像列表 | 右侧选中球员详情面板
    /// </summary>
    public class CharacterSystem : MonoBehaviour
    {
        public event System.Action Closed;

        // 元素颜色映射
        private static readonly Dictionary<string, Color> ElementColors = new Dictionary<string, Color>
        {
            { "金刚", new Color(0.85f, 0.75f, 0.3f) },
            { "梦幻", new Color(0.7f, 0.5f, 0.9f) },
            { "草木", new Color(0.3f, 0.8f, 0.3f) },
            { "雷火", new Color(1.0f, 0.4f, 0.2f) },
            { "冰雪", new Color(0.4f, 0.8f, 1.0f) },
            { "大地", new Color(0.7f, 0.55f, 0.35f) },
        };

        // 属性条颜色
        private static readonly Dictionary<string, Color> StatColors = new Dictionary<string, Color>
        {
            { "stamina", new Color(0.9f, 0.2f, 0.2f) },      // 红
            { "defense", new Color(0.9f, 0.75f, 0.1f) },     // 黄
            { "speed", new Color(0.2f, 0.5f, 0.95f) },       // 蓝
            { "attack", new Color(0.65f, 0.65f, 0.65f) },    // 黑（深灰）
            { "resilience", new Color(0.55f, 0.55f, 0.55f) },
            { "defense_factor", new Color(0.6f, 0.8f, 0.3f) },
            { "ball_speed", new Color(0.6f, 0.4f, 0.2f) },
        };

        private static readonly Dictionary<string, string> StatLabels = new Dictionary<string, string>
        {
            { "stamina", "体力" },
            { "defense", "防御" },
            { "speed", "速度" },
            { "attack", "攻击" },
            { "resilience", "韧性" },
            { "defense_factor", "防御因子" },
            { "ball_speed", "发球球速" },
        };

        // 属性最大值（用于条形图比例）
        private const float StatMax = 100.0f;

        // === 右侧详情面板排版规范常量 === 添加新属性时只需修改内容，不需要修改位置计算 ===
        private const float PanelX = 370.0f;
        private const float PanelY = 90.0f;
        private const float PanelWidth = 1070.0f;
        private const float PanelHeight = 700.0f;
        private const float PanelVisibleHeight = 625.0f;

        private const float ContentX = 370.0f;
        private const float NameY = 90.0f;
        private const float DescY = 130.0f;
        private const float StatYStart = 175.0f;
        private const float StatSpacing = 44.0f;
        private const float StatBarX = 440.0f;
        private const float StatBarWidth = 350.0f;
        private const float StatBarHeight = 18.0f;
        private const float StatValX = 800.0f;
        private const float SepAfterStats = 15.0f;
        private const float TalentTitleAfterSep = 15.0f;
        private const float TalentDescAfterTitle = 27.0f;
        private const float SpiritAfterDesc = 43.0f;
        private const float UltimateAfterSpirit = 35.0f;

        private List<Dictionary<string, object>> _charactersData = new List<Dictionary<string, object>>();
        private int _selectedIndex = 0;
        private Font _defaultFont;

        // 属性键数组（添加新属性时只需修改这里）
        private static readonly string[] StatKeys = { "stamina", "defense", "speed", "attack", "resilience", "defense_factor", "ball_speed" };

        // 左侧列表节点引用
        private VerticalLayoutGroup _avatarList;
        private List<Button> _avatarButtons = new List<Button>();

        // 右侧面板节点引用
        private Text _nameLabel;
        private Text _descLabel;
        private Dictionary<string, Dictionary<string, object>> _statBars = new Dictionary<string, Dictionary<string, object>>(); // {stat_key: {fill, val_label, bg}}
        private Text _talentTitle;
        private Text _talentDesc;
        private Text _spiritLabel;
        private Text _ultimateLabel;
        private RectTransform _rect;

        void Start()
        {
            _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _rect = GetComponent<RectTransform>();
            if (_rect == null) _rect = gameObject.AddComponent<RectTransform>();
            SetFullRect(_rect);

            LoadData();
            BuildUI();
            SelectCharacter(0);
        }

        private void SetFullRect(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 1f);
        }

        private void LoadData()
        {
            // 尝试从全局 DataManager 获取真实角色数据
            var dm = DataManager.Instance;
            if (dm != null)
            {
                var list = dm.GetAllCharacters();
                if (list != null && list.Count > 0)
                {
                    _charactersData = list;
                    return;
                }
            }

            // 兜底：DataManager 为空或数据为空时，使用硬编码测试数据
            Debug.LogWarning("[CharacterSystem] DataManager 数据不可用，使用硬编码测试数据兜底");
            _charactersData = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    {"id", "char_001"}, {"name", "猪猪侠"}, {"stamina", 80.0},
                    {"defense", 60.0}, {"speed", 75.0}, {"attack", 38.0},
                    {"resilience", 50.0}, {"defense_factor", 0.15}, {"ball_speed", 420.0},
                    {"element", "金刚"}, {"spirit_preference", "金刚"},
                    {"talent_name", "不屈意志"}, {"talent_desc", "体力低于30%时攻击力+15%"},
                    {"ultimate_skill", "猛虎金刚闪"}, {"description", "主角，攻守兼备的全能型球员"}
                },
                new Dictionary<string, object>
                {
                    {"id", "char_002"}, {"name", "超人强"}, {"stamina", 90.0},
                    {"defense", 85.0}, {"speed", 60.0}, {"attack", 55.0},
                    {"resilience", 70.0}, {"defense_factor", 0.18}, {"ball_speed", 380.0},
                    {"element", "雷火"}, {"spirit_preference", "雷火"},
                    {"talent_name", "钢铁壁垒"}, {"talent_desc", "防御时受到伤害减少20%"},
                    {"ultimate_skill", "绝对防御"}, {"description", "防守核心，铜墙铁壁的坚盾型球员"}
                }
            };
        }

        private void BuildUI()
        {
            // 全屏背景（完全不透明，彻底遮挡主菜单）
            var bgGo = new GameObject("BG", typeof(RectTransform), typeof(Image));
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.SetParent(transform, false);
            SetFullRect(bgRt);
            bgGo.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 1.0f);

            // 标题栏
            var titleBarGo = new GameObject("TitleBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var tbrt = titleBarGo.GetComponent<RectTransform>();
            tbrt.SetParent(transform, false);
            tbrt.anchoredPosition = new Vector2(0, 15);
            tbrt.sizeDelta = new Vector2(1440, 45);

            var title = NewLabel("Title", Vector2.zero, new Vector2(1300, 45), "角色系统", 30, new Color(1, 0.9f, 0.3f), titleBarGo.transform);
            title.alignment = TextAnchor.MiddleCenter;

            var closeBtn = NewButton("CloseBtn", Vector2.zero, new Vector2(50, 45), "✕", 22, Color.white, titleBarGo.transform);
            closeBtn.onClick.AddListener(OnClose);

            // === 左侧底板 ===
            var leftPanel = new GameObject("LeftPanel", typeof(RectTransform), typeof(Image));
            var lprt = leftPanel.GetComponent<RectTransform>();
            lprt.SetParent(transform, false);
            lprt.anchorMin = new Vector2(0, 1);
            lprt.anchorMax = new Vector2(0, 1);
            lprt.pivot = new Vector2(0, 1);
            lprt.anchoredPosition = new Vector2(30, -75);
            lprt.sizeDelta = new Vector2(270, 700);
            leftPanel.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.18f, 1.0f);

            // 左侧列表挂到 left_panel 下
            var listGo = new GameObject("AvatarList", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var listRt = listGo.GetComponent<RectTransform>();
            listRt.SetParent(lprt, false);
            listRt.anchorMin = new Vector2(0, 1);
            listRt.anchorMax = new Vector2(0, 1);
            listRt.pivot = new Vector2(0, 1);
            listRt.anchoredPosition = new Vector2(20, -5);
            listRt.sizeDelta = new Vector2(240, 690);
            _avatarList = listGo.GetComponent<VerticalLayoutGroup>();
            _avatarList.spacing = 6;
            _avatarList.childControlWidth = true;
            _avatarList.childControlHeight = true;
            _avatarList.childForceExpandWidth = true;
            _avatarList.childForceExpandHeight = false;
            var csf = listGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var listTitle = NewLabel("ListTitle", Vector2.zero, new Vector2(240, 28), "— 球员列表 —", 16, new Color(0.7f, 0.7f, 0.7f), listGo.transform);
            listTitle.alignment = TextAnchor.MiddleCenter;

            for (int i = 0; i < _charactersData.Count; i++)
            {
                var charData = _charactersData[i];
                var btnGo = new GameObject("AvatarBtn_" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                var brt = btnGo.GetComponent<RectTransform>();
                brt.SetParent(listGo.transform, false);
                brt.sizeDelta = new Vector2(240, 90);
                btnGo.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                var btn = btnGo.GetComponent<Button>();

                // 文字
                var txtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                var trt = txtGo.GetComponent<RectTransform>();
                trt.SetParent(brt, false);
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = Vector2.zero;
                trt.offsetMax = Vector2.zero;
                var lbl = txtGo.GetComponent<Text>();
                lbl.alignment = TextAnchor.MiddleLeft;
                lbl.horizontalOverflow = HorizontalWrapMode.Overflow;
                lbl.verticalOverflow = VerticalWrapMode.Overflow;
                BuildAvatarButton(lbl, charData, i);

                int idx = i;
                btn.onClick.AddListener(() => SelectCharacter(idx));
                _avatarButtons.Add(btn);
            }

            // === 右侧详情面板 ===
            var rightPanel = new GameObject("RightPanel", typeof(RectTransform), typeof(Image));
            var rprt = rightPanel.GetComponent<RectTransform>();
            rprt.SetParent(transform, false);
            rprt.anchorMin = new Vector2(0, 1);
            rprt.anchorMax = new Vector2(0, 1);
            rprt.pivot = new Vector2(0, 1);
            rprt.anchoredPosition = new Vector2(PanelX - 8, -(PanelY - 8));
            rprt.sizeDelta = new Vector2(PanelWidth + 16, PanelVisibleHeight + 16);
            rightPanel.GetComponent<Image>().color = new Color(0.10f, 0.10f, 0.15f, 1.0f);

            // 滚动容器
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            var srt = scrollGo.GetComponent<RectTransform>();
            srt.SetParent(transform, false);
            srt.anchorMin = new Vector2(0, 1);
            srt.anchorMax = new Vector2(0, 1);
            srt.pivot = new Vector2(0, 1);
            srt.anchoredPosition = new Vector2(PanelX, -PanelY);
            srt.sizeDelta = new Vector2(PanelWidth, PanelVisibleHeight);
            scrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0);

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var crt = content.GetComponent<RectTransform>();
            crt.SetParent(srt, false);
            crt.anchorMin = new Vector2(0, 1);
            crt.anchorMax = Vector2.one;
            crt.pivot = new Vector2(0.5f, 1);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            var cvg = content.GetComponent<VerticalLayoutGroup>();
            cvg.childControlWidth = true;
            cvg.childControlHeight = true;
            cvg.childForceExpandWidth = true;
            cvg.childForceExpandHeight = false;
            var csfc = content.GetComponent<ContentSizeFitter>();
            csfc.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sr = scrollGo.GetComponent<ScrollRect>();
            sr.content = crt;
            sr.vertical = true;
            sr.horizontal = false;

            // === 在content内添加所有元素 ===
            // 球员名称
            _nameLabel = NewLabel("Name", Vector2.zero, new Vector2(PanelWidth, 35), "", 28, Color.white, content.transform);
            _nameLabel.alignment = TextAnchor.MiddleLeft;

            // 描述
            _descLabel = NewLabel("Desc", new Vector2(0, DescY - NameY), new Vector2(PanelWidth, 20), "", 15, new Color(0.65f, 0.65f, 0.65f), content.transform);
            _descLabel.alignment = TextAnchor.MiddleLeft;

            // 属性条
            for (int idx = 0; idx < StatKeys.Length; idx++)
            {
                string key = StatKeys[idx];
                float y = (StatYStart - NameY) + idx * StatSpacing;

                // 属性名标签
                var statName = NewLabel("StatName_" + key, new Vector2(0, y), new Vector2(60, 22), StatLabels[key], 16, StatColors[key], content.transform);
                statName.alignment = TextAnchor.MiddleLeft;

                // 属性条背景
                var barBgGo = NewColorRect("BarBg_" + key, new Vector2(StatBarX - PanelX, y + 2), new Vector2(StatBarWidth, StatBarHeight), new Color(0.2f, 0.2f, 0.25f), content.transform);

                // 属性条填充
                var barFillGo = NewColorRect("BarFill_" + key, new Vector2(StatBarX - PanelX, y + 2), new Vector2(0, StatBarHeight), StatColors[key], content.transform);

                // 数值标签
                var valLabel = NewLabel("ValLabel_" + key, new Vector2(StatValX - PanelX, y), new Vector2(60, 22), "", 16, StatColors[key], content.transform);
                valLabel.alignment = TextAnchor.MiddleLeft;

                _statBars[key] = new Dictionary<string, object>
                {
                    { "fill", barFillGo.GetComponent<Image>() },
                    { "fill_rt", barFillGo.GetComponent<RectTransform>() },
                    { "val_label", valLabel },
                    { "bg", barBgGo.GetComponent<Image>() }
                };
            }

            // 计算分隔线位置
            float sepY = (StatYStart - NameY) + (StatKeys.Length - 1) * StatSpacing + 30.0f;

            // 分隔线
            NewColorRect("Sep", new Vector2(0, sepY), new Vector2(480, 1), new Color(0.4f, 0.4f, 0.45f), content.transform);

            // 天赋标题
            _talentTitle = NewLabel("TalentTitle", new Vector2(0, sepY + TalentTitleAfterSep), new Vector2(PanelWidth, 22), "", 17, new Color(1, 0.85f, 0.3f), content.transform);
            _talentTitle.alignment = TextAnchor.MiddleLeft;

            // 天赋描述
            _talentDesc = NewLabel("TalentDesc", new Vector2(0, sepY + TalentTitleAfterSep + TalentDescAfterTitle), new Vector2(480, 20), "", 14, new Color(0.7f, 0.7f, 0.7f), content.transform);
            _talentDesc.alignment = TextAnchor.MiddleLeft;

            // 元灵偏好
            _spiritLabel = NewLabel("SpiritLabel", new Vector2(0, sepY + TalentTitleAfterSep + TalentDescAfterTitle + SpiritAfterDesc), new Vector2(PanelWidth, 22), "", 17, Color.white, content.transform);
            _spiritLabel.alignment = TextAnchor.MiddleLeft;

            // 大招
            _ultimateLabel = NewLabel("UltimateLabel", new Vector2(0, sepY + TalentTitleAfterSep + TalentDescAfterTitle + SpiritAfterDesc + UltimateAfterSpirit), new Vector2(PanelWidth, 22), "", 17, new Color(1, 0.5f, 0.2f), content.transform);
            _ultimateLabel.alignment = TextAnchor.MiddleLeft;

            // 右侧圆形大头像占位（放在滚动容器外，固定位置）
            BuildLargeAvatarPlaceholder();
        }

        private void BuildAvatarButton(Text btnLabel, Dictionary<string, object> charData, int index)
        {
            string spirit = GetStr(charData, "spirit_preference", "");
            Color spiritColor;
            if (!ElementColors.TryGetValue(spirit, out spiritColor)) spiritColor = Color.white;
            string name = GetStr(charData, "name", "?");
            btnLabel.text = "  " + name + "\n  " + spirit;
            btnLabel.fontSize = 15;
            btnLabel.color = spiritColor;
            btnLabel.alignment = TextAnchor.MiddleLeft;
        }

        private void BuildLargeAvatarPlaceholder()
        {
            // 大圆形背景（元素颜色）
            var avatarCircle = NewColorRect("LargeAvatarBG", new Vector2(1150, 95), new Vector2(120, 120), new Color(0.3f, 0.3f, 0.35f), transform);
            var avatarChar = NewLabel("LargeAvatarChar", new Vector2(1150, 95), new Vector2(120, 120), "", 48, Color.white, transform);
            avatarChar.alignment = TextAnchor.MiddleCenter;

            var avatarName = NewLabel("LargeAvatarName", new Vector2(1150, 225), new Vector2(120, 25), "", 14, new Color(0.7f, 0.7f, 0.7f), transform);
            avatarName.alignment = TextAnchor.MiddleCenter;
        }

        private void SelectCharacter(int index)
        {
            if (index < 0 || index >= _charactersData.Count) return;
            _selectedIndex = index;
            var data = _charactersData[index];

            // 更新左侧选中高亮
            for (int i = 0; i < _avatarButtons.Count; i++)
            {
                var btn = _avatarButtons[i];
                if (i == index)
                {
                    btn.GetComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f, 1.0f);
                }
                else
                {
                    btn.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                }
            }

            // 球员名称
            _nameLabel.text = GetStr(data, "name", "");

            // 描述
            _descLabel.text = GetStr(data, "description", "");

            // 属性条（使用数组stat_keys）
            foreach (var key in StatKeys)
            {
                float val = GetFloat(data, key, 0f);
                var info = _statBars[key];
                var fillImg = info["fill"] as Image;
                var fillRt = info["fill_rt"] as RectTransform;
                var label = info["val_label"] as Text;

                // 防御因子范围0.1~0.2,按0.25算比例；发球球速范围300~600
                float maxVal;
                if (key == "defense_factor") maxVal = 0.25f;
                else if (key == "ball_speed") maxVal = 600.0f;
                else maxVal = StatMax;
                float ratio = Mathf.Clamp(val / maxVal, 0f, 1f);
                if (fillRt != null)
                    fillRt.sizeDelta = new Vector2(StatBarWidth * ratio, StatBarHeight);
                if (label != null)
                {
                    if (key == "defense_factor")
                        label.text = val.ToString("F2");
                    else
                        label.text = ((int)val).ToString();
                }
            }

            // 天赋
            _talentTitle.text = "天赋: " + GetStr(data, "talent_name", "");
            _talentDesc.text = GetStr(data, "talent_desc", "");

            // 元灵偏好
            string spirit = GetStr(data, "spirit_preference", "");
            Color spiritColor;
            if (!ElementColors.TryGetValue(spirit, out spiritColor)) spiritColor = Color.white;
            _spiritLabel.text = "元灵偏好: " + spirit;
            _spiritLabel.color = spiritColor;

            // 大招
            _ultimateLabel.text = "大招: " + GetStr(data, "ultimate_skill", "");

            // 大头像更新
            var largeBg = transform.Find("LargeAvatarBG");
            if (largeBg != null)
            {
                largeBg.GetComponent<Image>().color = new Color(spiritColor.r * 0.4f, spiritColor.g * 0.4f, spiritColor.b * 0.4f, 1.0f);
            }
            var largeChar = transform.Find("LargeAvatarChar")?.GetComponent<Text>();
            if (largeChar != null)
            {
                string display = GetStr(data, "name", "?");
                largeChar.text = display.Length > 0 ? display.Substring(0, 1) : "";
            }
            var largeName = transform.Find("LargeAvatarName")?.GetComponent<Text>();
            if (largeName != null)
            {
                largeName.text = GetStr(data, "name", "");
            }
        }

        private void OnClose()
        {
            Closed?.Invoke();
            Destroy(gameObject);
        }

        // ===== 工具 =====
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

        private static string GetStr(Dictionary<string, object> d, string key, string def)
        {
            if (d == null) return def;
            object v;
            return d.TryGetValue(key, out v) && v != null ? v.ToString() : def;
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
    }
}

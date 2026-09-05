using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using BattleBall.Core;
using PlayerSaveManager = BattleBall.Systems.PlayerSaveManager;
using BattleBall.Systems;                      // PlayerSaveManager
using BattleBall.Systems.Inventory;          // InventoryManager
using BattleBall.Systems.Training;           // TrainingManager
using BattleBall.Systems.Nutrition;          // NutritionManager

namespace BattleBall.UI
{
    /// <summary>
    /// 基地主界面
    /// 依赖：PlayerSaveManager（货币显示）、InventoryManager（背包数据）、TrainingManager（训练数据）
    /// </summary>
    public class BaseSystem : MonoBehaviour
    {
        public event System.Action Closed;

        private string _currentTab = "equipment";
        private Dictionary<string, Text> _currencyLabels = new Dictionary<string, Text>();

        // 像素 → 米，除以 100
        private RectTransform _rect;

        void Start()
        {
            _rect = GetComponent<RectTransform>();
            if (_rect == null) _rect = gameObject.AddComponent<RectTransform>();
            SetFullRect(_rect);
            BuildBaseUI();
        }

        // ===== 工具：建全屏锚点 =====
        private void SetFullRect(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ===== 工具：创建带背景色矩形 =====
        private GameObject NewColorRect(string name, Vector2 pos, Vector2 size, Color color, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            return go;
        }

        // ===== 工具：创建文本 =====
        private Text NewLabel(string name, Vector2 pos, Vector2 size, string text, int fontSize, Color color, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var lbl = go.GetComponent<Text>();
            lbl.text = text;
            lbl.fontSize = fontSize;
            lbl.color = color;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.horizontalOverflow = HorizontalWrapMode.Overflow;
            lbl.verticalOverflow = VerticalWrapMode.Overflow;
            return lbl;
        }

        // ===== 工具：创建按钮 =====
        private Button NewButton(string name, Vector2 pos, Vector2 size, string text, int fontSize, Color color, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos;
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
            lbl.color = color;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.horizontalOverflow = HorizontalWrapMode.Overflow;
            lbl.verticalOverflow = VerticalWrapMode.Overflow;
            return go.GetComponent<Button>();
        }

        private GameObject NewVerticalLayout(string name, Vector2 pos, Vector2 size, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var vg = go.GetComponent<VerticalLayoutGroup>();
            vg.childControlWidth = true;
            vg.childControlHeight = true;
            vg.childForceExpandWidth = true;
            vg.childForceExpandHeight = false;
            return go;
        }

        private GameObject NewGridLayout(string name, int columns, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            var gl = go.GetComponent<GridLayoutGroup>();
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = columns;
            gl.cellSize = new Vector2(2.7f, 1.0f);
            var csf = go.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go;
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

        // ===== 主构建入口 =====
        private void BuildBaseUI()
        {
            // 背景
            NewColorRect("BG", Vector2.zero, new Vector2(1440, 900), new Color(0.08f, 0.1f, 0.15f, 1f), transform);

            // 顶部标题栏
            NewColorRect("TitleBar", Vector2.zero, new Vector2(1440, 80), new Color(0.12f, 0.15f, 0.22f, 1f), transform);

            // 标题
            NewLabel("Title", new Vector2(50, 20), new Vector2(200, 40), "基地", 28, new Color(0.9f, 0.85f, 0.7f), transform);

            // 关闭按钮
            var btnClose = NewButton("BtnClose", new Vector2(1390, 20), new Vector2(40, 40), "×", 28, new Color(0.7f, 0.6f, 0.6f), transform);
            btnClose.onClick.AddListener(OnClose);

            // 货币栏
            BuildCurrencyBar();

            // 左侧导航栏
            BuildNavigation();

            // 右侧内容区背景
            NewColorRect("ContentBg", new Vector2(220, 80), new Vector2(1220, 820), new Color(0.06f, 0.08f, 0.12f, 0.98f), transform);

            // 默认显示装备模块
            ShowTab("equipment");
        }

        private void BuildCurrencyBar()
        {
            int startX = 500;
            int spacing = 220;

            var currencies = new[] {
                new { key = "fairy_coin", name = "童话币", color = new Color(1.0f, 0.9f, 0.6f) },
                new { key = "spirit_ore", name = "元灵矿石", color = new Color(0.6f, 0.8f, 1.0f) },
                new { key = "crystal", name = "水晶", color = new Color(0.8f, 0.6f, 1.0f) }
            };

            for (int i = 0; i < currencies.Length; i++)
            {
                var curr = currencies[i];
                int x = startX + i * spacing;

                NewLabel("CurrencyName_" + curr.key, new Vector2(x, 25), new Vector2(100, 20), curr.name, 14, curr.color, transform);

                var labelValue = NewLabel("CurrencyValue_" + curr.key, new Vector2(x, 48), new Vector2(150, 22), "0", 18, new Color(0.95f, 0.95f, 0.95f), transform);

                _currencyLabels[curr.key] = labelValue;
            }

            UpdateCurrencyDisplay();
        }

        private void UpdateCurrencyDisplay()
        {
            if (PlayerSaveManager.Instance == null) return;
            foreach (var key in _currencyLabels.Keys)
            {
                if (_currencyLabels[key] != null)
                {
                    int value = (int)PlayerSaveManager.Instance.GetCurrency(key);
                    _currencyLabels[key].text = value.ToString();
                }
            }
        }

        private void BuildNavigation()
        {
            NewColorRect("NavBg", new Vector2(0, 80), new Vector2(220, 820), new Color(0.1f, 0.12f, 0.18f, 0.98f), transform);

            var navItems = new[] {
                new { id = "equipment", name = "装备", icon = "👜" },
                new { id = "training", name = "训练", icon = "💪" },
                new { id = "nutrition", name = "营养", icon = "🍎" },
                new { id = "shop", name = "商店", icon = "🏪" }
            };

            int startY = 100;
            int spacing = 75;

            for (int i = 0; i < navItems.Length; i++)
            {
                var item = navItems[i];
                int y = startY + i * spacing;

                Color c = (item.id == _currentTab)
                    ? new Color(1.0f, 0.9f, 0.6f)
                    : new Color(0.7f, 0.7f, 0.8f);

                var btn = NewButton("NavBtn_" + item.id, new Vector2(20, y), new Vector2(180, 55), item.icon + " " + item.name, 18, c, transform);
                string id = item.id;
                btn.onClick.AddListener(() => SwitchTab(id));
            }
        }

        private void SwitchTab(string tabId)
        {
            if (tabId == _currentTab) return;
            _currentTab = tabId;

            foreach (Transform btn in transform)
            {
                if (btn.name.StartsWith("NavBtn_"))
                {
                    string btnTabId = btn.name.Replace("NavBtn_", "");
                    var txt = btn.GetComponentInChildren<Text>();
                    if (txt != null)
                    {
                        txt.color = (btnTabId == tabId)
                            ? new Color(1.0f, 0.9f, 0.6f)
                            : new Color(0.7f, 0.7f, 0.8f);
                    }
                }
            }

            ShowTab(tabId);
        }

        private void ShowTab(string tabId)
        {
            // 清除旧内容
            var toDestroy = new List<GameObject>();
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("TabContent_"))
                    toDestroy.Add(child.gameObject);
            }
            foreach (var go in toDestroy)
                Destroy(go);

            switch (tabId)
            {
                case "equipment": ShowEquipmentTab(); break;
                case "training": ShowTrainingTab(); break;
                case "nutrition": ShowNutritionTab(); break;
                case "shop": ShowShopTab(); break;
            }
        }

        private GameObject CreateContentRoot(string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var vg = go.GetComponent<VerticalLayoutGroup>();
            vg.spacing = 4;
            vg.childControlWidth = true;
            vg.childControlHeight = true;
            vg.childForceExpandWidth = true;
            vg.childForceExpandHeight = false;
            var csf = go.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go;
        }

        private void ShowEquipmentTab()
        {
            var content = CreateContentRoot("TabContent_Equipment", new Vector2(240, 90), new Vector2(1180, 810));

            NewLabel("Title", Vector2.zero, new Vector2(200, 30), "装备管理", 22, new Color(0.9f, 0.85f, 0.7f), content.transform);
            NewLabel("Desc", Vector2.zero, new Vector2(300, 20), "背包装备列表，点击查看详情", 14, new Color(0.5f, 0.5f, 0.6f), content.transform);

            var scroll = NewScrollView("Scroll", content.transform);
            var grid = NewGridLayout("Grid", 4, scroll.transform);

            List<Dictionary<string, object>> equipList = new List<Dictionary<string, object>>();
            if (InventoryManager.Instance != null)
                equipList = InventoryManager.Instance.GetBackpackEquipment();

            if (equipList.Count == 0)
            {
                NewLabel("Empty", Vector2.zero, new Vector2(200, 20), "背包暂无装备", 16, new Color(0.4f, 0.4f, 0.4f), grid.transform);
            }
            else
            {
                foreach (var entry in equipList)
                {
                    string itemId = GetStr(entry, "item_id", "");
                    Dictionary<string, object> itemDef = new Dictionary<string, object>();
                    if (InventoryManager.Instance != null)
                        itemDef = InventoryManager.Instance.GetItemDef(itemId);

                    float curDur = 0f;
                    if (entry.ContainsKey("durability"))
                        curDur = GetFloat(entry, "durability", 0f);
                    else
                        curDur = GetFloat(itemDef, "max_durability", 50f);

                    int count = GetInt(entry, "count", 1);
                    var cardObj = CreateItemCard(itemDef, count, curDur, grid.transform);
                }
            }

            // 已穿戴装备区
            NewLabel("EquippedTitle", Vector2.zero, new Vector2(300, 20), "━━ 已穿戴装备 ━━", 16, new Color(0.9f, 0.8f, 0.5f), content.transform);
            var equippedGrid = NewGridLayout("EquippedGrid", 3, content.transform);

            bool hasEquipped = false;
            if (PlayerSaveManager.Instance != null && HasMethod(PlayerSaveManager.Instance, "GetAllEquipped"))
            {
                var allEq = PlayerSaveManager.Instance.GetAllEquipped();
            var unlockedChars = new List<object>(); // 简化兜底: 角色解锁列表进Play测后再联调
                foreach (string charId in allEq.Keys)
                {
                    bool unlocked = false;
                    foreach (var uc in unlockedChars) { if (uc.ToString() == charId) { unlocked = true; break; } }
                    if (!unlocked) continue;

                    var charEq = (allEq != null && allEq.ContainsKey(charId)) ? allEq[charId] : new Dictionary<string, object>();
                    foreach (string slot in new[] { "glove", "jersey", "shoes" })
                    {
                        var slotData = GetObj(charEq, slot, null);
                        string itemId = "";
                        float curDur = 0f;
                        if (slotData is Dictionary<string, object> sd)
                        {
                            itemId = GetStr(sd, "item_id", "");
                            curDur = GetFloat(sd, "durability", 0f);
                        }
                        else
                        {
                            itemId = slotData == null ? "" : slotData.ToString();
                        }
                        if (itemId == "") continue;

                        Dictionary<string, object> itemDef = new Dictionary<string, object>();
                        if (InventoryManager.Instance != null)
                            itemDef = InventoryManager.Instance.GetItemDef(itemId);

                        CreateItemCard(itemDef, 1, curDur, equippedGrid.transform);
                        hasEquipped = true;
                    }
                }
            }
            if (!hasEquipped)
            {
                NewLabel("NoEquipped", Vector2.zero, new Vector2(200, 20), "暂无已穿戴装备", 14, new Color(0.4f, 0.4f, 0.4f), equippedGrid.transform);
            }
        }

        // ===== 创建物品卡片 =====
        private GameObject CreateItemCard(Dictionary<string, object> itemDef, int count, float curDurability, Transform parent)
        {
            var cardGo = new GameObject("ItemCard", typeof(RectTransform), typeof(Image));
            var crt = cardGo.GetComponent<RectTransform>();
            crt.SetParent(parent, false);
            crt.sizeDelta = new Vector2(2.7f, 1.0f);
            cardGo.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.22f, 0.98f);

            string iconPath = GetStr(itemDef, "icon", "");
            string itemName = GetStr(itemDef, "name", "未知物品");
            string rarity = GetStr(itemDef, "rarity", "common");
            string itemType = GetStr(itemDef, "type", "");
            string subType = GetStr(itemDef, "sub_type", "");
            int maxDur = GetInt(itemDef, "max_durability", 50);

            Color rarityColor = new Color(0.7f, 0.7f, 0.7f);
            if (InventoryManager.Instance != null && HasMethod(InventoryManager.Instance, "GetRarityColor"))
                rarityColor = InventoryManager.Instance.GetRarityColor(rarity);

            string finalIconPath = "";
            if (!string.IsNullOrEmpty(iconPath) && System.IO.File.Exists(iconPath))
            {
                finalIconPath = iconPath;
                Debug.Log("[Base] ✓ 图标路径有效: " + iconPath);
            }
            else
            {
                finalIconPath = GetFallbackIconPath(itemType, subType, rarity);
                Debug.Log("[Base] ! fallback: " + finalIconPath);
            }

            if (!string.IsNullOrEmpty(finalIconPath))
            {
                var tex = Resources.Load<Texture2D>(finalIconPath);
                if (tex != null)
                {
                    var iconBgGo = NewColorRect("IconBg", new Vector2(8, 8), new Vector2(80, 80), new Color(0.08f, 0.1f, 0.15f), cardGo.transform);
                    var iconRectGo = new GameObject("IconRect", typeof(RectTransform), typeof(RawImage));
                    var irt = iconRectGo.GetComponent<RectTransform>();
                    irt.SetParent(cardGo.transform, false);
                    irt.anchoredPosition = new Vector2(8, 8);
                    irt.sizeDelta = new Vector2(80, 80);
                    var ri = iconRectGo.GetComponent<RawImage>();
                    ri.texture = tex;
                }
                else
                {
                    Debug.Log("[Base] ✗ 加载失败");
                }
            }
            else
            {
                Debug.Log("[Base] ✗ 无可用图标路径");
            }

            NewLabel("Name", new Vector2(96, 10), new Vector2(160, 25), itemName, 16, rarityColor, cardGo.transform);

            var statsDict = GetDict(itemDef, "stats", new Dictionary<string, object>());
            var effectDict = GetDict(itemDef, "effect", new Dictionary<string, object>());
            string statText = "";

            if (statsDict.Count > 0)
            {
                int idx = 0;
                foreach (var k in statsDict.Keys)
                {
                    if (statText != "") statText += "\n";
                    statText += k + " +" + statsDict[k];
                    idx++;
                    if (idx >= 2) break;
                }
            }

            if (effectDict.Count > 0 && statText == "")
            {
                string effectStat = GetStr(effectDict, "stat", "");
                object effectVal = GetObj(effectDict, "value", 0);
                statText = effectStat + " +" + effectVal;
            }

            if (statText == "") statText = "暂无属性";

            NewLabel("Stats", new Vector2(96, 40), new Vector2(160, 50), statText, 12, new Color(0.6f, 0.6f, 0.7f), cardGo.transform);

            if (count > 1)
            {
                var cl = NewLabel("Count", new Vector2(230, 10), new Vector2(30, 20), "x" + count, 14, new Color(0.9f, 0.8f, 0.5f), cardGo.transform);
            }

            if (itemType == "equipment")
            {
                NewColorRect("DurBg", new Vector2(96, 82), new Vector2(160, 10), new Color(0.15f, 0.18f, 0.25f), cardGo.transform);

                float durPct = 0f;
                if (maxDur > 0)
                    durPct = Mathf.Clamp(curDurability / maxDur, 0f, 1f);

                Color barColor;
                if (durPct <= 0.3f) barColor = new Color(1.0f, 0.3f, 0.3f);
                else if (durPct <= 0.6f) barColor = new Color(1.0f, 0.8f, 0.2f);
                else barColor = new Color(0.3f, 0.9f, 0.3f);

                NewColorRect("DurBar", new Vector2(96, 82), new Vector2(160 * durPct, 10), barColor, cardGo.transform);

                var durLabel = NewLabel("DurLabel", new Vector2(96, 72), new Vector2(160, 12), ((int)curDurability) + "/" + maxDur, 11, new Color(0.6f, 0.6f, 0.7f), cardGo.transform);
            }

            return cardGo;
        }

        private string GetFallbackIconPath(string itemType, string subType, string rarity)
        {
            List<string> fallbackPaths = new List<string>();

            if (itemType == "equipment")
            {
                if (subType == "glove")
                {
                    fallbackPaths.Add("assets/icons/items/equipment/glove_" + rarity);
                    fallbackPaths.Add("assets/icons/items/equipment/glove_rare");
                }
                else if (subType == "jersey")
                {
                    fallbackPaths.Add("assets/icons/items/equipment/jersey_" + rarity);
                    fallbackPaths.Add("assets/icons/items/equipment/jersey_rare");
                }
                else if (subType == "shoes")
                {
                    fallbackPaths.Add("assets/icons/items/equipment/shoes_" + rarity);
                    fallbackPaths.Add("assets/icons/items/equipment/shoes_rare");
                }
                else
                {
                    fallbackPaths.Add("assets/icons/items/equipment/glove_rare");
                }
            }
            else if (itemType == "consumable" || subType == "food")
            {
                fallbackPaths.Add("assets/icons/items/food/food_" + rarity);
                fallbackPaths.Add("assets/icons/items/food/food_rare");
            }
            else
            {
                fallbackPaths.Add("assets/icons/items/equipment/glove_rare");
                fallbackPaths.Add("assets/icons/items/food/food_rare");
            }

            foreach (var path in fallbackPaths)
            {
                if (Resources.Load(path) != null)
                    return path;
            }
            return "";
        }

        private void ShowTrainingTab()
        {
            var content = CreateContentRoot("TabContent_Training", new Vector2(240, 90), new Vector2(1180, 810));

            NewLabel("Title", Vector2.zero, new Vector2(200, 30), "训练场地", 22, new Color(0.9f, 0.85f, 0.7f), content.transform);

            int fieldLevel = 1;
            if (TrainingManager.Instance != null && HasMethod(TrainingManager.Instance, "GetFieldLevel"))
                fieldLevel = TrainingManager.Instance.GetFieldLevel();

            NewLabel("Level", Vector2.zero, new Vector2(200, 20), "当前场地等级: Lv." + fieldLevel, 16, new Color(0.7f, 0.9f, 0.7f), content.transform);

            var btnUpgrade = NewButton("BtnUpgrade", Vector2.zero, new Vector2(200, 45), "升级场地", 16, Color.white, content.transform);
            btnUpgrade.onClick.AddListener(OnUpgradeField);

            NewLabel("TrainingTitle", Vector2.zero, new Vector2(200, 25), "球员训练", 18, new Color(0.8f, 0.8f, 0.8f), content.transform);
            var grid = NewGridLayout("Grid", 3, content.transform);

            var stats = new[] {
                new { key = "attack", name = "攻击", icon = "⚔️" },
                new { key = "defense", name = "防御", icon = "🛡️" },
                new { key = "speed", name = "速度", icon = "⚡" },
                new { key = "stamina", name = "体力", icon = "❤️" },
                new { key = "resilience", name = "韧性", icon = "💎" },
                new { key = "ball_speed", name = "球速", icon = "⚽" }
            };

            foreach (var stat in stats)
            {
                var cardGo = CreateTrainingCard(stat.key, stat.name, stat.icon, grid.transform);
            }
        }

        private GameObject CreateTrainingCard(string statKey, string statName, string icon, Transform parent)
        {
            var cardGo = new GameObject("TrainingCard_" + statKey, typeof(RectTransform), typeof(Image));
            var crt = cardGo.GetComponent<RectTransform>();
            crt.SetParent(parent, false);
            crt.sizeDelta = new Vector2(3.8f, 1.0f);
            cardGo.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.22f, 0.98f);

            NewLabel("Name", new Vector2(20, 10), new Vector2(340, 25), icon + " " + statName, 18, new Color(0.9f, 0.85f, 0.7f), cardGo.transform);
            NewLabel("Value", new Vector2(20, 40), new Vector2(340, 20), "当前: 0 / 上限: 0", 14, new Color(0.6f, 0.6f, 0.7f), cardGo.transform);
            var btnTrain = NewButton("BtnTrain", new Vector2(260, 60), new Vector2(100, 30), "训练 (+2)", 14, Color.white, cardGo.transform);
            string sk = statKey;
            btnTrain.onClick.AddListener(() => Debug.Log("[Base] 训练 " + sk));
            return cardGo;
        }

        private void ShowNutritionTab()
        {
            var content = CreateContentRoot("TabContent_Nutrition", new Vector2(240, 90), new Vector2(1180, 810));

            NewLabel("Title", Vector2.zero, new Vector2(200, 30), "营养系统", 22, new Color(0.9f, 0.85f, 0.7f), content.transform);
            NewLabel("Desc", Vector2.zero, new Vector2(400, 20), "赛前食用食物，全队获得属性加成（整场比赛有效）", 14, new Color(0.5f, 0.5f, 0.6f), content.transform);

            // 当前生效食物状态
            string activeFoodId = "";
            if (NutritionManager.Instance != null && HasMethod(NutritionManager.Instance, "GetActiveFoodId"))
                activeFoodId = NutritionManager.Instance.GetActiveFoodId();

            string activeText;
            Color activeColor;
            if (activeFoodId != "")
            {
                Dictionary<string, object> foodData = new Dictionary<string, object>();
                if (NutritionManager.Instance != null && HasMethod(NutritionManager.Instance, "GetFood"))
                    foodData = NutritionManager.Instance.GetFood(activeFoodId);
                string fname = GetStr(foodData, "name", activeFoodId);
                activeText = "🌿 当前生效食物: " + fname + "（比赛结束后清除）";
                activeColor = new Color(0.4f, 1.0f, 0.4f);
            }
            else
            {
                activeText = "💤 暂无生效食物（需在备战界面食用）";
                activeColor = new Color(0.6f, 0.6f, 0.6f);
            }

            var activeBox = new GameObject("ActiveBox", typeof(RectTransform), typeof(Image));
            activeBox.GetComponent<RectTransform>().SetParent(content.transform, false);
            activeBox.GetComponent<Image>().color = new Color(0.15f, 0.2f, 0.15f, 0.95f);
            NewLabel("ActiveLabel", Vector2.zero, new Vector2(500, 20), activeText, 14, activeColor, activeBox.transform);

            var scroll = NewScrollView("Scroll", content.transform);
            var grid = NewGridLayout("Grid", 4, scroll.transform);

            List<Dictionary<string, object>> foodList = new List<Dictionary<string, object>>();
            if (InventoryManager.Instance != null)
                foodList = InventoryManager.Instance.GetBackpackConsumables();

            if (foodList.Count == 0)
            {
                NewLabel("Empty", Vector2.zero, new Vector2(200, 20), "背包暂无食物", 16, new Color(0.4f, 0.4f, 0.4f), grid.transform);
            }
            else
            {
                foreach (var entry in foodList)
                {
                    string itemId = GetStr(entry, "item_id", "");
                    Dictionary<string, object> itemDef = new Dictionary<string, object>();
                    if (InventoryManager.Instance != null)
                        itemDef = InventoryManager.Instance.GetItemDef(itemId);

                    if (itemDef.Count == 0 && NutritionManager.Instance != null)
                    {
                        if (HasMethod(NutritionManager.Instance, "GetFood"))
                        {
                            var foodData = NutritionManager.Instance.GetFood(itemId);
                            if (foodData != null && foodData.Count > 0)
                                itemDef = foodData;
                        }
                    }

                    int count = GetInt(entry, "count", 1);
                    CreateItemCard(itemDef, count, 0f, grid.transform);
                }
            }
        }

        private void ShowShopTab()
        {
            var content = CreateContentRoot("TabContent_Shop", new Vector2(240, 90), new Vector2(1180, 810));

            NewLabel("Title", Vector2.zero, new Vector2(200, 30), "商店", 22, new Color(0.9f, 0.85f, 0.7f), content.transform);
            NewLabel("Desc", Vector2.zero, new Vector2(400, 20), "购买装备和食物（暂未开放）", 14, new Color(0.5f, 0.5f, 0.6f), content.transform);

            var coming = NewLabel("Coming", new Vector2(400, 300), new Vector2(400, 40), "🛒 商店功能开发中...", 24, new Color(0.7f, 0.7f, 0.8f), content.transform);
            coming.alignment = TextAnchor.MiddleCenter;
        }

        private void OnClose()
        {
            Closed?.Invoke();
            Destroy(gameObject);
        }

        private void OnOpenInventory()
        {
            Debug.Log("[Base] 打开背包");
        }

        private void OnUpgradeField()
        {
            if (TrainingManager.Instance != null && HasMethod(TrainingManager.Instance, "UpgradeField"))
            {
                bool success = TrainingManager.Instance.UpgradeField();
                if (success)
                {
                    UpdateCurrencyDisplay();
                    SwitchTab("training");
                    Debug.Log("[Base] 场地升级成功");
                }
                else
                {
                    Debug.Log("[Base] 场地升级失败");
                }
            }
            else
            {
                Debug.Log("[Base] TrainingManager 未找到");
            }
        }

        private void OnGoToPreparation()
        {
            Debug.Log("[Base] 前往备战界面");
            OnClose();
            SceneManager.LoadScene("battle_arena");
        }

        private void OnViewFood()
        {
            Debug.Log("[Base] 查看食物 - 请使用'前往备战界面选食物'按钮");
        }

        // ===== 工具：反射检测方法是否存在（GD has_method 等价） =====
        private static bool HasMethod(object obj, string methodName)
        {
            return obj != null && obj.GetType().GetMethod(methodName) != null;
        }

        // ===== 工具：Dict 取值（适配 GDScript 的 .get(key, default)） =====
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
            float r;
            return float.TryParse(v.ToString(), out r) ? r : def;
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
    }
}

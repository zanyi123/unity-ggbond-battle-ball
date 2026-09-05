using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BattleBall.Core;

namespace BattleBall.UI
{
    /// <summary>
    /// 角色选择界面 - 用于替补球员时选择角色
    /// </summary>
    public class CharacterSelection : MonoBehaviour
    {
        public event System.Action<string> CharacterSelected;

        private GridLayoutGroup _characterGrid;
        private List<Dictionary<string, object>> _availableCharacters = new List<Dictionary<string, object>>();
        private string _selectedCharId = "";
        private Font _defaultFont;

        void Start()
        {
            _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            SetupUI();
        }

        private void SetupUI()
        {
            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(600, 900);
            rt.anchoredPosition = new Vector2(300, 200);

            // 背景
            var bgGo = new GameObject("BG", typeof(RectTransform), typeof(Image));
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.SetParent(transform, false);
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            bgGo.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.3f, 0.98f);

            var title = NewLabel("Title", new Vector2(250, 20), new Vector2(100, 30), "选择角色", 20, Color.white, transform);
            title.alignment = TextAnchor.MiddleCenter;

            var gridGo = new GameObject("CharacterGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            var gridRt = gridGo.GetComponent<RectTransform>();
            gridRt.SetParent(transform, false);
            gridRt.anchoredPosition = new Vector2(50, 70);
            gridRt.sizeDelta = new Vector2(500, 280);
            _characterGrid = gridGo.GetComponent<GridLayoutGroup>();
            _characterGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _characterGrid.constraintCount = 3;
            _characterGrid.cellSize = new Vector2(1.5f, 1.2f);

            var closeBtn = NewButton("CloseBtn", new Vector2(250, 360), new Vector2(100, 30), "关闭", 14, Color.white, transform);
            closeBtn.onClick.AddListener(OnClose);
        }

        /// <summary>加载可用角色</summary>
        public void LoadCharacters(List<Dictionary<string, object>> characters)
        {
            _availableCharacters = characters ?? new List<Dictionary<string, object>>();

            // 清空旧卡片
            for (int i = 0; i < _characterGrid.transform.childCount; i++)
            {
                Destroy(_characterGrid.transform.GetChild(i).gameObject);
            }

            foreach (var charData in _availableCharacters)
            {
                var card = CreateCharacterCard(charData, _characterGrid.transform);
            }
        }

        /// <summary>创建角色卡片</summary>
        private GameObject CreateCharacterCard(Dictionary<string, object> charData, Transform parent)
        {
            var cardGo = new GameObject("CharacterCard", typeof(RectTransform), typeof(Image));
            var crt = cardGo.GetComponent<RectTransform>();
            crt.SetParent(parent, false);
            crt.sizeDelta = new Vector2(1.5f, 1.2f);
            cardGo.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 0.8f);

            var nameStr = GetStr(charData, "name", "未知");
            var nameLabel = NewLabel("Name", new Vector2(10, 10), new Vector2(130, 20), nameStr, 14, Color.white, cardGo.transform);

            float speed = GetFloat(charData, "speed", 100.0f);
            float attack = GetFloat(charData, "attack", 100.0f);
            string attrText = string.Format("速度: {0:F1}\n攻击: {1:F1}", speed, attack);
            var attrLabel = NewLabel("Attr", new Vector2(10, 30), new Vector2(130, 40), attrText, 12, new Color(0.7f, 0.7f, 0.7f), cardGo.transform);

            var selectBtn = NewButton("SelectBtn", new Vector2(35, 80), new Vector2(60, 25), "选择", 12, Color.white, cardGo.transform);
            string charId = GetStr(charData, "id", "");
            selectBtn.onClick.AddListener(() => OnCharacterSelected(charId));

            return cardGo;
        }

        private void OnCharacterSelected(string charId)
        {
            _selectedCharId = charId;
            CharacterSelected?.Invoke(charId);
            gameObject.SetActive(false);
        }

        private void OnClose()
        {
            gameObject.SetActive(false);
        }

        /// <summary>获取选中的角色ID</summary>
        public string GetSelectedCharacter()
        {
            return _selectedCharId;
        }

        // ===== 工具 =====
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

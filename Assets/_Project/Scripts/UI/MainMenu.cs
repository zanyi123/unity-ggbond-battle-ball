using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using BattleBall.Core;
using PlayerSaveManager = BattleBall.Systems.PlayerSaveManager;
using BattleBall.Systems;

namespace BattleBall.UI
{
    /// <summary>
    /// 主菜单 - 游戏入口
    /// 支持两种模式：玩家模式（普通用户）/ 管理员模式（开发者工具）
    /// 1:1 复刻 GD main_menu.gd 的 UI 布局和流程
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        private List<GameObject> _menuInteractiveNodes = new List<GameObject>();
        private string _currentMode = "";
        private List<GameObject> _modeSelectionNodes = new List<GameObject>();

        private CharacterSystem _charUI = null;
        private BaseSystem _baseUI = null;
        private GameObject _spiritUI = null;

        private Font _defaultFont;
        private Canvas _canvas;
        private RectTransform _canvasRect;

        void Start()
        {
            // 默认字体（Unity内置，必须设置否则文字不显示）
            _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 相机
            if (FindObjectOfType<Camera>() == null)
            {
                var camGo = new GameObject("MainCamera");
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.05f, 0.05f, 0.1f);
                camGo.tag = "MainCamera";
            }

            // EventSystem
            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            // Canvas - 左上角锚定，与GD坐标系一致
            _canvas = GetComponent<Canvas>();
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            if (GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1440, 900);
                scaler.matchWidthOrHeight = 0.5f;
            }
            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            _canvasRect = GetComponent<RectTransform>();
            if (_canvasRect == null) _canvasRect = gameObject.AddComponent<RectTransform>();
            // Canvas 全屏，pivot 左上角(0,1)，与GD一致
            _canvasRect.anchorMin = Vector2.zero;
            _canvasRect.anchorMax = Vector2.one;
            _canvasRect.offsetMin = Vector2.zero;
            _canvasRect.offsetMax = Vector2.zero;
            _canvasRect.pivot = new Vector2(0f, 1f);

            // 背景
            NewColorRect("BG", new Vector2(0, 0), new Vector2(1440, 900), new Color(0.1f, 0.1f, 0.2f));

            // 标题
            var title = NewLabel("Title", new Vector2(470, 120), new Vector2(500, 70),
                "猪猪侠之决竞球", 36, Color.yellow);

            // 模式选择或主菜单
            if (GameManager.Instance != null && GameManager.Instance.LastMenuMode == "player")
            {
                BuildMainMenu(false);
            }
            else if (GameManager.Instance != null && GameManager.Instance.LastMenuMode == "admin")
            {
                BuildMainMenu(true);
            }
            else
            {
                ShowModeSelection();
            }
        }

        // ===== 工具方法：GD左上角坐标 → Unity（pivot左上，y向上，所以y取反）=====

        /// <summary>创建纯色矩形（对应GD ColorRect）</summary>
        private GameObject NewColorRect(string name, Vector2 gdPos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_canvasRect, false);
            rt.anchorMin = new Vector2(0f, 1f);    // 左上角锚定
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);          // pivot 左上角
            rt.anchoredPosition = new Vector2(gdPos.x, -gdPos.y); // GD y向下 → Unity y向上取反
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return go;
        }

        /// <summary>创建文字标签（对应GD Label）</summary>
        private Text NewLabel(string name, Vector2 gdPos, Vector2 size, string text, int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_canvasRect, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(gdPos.x, -gdPos.y);
            rt.sizeDelta = size;

            var lbl = go.GetComponent<Text>();
            lbl.text = text;
            lbl.font = _defaultFont;           // 必须设置字体！
            lbl.fontSize = fontSize;
            lbl.color = color;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.horizontalOverflow = HorizontalWrapMode.Overflow;
            lbl.verticalOverflow = VerticalWrapMode.Overflow;
            return lbl;
        }

        /// <summary>创建按钮（对应GD Button）</summary>
        private Button NewButton(string name, Vector2 gdPos, Vector2 size, string text, int fontSize, Color textColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_canvasRect, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(gdPos.x, -gdPos.y);
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.25f, 0.35f, 0.55f, 0.9f);

            var txtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var trt = txtGo.GetComponent<RectTransform>();
            trt.SetParent(rt, false);
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            var lbl = txtGo.GetComponent<Text>();
            lbl.text = text;
            lbl.font = _defaultFont;          // 必须设置字体！
            lbl.fontSize = fontSize;
            lbl.color = textColor;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.horizontalOverflow = HorizontalWrapMode.Overflow;
            lbl.verticalOverflow = VerticalWrapMode.Overflow;
            return go.GetComponent<Button>();
        }

        // ===== 模式选择 =====

        private void ShowModeSelection()
        {
            ClearModeSelection();

            var subtitle = NewLabel("SubTitle", new Vector2(470, 200), new Vector2(500, 40),
                "请选择模式", 22, new Color(0.8f, 0.8f, 0.9f));
            _modeSelectionNodes.Add(subtitle.gameObject);

            var btnPlayer = NewButton("BtnPlayer", new Vector2(545, 320), new Vector2(350, 65),
                "玩家模式", 22, new Color(0.6f, 0.9f, 1.0f));
            btnPlayer.onClick.AddListener(OnEnterPlayerMode);
            _modeSelectionNodes.Add(btnPlayer.gameObject);

            var playerDesc = NewLabel("PlayerDesc", new Vector2(545, 395), new Vector2(350, 25),
                "正常游戏体验，不含开发者工具", 13, new Color(0.5f, 0.5f, 0.6f));
            _modeSelectionNodes.Add(playerDesc.gameObject);

            var btnAdmin = NewButton("BtnAdmin", new Vector2(545, 460), new Vector2(350, 65),
                "管理员模式", 22, new Color(0.5f, 1.0f, 0.6f));
            btnAdmin.onClick.AddListener(OnEnterAdminMode);
            _modeSelectionNodes.Add(btnAdmin.gameObject);

            var adminDesc = NewLabel("AdminDesc", new Vector2(545, 535), new Vector2(350, 25),
                "开发者工具 + 数据管理 + 物资发放（测试用）", 13, new Color(0.5f, 0.5f, 0.6f));
            _modeSelectionNodes.Add(adminDesc.gameObject);
        }

        private void ClearModeSelection()
        {
            foreach (var node in _modeSelectionNodes)
                if (node != null) Destroy(node);
            _modeSelectionNodes.Clear();
        }

        private void OnEnterPlayerMode()
        {
            _currentMode = "player";
            if (GameManager.Instance != null) GameManager.Instance.LastMenuMode = "player";
            ClearModeSelection();
            BuildMainMenu(false);
        }

        private void OnEnterAdminMode()
        {
            _currentMode = "admin";
            if (GameManager.Instance != null) GameManager.Instance.LastMenuMode = "admin";
            ClearModeSelection();
            BuildMainMenu(true);
        }

        // ===== 主菜单按钮 =====

        private void BuildMainMenu(bool isAdmin)
        {
            if (isAdmin)
            {
                var modeTag = NewLabel("ModeTag", new Vector2(1150, 15), new Vector2(260, 30),
                    "【管理员模式】", 16, new Color(0.5f, 1.0f, 0.6f));
                modeTag.alignment = TextAnchor.UpperRight;
                _menuInteractiveNodes.Add(modeTag.gameObject);
            }

            var btnStart = NewButton("BtnStart", new Vector2(545, 260), new Vector2(350, 55),
                "开始比赛", 22, Color.white);
            btnStart.onClick.AddListener(OnStartMatch);
            _menuInteractiveNodes.Add(btnStart.gameObject);

            var btnChars = NewButton("BtnCharacters", new Vector2(545, 335), new Vector2(350, 55),
                "角色系统", 22, Color.white);
            btnChars.onClick.AddListener(OnOpenCharacters);
            _menuInteractiveNodes.Add(btnChars.gameObject);

            var btnSpirits = NewButton("BtnSpirits", new Vector2(545, 410), new Vector2(350, 55),
                "元灵系统", 22, Color.white);
            btnSpirits.onClick.AddListener(OnOpenSpirits);
            _menuInteractiveNodes.Add(btnSpirits.gameObject);

            var btnBase = NewButton("BtnBase", new Vector2(545, 485), new Vector2(350, 55),
                "基地", 22, Color.white);
            btnBase.onClick.AddListener(OnOpenBase);
            _menuInteractiveNodes.Add(btnBase.gameObject);

            var btnTrade = NewButton("BtnTrade", new Vector2(545, 560), new Vector2(350, 55),
                "交易", 22, Color.white);
            btnTrade.onClick.AddListener(OnOpenTrade);
            _menuInteractiveNodes.Add(btnTrade.gameObject);

            bool rewardOn = RewardSystem.RewardEnabledStatic();
            var btnReward = NewButton("BtnReward", new Vector2(545, 635), new Vector2(350, 45),
                "比赛奖励: " + (rewardOn ? "已开启" : "已关闭"), 16,
                rewardOn ? new Color(0.9f, 0.8f, 0.3f) : new Color(0.6f, 0.6f, 0.6f));
            btnReward.onClick.AddListener(OnToggleReward);
            _menuInteractiveNodes.Add(btnReward.gameObject);

            if (isAdmin)
            {
                var btnDev = NewButton("BtnDev", new Vector2(545, 700), new Vector2(350, 55),
                    "快捷设置（开发者）", 22, new Color(0.3f, 0.9f, 0.5f));
                btnDev.onClick.AddListener(OnOpenDevSettings);
                _menuInteractiveNodes.Add(btnDev.gameObject);

                var btnBack = NewButton("BtnBackMode", new Vector2(545, 775), new Vector2(350, 45),
                    "← 返回模式选择", 16, new Color(0.7f, 0.7f, 0.8f));
                btnBack.onClick.AddListener(OnBackToModeSelection);
                _menuInteractiveNodes.Add(btnBack.gameObject);
            }
            else
            {
                var btnBack = NewButton("BtnBackMode", new Vector2(545, 700), new Vector2(350, 45),
                    "← 返回模式选择", 16, new Color(0.7f, 0.7f, 0.8f));
                btnBack.onClick.AddListener(OnBackToModeSelection);
                _menuInteractiveNodes.Add(btnBack.gameObject);
            }
        }

        private void ClearMainMenu()
        {
            foreach (var node in _menuInteractiveNodes)
                if (node != null) Destroy(node);
            _menuInteractiveNodes.Clear();
        }

        private void OnBackToModeSelection()
        {
            _currentMode = "";
            if (GameManager.Instance != null) GameManager.Instance.LastMenuMode = "";
            ClearMainMenu();
            ShowModeSelection();
        }

        // ===== 按钮回调 =====

        private void OnStartMatch()
        {
            SceneManager.LoadScene("TestBattle");
        }

        private void OnOpenCharacters()
        {
            if (_charUI != null) { Destroy(_charUI.gameObject); _charUI = null; }
            var go = new GameObject("CharacterSystem", typeof(RectTransform));
            _charUI = go.AddComponent<CharacterSystem>();
            go.GetComponent<RectTransform>().SetParent(_canvasRect, false);
            Debug.Log("[Main] 角色系统已打开");
        }

        private void OnOpenSpirits()
        {
            Debug.Log("[Main] 元灵系统 - 待实现");
        }

        private void OnOpenBase()
        {
            if (_baseUI != null) { Destroy(_baseUI.gameObject); _baseUI = null; }
            var go = new GameObject("BaseSystem", typeof(RectTransform));
            _baseUI = go.AddComponent<BaseSystem>();
            go.GetComponent<RectTransform>().SetParent(_canvasRect, false);
            Debug.Log("[Main] 基地已打开");
        }

        private void OnOpenTrade()
        {
            Debug.Log("[Main] 交易 - 待实现");
        }

        private void OnOpenDevSettings()
        {
            Debug.Log("[Main] 快捷设置 - 待实现");
        }

        private void OnToggleReward()
        {
            bool current = RewardSystem.RewardEnabledStatic();
            RewardSystem.RewardSetEnabledStatic(!current);
            // 重建奖励按钮
            ClearMainMenu();
            BuildMainMenu(_currentMode == "admin");
        }
    }
}

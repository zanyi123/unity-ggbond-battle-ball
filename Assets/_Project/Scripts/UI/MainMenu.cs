using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using BattleBall.Core;
using PlayerSaveManager = BattleBall.Systems.PlayerSaveManager;
using BattleBall.Systems;  // RewardSystem / PlayerSaveManager

namespace BattleBall.UI
{
    /// <summary>
    /// 主菜单 - 游戏入口
    /// 支持两种模式：玩家模式（普通用户）/ 管理员模式（开发者工具）
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        // 主菜单交互节点列表（打开子界面时隐藏，关闭时恢复）
        private List<GameObject> _menuInteractiveNodes = new List<GameObject>();
        // 当前模式："player" / "admin"
        private string _currentMode = "";
        // 模式选择界面节点
        private List<GameObject> _modeSelectionNodes = new List<GameObject>();

        private CharacterSystem _charUI = null;
        private MonoBehaviour _spiritUI = null; // 元灵系统类型未实现，用基类占位
        private BaseSystem _baseUI = null;

        private RectTransform _rect;

        void Start()
        {
            // 确保有相机(场景没相机会Game窗口黑屏)
            if (FindObjectOfType<Camera>() == null) {
                var camGo = new GameObject("MainCamera");
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.05f, 0.05f, 0.1f);
                camGo.tag = "MainCamera";
            }
            // 确保有 EventSystem（按钮点击需要）
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null) {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
            // 确保有 Canvas（UGUI 渲染必须）
            var canvas = GetComponent<Canvas>();
            if (canvas == null) {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
            }
            if (GetComponent<UnityEngine.UI.CanvasScaler>() == null) {
                var scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1440, 900);
                scaler.matchWidthOrHeight = 0.5f;
            }
            if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null) {
                gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
            _rect = GetComponent<RectTransform>();
            if (_rect == null) _rect = gameObject.AddComponent<RectTransform>();
            SetFullRect(_rect);

            // 背景
            NewColorRect("BG", Vector2.zero, new Vector2(1440, 900), new Color(0.1f, 0.1f, 0.2f), transform);

            // 标题
            var title = NewLabel("Title", new Vector2(470, 120), new Vector2(500, 70), "猪猪侠之决竞球", 36, Color.yellow, transform);
            title.alignment = TextAnchor.MiddleCenter;

            // 如果有上次记录的模式，直接进入；否则显示模式选择
            if (GameManager.Instance != null && GameManager.Instance.LastMenuMode == "player")
            {
                if (PlayerSaveManager.Instance != null && PlayerSaveManager.Instance.CurrentSlot.ToString() != GameManager.MODE_SLOT_PLAYER)
                    PlayerSaveManager.Instance.LoadSlot(int.TryParse(GameManager.MODE_SLOT_PLAYER, out var _ms1) ? _ms1 : 0);
                BuildMainMenu(false);
            }
            else if (GameManager.Instance != null && GameManager.Instance.LastMenuMode == "admin")
            {
                if (PlayerSaveManager.Instance != null && PlayerSaveManager.Instance.CurrentSlot.ToString() != GameManager.MODE_SLOT_ADMIN)
                    PlayerSaveManager.Instance.LoadSlot(int.TryParse(GameManager.MODE_SLOT_ADMIN, out var _ms2) ? _ms2 : 0);
                BuildMainMenu(true);
            }
            else
            {
                ShowModeSelection();
            }
        }

        // ===== 工具 =====
        private void SetFullRect(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>GD左上角坐标(x向右,y向下) → Unity中心坐标(x向右,y向上)</summary>
        private static Vector2 GDToUnity(Vector2 gdPos)
        {
            // 参考分辨率 1440x900
            return new Vector2(gdPos.x - 720f, 450f - gdPos.y);
        }

        private GameObject NewColorRect(string name, Vector2 pos, Vector2 size, Color color, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = GDToUnity(pos);
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return go;
        }

        private Text NewLabel(string name, Vector2 pos, Vector2 size, string text, int fontSize, Color color, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = GDToUnity(pos);
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

        private Button NewButton(string name, Vector2 pos, Vector2 size, string text, int fontSize, Color color, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = GDToUnity(pos);
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
            lbl.fontSize = fontSize;
            lbl.color = color;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.horizontalOverflow = HorizontalWrapMode.Overflow;
            lbl.verticalOverflow = VerticalWrapMode.Overflow;
            return go.GetComponent<Button>();
        }

        /// <summary>显示模式选择界面</summary>
        private void ShowModeSelection()
        {
            ClearModeSelection();

            var subtitle = NewLabel("SubTitle", new Vector2(470, 200), new Vector2(500, 40), "请选择模式", 22, new Color(0.8f, 0.8f, 0.9f), transform);
            subtitle.alignment = TextAnchor.MiddleCenter;
            _modeSelectionNodes.Add(subtitle.gameObject);

            // 玩家模式按钮
            var btnPlayer = NewButton("BtnPlayer", new Vector2(545, 320), new Vector2(350, 65), "玩家模式", 22, new Color(0.6f, 0.9f, 1.0f), transform);
            btnPlayer.onClick.AddListener(OnEnterPlayerMode);
            _modeSelectionNodes.Add(btnPlayer.gameObject);

            var playerDesc = NewLabel("PlayerDesc", new Vector2(545, 395), new Vector2(350, 25), "正常游戏体验，不含开发者工具", 13, new Color(0.5f, 0.5f, 0.6f), transform);
            playerDesc.alignment = TextAnchor.MiddleCenter;
            _modeSelectionNodes.Add(playerDesc.gameObject);

            // 管理员模式按钮
            var btnAdmin = NewButton("BtnAdmin", new Vector2(545, 460), new Vector2(350, 65), "管理员模式", 22, new Color(0.5f, 1.0f, 0.6f), transform);
            btnAdmin.onClick.AddListener(OnEnterAdminMode);
            _modeSelectionNodes.Add(btnAdmin.gameObject);

            var adminDesc = NewLabel("AdminDesc", new Vector2(545, 535), new Vector2(350, 25), "开发者工具 + 数据管理 + 物资发放（测试用）", 13, new Color(0.5f, 0.5f, 0.6f), transform);
            adminDesc.alignment = TextAnchor.MiddleCenter;
            _modeSelectionNodes.Add(adminDesc.gameObject);
        }

        private void ClearModeSelection()
        {
            foreach (var node in _modeSelectionNodes)
            {
                if (node != null) Destroy(node);
            }
            _modeSelectionNodes.Clear();
        }

        /// <summary>进入玩家模式</summary>
        private void OnEnterPlayerMode()
        {
            _currentMode = "player";
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LastMenuMode = "player";
                // 切换到玩家存档（数据隔离）
                if (PlayerSaveManager.Instance != null && PlayerSaveManager.Instance.CurrentSlot.ToString() != GameManager.MODE_SLOT_PLAYER)
                    PlayerSaveManager.Instance.LoadSlot(int.TryParse(GameManager.MODE_SLOT_PLAYER, out var _ms3) ? _ms3 : 0);
            }
            ClearModeSelection();
            BuildMainMenu(false);
        }

        /// <summary>进入管理员模式</summary>
        private void OnEnterAdminMode()
        {
            _currentMode = "admin";
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LastMenuMode = "admin";
                // 切换到管理员存档（数据隔离）
                if (PlayerSaveManager.Instance != null && PlayerSaveManager.Instance.CurrentSlot.ToString() != GameManager.MODE_SLOT_ADMIN)
                    PlayerSaveManager.Instance.LoadSlot(int.TryParse(GameManager.MODE_SLOT_ADMIN, out var _ms4) ? _ms4 : 0);
            }
            ClearModeSelection();
            BuildMainMenu(true);
        }

        /// <summary>返回模式选择界面</summary>
        private void OnBackToModeSelection()
        {
            _currentMode = "";
            if (GameManager.Instance != null)
                GameManager.Instance.LastMenuMode = "";

            // 清空主菜单按钮
            foreach (var node in _menuInteractiveNodes)
            {
                if (node != null) Destroy(node);
            }
            _menuInteractiveNodes.Clear();
            ShowModeSelection();
        }

        /// <summary>构建主菜单按钮</summary>
        /// <param name="isAdmin">是否显示管理员专属按钮</param>
        private void BuildMainMenu(bool isAdmin)
        {
            // 模式标识
            if (isAdmin)
            {
                var modeTag = NewLabel("ModeTag", new Vector2(1150, 15), new Vector2(260, 30), "【管理员模式】", 16, new Color(0.5f, 1.0f, 0.6f), transform);
                modeTag.alignment = TextAnchor.MiddleRight;
                _menuInteractiveNodes.Add(modeTag.gameObject);
            }

            // 开始比赛按钮
            var btnStart = NewButton("BtnStart", new Vector2(545, 260), new Vector2(350, 55), "开始比赛", 14, Color.white, transform);
            btnStart.onClick.AddListener(OnStartMatch);
            _menuInteractiveNodes.Add(btnStart.gameObject);

            // 角色系统按钮
            var btnChars = NewButton("BtnCharacters", new Vector2(545, 335), new Vector2(350, 55), "角色系统", 14, Color.white, transform);
            btnChars.onClick.AddListener(OnOpenCharacters);
            _menuInteractiveNodes.Add(btnChars.gameObject);

            // 元灵系统按钮
            var btnSpirits = NewButton("BtnSpirits", new Vector2(545, 410), new Vector2(350, 55), "元灵系统", 14, Color.white, transform);
            btnSpirits.onClick.AddListener(OnOpenSpirits);
            _menuInteractiveNodes.Add(btnSpirits.gameObject);

            // 基地按钮
            var btnBase = NewButton("BtnBase", new Vector2(545, 485), new Vector2(350, 55), "基地", 14, Color.white, transform);
            btnBase.onClick.AddListener(OnOpenBase);
            _menuInteractiveNodes.Add(btnBase.gameObject);

            // 交易按钮
            var btnTrade = NewButton("BtnTrade", new Vector2(545, 560), new Vector2(350, 55), "交易", 14, Color.white, transform);
            btnTrade.onClick.AddListener(OnOpenTrade);
            _menuInteractiveNodes.Add(btnTrade.gameObject);

            // 奖励开关按钮（所有玩家可用）
            bool rewardOn = RewardSystem.RewardEnabledStatic();
            var btnReward = NewButton("BtnReward", new Vector2(545, 635), new Vector2(350, 45), "比赛奖励: " + (rewardOn ? "已开启" : "已关闭"), 16, rewardOn ? new Color(0.9f, 0.8f, 0.3f) : new Color(0.6f, 0.6f, 0.6f), transform);
            btnReward.onClick.AddListener(OnToggleReward);
            _menuInteractiveNodes.Add(btnReward.gameObject);

            // 管理员专属：开发者工具按钮
            if (isAdmin)
            {
                var btnDev = NewButton("BtnDev", new Vector2(545, 700), new Vector2(350, 55), "快捷设置（开发者）", 14, new Color(0.3f, 0.9f, 0.5f), transform);
                btnDev.onClick.AddListener(OnOpenDevSettings);
                _menuInteractiveNodes.Add(btnDev.gameObject);

                // 管理员模式下返回按钮再往下挪一点
                var btnBackMode = NewButton("BtnBackMode", new Vector2(545, 775), new Vector2(350, 45), "← 返回模式选择", 16, new Color(0.7f, 0.7f, 0.8f), transform);
                btnBackMode.onClick.AddListener(OnBackToModeSelection);
                _menuInteractiveNodes.Add(btnBackMode.gameObject);
            }
            else
            {
                // 玩家模式返回按钮
                var btnBackMode = NewButton("BtnBackMode", new Vector2(545, 700), new Vector2(350, 45), "← 返回模式选择", 16, new Color(0.7f, 0.7f, 0.8f), transform);
                btnBackMode.onClick.AddListener(OnBackToModeSelection);
                _menuInteractiveNodes.Add(btnBackMode.gameObject);
            }
        }

        // 隐藏主菜单交互节点（打开子界面时调用）
        private void HideMenuInteractiveNodes()
        {
            foreach (var node in _menuInteractiveNodes)
            {
                if (node != null) node.SetActive(false);
            }
        }

        // 恢复主菜单交互节点（关闭子界面时调用）
        private void ShowMenuInteractiveNodes()
        {
            foreach (var node in _menuInteractiveNodes)
            {
                if (node != null) node.SetActive(true);
            }
        }

        private void OnStartMatch()
        {
            // 切换到备战场景（目前直接进入比赛）
            SceneManager.LoadScene("TestBattle");
        }

        private void OnOpenCharacters()
        {
            // 已打开且可见 → 不做任何事（角色系统有关闭按钮）
            if (_charUI != null && _charUI.gameObject.activeSelf) return;

            // 已打开但隐藏（不应发生，角色系统关闭时会 Destroy）→ 清理后重建
            if (_charUI != null)
            {
                Destroy(_charUI.gameObject);
                _charUI = null;
            }

            // 创建新的角色系统界面
            var go = new GameObject("CharacterSystem", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _charUI = go.AddComponent<CharacterSystem>();
            SetFullRect(go.GetComponent<RectTransform>());

            // 隐藏主菜单按钮，避免鼠标穿透
            HideMenuInteractiveNodes();

            // 关闭时恢复按钮并清理引用
            _charUI.Closed += () =>
            {
                ShowMenuInteractiveNodes();
                _charUI = null;
            };
        }

        private void OnOpenSpirits()
        {
            // 已打开且可见 → 隐藏
            if (_spiritUI != null && _spiritUI.gameObject.activeSelf)
            {
                _spiritUI.gameObject.SetActive(false);
                return;
            }

            // 已打开但隐藏 → 显示并刷新数据
            if (_spiritUI != null)
            {
                // Reflection to call RefreshData if available
                var mi = _spiritUI.GetType().GetMethod("RefreshData");
                if (mi != null) mi.Invoke(_spiritUI, null);
                _spiritUI.gameObject.SetActive(true);
                return;
            }

            // 首次打开 — SpiritUI 类型暂未实现，留空
            // var go = new GameObject("SpiritUI", typeof(RectTransform));
            // go.transform.SetParent(transform, false);
            // _spiritUI = go.AddComponent<SpiritUI>();
            // SetFullRect(go.GetComponent<RectTransform>());

            Debug.Log("[Main] 元灵系统已打开");
        }

        private void OnOpenBase()
        {
            if (_baseUI != null && _baseUI.gameObject.activeSelf) return;

            if (_baseUI != null)
            {
                Destroy(_baseUI.gameObject);
                _baseUI = null;
            }

            var go = new GameObject("BaseUI", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _baseUI = go.AddComponent<BaseSystem>();
            SetFullRect(go.GetComponent<RectTransform>());

            HideMenuInteractiveNodes();

            _baseUI.Closed += () =>
            {
                ShowMenuInteractiveNodes();
                _baseUI = null;
            };
        }

        private void OnOpenDevSettings()
        {
            // DevSettings 类型未实现，留空
            // var devPanelGo = new GameObject("DevSettings", typeof(RectTransform));
            // devPanelGo.transform.SetParent(transform, false);
            // var devPanel = devPanelGo.AddComponent<DevSettingsMain>();
            // SetFullRect(devPanelGo.GetComponent<RectTransform>());
            // devPanel.Closed += () => Destroy(devPanelGo);
            Debug.Log("[Main] 快捷设置系统已打开");
        }

        private void OnToggleReward()
        {
            bool current = RewardSystem.RewardEnabledStatic();
            RewardSystem.RewardSetEnabledStatic(!current);
            // 刷新菜单按钮显示
            RebuildRewardButton();
        }

        private void RebuildRewardButton()
        {
            Transform btn = transform.Find("BtnReward");
            if (btn != null)
            {
                var txt = btn.GetComponentInChildren<Text>();
                if (txt != null)
                {
                    bool rewardOn = RewardSystem.RewardEnabledStatic();
                    txt.text = "比赛奖励: " + (rewardOn ? "已开启" : "已关闭");
                    txt.color = rewardOn ? new Color(0.9f, 0.8f, 0.3f) : new Color(0.6f, 0.6f, 0.6f);
                }
            }
        }

        private void OnOpenTrade()
        {
            Debug.Log("[Main] 交易 - 待实现");
        }
    }
}

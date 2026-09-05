using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using BattleBall.Battle;
using BattleBall.Core;

namespace BattleBall.UI
{
    /// <summary>
    /// 战斗UI桥接脚本
    /// 解决Battle程序集不能引用UI程序集的循环依赖问题
    /// BattleManager通过事件通知，本脚本负责创建/销毁PreparationUI和MatchResultUI
    /// 挂载在TestBattle场景中，或由BattleManager自动创建
    /// </summary>
    public class BattleUIBridge : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            // 只在战斗场景自动创建
            if (SceneManager.GetActiveScene().name == "TestBattle")
            {
                if (FindObjectOfType<BattleUIBridge>() == null)
                {
                    var go = new GameObject("BattleUIBridge");
                    go.AddComponent<BattleUIBridge>();
                }
            }
        }

        private BattleManager _bm;
        private PreparationUI _prepUI;
        private MatchResultUI _resultUI;
        private Canvas _canvas;

        protected virtual void Start()
        {
            _bm = BattleManager.Instance;
            if (_bm == null)
            {
                Debug.LogWarning("[Bridge] BattleManager.Instance 为空");
                return;
            }
            // 确保有Canvas
            _EnsureCanvas();
            // 监听BattleManager事件
            _bm.ShowPreparationRequested += OnShowPreparation;
            _bm.HidePreparationRequested += OnHidePreparation;
            _bm.ShowResultRequested += OnShowResult;
            Debug.Log("[Bridge] 已挂载，监听BattleManager事件");
        }

        protected virtual void OnDestroy()
        {
            if (_bm != null)
            {
                _bm.ShowPreparationRequested -= OnShowPreparation;
                _bm.HidePreparationRequested -= OnHidePreparation;
                _bm.ShowResultRequested -= OnShowResult;
            }
        }

        private void _EnsureCanvas()
        {
            _canvas = FindObjectOfType<Canvas>();
            if (_canvas == null)
            {
                var go = new GameObject("UICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                _canvas = go.GetComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1440, 900);
            }
        }

        private void OnShowPreparation(bool isHalfTime)
        {
            if (_prepUI != null) { Destroy(_prepUI.gameObject); _prepUI = null; }
            var go = new GameObject("PreparationUI", typeof(RectTransform));
            _prepUI = go.AddComponent<PreparationUI>();
            go.GetComponent<RectTransform>().SetParent(_canvas.transform, false);
            // 传入球员数据
            var players = _bm.GetTeamAPlayers();
            _prepUI.LoadBattleData(players);
            _prepUI.SetHalfTimeMode(isHalfTime);
            // 监听备战界面事件
            _prepUI.MatchStartedFromPrep += () => _bm.OnPrepMatchStarted();
            _prepUI.BackToMenuRequested += () => _bm.OnBackToMenu();
            Debug.Log("[Bridge] 备战界面已创建 (中场=" + isHalfTime + ")");
        }

        private void OnHidePreparation()
        {
            if (_prepUI != null) { Destroy(_prepUI.gameObject); _prepUI = null; }
            Debug.Log("[Bridge] 备战界面已隐藏");
        }

        private void OnShowResult(int scoreA, int scoreB, string result)
        {
            if (_resultUI != null) { Destroy(_resultUI.gameObject); _resultUI = null; }
            var go = new GameObject("MatchResultUI", typeof(RectTransform));
            _resultUI = go.AddComponent<MatchResultUI>();
            go.GetComponent<RectTransform>().SetParent(_canvas.transform, false);
            _resultUI.ResultConfirmed += () =>
            {
                Destroy(_resultUI.gameObject); _resultUI = null;
                SceneManager.LoadScene("MainMenu");
            };
            Debug.Log("[Bridge] 结算界面已创建 A:" + scoreA + " B:" + scoreB + " " + result);
        }
    }
}

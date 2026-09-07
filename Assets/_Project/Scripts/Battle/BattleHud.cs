// ================================================================
// 决竞球 CLEAN_v2: BattleHud.cs [BattleBall.Battle]
// 比赛内 HUD：顶部比分/时间栏 + 底部己方(TeamA)球员状态面板
// 由 BattleUIBridge 创建并挂到战斗 Canvas 上；
// 备战/中场/结算界面弹出时由 Bridge 隐藏，比赛进行时显示。
// 数据源：BattleManager(scoreA/scoreB/team_a_players) + GameManager(MatchTime)
// ================================================================
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;
using BattleBall.Core;

namespace BattleBall.Battle
{

    public class BattleHud : MonoBehaviour
    {
        public Canvas canvas;
        public List<Transform> teamPlayers = new List<Transform>();
        public List<Transform> enemyPlayers = new List<Transform>();
        public int scoreA=0, scoreB=0; public float matchTime = 0f;

        // ===== UGUI 控件引用 =====
        private RectTransform _root;
        private Font _font;
        private Text _timeLabel;
        private Text _scoreLabel;
        private RectTransform _bottomPanel;
        private readonly List<RectTransform> _cardRts = new List<RectTransform>();
        private readonly List<Text> _cardNames = new List<Text>();
        private readonly List<Image> _hpFills = new List<Image>();
        private readonly List<Image> _hpBacks = new List<Image>();
        private readonly List<Image> _seFills = new List<Image>();   // 元灵能量条(蓝)
        private readonly List<Image> _seBacks = new List<Image>();
        private readonly List<Text> _hpTexts = new List<Text>();
        private readonly List<Text> _seTexts = new List<Text>();
        private readonly List<GameObject> _ballBadges = new List<GameObject>();
        private readonly List<Image> _cardBacks = new List<Image>();
        // ===== 元灵技能框(每卡3个, 动态显示冷却, 对应GD原版) =====
        private class SkillFrame
        {
            public Image bg;               // 框底(颜色区分就绪/空)
            public RectTransform overlay;  // 冷却遮罩(自顶向下覆盖)
            public Text nameTxt;           // 技能名缩写
            public Text cdTxt;             // 冷却剩余秒数
            public string curSkillId;      // 当前显示的技能ID(变更时才刷新名称)
        }
        private readonly List<List<SkillFrame>> _skillFrames = new List<List<SkillFrame>>();
        // 反射查询 SpiritSystemManager.get_skill_cooldown (Battle 程序集不可直接引用 Systems 程序集)
        private MonoBehaviour _spiritSys;
        private MethodInfo _miGetCd;
        private List<PlayerController> _cachedPlayers = new List<PlayerController>();
        private float _refreshTimer = 0f;
        private const float REFRESH_INTERVAL = 0.25f;

        protected T GetNode<T>(string path) where T : Component { var t = transform.Find(path); return t == null ? null : t.GetComponent<T>(); }
        protected GameObject GetNode(string path) { var t = transform.Find(path); return t == null ? null : t.gameObject; }
        protected Coroutine CreateTimer(float seconds, Action cb) { return StartCoroutine(_TimerCo(seconds, cb)); }
        private IEnumerator _TimerCo(float s, Action cb) { yield return new WaitForSeconds(s); cb?.Invoke(); }
        protected void CallDeferred(Action cb) { StartCoroutine(_CallDeferredCo(cb)); }
        private IEnumerator _CallDeferredCo(Action cb) { yield return null; cb?.Invoke(); }
        protected static float randf() { return UnityEngine.Random.value; }
        protected static float randf_range(float a, float b) { return UnityEngine.Random.Range(a, b); }
        protected void Emit(Action h) { h?.Invoke(); }
        protected void Emit<T1>(Action<T1> h, T1 a) { h?.Invoke(a); }
        protected void Emit<T1,T2>(Action<T1,T2> h, T1 a, T2 b) { h?.Invoke(a, b); }
        protected void Emit<T1,T2,T3>(Action<T1,T2,T3> h, T1 a, T2 b, T3 c) { h?.Invoke(a, b, c); }

        protected virtual void Update()
        {
            // 比赛时间优先取 GameManager 的权威值，取不到才本地累加
            if (GameManager.Instance != null)
                matchTime = GameManager.Instance.MatchTime;
            else
                matchTime += Time.deltaTime;

            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= REFRESH_INTERVAL)
            {
                _refreshTimer = 0f;
                RefreshData();
            }
        }

        public virtual void setup_players(List<Transform> team, List<Transform> enemies)
        {
            teamPlayers = team ?? new List<Transform>();
            enemyPlayers = enemies ?? new List<Transform>();
            _SyncPlayersFromTransforms();
            _RebuildCardsIfNeeded();
        }

        public virtual void show_skill_toast(string pn, string sn, float d=1.5f)
        {
            Debug.Log(string.Format("[HUD] 技能提示 {0}:{1}", pn, sn));
        }

        protected virtual void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        protected virtual void Start()
        {
            _root = GetComponent<RectTransform>();
            if (_root == null) _root = gameObject.AddComponent<RectTransform>();
            // 根节点铺满父 Canvas，与其它系统 UI 一致
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;
            _root.pivot = new Vector2(0f, 1f);
            BuildHud();
            _SyncPlayersFromManager();
            _RebuildCardsIfNeeded();
            RefreshData();
        }

        // ===== 构建 =====

        private void BuildHud()
        {
            // --- 顶部比分/时间栏（居中顶部） ---
            var topBar = _NewRect("TopBar", _root);
            topBar.anchorMin = new Vector2(0.5f, 1f);
            topBar.anchorMax = new Vector2(0.5f, 1f);
            topBar.pivot = new Vector2(0.5f, 1f);
            topBar.anchoredPosition = new Vector2(0f, 0f);
            topBar.sizeDelta = new Vector2(420f, 44f);
            var topImg = topBar.gameObject.AddComponent<Image>();
            topImg.color = new Color(0.08f, 0.08f, 0.12f, 0.85f);

            _scoreLabel = _NewLabel("Score", topBar, new Vector2(0f, 0f), new Vector2(240f, 44f), "0 : 0", 26, new Color(1f, 0.9f, 0.3f));
            _scoreLabel.alignment = TextAnchor.MiddleCenter;
            var scoreRt = _scoreLabel.rectTransform;
            scoreRt.anchorMin = new Vector2(0.5f, 0.5f);
            scoreRt.anchorMax = new Vector2(0.5f, 0.5f);
            scoreRt.pivot = new Vector2(0.5f, 0.5f);
            scoreRt.anchoredPosition = new Vector2(0f, 0f);

            _timeLabel = _NewLabel("Time", topBar, new Vector2(0f, 0f), new Vector2(120f, 44f), "00:00", 18, Color.white);
            _timeLabel.alignment = TextAnchor.MiddleRight;
            var timeRt = _timeLabel.rectTransform;
            timeRt.anchorMin = new Vector2(1f, 0.5f);
            timeRt.anchorMax = new Vector2(1f, 0.5f);
            timeRt.pivot = new Vector2(1f, 0.5f);
            timeRt.anchoredPosition = new Vector2(-10f, 0f);

            var teamLabel = _NewLabel("TeamTag", topBar, new Vector2(0f, 0f), new Vector2(60f, 44f), "A队", 16, new Color(0.5f, 0.8f, 1f));
            teamLabel.alignment = TextAnchor.MiddleLeft;
            var teamRt = teamLabel.rectTransform;
            teamRt.anchorMin = new Vector2(0f, 0.5f);
            teamRt.anchorMax = new Vector2(0f, 0.5f);
            teamRt.pivot = new Vector2(0f, 0.5f);
            teamRt.anchoredPosition = new Vector2(10f, 0f);

            // --- 底部己方球员状态面板 ---
            _bottomPanel = _NewRect("BottomPanel", _root);
            _bottomPanel.anchorMin = new Vector2(0.5f, 0f);
            _bottomPanel.anchorMax = new Vector2(0.5f, 0f);
            _bottomPanel.pivot = new Vector2(0.5f, 0f);
            _bottomPanel.anchoredPosition = new Vector2(0f, 8f);
            _bottomPanel.sizeDelta = new Vector2(720f, 104f);
        }

        private void _SyncPlayersFromManager()
        {
            var bm = BattleManager.Instance;
            if (bm != null)
            {
                // 注意：bm.team_a_players 从不填充，正确数据源是 GetTeamAPlayers()（从 teamA 动态构建）
                var players = bm.GetTeamAPlayers();
                if (players != null && players.Count > 0)
                    _cachedPlayers = players;
            }
        }

        private void _SyncPlayersFromTransforms()
        {
            var list = new List<PlayerController>();
            foreach (var t in teamPlayers)
            {
                if (t == null) continue;
                var pc = t.GetComponent<PlayerController>();
                if (pc != null) list.Add(pc);
            }
            if (list.Count > 0) _cachedPlayers = list;
        }

        private void _RebuildCardsIfNeeded()
        {
            if (_bottomPanel == null) return;
            if (_cardRts.Count == _cachedPlayers.Count) return;

            // 清空旧卡片
            for (int i = _bottomPanel.childCount - 1; i >= 0; i--)
                Destroy(_bottomPanel.GetChild(i).gameObject);
            _cardRts.Clear(); _cardNames.Clear(); _hpFills.Clear(); _hpBacks.Clear();
            _seFills.Clear(); _seBacks.Clear(); _hpTexts.Clear(); _seTexts.Clear();
            _ballBadges.Clear(); _cardBacks.Clear(); _skillFrames.Clear();

            int count = _cachedPlayers.Count;
            if (count == 0) return;

            const float cardW = 226f, cardH = 104f, gap = 12f;
            float totalW = count * cardW + (count - 1) * gap;

            for (int i = 0; i < count; i++)
            {
                var card = _NewRect("PlayerCard_" + i, _bottomPanel);
                card.anchorMin = new Vector2(0.5f, 0.5f);
                card.anchorMax = new Vector2(0.5f, 0.5f);
                card.pivot = new Vector2(0.5f, 0.5f);
                float x = -totalW / 2f + cardW / 2f + i * (cardW + gap);
                card.anchoredPosition = new Vector2(x, 0f);
                card.sizeDelta = new Vector2(cardW, cardH);
                var back = card.gameObject.AddComponent<Image>();
                back.color = new Color(0.12f, 0.14f, 0.2f, 0.88f);
                _cardRts.Add(card);
                _cardBacks.Add(back);

                // 名称（左上）
                var name = _NewLabel("Name", card, new Vector2(8f, 6f), new Vector2(130f, 22f), "球员" + (i + 1), 15, Color.white);
                name.alignment = TextAnchor.MiddleLeft;
                _cardNames.Add(name);

                // 持球徽章（右上）
                var badgeGo = new GameObject("BallBadge", typeof(RectTransform), typeof(Image));
                var brt = badgeGo.GetComponent<RectTransform>();
                brt.SetParent(card, false);
                brt.anchorMin = new Vector2(1f, 1f);
                brt.anchorMax = new Vector2(1f, 1f);
                brt.pivot = new Vector2(1f, 1f);
                brt.anchoredPosition = new Vector2(-8f, -8f);
                brt.sizeDelta = new Vector2(70f, 22f);
                var badgeImg = badgeGo.GetComponent<Image>();
                badgeImg.color = new Color(1f, 0.75f, 0.2f, 0.95f);
                var badgeTxt = _NewLabel("Txt", brt, new Vector2(0f, 0f), new Vector2(70f, 22f), "持球", 13, new Color(0.2f, 0.12f, 0f));
                badgeTxt.alignment = TextAnchor.MiddleCenter;
                var btRt = badgeTxt.rectTransform;
                btRt.anchorMin = new Vector2(0.5f, 0.5f);
                btRt.anchorMax = new Vector2(0.5f, 0.5f);
                btRt.pivot = new Vector2(0.5f, 0.5f);
                btRt.anchoredPosition = Vector2.zero;
                badgeGo.SetActive(false);
                _ballBadges.Add(badgeGo);

                // 体力条(绿, GD原版: 体力值即血量)
                var (hpBack, hpFill) = _BuildBar(card, "HP", 8f, 30f, cardW - 16f, 17f,
                    new Color(0.06f, 0.2f, 0.08f, 0.9f), new Color(0.3f, 0.85f, 0.35f), "体力");
                _hpBacks.Add(hpBack); _hpFills.Add(hpFill);

                // 元灵能量条(蓝)
                var (seBack, seFill) = _BuildBar(card, "SP", 8f, 50f, cardW - 16f, 17f,
                    new Color(0.06f, 0.12f, 0.28f, 0.9f), new Color(0.3f, 0.55f, 0.95f), "灵能");
                _seBacks.Add(seBack); _seFills.Add(seFill);

                // 元灵技能框 ×3 (动态显示冷却)
                var frames = new List<SkillFrame>();
                for (int s = 0; s < 3; s++)
                {
                    var fr = _NewRect("Skill_" + s, card);
                    fr.anchorMin = new Vector2(0f, 1f);
                    fr.anchorMax = new Vector2(0f, 1f);
                    fr.pivot = new Vector2(0f, 1f);
                    fr.anchoredPosition = new Vector2(8f + s * 72f, -72f);
                    fr.sizeDelta = new Vector2(66f, 26f);
                    var f = new SkillFrame();
                    f.bg = fr.gameObject.AddComponent<Image>();
                    f.bg.color = new Color(0.12f, 0.12f, 0.14f, 0.85f);

                    f.nameTxt = _NewLabel("Name", fr, new Vector2(0f, 0f), new Vector2(66f, 26f), "空", 10, new Color(0.75f, 0.85f, 1f));
                    f.nameTxt.alignment = TextAnchor.MiddleCenter;
                    var nRt = f.nameTxt.rectTransform;
                    nRt.anchorMin = nRt.anchorMax = new Vector2(0.5f, 0.5f);
                    nRt.pivot = new Vector2(0.5f, 0.5f);
                    nRt.anchoredPosition = Vector2.zero;

                    // 冷却遮罩(自顶向下覆盖)
                    var ovGo = new GameObject("CdMask", typeof(RectTransform), typeof(Image));
                    var ov = ovGo.GetComponent<RectTransform>();
                    ov.SetParent(fr, false);
                    ov.anchorMin = new Vector2(0f, 1f);
                    ov.anchorMax = new Vector2(1f, 1f);
                    ov.pivot = new Vector2(0.5f, 1f);
                    ov.anchoredPosition = Vector2.zero;
                    ov.sizeDelta = new Vector2(0f, 0f);
                    var ovImg = ovGo.GetComponent<Image>();
                    ovImg.color = new Color(0f, 0f, 0f, 0.68f);
                    ovImg.raycastTarget = false;
                    f.overlay = ov;

                    f.cdTxt = _NewLabel("Cd", fr, new Vector2(0f, 0f), new Vector2(66f, 26f), "", 13, Color.white);
                    f.cdTxt.alignment = TextAnchor.MiddleCenter;
                    var cRt = f.cdTxt.rectTransform;
                    cRt.anchorMin = cRt.anchorMax = new Vector2(0.5f, 0.5f);
                    cRt.pivot = new Vector2(0.5f, 0.5f);
                    cRt.anchoredPosition = Vector2.zero;

                    f.curSkillId = null;
                    frames.Add(f);
                }
                _skillFrames.Add(frames);
            }
        }

        /// <summary>构建一条状态条：底 + 填充 + 标题 + 数值文本，返回(底, 填充)</summary>
        private Tuple<Image, Image> _BuildBar(RectTransform parent, string tag, float x, float y, float w, float h,
            Color backColor, Color fillColor, string title)
        {
            var backGo = new GameObject("Bar_" + tag, typeof(RectTransform), typeof(Image));
            var back = backGo.GetComponent<RectTransform>();
            back.SetParent(parent, false);
            back.anchorMin = new Vector2(0f, 1f);
            back.anchorMax = new Vector2(0f, 1f);
            back.pivot = new Vector2(0f, 1f);
            back.anchoredPosition = new Vector2(x, -y);
            back.sizeDelta = new Vector2(w, h);
            var backImg = backGo.GetComponent<Image>();
            backImg.color = backColor;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fill = fillGo.GetComponent<RectTransform>();
            fill.SetParent(back, false);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = new Vector2(2f, 2f);
            fill.offsetMax = new Vector2(2f, -2f);
            fill.sizeDelta = new Vector2(w - 4f, 0f);
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.color = fillColor;
            fillImg.raycastTarget = false;

            var titleTxt = _NewLabel("Title", back, new Vector2(4f, 1f), new Vector2(36f, h - 2f), title, 11, Color.white);
            titleTxt.alignment = TextAnchor.MiddleLeft;
            var titleRt = titleTxt.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 0f);
            titleRt.anchorMax = new Vector2(0f, 1f);
            titleRt.pivot = new Vector2(0f, 0.5f);
            titleRt.anchoredPosition = new Vector2(4f, 0f);
            titleTxt.color = new Color(1f, 1f, 1f, 0.85f);

            var valTxt = _NewLabel("Val", back, new Vector2(0f, 0f), new Vector2(70f, h - 2f), "", 11, Color.white);
            valTxt.alignment = TextAnchor.MiddleRight;
            var valRt = valTxt.rectTransform;
            valRt.anchorMin = new Vector2(1f, 0f);
            valRt.anchorMax = new Vector2(1f, 1f);
            valRt.pivot = new Vector2(1f, 0.5f);
            valRt.anchoredPosition = new Vector2(-4f, 0f);

            if (tag == "HP") _hpTexts.Add(valTxt); else _seTexts.Add(valTxt);

            return Tuple.Create(backImg, fillImg);
        }

        // ===== 数据刷新 =====

        /// <summary>立即同步球员列表并刷新一次显示（Bridge 在重新激活时调用）</summary>
        public void RefreshNow()
        {
            _SyncPlayersFromManager();
            _RebuildCardsIfNeeded();
            RefreshData();
        }

        private void RefreshData()
        {
            // 球员列表可能延迟生成（场景加载后），每周期尝试同步
            if (_cachedPlayers.Count == 0) _SyncPlayersFromManager();
            _RebuildCardsIfNeeded();

            var bm = BattleManager.Instance;
            if (bm != null)
            {
                scoreA = bm.scoreA;
                scoreB = bm.scoreB;
            }
            if (_scoreLabel != null)
                _scoreLabel.text = string.Format("{0} : {1}", scoreA, scoreB);
            if (_timeLabel != null)
                _timeLabel.text = string.Format("{0:00}:{1:00}", Mathf.FloorToInt(Mathf.Max(0f, matchTime) / 60f), Mathf.FloorToInt(Mathf.Max(0f, matchTime)) % 60);

            for (int i = 0; i < _cardRts.Count && i < _cachedPlayers.Count; i++)
            {
                var p = _cachedPlayers[i];
                if (p == null) continue;

                // 名称
                string pname = "球员" + (i + 1);
                if (p.charData != null && p.charData.TryGetValue("name", out var nm) && nm != null)
                    pname = nm.ToString();
                if (_cardNames[i] != null)
                    _cardNames[i].text = string.Format("{0}  位置{1}", pname, p.index + 1);

                // 血条
                float hp = Mathf.Max(0f, p.hp);
                float maxHp = Mathf.Max(1f, p.maxHp);
                _SetBar(_hpFills[i], hp / maxHp);
                if (i < _hpTexts.Count && _hpTexts[i] != null)
                    _hpTexts[i].text = string.Format("{0}/{1}", Mathf.CeilToInt(hp), Mathf.CeilToInt(maxHp));

                // 元灵能量条(蓝)
                float se = Mathf.Clamp01(p.spirit_energy / Mathf.Max(1f, p.max_spirit_energy));
                _SetBar(_seFills[i], se);
                if (i < _seTexts.Count && _seTexts[i] != null)
                    _seTexts[i].text = Mathf.CeilToInt(se * 100f).ToString();

                // 元灵技能框(动态冷却)
                _RefreshSkillFrames(i, p);

                // 持球徽章
                if (i < _ballBadges.Count && _ballBadges[i] != null)
                    _ballBadges[i].SetActive(p.is_carrying_ball);

                // 阵亡置灰
                bool dead = p.is_defeated;
                if (_cardNames[i] != null)
                    _cardNames[i].color = dead ? new Color(0.5f, 0.5f, 0.5f) : Color.white;
                if (i < _cardBacks.Count && _cardBacks[i] != null)
                    _cardBacks[i].color = dead
                        ? new Color(0.1f, 0.1f, 0.1f, 0.7f)
                        : new Color(0.12f, 0.14f, 0.2f, 0.88f);
            }
        }

        private void _SetBar(Image fill, float ratio)
        {
            if (fill == null) return;
            ratio = Mathf.Clamp01(ratio);
            var rt = fill.rectTransform;
            // fill 锚定左拉伸，sizeDelta.x 即宽度
            var parent = rt.parent as RectTransform;
            float maxW = parent != null ? parent.rect.width - 4f : 100f;
            rt.sizeDelta = new Vector2(Mathf.Max(0f, maxW * ratio), 0f);
        }

        // ===== 元灵技能框 =====

        private void _RefreshSkillFrames(int i, PlayerController p)
        {
            if (i >= _skillFrames.Count) return;
            var frames = _skillFrames[i];
            var skills = p != null ? p.equipped_skills : null;
            for (int s = 0; s < frames.Count; s++)
            {
                var f = frames[s];
                if (f == null || f.bg == null) continue;
                string sid = (skills != null && s < skills.Count && !string.IsNullOrEmpty(skills[s])) ? skills[s] : null;

                // 技能变更时才刷新名称
                if (sid != f.curSkillId)
                {
                    f.curSkillId = sid;
                    f.nameTxt.text = (sid == null) ? "空" : _SkillShortName(sid);
                }

                if (sid == null)
                {
                    f.bg.color = new Color(0.12f, 0.12f, 0.14f, 0.85f); // 空槽: 暗灰
                    f.cdTxt.text = "";
                    f.overlay.sizeDelta = new Vector2(0f, 0f);
                    continue;
                }

                float cd = _QuerySkillCooldown(p.GetInstanceID(), sid);
                if (cd > 0f)
                {
                    f.bg.color = new Color(0.1f, 0.15f, 0.26f, 0.92f);
                    f.cdTxt.text = Mathf.CeilToInt(cd).ToString();
                    float maxCd = Mathf.Max(0.1f, _SkillMaxCooldown(sid));
                    f.overlay.sizeDelta = new Vector2(0f, 26f * Mathf.Clamp01(cd / maxCd));
                }
                else
                {
                    f.bg.color = new Color(0.16f, 0.3f, 0.22f, 0.95f); // 就绪: 偏亮绿
                    f.cdTxt.text = "";
                    f.overlay.sizeDelta = new Vector2(0f, 0f);
                }
            }
        }

        private string _SkillShortName(string sid)
        {
            var sd = _GetSkillData(sid);
            if (sd != null && sd.TryGetValue("name", out var n) && n != null)
            {
                var sn = n.ToString();
                return sn.Length > 4 ? sn.Substring(0, 4) : sn;
            }
            return sid.Length > 4 ? sid.Substring(0, 4) : sid;
        }

        private float _SkillMaxCooldown(string sid)
        {
            var sd = _GetSkillData(sid);
            if (sd != null && sd.TryGetValue("cooldown", out var c) && c != null)
            {
                float r;
                if (float.TryParse(c.ToString(), out r) && r > 0f) return r;
            }
            return 5f;
        }

        private Dictionary<string, object> _GetSkillData(string sid)
        {
            var dm = DataManager.Instance;
            if (dm == null) return null;
            var sd = dm.GetSkillById(sid);
            return (sd != null && sd.Count > 0) ? sd : null;
        }

        /// <summary>反射查询 SpiritSystemManager.get_skill_cooldown(playerId, skillId)。playerId 约定 = GetInstanceID()（与 SpiritAIManager 一致）。</summary>
        private float _QuerySkillCooldown(int playerId, string skillId)
        {
            if (_spiritSys == null)
            {
                var go = GameObject.Find("SpiritSystemManager");
                if (go != null) _spiritSys = go.GetComponent<MonoBehaviour>();
            }
            if (_spiritSys == null) return 0f;
            if (_miGetCd == null) _miGetCd = _spiritSys.GetType().GetMethod("get_skill_cooldown");
            if (_miGetCd == null) return 0f;
            try
            {
                var r = _miGetCd.Invoke(_spiritSys, new object[] { playerId, skillId });
                if (r is float f) return f;
                if (r is double d) return (float)d;
                if (r is int ii) return (float)ii;
            }
            catch { }
            return 0f;
        }

        // ===== UGUI 工具 =====

        private RectTransform _NewRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        private Text _NewLabel(string name, RectTransform parent, Vector2 pos, Vector2 size, string text, int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(pos.x, -pos.y);
            rt.sizeDelta = size;
            var txt = go.GetComponent<Text>();
            txt.font = _font;
            txt.text = text;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.raycastTarget = false;
            return txt;
        }

        // ===== OnGUI 降级显示（无 Canvas 时兜底） =====
        protected virtual void OnGUI() {
            if (canvas != null) return;
            GUILayout.BeginArea(new Rect(10,10,360,100));
            GUILayout.Label(string.Format("时间 {0:00}:{1:00}   A {2} : {3} B", Mathf.FloorToInt(matchTime/60f), Mathf.FloorToInt(matchTime)%60, scoreA, scoreB));
            GUILayout.EndArea();
        }
    }

}

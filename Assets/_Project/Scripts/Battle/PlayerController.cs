using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using BattleBall.Core;

namespace BattleBall.Battle
{
    public enum PlayerRole { PF, SG, PG, SF, C }
    public enum PlayerState { Idle, Moving, PreKickoff, ReadyCatch, Stunned, Defeated }

    public class PlayerController : MonoBehaviour
    {
        public string team = "A";
        public int index = 0;
        public PlayerRole role = PlayerRole.PF;
        public string playerId = "player1";
        public string charId = "char_001";
        public string element = "none";
        public bool isPlayerControlled = false;

        public float moveSpeed = 2.0f;
        public float sprintMult = 1.6f;
        public float maxStamina = 100f;
        public float staminaDrain = 15f;
        public float staminaRegen = 12f;
        public float turnSpeed = 10f;
        public float maxHp = 100f;
        public float attackPower = 30f;
        public float defense = 20f;
        public float defenseFactor = 0.15f;
        public float resilience = 50f;
        public float spirit_energy = 100f;
        public float max_spirit_energy = 100f;
        public string spirit_id = "";
        public bool is_carrying_ball = false;
        public bool is_defeated = false;
        public float max_stamina = 100f;

        // 场地边界(已×2，对应球场模型放大2倍)
        public float clampXMin = -13f, clampXMax = 13f;
        public float clampZMin = -7.8f, clampZMax = 7.8f;

        public float hp, stamina;
        public bool isSprinting;
        public float sprintCooldown = 0f;
        public float SPRINT_COOLDOWN_TIME = 2f;

        public Vector3 facingDir = new Vector3(1, 0, 0);
        public PlayerState state = PlayerState.Idle;
        public Transform lastAttacker;
        public BallController carriedBall = null;
        public Dictionary<string, object> charData = new Dictionary<string, object>();

        /// <summary>UI 兼容 (PascalCase): 角色ID (源自 charData 或 index)</summary>
        public string CharacterId { get { return (charData != null && charData.ContainsKey("id")) ? charData["id"]?.ToString() : index.ToString(); } }
        /// <summary>UI 兼容 (PascalCase): 角色数据字典 (同 charData)</summary>
        public Dictionary<string, object> CharData { get { return charData; } }

        // ===== UI (PreparationUI 1365/1377) 精灵装备兼容 =====
        public virtual void EquipSpirit(Dictionary<string, object> spiritData) { Debug.Log("[PC] EquipSpirit: "+(spiritData != null ? spiritData.ContainsKey("id")? spiritData["id"] : "?" : "null")); }
        public virtual void UnequipSpirit() { Debug.Log("[PC] UnequipSpirit 调用"); }

        private float _staggerTimer = 0f;
        private float _knockbackTimer = 0f;
        private float _knockbackDuration = 0f;
        private float _knockbackStartVel = 0f;
        public Vector3 knockbackDir = Vector3.zero;

        public Dictionary<string, float> statuses = new Dictionary<string, float>();
        public Dictionary<string, Tuple<float, float>> buffs = new Dictionary<string, Tuple<float, float>>();

        public event Action<Transform> defeated;
        public event Action<PlayerState, PlayerState> stateChanged;
        public event Action<Transform, float, string> onDamaged;

        private Rigidbody _rb;

        protected virtual void Awake()
        {
            hp = maxHp; stamina = maxStamina;
            _rb = GetComponent<Rigidbody>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
            _rb.isKinematic = true; _rb.useGravity = false;
            // 确保有碰撞体(球的OnTriggerEnter需要球员有Collider才能触发传球/接球/伤害)
            var col = GetComponent<Collider>();
            if (col == null) {
                var sc = gameObject.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = 0.4f;
            }
        }

        public virtual void Initialize(string _charId, string _team, bool _controlled)
        {
            charId = _charId; team = _team.ToUpper(); isPlayerControlled = _controlled;
            var dm = DataManager.Instance;
            if (dm != null)
            {
                var cd = dm.get_character_by_id(charId);
                if (cd != null && cd.Count > 0)
                {
                    charData = cd;
                    if (cd.ContainsKey("speed"))          moveSpeed   = _F(cd["speed"])         * 0.03f;
                    if (cd.ContainsKey("attack"))         attackPower = _F(cd["attack"])        * 1.0f;
                    if (cd.ContainsKey("defense"))        defense     = _F(cd["defense"]);
                    if (cd.ContainsKey("defense_factor")) defenseFactor = Mathf.Clamp(_F(cd["defense_factor"]), 0f, 0.8f);
                    if (cd.ContainsKey("stamina"))        maxStamina  = _F(cd["stamina"]) * 1.25f;
                    if (cd.ContainsKey("resilience"))     resilience  = _F(cd["resilience"]);
                    if (cd.ContainsKey("spirit_preference")) element = cd["spirit_preference"].ToString().ToLower();
                    hp = maxHp; stamina = maxStamina;
                    object nm;
                    cd.TryGetValue("name", out nm);
                    Debug.Log("[Init] " + (nm != null ? nm.ToString() : charId)
                              + " speed=" + moveSpeed.ToString("F2") + " atk=" + attackPower + " elem=" + element);
                }
            }
        }

        private static float _F(object o) { if (o == null) return 0f; float r; return float.TryParse(o.ToString(), out r) ? r : 0f; }

        public bool IsStatusActive(string st) { return statuses.ContainsKey(st) && statuses[st] > 0f; }

        protected virtual void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (state == PlayerState.Defeated) return;

            if (_knockbackTimer > 0f)
            {
                _knockbackTimer -= dt;
                _TickAllTimers(dt);
                if (_knockbackDuration > 0.0001f)
                {
                    float progress = 1f - (_knockbackTimer / _knockbackDuration);
                    float curSpeed = _knockbackStartVel * (1f - progress);
                    transform.position += knockbackDir * curSpeed * dt;
                }
                _ClampToField();
                if (_knockbackTimer <= 0f) { _knockbackTimer = 0f; knockbackDir = Vector3.zero; }
                return;
            }

            if (_staggerTimer > 0f || IsStatusActive("stunned") || IsStatusActive("rooted"))
            {
                if (_staggerTimer > 0f) { _staggerTimer -= dt; if (_staggerTimer < 0f) _staggerTimer = 0f; }
                _TickAllTimers(dt);
                return;
            }

            _TickAllTimers(dt);
            if (sprintCooldown > 0f) { sprintCooldown -= dt; if (sprintCooldown < 0f) sprintCooldown = 0f; }

            if (!isPlayerControlled) { _ClampToField(); return; }

            float ix = 0f, iz = 0f;
            ix = Input.GetAxisRaw("Horizontal"); iz = Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(ix) < 0.01f) { if (Input.GetKey(KeyCode.A)) ix = -1f; else if (Input.GetKey(KeyCode.D)) ix = 1f; }
            if (Mathf.Abs(iz) < 0.01f) { if (Input.GetKey(KeyCode.W)) iz =  1f; else if (Input.GetKey(KeyCode.S)) iz = -1f; }

            Vector3 dir = new Vector3(ix, 0, iz);
            bool moving = dir.sqrMagnitude > 0.001f;
            if (moving) dir.Normalize();

            float effSpeed = _GetEffectiveValue("speed", moveSpeed);
            isSprinting = false;
            if (moving && Input.GetKey(KeyCode.LeftShift) && stamina > 0f && sprintCooldown <= 0f)
            {
                isSprinting = true;
                effSpeed *= sprintMult;
                stamina = Mathf.Max(0f, stamina - staminaDrain * dt);
                if (stamina <= 0f) { sprintCooldown = SPRINT_COOLDOWN_TIME; isSprinting = false; }
            }
            else if (!moving)
            {
                stamina = Mathf.Min(maxStamina, stamina + staminaRegen * dt);
            }

            if (moving)
            {
                transform.position += dir * effSpeed * dt;
                var target = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z), Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, turnSpeed * dt * 0.06f);
                facingDir = new Vector3(dir.x, 0, dir.z).normalized;
                if (state == PlayerState.Idle) SetState(PlayerState.Moving);
            }
            else if (state == PlayerState.Moving)
            {
                SetState(PlayerState.Idle);
            }

            _ClampToField();
        }

        public virtual float _GetEffectiveValue(string attr, float baseVal)
        {
            float mult = 1f;
            foreach (var kv in buffs)
            {
                var id = kv.Key.ToLower();
                var m = kv.Value.Item1;
                if ((attr == "speed" && id.Contains("spe")) ||
                    (attr == "attack" && id.Contains("atk")) ||
                    (attr == "defense" && id.Contains("def")))
                {
                    mult += m;
                }
            }
            return baseVal * mult;
        }

        void _TickAllTimers(float dt)
        {
            var keys = new List<string>(statuses.Keys);
            foreach (var k in keys) { statuses[k] -= dt; if (statuses[k] <= 0f) statuses.Remove(k); }
            var bk = new List<string>(buffs.Keys);
            foreach (var k in bk)
            {
                var t = buffs[k];
                float nr = t.Item2 - dt;
                if (nr <= 0f) buffs.Remove(k);
                else buffs[k] = Tuple.Create(t.Item1, nr);
            }
        }

        protected virtual void _ClampToField()
        {
            var p = transform.position;
            float cx = Mathf.Clamp(p.x, clampXMin, clampXMax);
            float cz = Mathf.Clamp(p.z, clampZMin, clampZMax);
            if (Mathf.Abs(cx - p.x) > 0.0001f || Mathf.Abs(cz - p.z) > 0.0001f)
            {
                transform.position = new Vector3(cx, 0f, cz);
            }
        }

        public virtual void SetState(PlayerState s) { if (s == state) return; var o = state; state = s; stateChanged?.Invoke(o, s); }

        public virtual Dictionary<string, object> take_damage(float rawDamage, Transform attacker = null, string attackerElement = "none")
        {
            float elementMult = 1f;
            var dm = DataManager.Instance;
            if (dm != null && !string.IsNullOrEmpty(element) && !string.IsNullOrEmpty(attackerElement) && attackerElement != "none")
            {
                elementMult = dm.get_counter_multiplier(attackerElement, element);
            }
            float effAtk = rawDamage * elementMult;
            float effDef = _GetEffectiveValue("defense", defense);
            float actual = Mathf.Max(1f, effAtk * (1f - defenseFactor) - effDef * 0.1f);
            hp = Mathf.Max(0f, hp - actual);
            lastAttacker = attacker;

            string effect = "none";
            float ratio = maxHp > 0f ? (hp / maxHp) : 0f;
            if (hp <= 0f) effect = "knockback_and_fly";
            else if (ratio < 0.3f) effect = "knockback2";
            else if (actual > maxHp * 0.15f) effect = "knockback1";

            onDamaged?.Invoke(attacker, actual, effect);
            Debug.Log(string.Format("[HP] {0} takes {1:0.#} (elem={2:0.##}) hp={3:0} eff={4}",
                name, actual, elementMult, hp, effect));

            if (hp <= 0f) { SetState(PlayerState.Defeated); defeated?.Invoke(transform); }
            return new Dictionary<string, object> { {"hp", hp}, {"defeated", hp <= 0f}, {"damage", actual}, {"effect", effect} };
        }

        public virtual void _apply_knockback(Transform attacker, float distance = 1.0f, float duration = 0.18f)
        {
            if (attacker == null) return;
            Vector3 dir = (transform.position - attacker.position).normalized;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            knockbackDir = dir;
            _knockbackDuration = Mathf.Max(0.05f, duration);
            _knockbackTimer = _knockbackDuration;
            _knockbackStartVel = distance / Mathf.Max(0.05f, duration);
        }

        public virtual bool HasBall() { return carriedBall != null; }

        public virtual void SetCarryingBall(BallController ball, bool carry)
        {
            carriedBall = carry ? ball : null;
        }

        public void AddStatus(string statusId, float duration) { statuses[statusId] = duration; }
        public void AddBuff(string buffId, float magnitude, float duration) { buffs[buffId] = Tuple.Create(magnitude, duration); }
        public void AddBuff(string target, string tagId, float duration, float value, float interval, string source) { Debug.Log("[BuffMgr compat] AddBuff 6-arg: " + tagId); }
        // === SpiritSystem compat methods ===
        public System.Collections.Generic.List<string> equipped_skills = new System.Collections.Generic.List<string>();
        public float next_skill_mult = 1f;
        public int skill_bonus_uses = 0;
        public float skill_cd_mult = 1f;
        public float skill_cost_mult = 1f;
        public Vector3 previous_position;
        public bool light_on = false;
        public System.Collections.Generic.List<object> tick_effects = new System.Collections.Generic.List<object>();
        public void ReturnToPrevious() { transform.position = previous_position; }
        public void TeleportTo(Vector3 pos) { previous_position = transform.position; transform.position = pos; }
        public void AddNextSkillMult(float mult) { next_skill_mult *= mult; }
        public float GetAndConsumeNextSkillMult() { float m = next_skill_mult; next_skill_mult = 1f; return m; }
        // ---- AddSkillBonusUses: 1参(默认全部) / 2参(指定技能ID) ----
        public void AddSkillBonusUses(int uses) { skill_bonus_uses += uses; }
        public void AddSkillBonusUses(string skill_id, int bonus) { skill_bonus_uses += bonus; }
        // ---- AddSkillCdMult: 1参 / 3参(带标签+持续时间) ----
        public void AddSkillCdMult(float mult) { skill_cd_mult *= mult; }
        public void AddSkillCdMult(string tag_id, float mult, float duration) { skill_cd_mult *= mult; }
        // ---- AddSkillCostMult: 1参 / 3参(带标签+持续时间) ----
        public void AddSkillCostMult(float mult) { skill_cost_mult *= mult; }
        public void AddSkillCostMult(string tag_id, float mult, float duration) { skill_cost_mult *= mult; }
        // ---- TurnOnLight: 无参 / 2参(状态+时间)返回bool / 3参(含选项字典) ----
        public void TurnOnLight() { light_on = true; }
        public virtual bool TurnOnLight(string status, float duration) { AddStatus(status, duration); light_on = true; return true; }
        public virtual void TurnOnLight(string status, float duration, Dictionary<string, object> opts) { AddStatus(status, duration); light_on = true; }
        // ---- TurnOffLight: 无参 / 1参(指定状态名) ----
        public void TurnOffLight() { light_on = false; }
        public virtual void TurnOffLight(string status) { if (statuses.ContainsKey(status)) statuses.Remove(status); light_on = false; }
        // ---- AddTickEffect: 4参(旧) / 4参新(id+type+value+duration) / 5参新(含修饰符列表) ----
        public void AddTickEffect(string tag_id, float duration, float tick_interval, float tick_value) { }
        public void AddTickEffect(string id, string type, float value, float duration) { tick_effects.Add(id + ":" + type); }
        public void AddTickEffect(string id, string type, float value, float duration, List<string> modifiers) { tick_effects.Add(id + ":" + type); }
        // ---- GetVisualRadius: SkillVisualManager 兼容 ----
        public virtual float GetVisualRadius() { return 0.4f; }
        public void _OnDefeated() { is_defeated = true; }
    }
}

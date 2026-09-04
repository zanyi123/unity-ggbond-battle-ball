using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using BattleBall.Core;
using BattleBall.Battle;

namespace BattleBall.Battle
{
    /// <summary>按原GD input_manager.gd 术语：左键瞄准投球、右键接球/冲刺、1/2/3切人、4/5/6技能、C取消、Tab切主控</summary>
    public class InputManager : MonoBehaviour
    {
        public PlayerController controlledPlayer;
        public PlayerController controlled_player { get { return controlledPlayer; } set { controlledPlayer = value; } }
        public bool match_started = true;
        public bool isAiming = false;
        public float aimCharge = 0f; // 0..1
        public float maxAimDistance = 8f; // 8m
        /// <summary>瞬发模式: 左键按下直接发球(对应GD field_tag_test.gd)。false=蓄力模式(对应input_manager.gd)</summary>
        public bool quickServe = true;

        public event Action<int> player_switch_requested; // index
        public event Action<Vector3, float> throw_requested; // direction, power(0..1)
        public event Action throw_cancelled;
        public event Action catch_state_entered;
        public event Action catch_state_exited;
        public event Action<int> skill_requested; // slot 0..2
        public event Action<int> skill_cancel_requested; // player_id
        public event Action<int> quick_command_requested; // 1..4

        public static InputManager Instance { get; protected set; }
        protected virtual void Awake() {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }
        protected virtual void OnDestroy() { if (Instance == this) Instance = null; }

        protected virtual void Update() {
            if (!match_started || controlledPlayer == null) return;

            // === 1/2/3 切人 ===
            if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchTo(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchTo(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchTo(2);
            // Tab 切下一控
            else if (Input.GetKeyDown(KeyCode.Tab)) {
                var all = GetTeamPlayers();
                int cur = all.FindIndex(p => p == controlledPlayer);
                int nx = (cur + 1) % Mathf.Max(1, all.Count);
                if (all.Count > 0) SwitchTo(nx);
            }
            // === 4/5/6 技能 ===
            if (Input.GetKeyDown(KeyCode.Alpha4)) skill_requested?.Invoke(0);
            else if (Input.GetKeyDown(KeyCode.Alpha5)) skill_requested?.Invoke(1);
            else if (Input.GetKeyDown(KeyCode.Alpha6)) skill_requested?.Invoke(2);
            // === C 取消技能 ===
            else if (Input.GetKeyDown(KeyCode.C)) {
                skill_cancel_requested?.Invoke(controlledPlayer.GetInstanceID());
                if (isAiming) {
                    isAiming = false; aimCharge = 0f;
                    throw_cancelled?.Invoke();
                }
            }
            // 快捷命令 7/8/9/0
            if (Input.GetKeyDown(KeyCode.Alpha7)) quick_command_requested?.Invoke(1);
            else if (Input.GetKeyDown(KeyCode.Alpha8)) quick_command_requested?.Invoke(2);
            else if (Input.GetKeyDown(KeyCode.Alpha9)) quick_command_requested?.Invoke(3);
            else if (Input.GetKeyDown(KeyCode.Alpha0)) quick_command_requested?.Invoke(4);

            // === 鼠标输入：左键=瞄准/松开发投；右键=接球/冲刺 ===
            if (Input.GetMouseButtonDown(0)) _OnLeftDown();
            else if (Input.GetMouseButtonUp(0)) _OnLeftUp();
            else if (Input.GetMouseButtonDown(1)) _OnRightDown();
            else if (Input.GetMouseButtonUp(1)) _OnRightUp();

            if (isAiming) {
                aimCharge = Mathf.Min(1f, aimCharge + Time.deltaTime / 1.2f); // 约1.2秒充满
            }
        }

        public virtual List<PlayerController> GetTeamPlayers() {
            var r = new List<PlayerController>();
            var gm = GameManager.Instance;
            if (gm == null || gm.team_a == null) return r;
            foreach (var t in gm.team_a) if (t != null) {
                var p = t.GetComponent<PlayerController>();
                if (p != null && p.state != PlayerState.Defeated) r.Add(p);
            }
            return r;
        }

        public virtual void SwitchTo(int idx) {
            var list = GetTeamPlayers();
            if (idx < 0 || idx >= list.Count) return;
            controlledPlayer = list[idx];
            // 切控同步：标记 controlled
            var all = UnityEngine.Object.FindObjectsOfType<PlayerController>();
            foreach (var p in all) p.isPlayerControlled = (p == controlledPlayer);
            player_switch_requested?.Invoke(idx);
            Debug.Log("[Input] 切主控 -> " + controlledPlayer.name);
        }

        // 原GD：_on_left_click_press (quickServe=true对应GD field_tag_test.gd瞬发)
        protected virtual void _OnLeftDown() {
            if (controlledPlayer == null) return;
            if (controlledPlayer.IsStatusActive("disarmed")) return;
            if (controlledPlayer.HasBall()) {
                if (quickServe) {
                    // 瞬发模式: 左键按下直接发球(对应GD field_tag_test.gd _try_throw_ball)
                    var dir = _GetAimDirection();
                    var dist = _GetAimDistance();
                    if (dist > 0.2f) {
                        float power = Mathf.Clamp01(dist / maxAimDistance);
                        if (power < 0.1f) power = 0.1f;
                        throw_requested?.Invoke(dir, power);
                    }
                } else {
                    // 蓄力模式: 按下进入瞄准(对应GD input_manager.gd)
                    isAiming = true; aimCharge = 0f;
                }
            } else {
                // 无球：冲刺加速
                controlledPlayer.isSprinting = true;
            }
        }

        // 原GD：_on_left_click_release (蓄力模式下松开发球)
        protected virtual void _OnLeftUp() {
            if (controlledPlayer == null) return;
            if (isAiming) {
                // 蓄力模式: 松开发球
                isAiming = false;
                var dir = _GetAimDirection();
                var dist = _GetAimDistance();
                if (dist > 0.2f) {
                    float power = Mathf.Clamp01(dist / maxAimDistance);
                    if (power < 0.1f) power = 0.1f;
                    throw_requested?.Invoke(dir, power);
                } else {
                    throw_cancelled?.Invoke();
                }
                aimCharge = 0f;
            } else if (!quickServe) {
                // 蓄力模式下无球松开: 停止冲刺
                controlledPlayer.isSprinting = false;
            } else {
                // 瞬发模式下: 松开也停止冲刺
                controlledPlayer.isSprinting = false;
            }
        }

        protected virtual void _OnRightDown() {
            if (controlledPlayer == null) return;
            if (isAiming) {
                isAiming = false; aimCharge = 0f; throw_cancelled?.Invoke();
            } else if (!controlledPlayer.HasBall()) {
                catch_state_entered?.Invoke();
            }
        }
        protected virtual void _OnRightUp() {
            if (controlledPlayer == null) return;
            if (!controlledPlayer.HasBall()) {
                catch_state_exited?.Invoke();
            }
        }

        // === 瞄准方向：从屏幕射线与场地平面Y=0交点获取方向 ===
        protected virtual Vector3 _GetAimDirection() {
            var plane = new Plane(Vector3.up, Vector3.zero);
            var ray = Camera.main != null ? Camera.main.ScreenPointToRay(Input.mousePosition) : new Ray();
            if (Camera.main == null) return controlledPlayer.transform.forward;
            float d;
            if (plane.Raycast(ray, out d)) {
                var world = ray.GetPoint(d);
                var to = world - controlledPlayer.transform.position; to.y = 0f;
                if (to.sqrMagnitude < 0.0001f) return controlledPlayer.transform.forward;
                return to.normalized;
            }
            return controlledPlayer.transform.forward;
        }
        protected virtual float _GetAimDistance() {
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (Camera.main == null) return maxAimDistance;
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            float d;
            if (plane.Raycast(ray, out d)) {
                var world = ray.GetPoint(d);
                var to = world - controlledPlayer.transform.position; to.y = 0f;
                return to.magnitude;
            }
            return maxAimDistance;
        }

        // ===== 由BattleManager订阅：实际执行投球逻辑 =====
        public virtual void AttachBattleManager(BattleManager bm) {
            if (bm == null) return;
            throw_requested += (dir, power) => {
                if (controlledPlayer == null || !controlledPlayer.HasBall()) return;
                var ball = controlledPlayer.carriedBall;
                if (ball == null) return;
                controlledPlayer.SetCarryingBall(ball, false);
                float dist = Mathf.Lerp(2f, maxAimDistance, power);
                float dmg = controlledPlayer._GetEffectiveValue("attack", controlledPlayer.attackPower) * Mathf.Lerp(0.6f, 1.1f, power);
                ball.Launch(controlledPlayer.transform.position, dir, dmg, dist, controlledPlayer.transform, new List<Dictionary<string, object>>());
                Debug.Log("[Input] " + controlledPlayer.name + " 投球 power=" + power.ToString("F2") + " dist=" + dist.ToString("F1") + "m dmg=" + dmg.ToString("F1"));
            };
        }
    }
}

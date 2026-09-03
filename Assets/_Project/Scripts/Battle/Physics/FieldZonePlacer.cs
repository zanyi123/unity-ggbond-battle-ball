using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BattleBall.Battle.Physics
{
    /// <summary>
    /// 场地效果区域放置/清除系统
    /// 处理鼠标操作：预览跟随、点击放置、点击选中清除
    /// 挂载在 FieldZoneManager 下
    /// </summary>
    public class FieldZonePlacer : MonoBehaviour
    {
        // ==================== 状态枚举 ====================
        public enum Mode { NONE, PLACING, CLEARING }

        // ==================== 状态变量 ====================
        public int current_mode = (int)Mode.NONE;
        public Dictionary<string, object> place_params = new Dictionary<string, object>();
        public int remaining_ops = 0;
        public List<Component> selected_for_clear = new List<Component>();

        // ==================== 视觉节点 ====================
        private GameObject preview_node = null;
        private List<GameObject> highlight_nodes = new List<GameObject>();

        // ==================== 信号 ====================
        public event Action operation_finished;

        // ==================== 放置模式 ====================

        public void start_placing(Dictionary<string, object> parameters, int mouse_ops = 1)
        {
            _cancel_internal();
            current_mode = (int)Mode.PLACING;
            place_params = parameters;
            remaining_ops = mouse_ops;
            _create_preview(parameters);
            enabled = true;
            Debug.Log(string.Format("[ZonePlacer] 进入放置模式: type={0} size={1:F0}x{2:F0} ops={3}",
                _str(_get(parameters, "zone_type", "?")),
                _float(_get(parameters, "width", 120.0)),
                _float(_get(parameters, "height", 120.0)),
                mouse_ops));
        }

        private void _create_preview(Dictionary<string, object> parameters)
        {
            if (preview_node != null) Destroy(preview_node);

            preview_node = new GameObject("ZonePlacementPreview");

            int zone_type = _parse_zone_type(_get(parameters, "zone_type", 0));
            float w = _float(_get(parameters, "width", 120.0f)) * 0.01f;
            float h = _float(_get(parameters, "height", 120.0f)) * 0.01f;
            float half_w = w / 2.0f;
            float half_h = h / 2.0f;

            Color fill_color, border_color;
            _get_zone_colors(zone_type, out fill_color, out border_color);
            fill_color.a = 0.35f;

            // 填充（quad + material）
            var fill_go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fill_go.name = "Fill";
            fill_go.transform.SetParent(preview_node.transform, false);
            fill_go.transform.localScale = new Vector3(w, h, 1f);
            fill_go.transform.localPosition = new Vector3(0, 0, 0);
            var fill_mr = fill_go.GetComponent<MeshRenderer>();
            if (fill_mr != null)
            {
                fill_mr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                fill_mr.sharedMaterial.color = fill_color;
                fill_mr.sortingOrder = 100;
            }
            // 旋转 quad 面向摄像机
            var cam = Camera.main;
            if (cam != null) fill_go.transform.rotation = cam.transform.rotation;

            // 边框（LineRenderer）
            var line_go = new GameObject("Border");
            line_go.transform.SetParent(preview_node.transform, false);
            var line = line_go.AddComponent<LineRenderer>();
            line.widthMultiplier = 0.025f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = border_color;
            line.endColor = border_color;
            line.sortingOrder = 101;
            line.positionCount = 5;
            line.SetPosition(0, new Vector3(-half_w, -half_h, 0));
            line.SetPosition(1, new Vector3(half_w, -half_h, 0));
            line.SetPosition(2, new Vector3(half_w, half_h, 0));
            line.SetPosition(3, new Vector3(-half_w, half_h, 0));
            line.SetPosition(4, new Vector3(-half_w, -half_h, 0));
            line.useWorldSpace = false;

            // 标签（TextMesh）
            var label_go = new GameObject("Label");
            label_go.transform.SetParent(preview_node.transform, false);
            var tm = label_go.AddComponent<TextMesh>();
            tm.text = _get_zone_name(zone_type);
            tm.fontSize = 12;
            tm.anchor = TextAnchor.LowerCenter;
            tm.color = border_color;
            label_go.transform.localPosition = new Vector3(0, -half_h - 0.18f, 0);
            label_go.transform.localScale = Vector3.one * 0.05f;

            preview_node.transform.SetParent(transform, false);
        }

        // ==================== 清除模式 ====================

        public void start_clearing(int mouse_ops = 1)
        {
            _cancel_internal();
            current_mode = (int)Mode.CLEARING;
            remaining_ops = mouse_ops;
            selected_for_clear.Clear();
            enabled = true;
            Debug.Log("[ZonePlacer] 进入清除模式: ops=" + mouse_ops);
        }

        // ==================== 帧处理 ====================

        protected virtual void Awake() { enabled = false; }

        protected virtual void Update()
        {
            if (current_mode == (int)Mode.NONE) return;

            if (current_mode == (int)Mode.PLACING) _process_placing();
            else if (current_mode == (int)Mode.CLEARING) _process_clearing();
        }

        private void _process_placing()
        {
            if (preview_node != null)
                preview_node.transform.position = _get_mouse_world_pos();
        }

        private void _process_clearing()
        {
            _clear_highlights();
            Vector3 mouse_pos = _get_mouse_world_pos();
            var manager = _get_manager();
            if (manager == null) return;

            var hovered = _call_method(manager, "get_zone_at_position",
                new object[] { new Vector2(mouse_pos.x, mouse_pos.y), 0.80f }) as Component;
            if (hovered != null && !selected_for_clear.Contains(hovered))
                _create_highlight(hovered, false);

            foreach (var zone in selected_for_clear)
                if (zone != null) _create_highlight(zone, true);
        }

        protected virtual void LateUpdate()
        {
            if (current_mode == (int)Mode.NONE) return;

            if (Input.GetMouseButtonDown(0)) _on_left_click();
            if (Input.GetMouseButtonDown(1)) _on_right_click();
            if (Input.GetKeyDown(KeyCode.Escape)) cancel_operation();
        }

        // ==================== 点击处理 ====================

        private void _on_left_click()
        {
            if (current_mode == (int)Mode.PLACING)
            {
                _place_zone();
            }
            else if (current_mode == (int)Mode.CLEARING)
            {
                _clear_step();
            }
        }

        private void _place_zone()
        {
            Vector3 mouse_pos = _get_mouse_world_pos();
            var manager = _get_manager();
            if (manager == null) return;

            _call_method(manager, "create_zone",
                new object[] { place_params, new Vector2(mouse_pos.x, mouse_pos.y) });
            remaining_ops -= 1;

            Debug.Log(string.Format("[ZonePlacer] 放置区域 pos=({0:F2},{1:F2}) 剩余操作={2}",
                mouse_pos.x, mouse_pos.y, remaining_ops));

            if (remaining_ops <= 0) _finish_operation();
        }

        private void _clear_step()
        {
            Vector3 mouse_pos = _get_mouse_world_pos();
            var manager = _get_manager();
            if (manager == null) return;

            var clicked = _call_method(manager, "get_zone_at_position",
                new object[] { new Vector2(mouse_pos.x, mouse_pos.y), 0.80f }) as Component;

            if (clicked != null && !selected_for_clear.Contains(clicked))
            {
                selected_for_clear.Add(clicked);
                Debug.Log("[ZonePlacer] 选中区域 (" + selected_for_clear.Count + "个)");
                return;
            }

            if (selected_for_clear.Count > 0)
            {
                foreach (var zone in selected_for_clear)
                    if (zone != null) _call_method(manager, "remove_zone", new object[] { zone });
                Debug.Log("[ZonePlacer] 清除 " + selected_for_clear.Count + " 个区域");
                selected_for_clear.Clear();
                remaining_ops -= 1;
                _clear_highlights();

                if (remaining_ops <= 0) _finish_operation();
            }
        }

        private void _on_right_click()
        {
            if (current_mode == (int)Mode.CLEARING && selected_for_clear.Count > 0)
            {
                selected_for_clear.RemoveAt(selected_for_clear.Count - 1);
            }
            else if (current_mode == (int)Mode.PLACING)
            {
                cancel_operation();
            }
        }

        // ==================== 操作控制 ====================

        public void cancel_operation()
        {
            _cancel_internal();
            if (operation_finished != null) operation_finished();
            Debug.Log("[ZonePlacer] 操作已取消");
        }

        public bool is_operating() { return current_mode != (int)Mode.NONE; }

        private void _cancel_internal()
        {
            current_mode = (int)Mode.NONE;
            place_params = new Dictionary<string, object>();
            remaining_ops = 0;
            selected_for_clear.Clear();
            _clear_highlights();
            _remove_preview();
            enabled = false;
        }

        private void _finish_operation()
        {
            _cancel_internal();
            if (operation_finished != null) operation_finished();
            Debug.Log("[ZonePlacer] 操作完成");
        }

        // ==================== 视觉辅助 ====================

        private void _create_highlight(Component zone, bool is_selected)
        {
            if (zone == null) return;
            var hl = new GameObject("Highlight");
            var line = hl.AddComponent<LineRenderer>();
            line.widthMultiplier = 0.03f;
            Color c = is_selected ? new Color(1.0f, 0.3f, 0.3f, 0.9f) : new Color(1.0f, 1.0f, 0.3f, 0.7f);
            line.startColor = c; line.endColor = c;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.sortingOrder = 99;
            line.positionCount = 5;
            // 取 zone 的尺寸（duck-typed）
            Vector2 zone_size = _get_zone_size(zone);
            float half_w = zone_size.x / 2.0f + 0.04f;
            float half_h = zone_size.y / 2.0f + 0.04f;
            Vector3 pos = zone.transform.position;
            line.SetPosition(0, pos + new Vector3(-half_w, -half_h, 0));
            line.SetPosition(1, pos + new Vector3(half_w, -half_h, 0));
            line.SetPosition(2, pos + new Vector3(half_w, half_h, 0));
            line.SetPosition(3, pos + new Vector3(-half_w, half_h, 0));
            line.SetPosition(4, pos + new Vector3(-half_w, -half_h, 0));
            line.useWorldSpace = true;
            hl.transform.SetParent(transform, false);
            highlight_nodes.Add(hl);
        }

        private Vector2 _get_zone_size(Component zone)
        {
            // 尝试通过反射获取 zone_size 字段
            var t = zone.GetType();
            var fi = t.GetField("zone_size");
            if (fi != null)
            {
                var v = fi.GetValue(zone);
                if (v is Vector2 v2) return v2;
                if (v is Vector3 v3) return new Vector2(v3.x, v3.y);
            }
            var pi = t.GetProperty("zone_size");
            if (pi != null)
            {
                var v = pi.GetValue(zone, null);
                if (v is Vector2 v2) return v2;
                if (v is Vector3 v3) return new Vector2(v3.x, v3.y);
            }
            return new Vector2(1.2f, 1.2f); // 默认 1.2m x 1.2m
        }

        private void _clear_highlights()
        {
            foreach (var n in highlight_nodes)
                if (n != null) Destroy(n);
            highlight_nodes.Clear();
        }

        private void _remove_preview()
        {
            if (preview_node != null) Destroy(preview_node);
            preview_node = null;
        }

        // ==================== 工具 ====================

        private Vector3 _get_mouse_world_pos()
        {
            var cam = Camera.main;
            if (cam == null) return Vector3.zero;
            var mp = Input.mousePosition;
            mp.z = -cam.transform.position.z; // 投影到 z=0 平面
            return cam.ScreenToWorldPoint(mp);
        }

        private Component _get_manager()
        {
            var parent = transform.parent;
            if (parent == null) return null;
            // 检查 parent 是否有 create_zone 方法（GD has_method 语义）
            var mi = parent.GetType().GetMethod("create_zone");
            return mi != null ? parent : null;
        }

        private static object _call_method(Component target, string method, object[] args)
        {
            if (target == null) return null;
            try
            {
                var t = target.GetType();
                var mi = t.GetMethod(method);
                if (mi == null) return null;
                return mi.Invoke(target, args);
            }
            catch (Exception) { return null; }
        }

        private int _parse_zone_type(object val)
        {
            if (val is int i) return i;
            if (val is long l) return (int)l;
            string s = _str(val).ToLower();
            switch (s)
            {
                case "boost":
                case "加速": return 0;
                case "slow":
                case "减速": return 1;
                case "danger":
                case "危险": return 2;
                case "safe":
                case "安全": return 3;
            }
            return 0;
        }

        private void _get_zone_colors(int zone_type, out Color fillColor, out Color borderColor)
        {
            switch (zone_type)
            {
                case 0:
                    fillColor = new Color(0.2f, 0.8f, 0.2f, 0.25f);
                    borderColor = new Color(0.3f, 1.0f, 0.3f, 0.8f);
                    return;
                case 1:
                    fillColor = new Color(0.2f, 0.2f, 0.8f, 0.25f);
                    borderColor = new Color(0.3f, 0.3f, 1.0f, 0.8f);
                    return;
                case 2:
                    fillColor = new Color(0.8f, 0.2f, 0.2f, 0.25f);
                    borderColor = new Color(1.0f, 0.3f, 0.3f, 0.8f);
                    return;
                case 3:
                    fillColor = new Color(0.2f, 0.8f, 0.8f, 0.25f);
                    borderColor = new Color(0.3f, 1.0f, 1.0f, 0.8f);
                    return;
                default:
                    fillColor = new Color(0.2f, 0.8f, 0.2f, 0.25f);
                    borderColor = new Color(0.3f, 1.0f, 0.3f, 0.8f);
                    return;
            }
        }

        private string _get_zone_name(int zone_type)
        {
            switch (zone_type)
            {
                case 0: return "加速区";
                case 1: return "减速区";
                case 2: return "危险区";
                case 3: return "安全区";
                default: return "?";
            }
        }

        // 静态辅助
        private static object _get(Dictionary<string, object> d, string k, object def)
        {
            if (d == null) return def;
            object v; return d.TryGetValue(k, out v) ? v : def;
        }
        private static float _float(object v) { if (v == null) return 0f; if (v is float f) return f; if (v is double dd) return (float)dd; if (v is int i) return (float)i; if (v is long l) return (float)l; float r; return float.TryParse(v.ToString(), out r) ? r : 0f; }
        private static string _str(object v) { return v == null ? "" : v.ToString(); }
    }
}

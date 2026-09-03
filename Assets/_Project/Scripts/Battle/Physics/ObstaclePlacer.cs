using UnityEngine;
using System;
using System.Collections.Generic;

namespace BattleBall.Battle.Physics
{
    /// <summary>
    /// 障碍物放置/清除系统
    /// 处理鼠标操作：光环跟随、点击放置、点击选中清除
    /// 挂载在 ObstacleManager 下
    /// </summary>
    public class ObstaclePlacer : MonoBehaviour
    {
        // ==================== 状态枚举 ====================
        public enum Mode { NONE, PLACING, CLEARING }

        // ==================== 状态变量 ====================
        public int current_mode = (int)Mode.NONE;
        public Dictionary<string, object> place_params = new Dictionary<string, object>();
        public int remaining_ops = 0;
        public int clear_count = 0;
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
            Debug.Log("[ObstaclePlacer] 进入放置模式: shape=" + _str(_get(parameters, "shape", "rect")) + " 操作次数=" + mouse_ops);
        }

        private void _create_preview(Dictionary<string, object> parameters)
        {
            if (preview_node != null) Destroy(preview_node);

            preview_node = new GameObject("PlacementPreview");

            var ec = _get(parameters, "element_color", null);
            Color element_color = ec is Color c ? c : new Color(1.0f, 1.0f, 0.5f);
            Color color_alpha = new Color(element_color.r, element_color.g, element_color.b, 0.4f);
            string shape_type = _str(_get(parameters, "shape", "rect"));

            switch (shape_type)
            {
                case "rect":
                    {
                        float w = _float(_get(parameters, "width", 0.80f));
                        float h = _float(_get(parameters, "height", 0.30f));
                        // 填充
                        var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        fill.name = "Fill";
                        fill.transform.SetParent(preview_node.transform, false);
                        fill.transform.localScale = new Vector3(w, h, 1f);
                        var mr = fill.GetComponent<MeshRenderer>();
                        if (mr != null)
                        {
                            mr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                            mr.sharedMaterial.color = color_alpha;
                            mr.sortingOrder = 100;
                        }
                        var cam = Camera.main;
                        if (cam != null) fill.transform.rotation = cam.transform.rotation;

                        // 边框
                        var border_go = new GameObject("Border");
                        border_go.transform.SetParent(preview_node.transform, false);
                        var line = border_go.AddComponent<LineRenderer>();
                        line.widthMultiplier = 0.02f;
                        line.startColor = element_color; line.endColor = element_color;
                        line.material = new Material(Shader.Find("Sprites/Default"));
                        line.sortingOrder = 101;
                        line.useWorldSpace = false;
                        line.positionCount = 5;
                        line.SetPosition(0, new Vector3(-w / 2f, -h / 2f, 0));
                        line.SetPosition(1, new Vector3(w / 2f, -h / 2f, 0));
                        line.SetPosition(2, new Vector3(w / 2f, h / 2f, 0));
                        line.SetPosition(3, new Vector3(-w / 2f, h / 2f, 0));
                        line.SetPosition(4, new Vector3(-w / 2f, -h / 2f, 0));
                    }
                    break;
                case "circle":
                    {
                        float r = _float(_get(parameters, "radius", 0.40f));
                        var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        fill.name = "Fill";
                        fill.transform.SetParent(preview_node.transform, false);
                        fill.transform.localScale = new Vector3(r * 2, r * 2, 1f);
                        var mr = fill.GetComponent<MeshRenderer>();
                        if (mr != null)
                        {
                            mr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                            mr.sharedMaterial.color = color_alpha;
                            mr.sortingOrder = 100;
                        }
                        var cam = Camera.main;
                        if (cam != null) fill.transform.rotation = cam.transform.rotation;

                        var border_go = new GameObject("Border");
                        border_go.transform.SetParent(preview_node.transform, false);
                        var line = border_go.AddComponent<LineRenderer>();
                        line.widthMultiplier = 0.02f;
                        line.startColor = element_color; line.endColor = element_color;
                        line.material = new Material(Shader.Find("Sprites/Default"));
                        line.sortingOrder = 101;
                        line.useWorldSpace = false;
                        var pts = new Vector3[25];
                        for (int i = 0; i < 25; i++)
                        {
                            float angle = (2f * Mathf.PI * i / 24f);
                            pts[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * r;
                        }
                        line.positionCount = 25;
                        for (int i = 0; i < 25; i++) line.SetPosition(i, pts[i]);
                    }
                    break;
                case "crescent":
                    {
                        float r = _float(_get(parameters, "radius", 0.40f));
                        float arc_angle = _float(_get(parameters, "arc_angle", 120.0f)) * Mathf.Deg2Rad;
                        var points = _build_crescent_points(r, arc_angle);
                        // 填充：Mesh
                        var fill = new GameObject("Fill");
                        fill.transform.SetParent(preview_node.transform, false);
                        var mf = fill.AddComponent<MeshFilter>();
                        var mr = fill.AddComponent<MeshRenderer>();
                        var mesh = new Mesh();
                        var verts = new Vector3[points.Count];
                        for (int i = 0; i < points.Count; i++)
                            verts[i] = new Vector3(points[i].x, points[i].y, 0);
                        mesh.vertices = verts;
                        mesh.triangles = _fan_triangulate(verts);
                        mesh.RecalculateNormals();
                        mf.sharedMesh = mesh;
                        mr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                        mr.sharedMaterial.color = color_alpha;
                        mr.sortingOrder = 100;

                        // 边框
                        var border_go = new GameObject("Border");
                        border_go.transform.SetParent(preview_node.transform, false);
                        var line = border_go.AddComponent<LineRenderer>();
                        line.widthMultiplier = 0.02f;
                        line.startColor = element_color; line.endColor = element_color;
                        line.material = new Material(Shader.Find("Sprites/Default"));
                        line.sortingOrder = 101;
                        line.useWorldSpace = false;
                        var line_pts = new List<Vector3>();
                        foreach (var p in points) line_pts.Add(new Vector3(p.x, p.y, 0));
                        if (points.Count > 0) line_pts.Add(new Vector3(points[0].x, points[0].y, 0));
                        line.positionCount = line_pts.Count;
                        for (int i = 0; i < line_pts.Count; i++) line.SetPosition(i, line_pts[i]);
                    }
                    break;
            }

            preview_node.transform.SetParent(transform, false);
        }

        private List<Vector2> _build_crescent_points(float radius, float arc_angle)
        {
            var points = new List<Vector2>();
            int segments = 12;
            float half_arc = arc_angle / 2.0f;
            for (int i = 0; i <= segments; i++)
            {
                float angle = -half_arc + (arc_angle * i / segments);
                points.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            float inner_radius = radius * 0.6f;
            for (int i = 0; i <= segments; i++)
            {
                float angle = half_arc - (arc_angle * i / segments);
                points.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * inner_radius);
            }
            return points;
        }

        private int[] _fan_triangulate(Vector3[] verts)
        {
            if (verts.Length < 3) return new int[0];
            var tris = new int[(verts.Length - 2) * 3];
            int idx = 0;
            for (int i = 1; i < verts.Length - 1; i++)
            {
                tris[idx++] = 0;
                tris[idx++] = i;
                tris[idx++] = i + 1;
            }
            return tris;
        }

        // ==================== 清除模式 ====================

        public void start_clearing(int clear_count_val, int mouse_ops = 1)
        {
            _cancel_internal();
            current_mode = (int)Mode.CLEARING;
            this.clear_count = clear_count_val;
            remaining_ops = mouse_ops;
            selected_for_clear.Clear();
            enabled = true;
            Debug.Log("[ObstaclePlacer] 进入清除模式: 清除数=" + clear_count_val + " 操作次数=" + mouse_ops);
        }

        // ==================== 帧处理 ====================

        protected virtual void Awake() { enabled = false; }

        protected virtual void Update()
        {
            if (current_mode == (int)Mode.NONE) return;
            if (current_mode == (int)Mode.PLACING) _process_placing();
            else if (current_mode == (int)Mode.CLEARING) _process_clearing();
        }

        protected virtual void LateUpdate()
        {
            if (current_mode == (int)Mode.NONE) return;
            if (Input.GetMouseButtonDown(0)) _on_left_click();
            if (Input.GetMouseButtonDown(1)) _on_right_click();
            if (Input.GetKeyDown(KeyCode.Escape)) cancel_operation();
        }

        private void _process_placing()
        {
            if (preview_node == null) return;
            Vector3 mouse_pos = _get_mouse_world_pos();
            preview_node.transform.position = mouse_pos;
            // 月牙形：实时旋转凹面朝向释放球员
            if (_str(_get(place_params, "shape", "")) == "crescent")
            {
                var caster_pos_v = _get(place_params, "caster_position", null);
                if (caster_pos_v is Vector2 cp2)
                {
                    Vector3 caster_pos = new Vector3(cp2.x, 0, cp2.y);
                    if (Vector3.Distance(mouse_pos, caster_pos) > 0.01f)
                    {
                        Vector3 dir = mouse_pos - caster_pos;
                        if (dir.sqrMagnitude > 0.0001f)
                        {
                            // 在 XZ 平面：绕 y 轴旋转
                            float angle = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
                            preview_node.transform.rotation = Quaternion.Euler(0, -angle, 0);
                        }
                    }
                }
                else if (caster_pos_v is Vector3 cp3)
                {
                    if (Vector3.Distance(mouse_pos, cp3) > 0.01f)
                    {
                        Vector3 dir = mouse_pos - cp3;
                        if (dir.sqrMagnitude > 0.0001f)
                        {
                            float angle = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
                            preview_node.transform.rotation = Quaternion.Euler(0, -angle, 0);
                        }
                    }
                }
            }
        }

        private void _process_clearing()
        {
            _clear_highlights();
            Vector3 mouse_pos = _get_mouse_world_pos();
            var manager = _get_manager();
            if (manager == null) return;

            var hovered = _call_method(manager, "get_obstacle_at_position",
                new object[] { new Vector2(mouse_pos.x, mouse_pos.z), 0.50f }) as Component;
            if (hovered != null && !selected_for_clear.Contains(hovered))
                _create_highlight(hovered, false);

            foreach (var obs in selected_for_clear)
                if (obs != null) _create_highlight(obs, true);
        }

        // ==================== 点击处理 ====================

        private void _on_left_click()
        {
            if (current_mode == (int)Mode.PLACING)
                _place_obstacle();
            else if (current_mode == (int)Mode.CLEARING)
                _clear_step();
        }

        private void _place_obstacle()
        {
            Vector3 mouse_pos = _get_mouse_world_pos();
            var manager = _get_manager();
            if (manager == null) return;

            // 月牙形：内环（凹面）朝向释放技能的球员
            float rotation = 0f;
            var caster_pos_v = _get(place_params, "caster_position", null);
            if (caster_pos_v != null && _str(_get(place_params, "shape", "")) == "crescent")
            {
                Vector3 caster_pos = caster_pos_v is Vector2 cp2 ? new Vector3(cp2.x, 0, cp2.y)
                    : caster_pos_v is Vector3 cp3 ? cp3 : Vector3.zero;
                if (Vector3.Distance(mouse_pos, caster_pos) > 0.01f)
                {
                    Vector3 dir = mouse_pos - caster_pos;
                    if (dir.sqrMagnitude > 0.0001f)
                        rotation = Mathf.Atan2(dir.z, dir.x);
                }
            }

            _call_method(manager, "create_obstacle",
                new object[] { place_params, new Vector2(mouse_pos.x, mouse_pos.z), rotation });
            remaining_ops -= 1;

            Debug.Log(string.Format("[ObstaclePlacer] 放置障碍物 pos=({0:F1},{1:F1}) 剩余操作={2}",
                mouse_pos.x, mouse_pos.z, remaining_ops));

            if (remaining_ops <= 0) _finish_operation();
        }

        private void _clear_step()
        {
            Vector3 mouse_pos = _get_mouse_world_pos();
            var manager = _get_manager();
            if (manager == null) return;

            var clicked = _call_method(manager, "get_obstacle_at_position",
                new object[] { new Vector2(mouse_pos.x, mouse_pos.z), 0.50f }) as Component;

            if (clicked != null && !selected_for_clear.Contains(clicked))
            {
                if (selected_for_clear.Count < clear_count)
                {
                    selected_for_clear.Add(clicked);
                    Debug.Log("[ObstaclePlacer] 选中障碍物 (" + selected_for_clear.Count + "/" + clear_count + ")");
                }
                return;
            }

            if (selected_for_clear.Count > 0)
            {
                _call_method(manager, "remove_obstacles", new object[] { selected_for_clear.ToArray() });
                Debug.Log("[ObstaclePlacer] 清除 " + selected_for_clear.Count + " 个障碍物");
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
                Debug.Log("[ObstaclePlacer] 取消选中 (" + selected_for_clear.Count + "/" + clear_count + ")");
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
            Debug.Log("[ObstaclePlacer] 操作已取消");
        }

        public bool is_operating() { return current_mode != (int)Mode.NONE; }

        private void _cancel_internal()
        {
            current_mode = (int)Mode.NONE;
            place_params = new Dictionary<string, object>();
            remaining_ops = 0;
            clear_count = 0;
            selected_for_clear.Clear();
            _clear_highlights();
            _remove_preview();
            enabled = false;
        }

        private void _finish_operation()
        {
            _cancel_internal();
            if (operation_finished != null) operation_finished();
            Debug.Log("[ObstaclePlacer] 操作完成");
        }

        // ==================== 视觉辅助 ====================

        private void _create_highlight(Component obstacle, bool is_selected)
        {
            if (obstacle == null) return;
            var hl = new GameObject("Highlight");
            var line = hl.AddComponent<LineRenderer>();
            line.widthMultiplier = 0.03f;
            Color c = is_selected ? new Color(1.0f, 0.3f, 0.3f, 0.8f) : new Color(0.3f, 0.6f, 1.0f, 0.6f);
            line.startColor = c; line.endColor = c;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.sortingOrder = 99;

            float extent = 0.50f;
            Vector3 pos = obstacle.transform.position;
            // 在 XZ 平面构建方框（用 LineRenderer）
            line.useWorldSpace = true;
            line.positionCount = 5;
            line.SetPosition(0, pos + new Vector3(-extent, 0, -extent));
            line.SetPosition(1, pos + new Vector3(extent, 0, -extent));
            line.SetPosition(2, pos + new Vector3(extent, 0, extent));
            line.SetPosition(3, pos + new Vector3(-extent, 0, extent));
            line.SetPosition(4, pos + new Vector3(-extent, 0, -extent));

            hl.transform.SetParent(transform, false);
            highlight_nodes.Add(hl);
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

        // ==================== 工具方法 ====================

        private Vector3 _get_mouse_world_pos()
        {
            var cam = Camera.main;
            if (cam == null) return Vector3.zero;
            var mp = Input.mousePosition;
            // 投影到 y=0 平面（XZ 平面）
            var plane = new Plane(Vector3.up, Vector3.zero);
            var ray = cam.ScreenPointToRay(mp);
            if (plane.Raycast(ray, out var enter))
                return ray.GetPoint(enter);
            return cam.ScreenToWorldPoint(mp);
        }

        private Component _get_manager()
        {
            var parent = transform.parent;
            if (parent == null) return null;
            var mi = parent.GetType().GetMethod("create_obstacle");
            return mi != null ? parent : null;
        }

        private static object _call_method(Component target, string method, object[] args)
        {
            if (target == null) return null;
            try
            {
                var mi = target.GetType().GetMethod(method);
                if (mi == null) return null;
                return mi.Invoke(target, args);
            }
            catch (Exception) { return null; }
        }

        // ===== 内部工具 =====
        private static object _get(Dictionary<string, object> d, string k, object def)
        {
            if (d == null) return def;
            object v; return d.TryGetValue(k, out v) ? v : def;
        }
        private static float _float(object v) { if (v == null) return 0f; if (v is float f) return f; if (v is double dd) return (float)dd; if (v is int i) return (float)i; if (v is long l) return (float)l; float r; return float.TryParse(v.ToString(), out r) ? r : 0f; }
        private static string _str(object v) { return v == null ? "" : v.ToString(); }
    }
}

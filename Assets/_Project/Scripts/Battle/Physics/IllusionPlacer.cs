using UnityEngine;
using Random = UnityEngine.Random;
using System;
using System.Collections.Generic;

namespace BattleBall.Battle.Physics
{
    /// <summary>
    /// 幻象放置/清除系统
    /// 处理鼠标操作：两种放置模式 + 清除模式
    /// 挂载在 IllusionManager 下
    /// 放置模式：
    ///   1. any（任意放置）：鼠标为半径生成预放置圆，可放置在场地内
    ///      - 左键1次生成1个，连续可放多个（mouse_ops=上限）
    ///      - 不在场地内→预放置圆消失（禁止放置）
    ///   2. near（近身放置）：强制生成1个
    ///      - 鼠标悬停球员→球员边缘高亮（预选择）
    ///      - 以球员为中心生成180度预放置扇形（半径固定1m）
    ///      - 鼠标控制扇形转动（可360）
    ///      - 左键确认→该扇形内随机位置生成幻象
    /// 清除模式：鼠标选中幻象（高亮框）→ 左键确认清除
    /// </summary>
    public class IllusionPlacer : MonoBehaviour
    {
        // ==================== 状态枚举 ====================
        public enum Mode { NONE, PLACING_ANY, PLACING_NEAR, CLEARING }

        // ==================== 场地边界（与 ball 一致，单位：米）====================
        public const float FIELD_X_MIN = -5.10f;
        public const float FIELD_X_MAX = 5.10f;
        public const float FIELD_Y_MIN = -3.25f;
        public const float FIELD_Y_MAX = 3.25f;

        public const float PLACE_RADIUS = 0.30f;    // 任意放置预览圆半径
        public const float NEAR_RADIUS = 1.00f;    // 近身扇形半径
        public const float NEAR_HALF_ARC = 90.0f;  // 扇形半弧度（180度）

        // ==================== 状态变量 ====================
        public int current_mode = (int)Mode.NONE;
        public Dictionary<string, object> place_params = new Dictionary<string, object>();
        public int remaining_ops = 0;
        public BattleBall.Battle.PlayerController source_player = null;

        // ==================== 视觉节点 ====================
        private GameObject preview_node = null;
        private GameObject sector_node = null;
        private GameObject highlight_circle = null;
        private List<GameObject> clear_highlights = new List<GameObject>();
        private List<Component> selected_for_clear = new List<Component>();

        // ==================== 信号 ====================
        public event Action operation_finished;

        // ==================== 放置模式 ====================

        public void start_placing(Dictionary<string, object> parameters, int mouse_ops = 1)
        {
            _cancel_internal();
            var sp = _get(parameters, "source_player", null);
            source_player = sp as BattleBall.Battle.PlayerController;
            if (source_player == null)
            {
                Debug.LogError("[IllusionPlacer] 无效的源球员");
                return;
            }

            string mode_str = _str(_get(parameters, "place_mode", "any"));
            place_params = parameters;
            remaining_ops = mouse_ops;

            if (mode_str == "near")
            {
                current_mode = (int)Mode.PLACING_NEAR;
                remaining_ops = 1;
                _create_sector_preview();
            }
            else
            {
                current_mode = (int)Mode.PLACING_ANY;
                _create_circle_preview();
            }

            enabled = true;
            Debug.Log(string.Format("[IllusionPlacer] 进入放置模式: mode={0} ops={1} source={2}",
                mode_str, remaining_ops, source_player.team));
        }

        private void _create_circle_preview(Dictionary<string, object> parameters_unused = null)
        {
            _remove_preview();
            preview_node = new GameObject("IllusionCirclePreview");

            // 外圆（边框 LineRenderer）
            var line_go = new GameObject("Border");
            line_go.transform.SetParent(preview_node.transform, false);
            var line = line_go.AddComponent<LineRenderer>();
            line.widthMultiplier = 0.02f;
            line.startColor = new Color(0.7f, 0.7f, 1.0f, 0.7f);
            line.endColor = new Color(0.7f, 0.7f, 1.0f, 0.7f);
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.sortingOrder = 100;
            var pts = _circle_points(PLACE_RADIUS, 24);
            line.positionCount = pts.Length;
            for (int i = 0; i < pts.Length; i++) line.SetPosition(i, pts[i]);
            line.useWorldSpace = false;

            // 填充
            var fill_go = new GameObject("Fill");
            fill_go.transform.SetParent(preview_node.transform, false);
            var sr = fill_go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(new Texture2D(1, 1), new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
            sr.color = new Color(0.5f, 0.5f, 1.0f, 0.2f);
            fill_go.transform.localScale = new Vector3(PLACE_RADIUS * 2, PLACE_RADIUS * 2, 1f);
            sr.sortingOrder = 99;

            preview_node.transform.SetParent(transform, false);
        }

        private void _create_sector_preview()
        {
            _remove_preview();
            sector_node = new GameObject("IllusionSectorPreview");
            sector_node.transform.SetParent(transform, false);
            // 扇形内容在 Update 里随鼠标方向实时重建
        }

        // ==================== 清除模式 ====================

        public void start_clearing(int mouse_ops = 1)
        {
            _cancel_internal();
            current_mode = (int)Mode.CLEARING;
            remaining_ops = mouse_ops;
            selected_for_clear.Clear();
            enabled = true;
            Debug.Log("[IllusionPlacer] 进入清除模式: ops=" + mouse_ops);
        }

        // ==================== 帧处理 ====================

        protected virtual void Awake() { enabled = false; }

        protected virtual void Update()
        {
            if (current_mode == (int)Mode.NONE) return;

            switch (current_mode)
            {
                case (int)Mode.PLACING_ANY: _process_placing_any(); break;
                case (int)Mode.PLACING_NEAR: _process_placing_near(); break;
                case (int)Mode.CLEARING: _process_clearing(); break;
            }
        }

        protected virtual void LateUpdate()
        {
            if (current_mode == (int)Mode.NONE) return;
            if (Input.GetMouseButtonDown(0)) _on_left_click();
            if (Input.GetMouseButtonDown(1)) _on_right_click();
            if (Input.GetKeyDown(KeyCode.Escape)) cancel_operation();
        }

        private void _process_placing_any()
        {
            if (preview_node == null) return;
            Vector3 mouse_pos = _get_mouse_world_pos();
            preview_node.transform.position = mouse_pos;
            bool in_field = _is_in_field(mouse_pos);
            preview_node.SetActive(in_field);
        }

        private void _process_placing_near()
        {
            if (source_player == null) return;
            Vector3 mouse_pos = _get_mouse_world_pos();
            Vector3 player_pos = source_player.transform.position;
            float hover_dist = Vector3.Distance(player_pos, mouse_pos);

            bool hovering_player = hover_dist <= 0.40f;
            _update_player_highlight(hovering_player);

            Vector3 dir = mouse_pos - player_pos;
            if (dir.sqrMagnitude > 0.0001f) dir.Normalize();
            float angle_deg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            _rebuild_sector(player_pos, angle_deg);
        }

        private void _process_clearing()
        {
            _clear_highlights();
            Vector3 mouse_pos = _get_mouse_world_pos();
            var manager = _get_manager();
            if (manager == null) return;

            var hovered = _call_method(manager, "get_illusion_at_position",
                new object[] { new Vector2(mouse_pos.x, mouse_pos.y), 0.60f }) as Component;
            if (hovered != null && !selected_for_clear.Contains(hovered))
                _create_clear_highlight(hovered, false);

            foreach (var ill in selected_for_clear)
                if (ill != null) _create_clear_highlight(ill, true);
        }

        // ==================== 点击处理 ====================

        private void _on_left_click()
        {
            switch (current_mode)
            {
                case (int)Mode.PLACING_ANY: _place_any(); break;
                case (int)Mode.PLACING_NEAR: _place_near(); break;
                case (int)Mode.CLEARING: _clear_step(); break;
            }
        }

        private void _on_right_click()
        {
            if (current_mode == (int)Mode.CLEARING && selected_for_clear.Count > 0)
                selected_for_clear.RemoveAt(selected_for_clear.Count - 1);
            else if (current_mode == (int)Mode.PLACING_ANY || current_mode == (int)Mode.PLACING_NEAR)
                cancel_operation();
        }

        private void _place_any()
        {
            Vector3 mouse_pos = _get_mouse_world_pos();
            if (!_is_in_field(mouse_pos)) return;
            var manager = _get_manager();
            if (manager == null) return;
            _call_method(manager, "create_illusion",
                new object[] { source_player, place_params, new Vector2(mouse_pos.x, mouse_pos.y) });
            remaining_ops -= 1;
            Debug.Log(string.Format("[IllusionPlacer] 任意放置 pos=({0:F2},{1:F2}) 剩余={2}",
                mouse_pos.x, mouse_pos.y, remaining_ops));
            if (remaining_ops <= 0) _finish_operation();
        }

        private void _place_near()
        {
            if (source_player == null) return;
            Vector3 mouse_pos = _get_mouse_world_pos();
            Vector3 player_pos = source_player.transform.position;
            Vector3 dir = mouse_pos - player_pos;
            if (dir.sqrMagnitude > 0.0001f) dir.Normalize();
            float base_angle = Mathf.Atan2(dir.y, dir.x);
            // 在 ±NEAR_HALF_ARC 内随机角度
            float random_offset = Random.Range(-NEAR_HALF_ARC, NEAR_HALF_ARC) * Mathf.Deg2Rad;
            float final_angle = base_angle + random_offset;
            float random_r = NEAR_RADIUS * Random.Range(0.4f, 1.0f);
            Vector3 pos = player_pos + new Vector3(Mathf.Cos(final_angle), Mathf.Sin(final_angle), 0f) * random_r;

            if (!_is_in_field(pos))
            {
                Debug.Log("[IllusionPlacer] 近身放置位置超出场地，取消");
                _finish_operation();
                return;
            }

            var manager = _get_manager();
            if (manager == null) return;
            _call_method(manager, "create_illusion",
                new object[] { source_player, place_params, new Vector2(pos.x, pos.y) });
            remaining_ops = 0;
            _finish_operation();
        }

        private void _clear_step()
        {
            Vector3 mouse_pos = _get_mouse_world_pos();
            var manager = _get_manager();
            if (manager == null) return;

            var clicked = _call_method(manager, "get_illusion_at_position",
                new object[] { new Vector2(mouse_pos.x, mouse_pos.y), 0.60f }) as Component;
            if (clicked != null && !selected_for_clear.Contains(clicked))
            {
                selected_for_clear.Add(clicked);
                return;
            }

            if (selected_for_clear.Count > 0)
            {
                foreach (var ill in selected_for_clear)
                    if (ill != null) _call_method(manager, "remove_illusion", new object[] { ill });
                Debug.Log("[IllusionPlacer] 清除 " + selected_for_clear.Count + " 个幻象");
                selected_for_clear.Clear();
                remaining_ops -= 1;
                _clear_highlights();
                if (remaining_ops <= 0) _finish_operation();
            }
        }

        // ==================== 操作控制 ====================

        public void cancel_operation()
        {
            _cancel_internal();
            if (operation_finished != null) operation_finished();
            Debug.Log("[IllusionPlacer] 操作已取消");
        }

        public bool is_operating() { return current_mode != (int)Mode.NONE; }

        private void _cancel_internal()
        {
            current_mode = (int)Mode.NONE;
            place_params = new Dictionary<string, object>();
            remaining_ops = 0;
            source_player = null;
            selected_for_clear.Clear();
            _remove_preview();
            _remove_sector();
            _remove_player_highlight();
            _clear_highlights();
            enabled = false;
        }

        private void _finish_operation()
        {
            _cancel_internal();
            if (operation_finished != null) operation_finished();
            Debug.Log("[IllusionPlacer] 操作完成");
        }

        // ==================== 视觉辅助 ====================

        private void _update_player_highlight(bool hovering)
        {
            if (hovering)
            {
                if (highlight_circle == null)
                {
                    highlight_circle = new GameObject("PlayerHighlight");
                    var line = highlight_circle.AddComponent<LineRenderer>();
                    line.widthMultiplier = 0.03f;
                    line.startColor = new Color(1.0f, 1.0f, 0.3f, 0.9f);
                    line.endColor = new Color(1.0f, 1.0f, 0.3f, 0.9f);
                    line.material = new Material(Shader.Find("Sprites/Default"));
                    line.sortingOrder = 100;
                    var pts = _circle_points(0.34f, 28);
                    line.positionCount = pts.Length;
                    for (int i = 0; i < pts.Length; i++) line.SetPosition(i, pts[i]);
                    line.useWorldSpace = false;
                    highlight_circle.transform.SetParent(transform, false);
                }
                if (source_player != null)
                    highlight_circle.transform.position = source_player.transform.position;
            }
            else
            {
                _remove_player_highlight();
            }
        }

        private void _rebuild_sector(Vector3 center, float angle_deg)
        {
            if (sector_node == null) return;

            // 清空旧子节点
            for (int i = sector_node.transform.childCount - 1; i >= 0; i--)
                Destroy(sector_node.transform.GetChild(i).gameObject);

            sector_node.transform.position = center;

            // 扇形多边形点集
            var pts_list = new List<Vector3> { Vector3.zero };
            int segments = 16;
            for (int i = 0; i <= segments; i++)
            {
                float a = (angle_deg - NEAR_HALF_ARC + (180.0f * i / segments)) * Mathf.Deg2Rad;
                pts_list.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * NEAR_RADIUS);
            }

            var poly_go = _make_polygon(pts_list.ToArray(),
                new Color(0.7f, 0.7f, 1.0f, 0.2f),
                new Color(0.7f, 0.7f, 1.0f, 0.6f));
            poly_go.transform.SetParent(sector_node.transform, false);
        }

        private GameObject _make_polygon(Vector3[] points, Color fill_color, Color border_color)
        {
            var container = new GameObject("Polygon");
            // 填充（用 SpriteRenderer 近似多边形，简化为 Quad）
            var fill_go = new GameObject("Fill");
            fill_go.transform.SetParent(container.transform, false);
            var sr = fill_go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(new Texture2D(1, 1), new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
            sr.color = fill_color;
            // 计算 bounds
            var min = new Vector3(float.MaxValue, float.MaxValue, 0);
            var max = new Vector3(float.MinValue, float.MinValue, 0);
            foreach (var p in points)
            {
                if (p.x < min.x) min.x = p.x;
                if (p.y < min.y) min.y = p.y;
                if (p.x > max.x) max.x = p.x;
                if (p.y > max.y) max.y = p.y;
            }
            fill_go.transform.localScale = new Vector3(max.x - min.x, max.y - min.y, 1f);
            fill_go.transform.localPosition = new Vector3((min.x + max.x) / 2f, (min.y + max.y) / 2f, 0f);
            sr.sortingOrder = 95;

            // 边框
            var line_go = new GameObject("Border");
            line_go.transform.SetParent(container.transform, false);
            var line = line_go.AddComponent<LineRenderer>();
            line.widthMultiplier = 0.02f;
            line.startColor = border_color;
            line.endColor = border_color;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.sortingOrder = 96;
            var line_pts = new List<Vector3>(points);
            if (points.Length > 1) line_pts.Add(points[1]); // 闭合
            line.positionCount = line_pts.Count;
            for (int i = 0; i < line_pts.Count; i++) line.SetPosition(i, line_pts[i]);
            line.useWorldSpace = false;

            return container;
        }

        private void _create_clear_highlight(Component ill, bool is_selected)
        {
            if (ill == null) return;
            var hl = new GameObject("ClearHighlight");
            var line = hl.AddComponent<LineRenderer>();
            line.widthMultiplier = 0.03f;
            Color c = is_selected ? new Color(1.0f, 0.3f, 0.3f, 0.9f) : new Color(1.0f, 1.0f, 0.3f, 0.7f);
            line.startColor = c; line.endColor = c;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.sortingOrder = 99;
            var pts = _circle_points(0.32f, 24);
            line.positionCount = pts.Length;
            for (int i = 0; i < pts.Length; i++) line.SetPosition(i, pts[i]);
            line.useWorldSpace = false;
            hl.transform.position = ill.transform.position;
            hl.transform.SetParent(transform, false);
            clear_highlights.Add(hl);
        }

        private void _clear_highlights()
        {
            foreach (var n in clear_highlights)
                if (n != null) Destroy(n);
            clear_highlights.Clear();
        }

        private void _remove_preview()
        {
            if (preview_node != null) Destroy(preview_node);
            preview_node = null;
        }

        private void _remove_sector()
        {
            if (sector_node != null) Destroy(sector_node);
            sector_node = null;
        }

        private void _remove_player_highlight()
        {
            if (highlight_circle != null) Destroy(highlight_circle);
            highlight_circle = null;
        }

        // ==================== 工具 ====================

        private Vector3[] _circle_points(float radius, int segments)
        {
            var pts = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float a = (2f * Mathf.PI * i / segments);
                pts[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
            }
            return pts;
        }

        private bool _is_in_field(Vector3 pos)
        {
            return pos.x >= FIELD_X_MIN && pos.x <= FIELD_X_MAX
                && pos.y >= FIELD_Y_MIN && pos.y <= FIELD_Y_MAX;
        }

        private Vector3 _get_mouse_world_pos()
        {
            var cam = Camera.main;
            if (cam == null) return Vector3.zero;
            var mp = Input.mousePosition;
            mp.z = -cam.transform.position.z;
            return cam.ScreenToWorldPoint(mp);
        }

        private Component _get_manager()
        {
            var parent = transform.parent;
            if (parent == null) return null;
            var mi = parent.GetType().GetMethod("create_illusion");
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
        private static string _str(object v) { return v == null ? "" : v.ToString(); }
    }
}

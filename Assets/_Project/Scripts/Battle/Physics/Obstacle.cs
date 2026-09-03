using UnityEngine;
using System;
using System.Collections.Generic;

namespace BattleBall.Battle.Physics
{
    /// <summary>
    /// 障碍物节点
    /// 放置在场上的障碍物，可阻挡球的攻击
    /// 特征值：防御生命值、降低球速值、抵消消耗速率
    /// </summary>
    public class Obstacle : MonoBehaviour
    {
        // ==================== 参数 ====================

        public float obstacle_hp = 50.0f;
        public float max_obstacle_hp = 50.0f;
        public float attack_consume_rate = 20.0f;   // 球攻击力消耗速率（/s），同时消耗障碍物HP
        public float speed_consume_rate = 20.0f;    // 球速消耗速率（m/s）
        public string source_skill = "";
        public float remaining_duration = 10.0f;
        public string shape_type = "rect";
        public Color element_color = new Color(1.0f, 1.0f, 0.5f);
        public float _cached_hit_radius = 0.44f;    // 缓存碰撞半径
        public float _cached_width = 0.80f;          // 缓存矩形宽度
        public float _cached_height = 0.30f;        // 缓存矩形高度
        public float _cached_radius = 0.40f;         // 缓存圆形/月牙半径

        // ==================== 节点引用 ====================

        private GameObject visual_node = null;
        private GameObject hp_bar = null;
        private float duration_timer = 0f;
        private bool duration_timer_active = false;

        // ==================== 信号 ====================

        public event Action<Obstacle> obstacle_destroyed;
        public event Action<Obstacle> obstacle_expired;

        // ==================== 初始化 ====================

        public void setup(Dictionary<string, object> parameters)
        {
            obstacle_hp = _float(_get(parameters, "hp", 50.0f));
            max_obstacle_hp = obstacle_hp;
            // 新参数优先，旧参数兼容
            attack_consume_rate = _get(parameters, "attack_consume_rate", null) != null
                ? _float(_get(parameters, "attack_consume_rate", 20.0f))
                : _float(_get(parameters, "consume_rate", 20.0f));
            speed_consume_rate = _get(parameters, "speed_consume_rate", null) != null
                ? _float(_get(parameters, "speed_consume_rate", 20.0f))
                : _float(_get(parameters, "speed_reduction", 20.0f));
            source_skill = _str(_get(parameters, "source_skill", ""));
            remaining_duration = _float(_get(parameters, "duration", 10.0f));
            shape_type = _str(_get(parameters, "shape", "rect"));

            var ec = _get(parameters, "element_color", null);
            if (ec is Color c) element_color = c;

            // 创建碰撞形状
            _create_collision(parameters);
            // 创建视觉
            _create_visual(parameters);
            // 创建HP条
            _create_hp_bar();
            // 创建持续时间计时器
            _create_duration_timer();
        }

        private void _create_collision(Dictionary<string, object> parameters)
        {
            if (shape_type == "crescent")
            {
                // 月牙形用 MeshCollider + 凹多边形
                float radius = _float(_get(parameters, "radius", 0.40f));
                float arc_angle = _float(_get(parameters, "arc_angle", 120.0f)) * Mathf.Deg2Rad;
                var raw_points = _build_crescent_points(radius, arc_angle);
                // Unity 3D：在 XZ 平面（y=0）
                var mesh_go = new GameObject("CrescentCollision");
                mesh_go.transform.SetParent(transform, false);
                var mc = mesh_go.AddComponent<MeshCollider>();
                // 简化：用凸包近似（实际凹多边形 MeshCollider 需要 convoluted mesh）
                var mesh = new Mesh();
                var verts = new Vector3[raw_points.Count];
                for (int i = 0; i < raw_points.Count; i++)
                    verts[i] = new Vector3(raw_points[i].x, 0, raw_points[i].y);
                mesh.vertices = verts;
                mesh.triangles = _fan_triangulate(verts);
                mesh.RecalculateNormals();
                mc.sharedMesh = mesh;
                return;
            }

            switch (shape_type)
            {
                case "rect":
                    {
                        var col = gameObject.AddComponent<BoxCollider>();
                        float w = _float(_get(parameters, "width", 0.80f));
                        float h = _float(_get(parameters, "height", 0.30f));
                        col.size = new Vector3(w, 0.1f, h);
                        col.center = new Vector3(0, 0, 0);
                    }
                    break;
                case "circle":
                    {
                        var col = gameObject.AddComponent<SphereCollider>();
                        float r = _float(_get(parameters, "radius", 0.40f));
                        col.radius = r;
                    }
                    break;
                default:
                    {
                        var col = gameObject.AddComponent<BoxCollider>();
                        col.size = new Vector3(0.80f, 0.1f, 0.30f);
                    }
                    break;
            }

            // 缓存碰撞半径
            switch (shape_type)
            {
                case "rect":
                    _cached_width = _float(_get(parameters, "width", 0.80f));
                    _cached_height = _float(_get(parameters, "height", 0.30f));
                    _cached_hit_radius = Mathf.Sqrt(_cached_width * _cached_width + _cached_height * _cached_height) / 2.0f + 0.14f;
                    break;
                case "circle":
                    _cached_radius = _float(_get(parameters, "radius", 0.40f));
                    _cached_hit_radius = _cached_radius + 0.14f;
                    break;
                case "crescent":
                    _cached_radius = _float(_get(parameters, "radius", 0.40f));
                    _cached_hit_radius = _cached_radius + 0.14f;
                    break;
                default:
                    _cached_hit_radius = 0.44f;
                    break;
            }
        }

        private int[] _fan_triangulate(Vector3[] verts)
        {
            // 以 verts[0] 为中心做扇形三角化（凹多边形效果可能不准，但够用）
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

        private List<Vector2> _build_crescent_points(float radius, float arc_angle)
        {
            var points = new List<Vector2>();
            int segments = 12;
            float half_arc = arc_angle / 2.0f;
            // 外弧
            for (int i = 0; i <= segments; i++)
            {
                float angle = -half_arc + (arc_angle * i / segments);
                points.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            // 内弧（反向，半径小一些）
            float inner_radius = radius * 0.6f;
            for (int i = 0; i <= segments; i++)
            {
                float angle = half_arc - (arc_angle * i / segments);
                points.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * inner_radius);
            }
            return points;
        }

        private void _create_visual(Dictionary<string, object> parameters)
        {
            visual_node = new GameObject("Visual");
            visual_node.transform.SetParent(transform, false);

            Color color_with_alpha = new Color(element_color.r, element_color.g, element_color.b, 0.7f);

            switch (shape_type)
            {
                case "rect":
                    {
                        var rect = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        rect.name = "Rect";
                        float w = _float(_get(parameters, "width", 0.80f));
                        float h = _float(_get(parameters, "height", 0.30f));
                        rect.transform.SetParent(visual_node.transform, false);
                        rect.transform.localScale = new Vector3(w, h, 1f);
                        var mr = rect.GetComponent<MeshRenderer>();
                        if (mr != null)
                        {
                            mr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                            mr.sharedMaterial.color = color_with_alpha;
                            mr.sortingOrder = 50;
                        }
                        // 旋转 quad 让它躺在 XZ 平面
                        var cam = Camera.main;
                        if (cam != null) rect.transform.rotation = cam.transform.rotation;
                    }
                    break;
                case "circle":
                    {
                        var circle = new GameObject("Circle");
                        circle.transform.SetParent(visual_node.transform, false);
                        float r = _float(_get(parameters, "radius", 0.40f));
                        // 用 Quad 近似圆形（实际圆形需要圆形 sprite）
                        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        quad.transform.SetParent(circle.transform, false);
                        quad.transform.localScale = new Vector3(r * 2, r * 2, 1f);
                        var mr = quad.GetComponent<MeshRenderer>();
                        if (mr != null)
                        {
                            mr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                            mr.sharedMaterial.color = color_with_alpha;
                            mr.sortingOrder = 50;
                        }
                        var cam = Camera.main;
                        if (cam != null) quad.transform.rotation = cam.transform.rotation;
                    }
                    break;
                case "crescent":
                    {
                        var crescent = new GameObject("Crescent");
                        crescent.transform.SetParent(visual_node.transform, false);
                        float r = _float(_get(parameters, "radius", 0.40f));
                        float arc_angle = _float(_get(parameters, "arc_angle", 120.0f)) * Mathf.Deg2Rad;
                        var points = _build_crescent_points(r, arc_angle);
                        // 用 Mesh 渲染月牙
                        var mf = crescent.AddComponent<MeshFilter>();
                        var mr = crescent.AddComponent<MeshRenderer>();
                        var mesh = new Mesh();
                        var verts = new Vector3[points.Count];
                        for (int i = 0; i < points.Count; i++)
                            verts[i] = new Vector3(points[i].x, points[i].y, 0);
                        mesh.vertices = verts;
                        mesh.triangles = _fan_triangulate(verts);
                        mesh.RecalculateNormals();
                        mf.sharedMesh = mesh;
                        mr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                        mr.sharedMaterial.color = color_with_alpha;
                        mr.sortingOrder = 50;
                    }
                    break;
            }

            // 边框
            _create_border(parameters);
        }

        private void _create_border(Dictionary<string, object> parameters)
        {
            Color border_color = new Color(element_color.r, element_color.g, element_color.b, 1.0f);
            var border_go = new GameObject("Border");
            border_go.transform.SetParent(visual_node.transform, false);
            var line = border_go.AddComponent<LineRenderer>();
            line.widthMultiplier = 0.02f;
            line.startColor = border_color;
            line.endColor = border_color;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.sortingOrder = 51;
            line.useWorldSpace = false;

            switch (shape_type)
            {
                case "rect":
                    {
                        float w = _float(_get(parameters, "width", 0.80f)) / 2.0f;
                        float h = _float(_get(parameters, "height", 0.30f)) / 2.0f;
                        line.positionCount = 5;
                        line.SetPosition(0, new Vector3(-w, -h, 0));
                        line.SetPosition(1, new Vector3(w, -h, 0));
                        line.SetPosition(2, new Vector3(w, h, 0));
                        line.SetPosition(3, new Vector3(-w, h, 0));
                        line.SetPosition(4, new Vector3(-w, -h, 0));
                    }
                    break;
                case "circle":
                    {
                        float r = _float(_get(parameters, "radius", 0.40f));
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
                        var pts = new List<Vector3>();
                        foreach (var p in points) pts.Add(new Vector3(p.x, p.y, 0));
                        if (points.Count > 0) pts.Add(new Vector3(points[0].x, points[0].y, 0));
                        line.positionCount = pts.Count;
                        for (int i = 0; i < pts.Count; i++) line.SetPosition(i, pts[i]);
                    }
                    break;
            }
        }

        private void _create_hp_bar()
        {
            // HP条：用 UI Slider 简化实现，作为子 GameObject
            hp_bar = new GameObject("HpBar");
            hp_bar.transform.SetParent(transform, false);
            // 简化：使用一个 SpriteRenderer Quad + 颜色变化近似 HP 条
            var sr_go = new GameObject("Fill");
            sr_go.transform.SetParent(hp_bar.transform, false);
            var sr = sr_go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(new Texture2D(1, 1), new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
            sr.color = new Color(0.2f, 0.9f, 0.2f);
            sr.sortingOrder = 60;
            sr_go.transform.localScale = new Vector3(0.40f, 0.05f, 1f);
            sr_go.transform.localPosition = new Vector3(0, 0.45f, 0);
        }

        private void _create_duration_timer()
        {
            if (remaining_duration <= 0.0f) return;
            duration_timer = remaining_duration;
            duration_timer_active = true;
        }

        // ==================== 球碰撞处理 ====================

        public void consume_frame(float delta)
        {
            // 每帧消耗：球卡在障碍物上时调用
            // 球的攻击力以 attack_consume_rate 速率消耗，同时消耗障碍物HP
            // 球速以 speed_consume_rate 速率消耗
            float atk_consumed = attack_consume_rate * delta;
            // float spd_consumed = speed_consume_rate * delta;  // 由 ball 自己处理
            obstacle_hp -= atk_consumed;
            _update_hp_bar();
        }

        private void _update_hp_bar()
        {
            if (hp_bar == null) return;
            var sr = hp_bar.GetComponentInChildren<SpriteRenderer>();
            if (sr == null) return;
            float ratio = max_obstacle_hp > 0f ? (obstacle_hp / max_obstacle_hp) : 0f;
            // 缩放表示 HP
            sr.transform.localScale = new Vector3(0.40f * Mathf.Clamp01(ratio), 0.05f, 1f);
            // HP低时变红
            if (ratio < 0.3f) sr.color = new Color(0.9f, 0.2f, 0.2f);
            else if (ratio < 0.6f) sr.color = new Color(0.9f, 0.7f, 0.2f);
        }

        // ==================== 生命周期 ====================

        protected virtual void Update()
        {
            // 倒计时
            if (duration_timer_active)
            {
                duration_timer -= Time.deltaTime;
                if (duration_timer <= 0f)
                {
                    duration_timer_active = false;
                    _on_duration_expired();
                }
            }
        }

        private void _destroy()
        {
            // 障碍物被摧毁
            if (obstacle_destroyed != null) obstacle_destroyed(this);
            Destroy(gameObject);
        }

        private void _on_duration_expired()
        {
            // 持续时间结束
            if (obstacle_expired != null) obstacle_expired(this);
            Destroy(gameObject);
        }

        public void remove()
        {
            // 外部调用移除（清除标签用）
            Destroy(gameObject);
        }

        // ==================== 查询 ====================

        public bool is_alive()
        {
            return obstacle_hp > 0.0f && this != null;
        }

        public float get_hp_ratio()
        {
            if (max_obstacle_hp <= 0.0f) return 0.0f;
            return obstacle_hp / max_obstacle_hp;
        }

        public float get_hit_radius()
        {
            return _cached_hit_radius;
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

// ================================================================
// 决竞球 Godot→Unity 翻译: SkillOutlineNode.cs [BattleBall.Systems.SpiritSystem]
// 源: E:/项目储存/决竞球battle-ball/scripts/systems/spirit_system/skill_outline_node.gd
// 集成备注:
//   1) Godot Node2D._draw() → Unity 使用 MeshRenderer + 动态生成 Mesh 自绘
//   2) draw_circle / draw_arc / draw_rect → 生成顶点构建 Mesh
//   3) z_index → MeshRenderer.sortingOrder
//   4) 父节点坐标系保持一致 (本地坐标, transform.SetParent(worldPositionStays=false))
//   5) GL.IssuePluginEvent / Graphics.DrawMeshNow 等方案不便于生命周期管理,
//      改用 MeshFilter + MeshRenderer + MeshRenderer.sharedMaterial
// ================================================================
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace BattleBall.Systems.SpiritSystem
{
    public enum DrawMode { RING, STRIP }

    public class SkillOutlineNode : MonoBehaviour
    {
        public DrawMode draw_mode = DrawMode.RING;
        public float ring_radius = 0.28f;      // Godot 28px → Unity 0.28m
        public float ring_width = 0.05f;       // Godot 5px → Unity 0.05m
        public Vector2 strip_dir = Vector2.right;
        public float strip_thickness = 0.08f;  // Godot 8px → Unity 0.08m
        public Color base_color = new Color(0.6f, 0.6f, 0.6f, 0.55f);
        public Color border_color = new Color(0.6f, 0.6f, 0.6f, 1.0f);

        protected MeshFilter _mesh_filter;
        protected MeshRenderer _mesh_renderer;
        protected Mesh _fill_mesh;     // 半透明填充
        protected Mesh _border_mesh;   // 实色边框

        // ===== 数值/类型转换辅助 =====
        protected static float _F(object o) { if (o == null) return 0f; float r; return float.TryParse(o.ToString(), out r) ? r : 0f; }
        protected static int _I(object o) { if (o == null) return 0; int r; return int.TryParse(o.ToString(), out r) ? r : 0; }
        protected static string _S(object o) { return o == null ? "" : o.ToString(); }

        protected virtual void Awake()
        {
            _mesh_filter = gameObject.GetComponent<MeshFilter>();
            if (_mesh_filter == null) _mesh_filter = gameObject.AddComponent<MeshFilter>();
            _mesh_renderer = gameObject.GetComponent<MeshRenderer>();
            if (_mesh_renderer == null) _mesh_renderer = gameObject.AddComponent<MeshRenderer>();
        }

        protected virtual void OnDestroy()
        {
            if (_fill_mesh != null) { Destroy(_fill_mesh); _fill_mesh = null; }
            if (_border_mesh != null) { Destroy(_border_mesh); _border_mesh = null; }
        }

        // ================================================================
        // 公开配置接口 (由 SkillVisualManager 调用)
        // ================================================================

        // 配置为圆环模式
        public virtual void setup_ring(float radius, float width, Color color, float alpha)
        {
            draw_mode = DrawMode.RING;
            ring_radius = radius;
            ring_width = width;
            base_color = new Color(color.r, color.g, color.b, alpha);
            border_color = new Color(color.r, color.g, color.b, 1.0f);
            // z_index = 5 → MeshRenderer.sortingOrder
            if (_mesh_renderer != null) _mesh_renderer.sortingOrder = 5;
            _rebuild_mesh();
        }

        // 配置为朝向条带模式
        public virtual void setup_strip(float radius, Vector2 facing, Color color, float alpha)
        {
            draw_mode = DrawMode.STRIP;
            ring_radius = radius;
            // 把朝向量化到四方向
            if (Mathf.Abs(facing.x) >= Mathf.Abs(facing.y))
                strip_dir = new Vector2(facing.x >= 0 ? 1.0f : -1.0f, 0.0f);
            else
                strip_dir = new Vector2(0.0f, facing.y >= 0 ? 1.0f : -1.0f);
            base_color = new Color(color.r, color.g, color.b, alpha);
            border_color = new Color(color.r, color.g, color.b, 1.0f);
            if (_mesh_renderer != null) _mesh_renderer.sortingOrder = 6;
            _rebuild_mesh();
        }

        // ================================================================
        // Mesh 重建 (等价于 Godot 的 queue_redraw() + _draw())
        // ================================================================

        protected virtual void _rebuild_mesh()
        {
            // 使用双 Mesh (填充 + 边框) 分层绘制
            // 简化方案: 用两个 child GameObject 分别持有 fill/border Mesh
            switch (draw_mode)
            {
                case DrawMode.RING:
                    _build_ring();
                    break;
                case DrawMode.STRIP:
                    _build_strip();
                    break;
            }
        }

        // 绘制圆环外膜: 中心半透明圆 + 实色边框
        // Godot: draw_circle(Vector2.ZERO, outer_r, base_color)
        //        draw_arc(Vector2.ZERO, outer_r, 0, TAU, 48, border_color, 2.0)
        protected virtual void _build_ring()
        {
            float outer_r = ring_radius + ring_width;
            int segments = 48;

            // === 半透明填充圆 ===
            var fill_verts = new List<Vector3>();
            var fill_tris = new List<int>();
            fill_verts.Add(Vector3.zero); // 中心顶点
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * 2f * Mathf.PI / segments;
                fill_verts.Add(new Vector3(Mathf.Cos(angle) * outer_r, Mathf.Sin(angle) * outer_r, 0));
            }
            for (int i = 1; i <= segments; i++)
            {
                fill_tris.Add(0);
                fill_tris.Add(i);
                fill_tris.Add(i + 1);
            }
            _fill_mesh = new Mesh { name = "RingFill" };
            _fill_mesh.SetVertices(fill_verts);
            _fill_mesh.SetTriangles(fill_tris, 0);
            _fill_mesh.RecalculateNormals();
            _apply_mesh_to_child("Fill", _fill_mesh, base_color, 5);

            // === 实色外边框 (环形线段近似) ===
            var border_verts = new List<Vector3>();
            var border_tris = new List<int>();
            float border_inner = outer_r - 0.02f; // 边框厚度 2px → 0.02m
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * 2f * Mathf.PI / segments;
                border_verts.Add(new Vector3(Mathf.Cos(angle) * outer_r, Mathf.Sin(angle) * outer_r, 0));
                border_verts.Add(new Vector3(Mathf.Cos(angle) * border_inner, Mathf.Sin(angle) * border_inner, 0));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2, b = i * 2 + 1, c = (i + 1) * 2, d = (i + 1) * 2 + 1;
                border_tris.Add(a); border_tris.Add(b); border_tris.Add(c);
                border_tris.Add(b); border_tris.Add(d); border_tris.Add(c);
            }
            _border_mesh = new Mesh { name = "RingBorder" };
            _border_mesh.SetVertices(border_verts);
            _border_mesh.SetTriangles(border_tris, 0);
            _border_mesh.RecalculateNormals();
            _apply_mesh_to_child("Border", _border_mesh, border_color, 6);
        }

        // 绘制朝向条带: 在朝向那一侧画一条圆角矩形色带
        // Godot: draw_rect(rect, base_color, true) + draw_rect(rect, border_color, false, 1.5)
        protected virtual void _build_strip()
        {
            float half_len = ring_radius;
            float half_thick = strip_thickness / 2.0f;
            Vector2 center = strip_dir * ring_radius;

            Rect rect;
            if (strip_dir.x != 0.0f)
            {
                // 左右朝向: 竖条
                rect = new Rect(center.x - half_thick, -half_len, strip_thickness, half_len * 2.0f);
            }
            else
            {
                // 上下朝向: 横条
                rect = new Rect(-half_len, center.y - half_thick, half_len * 2.0f, strip_thickness);
            }

            // === 半透明填充矩形 ===
            var fill_verts = new List<Vector3> {
                new Vector3(rect.xMin, rect.yMin, 0),
                new Vector3(rect.xMax, rect.yMin, 0),
                new Vector3(rect.xMax, rect.yMax, 0),
                new Vector3(rect.xMin, rect.yMax, 0),
            };
            var fill_tris = new List<int> { 0, 1, 2, 0, 2, 3 };
            _fill_mesh = new Mesh { name = "StripFill" };
            _fill_mesh.SetVertices(fill_verts);
            _fill_mesh.SetTriangles(fill_tris, 0);
            _fill_mesh.RecalculateNormals();
            _apply_mesh_to_child("Fill", _fill_mesh, base_color, 6);

            // === 实色外边框 (描边矩形, 厚度 1.5px → 0.015m) ===
            float bw = 0.015f;
            var border_verts = new List<Vector3> {
                // 外圈 4 顶点
                new Vector3(rect.xMin - bw, rect.yMin - bw, 0),
                new Vector3(rect.xMax + bw, rect.yMin - bw, 0),
                new Vector3(rect.xMax + bw, rect.yMax + bw, 0),
                new Vector3(rect.xMin - bw, rect.yMax + bw, 0),
                // 内圈 4 顶点
                new Vector3(rect.xMin, rect.yMin, 0),
                new Vector3(rect.xMax, rect.yMin, 0),
                new Vector3(rect.xMax, rect.yMax, 0),
                new Vector3(rect.xMin, rect.yMax, 0),
            };
            var border_tris = new List<int> {
                // 上边
                0, 4, 5,  0, 5, 1,
                // 右边
                1, 5, 6,  1, 6, 2,
                // 下边
                2, 6, 7,  2, 7, 3,
                // 左边
                3, 7, 4,  3, 4, 0,
            };
            _border_mesh = new Mesh { name = "StripBorder" };
            _border_mesh.SetVertices(border_verts);
            _border_mesh.SetTriangles(border_tris, 0);
            _border_mesh.RecalculateNormals();
            _apply_mesh_to_child("Border", _border_mesh, border_color, 7);
        }

        // 创建子 GameObject 持有 Mesh + Material (避免单 Mesh 的双材质冲突)
        protected virtual void _apply_mesh_to_child(string child_name, Mesh mesh, Color color, int sort_order)
        {
            // 清理旧子节点
            var old = transform.Find(child_name);
            if (old != null) Destroy(old.gameObject);

            var go = new GameObject(child_name);
            go.transform.SetParent(transform, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sortingOrder = sort_order;
            // 使用 Sprites/Default 着色器, 支持透明
            mr.sharedMaterial = _make_color_material(color);
        }

        // 创建一个透明色块 Material (基于 Sprites/Default 着色器)
        protected static Material _make_color_material(Color color)
        {
            var shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader != null ? shader : Shader.Find("Unlit/Transparent"));
            mat.color = color;
            return mat;
        }
    }
}

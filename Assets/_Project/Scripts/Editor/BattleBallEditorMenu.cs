using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BattleBall.EditorTools
{
    /// <summary>
    /// 顶部菜单入口：
    ///   BattleBall -> Apply All Presets (Models + Animations)
    /// 自动遍历所有3D资源并强制重导入，让 BattleBallImportProcessor 跑起来。
    /// </summary>
    public static class BattleBallEditorMenu
    {
        private static readonly string[] TargetDirs =
        {
            "Assets/_Project/Models",
            "Assets/_Project/Animations/Characters/Clips"
        };

        private static readonly HashSet<string> Extensions =
            new HashSet<string> { ".fbx", ".glb", ".gltf", ".obj" };

        [MenuItem("BattleBall/Apply All Presets (Models + Animations)")]
        public static void ApplyAllPresets()
        {
            var files = new List<string>();
            foreach (var dir in TargetDirs)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    if (Extensions.Contains(ext)) files.Add(f);
                }
            }

            if (files.Count == 0)
            {
                EditorUtility.DisplayDialog("BattleBall Phase 1",
                    "没找到任何 FBX/GLB 资源，先把 3D 模型复制到 Assets/_Project/Models 下再点。",
                    "OK");
                return;
            }

            int ok = EditorUtility.DisplayDialogComplex("BattleBall Phase 1",
                $"找到 {files.Count} 个 3D 资源。\n\n点击 OK 开始【强制重导入 + 自动应用预设】\n(预计 3~10 分钟，导入时别关编辑器)",
                "OK 开始", "取消", "");
            if (ok != 0) return;

            EditorUtility.DisplayProgressBar("BattleBall Phase 1",
                "正在重导入并应用预设...", 0f);

            try
            {
                int done = 0;
                foreach (var f in files)
                {
                    done++;
                    EditorUtility.DisplayProgressBar("BattleBall Phase 1",
                        $"[{done}/{files.Count}]  导入: {Path.GetFileName(f)}",
                        (float)done / files.Count);

                    var ap = f.Replace('\\', '/');
                    AssetDatabase.ImportAsset(ap, ImportAssetOptions.ForceUpdate);
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorUtility.DisplayDialog("BattleBall Phase 1",
                "Phase 1 自动预设完成！\n\n下一步：\n" +
                "① 把 player1~8 拖进场景看颜色是否正常\n" +
                "② 如果控制台有 [BattleBall] Avatar auto-config failed 的警告：\n" +
                "   双击那行，选中对应球员 FBX -> Rig -> Configure 手动补一下骨骼映射。\n" +
                "③ 动画的 Avatar Source 若找不到：先确保有一个球员的 Avatar Configure Done 成功\n" +
                "   再重新点一次菜单 Apply All Presets。",
                "OK");
        }
    }
}

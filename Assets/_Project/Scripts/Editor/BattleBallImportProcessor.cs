using UnityEditor;
using UnityEngine;
using System.IO;

namespace BattleBall.EditorTools
{
    public class BattleBallImportProcessor : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            var importer = (ModelImporter)assetImporter;
            // [TODO Step-A] 模型导入预设: globalScale / materialLocation / animationType
            Debug.Log("[ImportProcessor] Preprocess: " + importer.assetPath);
        }
    }
}

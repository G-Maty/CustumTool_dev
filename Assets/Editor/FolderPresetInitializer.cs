using System.IO;
using UnityEditor;
using UnityEngine;

namespace CustomTools.Editor
{
    /// <summary>
    /// Unity エディタ起動時（スクリプトコンパイル後）にデフォルトプリセットの存在を確認し、
    /// なければ自動生成する初期化クラス。
    /// </summary>
    [InitializeOnLoad]
    public static class FolderPresetInitializer
    {
        private const string PresetsFolder = "Assets/Editor/Presets";
        private const string DefaultPresetPath = "Assets/Editor/Presets/DefaultGameProject.asset";

        static FolderPresetInitializer()
        {
            // エディタの初期化完了後に実行するためデリゲート登録
            EditorApplication.delayCall += EnsureDefaultPreset;
        }

        private static void EnsureDefaultPreset()
        {
            // 既に存在する場合はスキップ
            if (File.Exists(DefaultPresetPath))
                return;

            // Presets フォルダがなければ作成
            if (!AssetDatabase.IsValidFolder(PresetsFolder))
            {
                AssetDatabase.CreateFolder("Assets/Editor", "Presets");
            }

            // デフォルトプリセットを作成
            var preset = ScriptableObject.CreateInstance<FolderPreset>();
            preset.presetName = "汎用ゲームプロジェクト";
            preset.folderNames = new[]
            {
                "Scripts",
                "Prefabs",
                "Materials",
                "Textures",
                "Audio",
                "Animations",
                "Scenes",
                "UI",
                "Shaders"
            };

            AssetDatabase.CreateAsset(preset, DefaultPresetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Folder Generator] デフォルトプリセットを作成しました: {DefaultPresetPath}");
        }
    }
}

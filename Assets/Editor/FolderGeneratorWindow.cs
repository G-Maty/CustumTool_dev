using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CustomTools.Editor
{
    /// <summary>
    /// 指定フォルダ内にプリセットで定義したフォルダ群を一括生成する EditorWindow。
    /// メニュー: Tools > Custom Tools > Folder Generator
    /// </summary>
    public class FolderGeneratorWindow : EditorWindow
    {
        // ────────────────────────────── フィールド ──────────────────────────────

        // 対象フォルダ
        private DefaultAsset _targetFolder;
        private string _newFolderName = "";
        private bool _showNewFolderField;

        // プリセット
        private FolderPreset _selectedPreset;
        private List<bool> _folderToggles = new List<bool>();
        private string _cachedPresetPath = "";

        // カスタムフォルダ追加
        private List<string> _customFolderNames = new List<string>();
        private string _newCustomFolderName = "";

        // スクロール
        private Vector2 _scrollPos;

        // ────────────────────────────── メニュー ──────────────────────────────

        [MenuItem("Tools/Custom Tools/Folder Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<FolderGeneratorWindow>("Folder Generator");
            window.minSize = new Vector2(360, 480);
        }

        // ────────────────────────────── GUI 描画 ──────────────────────────────

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawTargetFolderSection();
            EditorGUILayout.Space(8);
            DrawPresetSection();
            EditorGUILayout.Space(8);
            DrawCustomFolderSection();
            EditorGUILayout.Space(12);
            DrawGenerateButton();

            EditorGUILayout.EndScrollView();
        }

        // ======================== 1. 対象フォルダ指定 ========================

        private void DrawTargetFolderSection()
        {
            EditorGUILayout.LabelField("対象フォルダ", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _targetFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                    "フォルダ",
                    _targetFolder,
                    typeof(DefaultAsset),
                    false);

                if (GUILayout.Button("新規作成", GUILayout.Width(70)))
                {
                    _showNewFolderField = !_showNewFolderField;
                }
            }

            // 対象がフォルダでない場合は警告
            if (_targetFolder != null && !IsFolder(_targetFolder))
            {
                EditorGUILayout.HelpBox("選択されたアセットはフォルダではありません。フォルダを選択してください。", MessageType.Warning);
                _targetFolder = null;
            }

            // 新規フォルダ作成 UI
            if (_showNewFolderField)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("新しいフォルダを作成", EditorStyles.miniLabel);

                _newFolderName = EditorGUILayout.TextField("フォルダ名", _newFolderName);

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newFolderName)))
                {
                    if (GUILayout.Button("Assets 直下に作成して選択"))
                    {
                        CreateAndSelectNewFolder();
                    }
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void CreateAndSelectNewFolder()
        {
            string folderName = _newFolderName.Trim();
            string parentPath = "Assets";

            // 既に対象フォルダが指定されていれば、その中に作成する
            if (_targetFolder != null)
            {
                parentPath = AssetDatabase.GetAssetPath(_targetFolder);
            }

            string fullPath = $"{parentPath}/{folderName}";

            if (AssetDatabase.IsValidFolder(fullPath))
            {
                EditorUtility.DisplayDialog("情報", $"フォルダ '{fullPath}' は既に存在します。そのフォルダを選択しました。", "OK");
            }
            else
            {
                AssetDatabase.CreateFolder(parentPath, folderName);
                AssetDatabase.Refresh();
            }

            // Object として取得して設定
            _targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(fullPath);
            _newFolderName = "";
            _showNewFolderField = false;
        }

        // ======================== 2. プリセット選択 ========================

        private void DrawPresetSection()
        {
            EditorGUILayout.LabelField("プリセット", EditorStyles.boldLabel);

            var newPreset = (FolderPreset)EditorGUILayout.ObjectField(
                "プリセットアセット",
                _selectedPreset,
                typeof(FolderPreset),
                false);

            // プリセットが変更されたらトグルを再構築
            if (newPreset != _selectedPreset || HasPresetChanged(newPreset))
            {
                _selectedPreset = newPreset;
                RebuildToggles();
            }

            if (_selectedPreset == null)
            {
                EditorGUILayout.HelpBox("プリセットアセットを選択してください。\n右クリック > Create > Custom Tools > Folder Preset で新規作成できます。", MessageType.Info);
                return;
            }

            // プリセット情報表示
            EditorGUILayout.LabelField($"プリセット名: {_selectedPreset.presetName}", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("生成するフォルダ一覧:", EditorStyles.miniBoldLabel);

            if (_selectedPreset.folderNames == null || _selectedPreset.folderNames.Length == 0)
            {
                EditorGUILayout.HelpBox("プリセットにフォルダ名が設定されていません。", MessageType.Warning);
                return;
            }

            // 各フォルダのトグル表示
            EditorGUI.indentLevel++;
            for (int i = 0; i < _selectedPreset.folderNames.Length; i++)
            {
                if (i < _folderToggles.Count)
                {
                    _folderToggles[i] = EditorGUILayout.ToggleLeft(
                        _selectedPreset.folderNames[i],
                        _folderToggles[i]);
                }
            }
            EditorGUI.indentLevel--;

            // 一括操作
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("すべて選択", EditorStyles.miniButtonLeft))
                {
                    SetAllToggles(true);
                }
                if (GUILayout.Button("すべて解除", EditorStyles.miniButtonRight))
                {
                    SetAllToggles(false);
                }
            }
        }

        private void RebuildToggles()
        {
            _folderToggles.Clear();
            if (_selectedPreset != null && _selectedPreset.folderNames != null)
            {
                for (int i = 0; i < _selectedPreset.folderNames.Length; i++)
                {
                    _folderToggles.Add(true);
                }
                _cachedPresetPath = AssetDatabase.GetAssetPath(_selectedPreset);
            }
        }

        private bool HasPresetChanged(FolderPreset preset)
        {
            if (preset == null) return false;
            string path = AssetDatabase.GetAssetPath(preset);
            if (path != _cachedPresetPath) return true;
            if (preset.folderNames == null) return _folderToggles.Count > 0;
            return preset.folderNames.Length != _folderToggles.Count;
        }

        private void SetAllToggles(bool value)
        {
            for (int i = 0; i < _folderToggles.Count; i++)
            {
                _folderToggles[i] = value;
            }
        }

        // ======================== 3. カスタムフォルダ追加 ========================

        private void DrawCustomFolderSection()
        {
            EditorGUILayout.LabelField("カスタムフォルダ追加", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _newCustomFolderName = EditorGUILayout.TextField(_newCustomFolderName);

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newCustomFolderName)))
                {
                    if (GUILayout.Button("+", GUILayout.Width(30)))
                    {
                        string trimmed = _newCustomFolderName.Trim();
                        if (!_customFolderNames.Contains(trimmed))
                        {
                            _customFolderNames.Add(trimmed);
                        }
                        _newCustomFolderName = "";
                        GUI.FocusControl(null);
                    }
                }
            }

            // カスタムフォルダ一覧
            for (int i = _customFolderNames.Count - 1; i >= 0; i--)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"  + {_customFolderNames[i]}");
                    if (GUILayout.Button("×", GUILayout.Width(24)))
                    {
                        _customFolderNames.RemoveAt(i);
                    }
                }
            }
        }

        // ======================== 4. 生成ボタン ========================

        private void DrawGenerateButton()
        {
            bool hasTarget = _targetFolder != null;
            bool hasFolders = GetSelectedFolderCount() > 0;
            bool canGenerate = hasTarget && hasFolders;

            if (!hasTarget)
            {
                EditorGUILayout.HelpBox("対象フォルダを指定してください。", MessageType.None);
            }
            else if (!hasFolders)
            {
                EditorGUILayout.HelpBox("生成するフォルダが選択されていません。", MessageType.None);
            }

            using (new EditorGUI.DisabledScope(!canGenerate))
            {
                // 大きめのボタン
                GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 36
                };

                if (GUILayout.Button($"フォルダを生成 ({GetSelectedFolderCount()} 個)", buttonStyle))
                {
                    GenerateFolders();
                }
            }
        }

        // ======================== 生成ロジック ========================

        private void GenerateFolders()
        {
            string targetPath = AssetDatabase.GetAssetPath(_targetFolder);
            List<string> foldersToCreate = GetSelectedFolderNames();

            int createdCount = 0;
            int skippedCount = 0;
            List<string> createdFolders = new List<string>();
            List<string> skippedFolders = new List<string>();

            foreach (string folderName in foldersToCreate)
            {
                string fullPath = $"{targetPath}/{folderName}";

                if (AssetDatabase.IsValidFolder(fullPath))
                {
                    skippedCount++;
                    skippedFolders.Add(folderName);
                    Debug.Log($"[Folder Generator] スキップ (既存): {fullPath}");
                }
                else
                {
                    AssetDatabase.CreateFolder(targetPath, folderName);
                    createdCount++;
                    createdFolders.Add(folderName);
                    Debug.Log($"[Folder Generator] 作成: {fullPath}");
                }
            }

            AssetDatabase.Refresh();

            // 結果サマリー
            string summary = $"フォルダ生成完了!\n\n" +
                             $"作成: {createdCount} 個\n" +
                             $"スキップ (既存): {skippedCount} 個\n\n" +
                             $"対象: {targetPath}";

            if (createdFolders.Count > 0)
            {
                summary += $"\n\n作成されたフォルダ:\n  {string.Join("\n  ", createdFolders)}";
            }

            if (skippedFolders.Count > 0)
            {
                summary += $"\n\nスキップされたフォルダ:\n  {string.Join("\n  ", skippedFolders)}";
            }

            Debug.Log($"[Folder Generator] {summary}");
            EditorUtility.DisplayDialog("Folder Generator", summary, "OK");
        }

        // ======================== ヘルパー ========================

        private int GetSelectedFolderCount()
        {
            int count = 0;

            // プリセットからの選択数
            if (_selectedPreset != null && _selectedPreset.folderNames != null)
            {
                for (int i = 0; i < _selectedPreset.folderNames.Length && i < _folderToggles.Count; i++)
                {
                    if (_folderToggles[i]) count++;
                }
            }

            // カスタムフォルダ数
            count += _customFolderNames.Count;

            return count;
        }

        private List<string> GetSelectedFolderNames()
        {
            List<string> names = new List<string>();

            // プリセットから有効なもの
            if (_selectedPreset != null && _selectedPreset.folderNames != null)
            {
                for (int i = 0; i < _selectedPreset.folderNames.Length && i < _folderToggles.Count; i++)
                {
                    if (_folderToggles[i] && !string.IsNullOrWhiteSpace(_selectedPreset.folderNames[i]))
                    {
                        names.Add(_selectedPreset.folderNames[i].Trim());
                    }
                }
            }

            // カスタムフォルダ
            foreach (string customName in _customFolderNames)
            {
                if (!names.Contains(customName))
                {
                    names.Add(customName);
                }
            }

            return names;
        }

        private static bool IsFolder(DefaultAsset asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            return AssetDatabase.IsValidFolder(path);
        }
    }
}

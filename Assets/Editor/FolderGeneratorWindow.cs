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

        // 生成結果
        private bool _showResult;
        private int _resultCreatedCount;
        private int _resultSkippedCount;
        private string _resultTargetPath = "";
        private List<string> _resultCreatedFolders = new List<string>();
        private List<string> _resultSkippedFolders = new List<string>();

        // ────────────────────────────── メニュー ──────────────────────────────

        [MenuItem("Tools/Custom Tools/Folder Generator")] // ヘッダメニュー名/ヘッダ以下のメニュー名
        /// <summary>
        /// Folder Generator ウィンドウを開き、最小サイズを設定します。
        /// </summary>
        public static void ShowWindow()
        {
            var window = GetWindow<FolderGeneratorWindow>("Folder Generator");
            window.minSize = new Vector2(360, 480);
        }

        // ────────────────────────────── GUI 描画 ──────────────────────────────

        /// <summary>
        /// UI本体：EditorWindowの画面を毎フレーム描画
        /// エディタウィンドウ全体の GUI を描画
        /// </summary>
        private void OnGUI()
        {
            // スクロール位置を保持しつつ、全体をスクロール可能にする
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // 対象フォルダを選ぶUI
            DrawTargetFolderSection();
            EditorGUILayout.Space(8);

            // プリセット選択UI
            DrawPresetSection();
            EditorGUILayout.Space(8);

            // カスタムフォルダ追加UI
            DrawCustomFolderSection();
            EditorGUILayout.Space(12);

            // 生成ボタン
            DrawGenerateButton();

            // 生成結果表示
            if (_showResult)
            {
                EditorGUILayout.Space(8);
                DrawResultSection();
            }

            EditorGUILayout.EndScrollView();
        }

        // ======================== 1. 対象フォルダ指定 ========================

        /// <summary>
        /// 対象フォルダの選択 UI と新規フォルダ作成 UI を描画
        /// </summary>
        private void DrawTargetFolderSection()
        {
            EditorGUILayout.LabelField("対象フォルダ", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope()) // 水平レイアウトでフォルダ選択と新規作成ボタンを配置
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

            // 新規フォルダ作成 UI：ボタンが押されたときに処理される
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

        /// <summary>
        /// 入力内容をもとに新しいフォルダを作成し、対象フォルダとして選択
        /// </summary>
        private void CreateAndSelectNewFolder()
        {
            string folderName = _newFolderName.Trim();
            string parentPath = "Assets";

            string fullPath = $"{parentPath}/{folderName}";

            if (AssetDatabase.IsValidFolder(fullPath))
            {
                EditorUtility.DisplayDialog("情報", $"フォルダ '{fullPath}' は既に存在します。そのフォルダを選択しました。", "OK");
            }
            else
            {
                AssetDatabase.CreateFolder(parentPath, folderName);
                AssetDatabase.Refresh(); //Project 内のアセット状態を再読み込みして更新するため
            }

            // 作成したフォルダをターゲットフォルダに設定
            _targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(fullPath);
            _newFolderName = "";
            _showNewFolderField = false;
        }

        // ======================== 2. プリセット選択 ========================

        /// <summary>
        /// プリセットの選択 UI とフォルダ選択トグルを描画
        /// </summary>
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

        /// <summary>
        /// 現在のプリセット内容に合わせてフォルダ選択トグルを再構築
        /// </summary>
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

        /// <summary>
        /// 指定したプリセットが現在のキャッシュ状態から変更されているかを判定
        /// </summary>
        /// <param name="preset">変更有無を確認するプリセットです。</param>
        /// <returns>プリセット構成が変化していれば true を返します。</returns>
        private bool HasPresetChanged(FolderPreset preset)
        {
            if (preset == null) return false;
            string path = AssetDatabase.GetAssetPath(preset);
            if (path != _cachedPresetPath) return true;
            if (preset.folderNames == null) return _folderToggles.Count > 0;
            return preset.folderNames.Length != _folderToggles.Count;
        }

        /// <summary>
        /// すべてのフォルダ選択トグルを指定した値に設定
        /// </summary>
        /// <param name="value">各トグルに設定する値です。</param>
        private void SetAllToggles(bool value)
        {
            for (int i = 0; i < _folderToggles.Count; i++)
            {
                _folderToggles[i] = value;
            }
        }

        // ======================== 3. カスタムフォルダ追加 ========================

        /// <summary>
        /// カスタムフォルダ名の追加・削除 UI を描画
        /// </summary>
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

        /// <summary>
        /// 生成可否の状態に応じてフォルダ生成ボタンと補足メッセージを描画
        /// </summary>
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

        /// <summary>
        /// 選択されたフォルダ名を対象フォルダ配下に生成し、結果を記録
        /// </summary>
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

            // 結果をフィールドに保存してウィンドウ内に表示
            _showResult = true;
            _resultCreatedCount = createdCount;
            _resultSkippedCount = skippedCount;
            _resultTargetPath = targetPath;
            _resultCreatedFolders = createdFolders;
            _resultSkippedFolders = skippedFolders;

            Debug.Log($"[Folder Generator] フォルダ生成完了 — 作成: {createdCount} 個, スキップ: {skippedCount} 個 ({targetPath})");
        }

        // ======================== 5. 結果表示 ========================

        /// <summary>
        /// フォルダ生成結果のサマリーと詳細一覧を描画
        /// </summary>
        private void DrawResultSection()
        {
            // 区切り線
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // ヘッダーと閉じるボタン
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("生成結果", EditorStyles.boldLabel);
                if (GUILayout.Button("×", GUILayout.Width(24)))
                {
                    _showResult = false;
                    return;
                }
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // サマリー
            EditorGUILayout.LabelField($"対象: {_resultTargetPath}", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(4);

            // 作成カウント（緑系）
            var createdStyle = new GUIStyle(EditorStyles.label) { richText = true };
            EditorGUILayout.LabelField(
                $"<b>作成:</b> {_resultCreatedCount} 個　　<b>スキップ (既存):</b> {_resultSkippedCount} 個",
                createdStyle);

            // 作成されたフォルダ一覧
            if (_resultCreatedFolders.Count > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("作成されたフォルダ:", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                foreach (string folder in _resultCreatedFolders)
                {
                    EditorGUILayout.LabelField($"+ {folder}", EditorStyles.miniLabel);
                }
                EditorGUI.indentLevel--;
            }

            // スキップされたフォルダ一覧
            if (_resultSkippedFolders.Count > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("スキップされたフォルダ:", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                foreach (string folder in _resultSkippedFolders)
                {
                    EditorGUILayout.LabelField($"- {folder}", EditorStyles.miniLabel);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        // ======================== ヘルパー ========================

        /// <summary>
        /// 現在選択されている生成対象フォルダ数を取得
        /// </summary>
        /// <returns>プリセットとカスタム入力を含む選択済みフォルダ数を返します。</returns>
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

        /// <summary>
        /// 現在選択されているフォルダ名の一覧を重複を除いて取得
        /// </summary>
        /// <returns>生成対象となるフォルダ名の一覧を返します。</returns>
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

        /// <summary>
        /// 指定したアセットがフォルダかどうかを判定
        /// </summary>
        /// <param name="asset">判定対象のアセットです。</param>
        /// <returns>アセットが有効なフォルダであれば true を返します。</returns>
        private static bool IsFolder(DefaultAsset asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            return AssetDatabase.IsValidFolder(path);
        }
    }
}

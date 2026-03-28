# CustomTool_dev

Unity Editor 拡張として動作するフォルダ生成ツールです。指定した対象フォルダ配下に、プリセットで定義した複数のフォルダを一括生成できます。

## 概要

このプロジェクトには、Unity のメニューから開ける Folder Generator ウィンドウが含まれています。

- 対象フォルダを選択して生成先を指定
- ScriptableObject ベースのプリセットから生成フォルダを選択
- 任意のカスタムフォルダ名を追加
- 既存フォルダはスキップし、結果をウィンドウ内に表示
- 初回起動時にデフォルトプリセットを自動生成

## 対応環境

- Unity 6
  - 確認済みプロジェクトバージョン: 6000.3.12f1
- Editor 専用アセンブリ
  - Assets/Editor/CustomTools.Editor.asmdef

## 主なファイル

- Assets/Editor/FolderGeneratorWindow.cs
  - メインの EditorWindow 実装
- Assets/Editor/FolderPreset.cs
  - フォルダ構成を保持する ScriptableObject
- Assets/Editor/FolderPresetInitializer.cs
  - デフォルトプリセットの自動生成処理
- Assets/Editor/Presets/DefaultGameProject.asset
  - 起動時に用意される既定プリセット

## 使い方

1. Unity Editor でプロジェクトを開く
2. メニューの Tools > Custom Tools > Folder Generator を開く
3. 対象フォルダを選択する
4. 必要に応じてプリセットアセットを指定する
5. 生成したいフォルダだけをチェックする
6. 必要ならカスタムフォルダ名を追加する
7. フォルダを生成 ボタンを押す

## 対象フォルダの指定

- Project ウィンドウ上の既存フォルダを選択できます
- 新規作成 ボタンから Assets 直下に新しいフォルダを作成し、そのまま対象として選択できます
- フォルダ以外のアセットを選択した場合は警告が表示されます

## プリセット

プリセットは FolderPreset ScriptableObject で管理します。新規作成は Project ウィンドウで以下のメニューから行えます。

- Create > Custom Tools > Folder Preset

プリセットには次の情報を保持します。

- presetName
- folderNames

Folder Generator では、プリセットに含まれるフォルダ一覧を個別にオンオフできます。すべて選択、すべて解除にも対応しています。

## デフォルトプリセット

プロジェクトの読み込み後、既定のプリセットが存在しない場合は自動で以下に作成されます。

- Assets/Editor/Presets/DefaultGameProject.asset

既定プリセットに含まれるフォルダは次のとおりです。

- Scripts
- Prefabs
- Materials
- Textures
- Audio
- Animations
- Scenes
- UI
- Shaders

## 生成時の挙動

- 既に存在するフォルダは作成せずスキップします
- 新規作成数とスキップ数を結果欄に表示します
- 作成したフォルダ名とスキップしたフォルダ名をそれぞれ一覧表示します
- 生成後に AssetDatabase.Refresh を実行します

## 補足

- このツールは Editor フォルダ配下に配置されており、ランタイムビルドには含まれません
- デフォルトの生成先親フォルダは Assets です
- カスタムフォルダ名は重複追加されないよう制御されています

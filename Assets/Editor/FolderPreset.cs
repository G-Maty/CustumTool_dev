using UnityEngine;

namespace CustomTools.Editor
{
    /// <summary>
    /// フォルダ構成のプリセットを定義する ScriptableObject。
    /// 右クリックメニューから新しいプリセットアセットを作成できる。
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewFolderPreset",
        menuName = "Custom Tools/Folder Preset",
        order = 100)]
    public class FolderPreset : ScriptableObject
    {
        [Tooltip("プリセットの表示名")]
        public string presetName = "New Preset";

        [Tooltip("生成するフォルダ名の一覧")]
        public string[] folderNames = new string[0];
    }
}

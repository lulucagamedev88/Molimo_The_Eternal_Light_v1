using UnityEngine;
using UnityEditor;
using System.IO;

namespace MolimoByLulucaGameDev
{
    public class BatchRenamerWindow : EditorWindow
    {
        private string baseName = "Objeto";
        private string prefix = "";
        private string suffix = "";
        private int startIndex = 1;

        private string searchString = "";
        private string replaceString = "";

        private enum RenameMode { Sequential, FindAndReplace, AddPrefixSuffix }
        private RenameMode renameMode = RenameMode.Sequential;

        [MenuItem("Tools/Molimo/Ferramenta de Renomear em Massa")]
        public static void OpenWindow()
        {
            BatchRenamerWindow window = GetWindow<BatchRenamerWindow>("Renomeador em Massa");
            window.minSize = new Vector2(350, 320);
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            GUILayout.Label("Ferramenta de Renomeação em Massa", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Selecione os itens na Hierarquia ou no Project e escolha o modo de operação desejado.", MessageType.Info);
            EditorGUILayout.Space(5);

            // Modo de operação
            renameMode = (RenameMode)EditorGUILayout.EnumPopup("Modo de Operação", renameMode);
            EditorGUILayout.Space(10);

            // Campos dinâmicos dependendo do modo
            switch (renameMode)
            {
                case RenameMode.Sequential:
                    baseName = EditorGUILayout.TextField("Nome Base", baseName);
                    startIndex = EditorGUILayout.IntField("Índice Inicial", startIndex);
                    break;

                case RenameMode.FindAndReplace:
                    searchString = EditorGUILayout.TextField("Procurar por", searchString);
                    replaceString = EditorGUILayout.TextField("Substituir por", replaceString);
                    break;

                case RenameMode.AddPrefixSuffix:
                    EditorGUILayout.HelpBox("Apenas adicionará o Prefixo e/ou Sufixo mantendo o nome atual.", MessageType.None);
                    break;
            }

            EditorGUILayout.Space(5);
            prefix = EditorGUILayout.TextField("Prefixo (Opcional)", prefix);
            suffix = EditorGUILayout.TextField("Sufixo (Opcional)", suffix);

            EditorGUILayout.Space(20);

            // Botões de Ação
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Renomear Selecionados (Hierarquia)", GUILayout.Height(35)))
            {
                RenameHierarchyObjects();
            }

            EditorGUILayout.Space(5);

            GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
            if (GUILayout.Button("Renomear Selecionados (Project Assets)", GUILayout.Height(35)))
            {
                RenameProjectAssets();
            }

            GUI.backgroundColor = Color.white;
        }

        private void RenameHierarchyObjects()
        {
            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("Aviso", "Nenhum GameObject selecionado na Hierarquia!", "OK");
                return;
            }

            // Ordena com base na ordem visual da hierarquia
            System.Array.Sort(selectedObjects, (a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

            int index = startIndex;
            foreach (var obj in selectedObjects)
            {
                Undo.RecordObject(obj, "Batch Rename Hierarchy");
                string newName = GenerateNewName(obj.name, ref index);

                obj.name = newName;
                EditorUtility.SetDirty(obj);
            }

            Debug.Log($"[BatchRenamer] {selectedObjects.Length} objetos renomeados na Hierarquia com sucesso!");
        }

        private void RenameProjectAssets()
        {
            string[] guids = Selection.assetGUIDs;
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("Aviso", "Nenhum asset selecionado na aba Project!", "OK");
                return;
            }

            int index = startIndex;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;

                // Evita tentar renomear pastas diretamente por segurança
                if (Directory.Exists(path)) continue;

                string fileName = Path.GetFileNameWithoutExtension(path);
                string newFileName = GenerateNewName(fileName, ref index);

                string result = AssetDatabase.RenameAsset(path, newFileName);
                if (!string.IsNullOrEmpty(result))
                {
                    Debug.LogWarning($"[BatchRenamer] Erro ao renomear asset em {path}: {result}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[BatchRenamer] Processo concluído para os assets selecionados!");
        }

        private string GenerateNewName(string originalName, ref int index)
        {
            string computedName = originalName;

            switch (renameMode)
            {
                case RenameMode.Sequential:
                    computedName = $"{baseName}_{index:00}";
                    index++;
                    break;

                case RenameMode.FindAndReplace:
                    if (!string.IsNullOrEmpty(searchString))
                    {
                        computedName = originalName.Replace(searchString, replaceString);
                    }
                    break;

                case RenameMode.AddPrefixSuffix:
                    computedName = originalName;
                    break;
            }

            return $"{prefix}{computedName}{suffix}";
        }
    }
}
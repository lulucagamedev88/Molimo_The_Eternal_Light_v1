using UnityEngine;
using UnityEditor;

namespace MolimoByLulucaGameDev
{
    [CustomEditor(typeof(WallChainGenerator))]
    public class WallChainGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            WallChainGenerator generator = (WallChainGenerator)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Gerenciador de Pontos", EditorStyles.boldLabel);

            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("➕ Adicionar Novo Ponto", GUILayout.Height(35)))
            {
                Undo.RecordObject(generator, "Add Wall Point");
                generator.AddPoint();
                EditorUtility.SetDirty(generator);
            }

            GUI.backgroundColor = new Color(0.9f, 0.5f, 0.4f);
            using (new EditorGUI.DisabledScope(generator.Points.Count == 0))
            {
                if (GUILayout.Button("➖ Remover Último Ponto", GUILayout.Height(30)))
                {
                    Undo.RecordObject(generator, "Remove Wall Point");
                    generator.RemoveLastPoint();
                    EditorUtility.SetDirty(generator);
                }
            }

            GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
            using (new EditorGUI.DisabledScope(generator.Points.Count == 0))
            {
                if (GUILayout.Button("🗑️ Limpar Todos os Pontos", GUILayout.Height(25)))
                {
                    if (EditorUtility.DisplayDialog(
                            "Confirmação",
                            "Deseja apagar todos os pontos de limite?",
                            "Sim", "Não"))
                    {
                        Undo.RecordObject(generator, "Clear Wall Points");
                        generator.ClearAllPoints();
                        EditorUtility.SetDirty(generator);
                    }
                }
            }

            GUI.backgroundColor = Color.white;

            // [NOVO] Botão para restaurar a cor padrão do gizmo.
            // Útil quando o campo Color foi herdado de uma versão antiga
            // e ficou rosa/magenta na cena.
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Visualização", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Cor atual do gizmo:", GUILayout.Width(130));
                Rect colorRect = GUILayoutUtility.GetRect(60, 18);
                EditorGUI.DrawRect(colorRect, generator.GizmoColor);
            }

            GUI.backgroundColor = new Color(0.6f, 0.6f, 0.9f);
            if (GUILayout.Button("🎨 Restaurar Cor Padrão do Gizmo"))
            {
                Undo.RecordObject(generator, "Reset Gizmo Color");
                generator.ResetGizmoColor();
                EditorUtility.SetDirty(generator);
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;
        }

        private void OnSceneGUI()
        {
            WallChainGenerator generator = (WallChainGenerator)target;
            var points = generator.Points;

            if (points == null || points.Count == 0) return;

            for (int i = 0; i < points.Count; i++)
            {
                Transform point = points[i];
                if (point == null) continue;

                Quaternion handleRotation = Quaternion.identity;
                if (points.Count > 1)
                {
                    Vector3 dir = Vector3.zero;
                    if (i < points.Count - 1 && points[i + 1] != null)
                        dir = points[i + 1].position - point.position;
                    else if (i > 0 && points[i - 1] != null)
                        dir = point.position - points[i - 1].position;

                    dir.y = 0;
                    if (dir.sqrMagnitude > 1e-4f)
                        handleRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                }

                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(point.position, handleRotation);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(point, "Move Wall Point");
                    point.position = newPos;
                    EditorUtility.SetDirty(point);
                    generator.MarkDirty();
                    generator.GenerateWall();
                }

                Handles.Label(point.position + Vector3.up * 0.5f,
                              $"Ponto {i}", EditorStyles.whiteBoldLabel);
            }
        }
    }
}
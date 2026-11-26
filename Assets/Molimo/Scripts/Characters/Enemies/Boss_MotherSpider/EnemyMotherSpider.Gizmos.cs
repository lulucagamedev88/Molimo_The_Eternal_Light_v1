using UnityEngine;

namespace MolimoByLulucaGameDev
{

    public partial class EnemyMotherSpider
    {
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, disengageRadius);
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, attackZoneRadius);
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, secondaryAttackRange);
            Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, landAttackRadius);

            if (Application.isPlaying)
            {
                Gizmos.color = new Color(0.2f, 1f, 1f, 0.8f);
                Gizmos.DrawWireSphere(_homePosition, returnHomeArriveDistance);
                Gizmos.DrawLine(transform.position, _homePosition);
            }

            DrawPathGizmos(pathWaypoints, Color.magenta, "PRIMARY");

            DrawPathGizmos(secondaryPathWaypoints, Color.cyan, "SECONDARY");

            if (Application.isPlaying && HasUsablePath())
            {
                Gizmos.color = _usingSecondaryPath ? Color.cyan : Color.magenta;
                Gizmos.DrawWireSphere(GetPositionAtDistance(_currentPathDistance), 0.28f);

                if (HasPlayer())
                {
                    bool useSecondary = ShouldUseSecondaryPath(playerTarget.position);
                    Gizmos.color = useSecondary
                        ? new Color(0f, 1f, 1f, 0.9f)
                        : new Color(1f, 0f, 1f, 0.9f);
                    Gizmos.DrawLine(playerTarget.position, transform.position);
                }
            }
        }

        private void DrawPathGizmos(Transform[] path, Color color, string label)
        {
            if (path == null || path.Length < 2) return;

            Gizmos.color = color;
            for (int i = 0; i < path.Length; i++)
            {
                if (path[i] == null) continue;
                Gizmos.DrawSphere(path[i].position, 0.15f);
                if (i < path.Length - 1 && path[i + 1] != null)
                    Gizmos.DrawLine(path[i].position, path[i + 1].position);
            }

#if UNITY_EDITOR
            UnityEditor.Handles.color = color;
            if (path[0] != null)
                UnityEditor.Handles.Label(path[0].position + Vector3.up * 0.4f, label);
#endif
        }
    }
}

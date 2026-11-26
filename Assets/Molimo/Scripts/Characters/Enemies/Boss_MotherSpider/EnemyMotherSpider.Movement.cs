using UnityEngine;

namespace MolimoByLulucaGameDev
{

    public partial class EnemyMotherSpider
    {

        private Vector3 _chaseDir;
        private Quaternion _snappedRotation;
        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private float _pathLength;
        private float _currentPathDistance;
        private float _stuckTimer;
        private Vector3 _stuckLastPos;
        private bool _stuckWarned;
        private PhysicsMaterial _frictionMaterial;

        private bool _usingSecondaryPath;

        private Transform[] GetActivePath()
        {
            if (_usingSecondaryPath &&
                secondaryPathWaypoints != null && secondaryPathWaypoints.Length >= 2)
                return secondaryPathWaypoints;
            return pathWaypoints;
        }

        private bool HasUsablePath()
        {
            Transform[] path = GetActivePath();
            return path != null && path.Length >= 2;
        }

        private static bool IsPathUsable(Transform[] path)
            => path != null && path.Length >= 2;

        private bool ShouldUseSecondaryPath(Vector3 target)
        {
            bool hasPrimary = IsPathUsable(pathWaypoints);
            bool hasSecondary = IsPathUsable(secondaryPathWaypoints);

            if (!hasSecondary) return false;
            if (!hasPrimary) return true;

            float dPrimary = DistanceFromPointToPath(pathWaypoints, target);
            float dSecondary = DistanceFromPointToPath(secondaryPathWaypoints, target);

            if (_usingSecondaryPath)
                return dSecondary < dPrimary + 1f;
            return dSecondary + 1f < dPrimary;
        }

        private float DistanceFromPointToPath(Transform[] path, Vector3 point)
        {
            if (!IsPathUsable(path)) return float.MaxValue;

            Vector3 p = point; p.y = 0f;
            float bestSq = float.MaxValue;

            for (int i = 0; i < path.Length - 1; i++)
            {
                if (path[i] == null || path[i + 1] == null) continue;

                Vector3 a = path[i].position; a.y = 0f;
                Vector3 b = path[i + 1].position; b.y = 0f;
                Vector3 ab = b - a;
                float lenSq = ab.sqrMagnitude;
                if (lenSq < 0.0001f) continue;

                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / lenSq);
                Vector3 proj = a + ab * t;
                float dsq = (proj - p).sqrMagnitude;
                if (dsq < bestSq) bestSq = dsq;
            }

            return Mathf.Sqrt(bestSq);
        }

        private bool RefreshActivePathForTarget(Vector3 target)
        {
            bool useSecondary = ShouldUseSecondaryPath(target);
            if (useSecondary == _usingSecondaryPath) return false;

            _usingSecondaryPath = useSecondary;
            RecalculatePathLength();
            if (HasUsablePath())
                _currentPathDistance = GetClosestDistanceOnPathToTarget(rb != null ? rb.position : transform.position);

            if (logPath)
                Debug.Log($"[EnemyMotherSpider] Path SWITCH → {(_usingSecondaryPath ? "SECONDARY" : "PRIMARY")} " +
                          $"(len={_pathLength:F2}, dist={_currentPathDistance:F2})");
            return true;
        }

        private void RecalculatePathLength()
        {
            _pathLength = 0f;
            if (!HasUsablePath()) return;

            Transform[] path = GetActivePath();
            for (int i = 0; i < path.Length - 1; i++)
            {
                Transform the = path[i];
                Transform b = path[i + 1];
                if (the == null || b == null) continue;
                _pathLength += Vector3.Distance(the.position, b.position);
            }

            if (logPath)
                Debug.Log($"[EnemyMotherSpider] Path length = {_pathLength:F2} m " +
                          $"({path.Length} waypoints, active={(_usingSecondaryPath ? "SECONDARY" : "PRIMARY")}).");
        }

        private void InitializePathDistance()
        {
            if (!HasUsablePath()) { _currentPathDistance = 0f; return; }
            _currentPathDistance = GetClosestDistanceOnPathToTarget(transform.position);
            if (logPath)
                Debug.Log($"[EnemyMotherSpider] _currentPathDistance init = {_currentPathDistance:F2} / {_pathLength:F2}.");
        }

        private Vector3 GetPositionAtDistance(float distance)
        {
            if (!HasUsablePath()) return transform.position;
            distance = Mathf.Clamp(distance, 0f, _pathLength);

            Transform[] path = GetActivePath();
            float acc = 0f;
            for (int i = 0; i < path.Length - 1; i++)
            {
                Transform the = path[i];
                Transform b = path[i + 1];
                if (the == null || b == null) continue;

                Vector3 pa = the.position; pa.y = 0f;
                Vector3 pb = b.position; pb.y = 0f;
                Vector3 ab = pb - pa;
                float segLen = ab.magnitude;
                if (segLen < 0.0001f) continue;

                if (acc + segLen >= distance)
                {
                    float t = (distance - acc) / segLen;
                    Vector3 p = Vector3.Lerp(pa, pb, t);
                    p.y = the.position.y;
                    return p;
                }
                acc += segLen;
            }
            return path[path.Length - 1].position;
        }

        private Vector3 GetPathTangent(float distance)
        {
            if (!HasUsablePath()) return transform.forward;
            distance = Mathf.Clamp(distance, 0f, _pathLength);

            Transform[] path = GetActivePath();
            float acc = 0f;
            for (int i = 0; i < path.Length - 1; i++)
            {
                Transform the = path[i];
                Transform b = path[i + 1];
                if (the == null || b == null) continue;

                Vector3 pa = the.position; pa.y = 0f;
                Vector3 pb = b.position; pb.y = 0f;
                Vector3 ab = pb - pa;
                float segLen = ab.magnitude;
                if (segLen < 0.0001f) continue;

                if (acc + segLen >= distance || i == path.Length - 2)
                    return ab / segLen;
                acc += segLen;
            }
            return transform.forward;
        }

        private float GetClosestDistanceOnPathToTarget(Vector3 target)
        {
            if (!HasUsablePath()) return 0f;

            Transform[] path = GetActivePath();
            Vector3 vt = target; vt.y = 0f;
            float bestSq = float.MaxValue;
            float bestDist = 0f;

            float acc = 0f;
            for (int i = 0; i < path.Length - 1; i++)
            {
                Transform the = path[i];
                Transform b = path[i + 1];
                if (the == null || b == null) continue;

                Vector3 pa = the.position; pa.y = 0f;
                Vector3 pb = b.position; pb.y = 0f;
                Vector3 ab = pb - pa;
                float segLenSq = ab.sqrMagnitude;
                if (segLenSq < 0.0001f) continue;
                float segLen = Mathf.Sqrt(segLenSq);

                float t = Mathf.Clamp01(Vector3.Dot(vt - pa, ab) / segLenSq);
                Vector3 proj = pa + ab * t;
                float dsq = (proj - vt).sqrMagnitude;

                if (dsq < bestSq) { bestSq = dsq; bestDist = acc + t * segLen; }
                acc += segLen;
            }
            return bestDist;
        }

        private void ComputePathDirectionToward(Vector3 worldTarget)
        {
            _chaseDir = Vector3.zero;
            if (!HasUsablePath() || _pathLength <= 0.0001f) return;

            float targetDist = GetClosestDistanceOnPathToTarget(worldTarget);
            float diff = targetDist - _currentPathDistance;

            if (Mathf.Abs(diff) < 0.05f) return;

            Vector3 tangent = GetPathTangent(_currentPathDistance);
            _chaseDir = tangent * Mathf.Sign(diff);
        }

        private void SnapToPath()
        {
            if (!HasUsablePath()) return;

            _currentPathDistance = GetClosestDistanceOnPathToTarget(rb != null ? rb.position : transform.position);
            Vector3 targetPos = GetPositionAtDistance(_currentPathDistance);
            targetPos.y = rb != null ? rb.position.y : transform.position.y;

            if (rb != null)
            {
                rb.position = targetPos;
                if (!rb.isKinematic) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            }
            else transform.position = targetPos;

            if (logPath) Debug.Log($"[EnemyMotherSpider] SnapToPath → {_currentPathDistance:F2}/{_pathLength:F2}.");
        }

        private void MoveAlongPath(float speed)
        {
            if (_chaseDir.sqrMagnitude < 0.0001f) { StopHorizontal(); return; }
            if (speed <= 0f) return;
            if (_pathLength <= 0.0001f) { ApplyMovementToward_LinearFallback(speed); return; }

            Vector3 tangent = GetPathTangent(_currentPathDistance);
            float dirSign = Vector3.Dot(_chaseDir, tangent) >= 0f ? 1f : -1f;

            _currentPathDistance += dirSign * speed * Time.fixedDeltaTime;
            _currentPathDistance = Mathf.Clamp(_currentPathDistance, 0f, _pathLength);

            Vector3 targetPos = GetPositionAtDistance(_currentPathDistance);
            targetPos.y = rb.position.y;

            if (rb.isKinematic) { rb.MovePosition(targetPos); return; }

            Vector3 delta = targetPos - rb.position;
            delta.y = 0f;
            Vector3 desiredVel = delta / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            desiredVel.y = 0f;
            if (desiredVel.magnitude > speed * 2f) desiredVel = desiredVel.normalized * speed * 2f;

            Vector3 v = rb.linearVelocity;
            rb.linearVelocity = new Vector3(desiredVel.x, v.y, desiredVel.z);

            if (logMovement && Time.frameCount % 20 == 0)
                Debug.Log($"[MOTHER-PATH] s={_currentPathDistance:F2}/{_pathLength:F2} sign={dirSign} " +
                          $"active={(_usingSecondaryPath ? "SEC" : "PRI")} vel={rb.linearVelocity}");
        }

        private void ApplyMovementToward_LinearFallback(float speed)
        {
            if (_chaseDir.sqrMagnitude < 0.0001f) { StopHorizontal(); return; }
            if (rb.isKinematic) { rb.MovePosition(rb.position + _chaseDir * speed * Time.fixedDeltaTime); return; }
            Vector3 v = rb.linearVelocity;
            Vector3 targetFlat = _chaseDir * speed;
            rb.linearVelocity = new Vector3(targetFlat.x, v.y, targetFlat.z);
        }

        private void ApplyMovementToward(float speed)
        {
            if (constrainToPath && HasUsablePath()) { MoveAlongPath(speed); return; }
            if (_chaseDir.sqrMagnitude < 0.0001f) { StopHorizontal(); return; }

            if (rb.isKinematic)
            {
                rb.MovePosition(rb.position + _chaseDir * speed * Time.fixedDeltaTime);
                return;
            }
            Vector3 v = rb.linearVelocity;
            Vector3 targetFlat = _chaseDir * speed;
            rb.linearVelocity = new Vector3(targetFlat.x, v.y, targetFlat.z);

            if (logMovement) Debug.Log($"[MOTHER-MOVE] state={_state} speed={speed} vel={rb.linearVelocity}");
        }

        private void UpdateChaseDirection()
        {
            if (playerTarget == null) { _chaseDir = Vector3.zero; return; }
            if (constrainToPath && HasUsablePath()) { ComputePathDirectionToward(playerTarget.position); return; }

            Vector3 dir = playerTarget.position - transform.position;
            dir.y = 0f;
            _chaseDir = dir.sqrMagnitude < 0.01f ? Vector3.zero : dir.normalized;
        }

        private void MonitorStuck()
        {
            if (_stuckWarned) return;
            _stuckTimer += Time.fixedDeltaTime;
            if (_stuckTimer < 1f) return;

            Vector3 delta = rb.position - _stuckLastPos;
            delta.y = 0f;
            _stuckTimer = 0f;
            _stuckLastPos = rb.position;

            if (DistanceToPlayer() <= attackZoneRadius) return;
            if (delta.magnitude >= chaseSpeed * 0.1f) return;

            _stuckWarned = true;
            Debug.LogWarning($"[EnemyMotherSpider] in Chase almost stopped (andou {delta.magnitude:F2} m in 1s). " +
                             $"pathDist={_currentPathDistance:F2}/{_pathLength:F2} vel={rb.linearVelocity}");
        }

        private void ApplyMovementPhysics()
        {
            if (rb != null) rb.linearDamping = 0f;
            if (animator != null && animator.applyRootMotion) animator.applyRootMotion = false;

            float friction = useFrictionlessMaterial ? 0f : Mathf.Clamp01(groundFriction);
            _frictionMaterial = new PhysicsMaterial("MotherSpider_Ground")
            {
                dynamicFriction = friction,
                staticFriction = friction,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };

            Collider[] cols = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                Collider c = cols[i];
                if (c == null || c.isTrigger) continue;
                c.sharedMaterial = _frictionMaterial;
            }
        }

        private void UpdateSnappedRotation()
        {
            if (rotationMode == RotationMode.Locked) return;

            if (_state == State.ReturnHome)
            {
                if (constrainToPath && HasUsablePath())
                {
                    Vector3 tangent = GetPathTangent(_currentPathDistance);
                    if (tangent.sqrMagnitude > 0.0001f) _snappedRotation = ComputeFacingRotation(tangent);
                    return;
                }
                Vector3 homeDir = _homePosition - transform.position;
                homeDir.y = 0f;
                if (homeDir.sqrMagnitude < 0.0001f) return;
                _snappedRotation = ComputeFacingRotation(homeDir);
                return;
            }

            if (!HasPlayer()) return;
            if (_state == State.Jump || _state == State.Rest || _state == State.Stagger) return;

            bool turning = _state == State.Turn;
            bool chasing = _state == State.Chase;
            bool inAttackZone = IsPlayerInAttackZone();
            if (!turning && !chasing && !inAttackZone) return;

            Vector3 dir = playerTarget.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            _snappedRotation = ComputeFacingRotation(dir);
        }

        private Quaternion ComputeFacingRotation(Vector3 flatDir)
        {
            float angle = Mathf.Atan2(flatDir.x, flatDir.z) * Mathf.Rad2Deg;
            if (rotationMode == RotationMode.EightDirections) angle = Mathf.Round(angle / 45f) * 45f;
            angle += rotationOffsetY;
            return Quaternion.Euler(0f, angle, 0f);
        }

        private Quaternion SnapQuaternion(Quaternion rot)
        {
            if (rotationMode == RotationMode.Locked) return rot;
            float y = rot.eulerAngles.y;
            y = Mathf.Round(y / 45f) * 45f;
            return Quaternion.Euler(0f, y, 0f);
        }

        private void SnapRotationToPlayer()
        {
            if (!HasPlayer()) return;
            Vector3 dir = playerTarget.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            _snappedRotation = ComputeFacingRotation(dir);
            rb.MoveRotation(_snappedRotation);
        }

        private bool IsFacingPlayer()
        {
            if (!HasPlayer()) return true;
            Vector3 dir = playerTarget.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return true;
            Quaternion targetRot = ComputeFacingRotation(dir);
            return Quaternion.Angle(_snappedRotation, targetRot) <= 0.1f;
        }
    }
}

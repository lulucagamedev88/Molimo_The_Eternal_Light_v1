using System.Collections;
using UnityEngine;

namespace MolimoByLulucaGameDev
{

    public partial class EnemyMotherSpider
    {
        private float _stateTimer;
        private float _attackCooldownTimer;
        private Coroutine _jumpRoutine;
        private Coroutine _recoveryRoutine;
        private bool _walkSoundPlaying;
        private float _walkImpulseTimer;

        private void EnterIdle()
        {
            ChangeState(State.Idle);
            _stateTimer = idleDuration;
            SetWalking(false);
            StopHorizontal();
        }

        private void TickIdle()
        {
            if (!HasPlayer()) return;
            float d = DistanceToPlayer();
            if (d <= attackZoneRadius && _attackCooldownTimer <= 0f) { EnterPrepare(); return; }
            if (d <= detectionRadius) { EnterChase(); return; }
        }

        private void EnterChase()
        {
            ChangeState(State.Chase);
            SetWalking(true);
            if (rb != null && !rb.isKinematic) rb.WakeUp();

            _stuckTimer = 0f;
            _stuckLastPos = rb.position;
            _stuckWarned = false;
            _walkImpulseTimer = 0f;

            if (HasPlayer())
                RefreshActivePathForTarget(playerTarget.position);

            UpdateChaseDirection();

            if (logStates)
                Debug.Log($"[EnemyMotherSpider] CHASE iniciado — path={(_usingSecondaryPath ? "SECONDARY" : "PRIMARY")}.");
        }

        private void TickChase()
        {
            if (!HasPlayer()) { EnterReturnHome(); return; }
            float d = DistanceToPlayer();

            if (d > disengageRadius) { EnterReturnHome(); return; }

            if (d <= attackZoneRadius && _attackCooldownTimer <= 0f)
            {
                SetWalking(false);
                EnterPrepare();
                return;
            }

            RefreshActivePathForTarget(playerTarget.position);

            UpdateChaseDirection();
            TickWalkImpulse();
        }

        private void EnterPrepare()
        {
            ChangeState(State.Prepare);
            _stateTimer = prepareDuration;
            SetWalking(false);
            StopHorizontal();
            PlaySfxOneShot(waitAttackSfx);
            SetTrigger(_hWaitAttack);
            if (logStates) Debug.Log("[EnemyMotherSpider] PREPARE — Spider_WaitAttack.");
        }

        private void TickPrepare()
        {
            StopHorizontal();
            if (!HasPlayer()) { EnterIdle(); return; }
            if (_stateTimer > 0f) return;

            float d = DistanceToPlayer();
            if (d <= secondaryAttackRange)
            {
                if (logAttackDecision)
                    Debug.Log($"[EnemyMotherSpider] SECONDARY (Spider_Attack in terra). Dist={d:F2} ≤ {secondaryAttackRange}.");
                EnterSecondaryAttack();
            }
            else
            {
                if (logAttackDecision)
                    Debug.Log($"[EnemyMotherSpider] PRIMARY (Jump + stomp). Dist={d:F2} > {secondaryAttackRange}.");
                if (_jumpRoutine != null) StopCoroutine(_jumpRoutine);
                _jumpRoutine = StartCoroutine(JumpRoutine());
            }
        }

        private IEnumerator JumpRoutine()
        {
            ChangeState(State.Jump);
            SetWalking(false);
            PlaySfxOneShot(attackSfx);
            SetTrigger(_hJump);

            if (ignorePlayerCollisionDuringJump) SetPlayerCollisionIgnored(true);

            bool savedGravity = rb.useGravity;
            rb.useGravity = false;

            Vector3 aim = playerTarget != null ? playerTarget.position : transform.position;
            if (constrainToPath && HasUsablePath())
            {
                float targetDist = GetClosestDistanceOnPathToTarget(aim);
                aim = GetPositionAtDistance(targetDist);
            }

            Vector2 err = Random.insideUnitCircle * jumpAimError;
            Vector3 target = new Vector3(aim.x + err.x, transform.position.y, aim.z + err.y);
            Vector3 start = rb.position;

            float t = 0f;
            while (t < jumpDuration)
            {
                t += Time.fixedDeltaTime;
                float k = Mathf.Clamp01(t / jumpDuration);
                Vector3 flat = Vector3.Lerp(new Vector3(start.x, 0f, start.z), new Vector3(target.x, 0f, target.z), k);
                float y = start.y + 4f * jumpHeight * k * (1f - k);
                rb.MovePosition(new Vector3(flat.x, y, flat.z));
                yield return new WaitForFixedUpdate();
            }

            rb.position = new Vector3(target.x, start.y, target.z);
            rb.useGravity = savedGravity;
            StopHorizontal();
            SnapToPath();

            _jumpRoutine = null;
            EnterLandAttack();
        }

        private void EnterLandAttack()
        {
            ChangeState(State.LandAttack);
            SetWalking(false);
            _stateTimer = landRecovery;
            PlaySfxOneShot(impactSfx);
            PushAwayFromPlayer(minDistanceFromPlayerOnLand);
            TryDealStompDamage();
            GenerateImpulse();
            if (breakOnLand) DamageBreakablesInRadius(transform.position, landAttackRadius);
        }

        private void TickLandAttack()
        {
            StopHorizontal();
            if (_stateTimer <= 0f) EnterRest();
        }

        private void EnterSecondaryAttack()
        {
            ChangeState(State.SecondaryAttack);
            _stateTimer = secondaryAttackDuration;
            SetWalking(false);
            StopHorizontal();
            PlaySfxOneShot(attackSfx);
            SetTrigger(_hAttack);
            _attackHitboxCooldownTimer = 0f;
            SetAttackHitboxActive(true);
        }

        private void TickSecondaryAttack()
        {
            StopHorizontal();
            if (_recoveryRoutine != null) return;
            if (_stateTimer > 0f) return;
            SetAttackHitboxActive(false);
            if (secondaryAttackRecovery > 0f)
            {
                _recoveryRoutine = StartCoroutine(RecoveryThenRest(secondaryAttackRecovery));
                return;
            }
            EnterRest();
        }

        private IEnumerator RecoveryThenRest(float duration)
        {
            yield return new WaitForSeconds(duration);
            _recoveryRoutine = null;
            if (_state == State.Dead) yield break;
            if (_state != State.SecondaryAttack) yield break;
            EnterRest();
        }

        private void EnterReturnHome()
        {
            if (_jumpRoutine != null) { StopCoroutine(_jumpRoutine); _jumpRoutine = null; }
            if (_recoveryRoutine != null) { StopCoroutine(_recoveryRoutine); _recoveryRoutine = null; }
            SetAttackHitboxActive(false);
            if (rb != null) rb.useGravity = true;

            ChangeState(State.ReturnHome);
            SetWalking(true);
            if (rb != null && !rb.isKinematic) rb.WakeUp();

            StopHorizontal();
            UpdateReturnHomeDirection();

            if (logReturnHome)
                Debug.Log($"[EnemyMotherSpider] RETURN HOME iniciado — path={(_usingSecondaryPath ? "SECONDARY" : "PRIMARY")}.");
        }

        private void TickReturnHome()
        {

            if (HasPlayer() && DistanceToPlayer() <= detectionRadius)
            {
                EnterChase();
                return;
            }

            if (constrainToPath && HasUsablePath())
            {

                if (_currentPathDistance <= returnHomeArriveDistance) { ArriveHome(); return; }
                UpdateReturnHomeDirection();
                return;
            }

            float dist = Vector3.Distance(transform.position, _homePosition);
            if (dist <= returnHomeArriveDistance) { ArriveHome(); return; }
            UpdateReturnHomeDirection();
        }

        private void UpdateReturnHomeDirection()
        {
            if (constrainToPath && HasUsablePath())
            {

                _chaseDir = Vector3.zero;
                Vector3 tangent = GetPathTangent(_currentPathDistance);
                if (tangent.sqrMagnitude > 0.0001f)
                    _chaseDir = -tangent;
                return;
            }

            Vector3 dir = _homePosition - transform.position;
            dir.y = 0f;
            _chaseDir = dir.sqrMagnitude < 0.01f ? Vector3.zero : dir.normalized;
        }

        private void ArriveHome()
        {
            if (rb != null)
            {
                StopHorizontal();
                _snappedRotation = _homeRotation;
                rb.MoveRotation(_homeRotation);
            }
            SetWalking(false);
            if (logReturnHome) Debug.Log("[EnemyMotherSpider] Chegou the Home.");
            EnterIdle();
        }

        private void EnterRest()
        {
            ChangeState(State.Rest);
            _stateTimer = restDuration;
            SetWalking(false);
            StopHorizontal();
            if (ignorePlayerCollisionDuringJump && !_playerDeathHandled) SetPlayerCollisionIgnored(false);
            OnRestStart?.Invoke();
        }

        private void TickRest()
        {
            StopHorizontal();
            if (_stateTimer > 0f) return;
            ExitRest();
        }

        private void ExitRest()
        {
            OnRestEnd?.Invoke();

            if (turnAfterRest && rotationMode != RotationMode.Locked && HasPlayer()) { EnterTurn(); return; }
            SnapRotationToPlayer();
            FinishRecovery();
        }

        private void EnterTurn()
        {
            ChangeState(State.Turn);
            _stateTimer = turnMaxDuration;
            SetWalking(false);
            StopHorizontal();
            SnapRotationToPlayer();
        }

        private void TickTurn()
        {
            StopHorizontal();
            if (!HasPlayer()) { FinishRecovery(); return; }
            if (_stateTimer <= 0f || IsFacingPlayer()) FinishRecovery();
        }

        private void FinishRecovery()
        {
            _attackCooldownTimer = attackCooldown;
            if (HasPlayer() && DistanceToPlayer() > attackZoneRadius) EnterChase();
            else EnterIdle();
        }

        private void EnterStagger()
        {
            ChangeState(State.Stagger);
            _stateTimer = staggerDuration;
            SetTrigger(_hTakeDamage);
            SetWalking(false);
            StopHorizontal();
        }

        private void TickStagger()
        {
            if (_stateTimer <= 0f) EnterIdle();
        }

        private void SetWalking(bool walking)
        {
            SetBool(_hWalk, walking);
            if (walking && !_walkSoundPlaying)
            {
                StartLoopSfx(walkSfx, loopAudioSource);
                _walkSoundPlaying = true;
            }
            else if (!walking && _walkSoundPlaying)
            {
                StopLoopSfx(loopAudioSource);
                _walkSoundPlaying = false;
            }
        }

        private void TickWalkImpulse()
        {
            if (!walkImpulseEnabled) return;
            if (_state != State.Chase) return;
            _walkImpulseTimer -= Time.deltaTime;
            if (_walkImpulseTimer > 0f) return;
            _walkImpulseTimer = Mathf.Max(0.05f, walkImpulseInterval);
            GenerateImpulse();
        }
    }
}

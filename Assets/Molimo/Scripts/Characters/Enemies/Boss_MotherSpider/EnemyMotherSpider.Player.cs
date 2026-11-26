using System.Collections.Generic;
using UnityEngine;

namespace MolimoByLulucaGameDev
{

    public partial class EnemyMotherSpider
    {

        private Collider[] _playerColliders;
        private Collider[] _spiderSolidColliders;
        private bool _playerCollisionIgnored;
        private bool _playerDeathHandled;
        private float _resolvePlayerTimer;
        private bool _warnedNoPlayer;
        private bool _warnedBrainNotOperational;
        private bool _wasDetectingPlayer;

        private void CacheSpiderSolidColliders()
        {
            Collider[] all = GetComponentsInChildren<Collider>(true);
            List<Collider> solid = new List<Collider>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                Collider c = all[i];
                if (c == null || c.isTrigger) continue;
                solid.Add(c);
            }
            _spiderSolidColliders = solid.ToArray();
        }

        private bool CachePlayerColliders()
        {
            if (playerTarget == null) { _playerColliders = null; return false; }
            _playerColliders = playerTarget.GetComponentsInChildren<Collider>(true);
            if (_playerColliders == null || _playerColliders.Length == 0)
            {
                Rigidbody prb = playerTarget.GetComponentInChildren<Rigidbody>();
                if (prb != null)
                {
                    Collider[] pc = prb.GetComponents<Collider>();
                    if (pc != null && pc.Length > 0) { _playerColliders = pc; return true; }
                }
                CharacterController cc = playerTarget.GetComponentInChildren<CharacterController>();
                if (cc != null) { _playerColliders = new Collider[] { cc }; return true; }
                _playerColliders = null;
                return false;
            }
            return true;
        }

        private void SetPlayerCollisionIgnored(bool ignored)
        {
            if (_playerCollisionIgnored == ignored) return;
            if (_spiderSolidColliders == null || _spiderSolidColliders.Length == 0) return;

            if (_playerColliders == null || _playerColliders.Length == 0)
            {
                if (!CachePlayerColliders())
                {
                    if (logPlayerCollision) Debug.LogWarning("[EnemyMotherSpider] without colliders of the player.");
                    return;
                }
            }

            for (int i = 0; i < _spiderSolidColliders.Length; i++)
            {
                Collider the = _spiderSolidColliders[i];
                if (the == null) continue;
                for (int j = 0; j < _playerColliders.Length; j++)
                {
                    Collider b = _playerColliders[j];
                    if (b == null) continue;
                    Physics.IgnoreCollision(the, b, ignored);
                }
            }
            _playerCollisionIgnored = ignored;

            if (logPlayerCollision) Debug.Log($"[EnemyMotherSpider] collision with Player {(ignored ? "ignored" : "enables")}.");
        }

        private void PushAwayFromPlayer(float minDistance)
        {
            if (minDistance <= 0f || playerTarget == null) return;
            if (rb == null || rb.isKinematic) return;

            Vector3 spiderPos = rb.position;
            Vector3 playerPos = playerTarget.position;
            Vector3 flat = spiderPos - playerPos;
            flat.y = 0f;
            float dist = flat.magnitude;
            if (dist >= minDistance) return;

            Vector3 dir = dist > 0.01f ? flat / dist : -transform.forward;
            Vector3 newPos = playerPos + dir * minDistance;
            newPos.y = spiderPos.y;
            rb.position = newPos;

            if (logPlayerCollision) Debug.Log($"[EnemyMotherSpider] PushAway: dist {dist:F2} → {minDistance:F2}.");
        }

        private void TrackPlayerLifeState()
        {
            if (playerTarget == null) return;
            bool dead = false;

            if (playerBrain != null && !playerBrain.IsAlive) dead = true;
            else
            {
                PlayerHealth hp = GetPlayerHealth();
                if (hp != null && hp.CurrentHealth <= 0f) dead = true;
            }

            if (dead)
            {
                if (_playerDeathHandled) return;
                _playerDeathHandled = true;
                _wasDetectingPlayer = false;

                if (ignorePlayerCollisionWhenDead) SetPlayerCollisionIgnored(true);
                if (logPlayerCollision) Debug.Log("[EnemyMotherSpider] Player morreu.");

                if (returnHomeOnPlayerDeath) EnterReturnHome();
                else EnterIdle();
                return;
            }

            if (_playerDeathHandled)
            {
                _playerDeathHandled = false;
                SetPlayerCollisionIgnored(false);
                if (logPlayerCollision) Debug.Log("[EnemyMotherSpider] Player alive of novo — flag resetado.");
            }
        }

        private void TrackDetectionEvents()
        {
            if (!HasPlayer()) return;
            float d = DistanceToPlayer();
            bool detectedNow = d <= detectionRadius;

            if (detectedNow && !_wasDetectingPlayer)
            {
                _wasDetectingPlayer = true;
                OnPlayerDetected?.Invoke();
                if (logStates) Debug.Log("[EnemyMotherSpider] Player detected.");
            }
            else if (!detectedNow && _wasDetectingPlayer && d > disengageRadius)
            {
                _wasDetectingPlayer = false;
                OnPlayerLost?.Invoke();
                if (logStates) Debug.Log("[EnemyMotherSpider] Player PERDIDO.");

                if (_state == State.Chase) EnterReturnHome();
            }
        }

        private void ResolvePlayer()
        {
            bool hadPlayer = playerTarget != null;
            if (playerBrain == null) playerBrain = Object.FindAnyObjectByType<PlayerBrain>();
            if (playerBrain != null && playerTarget == null) playerTarget = playerBrain.transform;

            if (playerTarget == null)
            {
                GameObject go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) playerTarget = go.transform;
            }
            if (playerTarget != null && (!hadPlayer || _playerColliders == null || _playerColliders.Length == 0))
                CachePlayerColliders();
            if (playerTarget == null && !_warnedNoPlayer)
            {
                _warnedNoPlayer = true;
                Debug.LogWarning("[EnemyMotherSpider] Player not found.");
            }
        }

        private void RetryResolvePlayer()
        {
            if (playerTarget != null)
            {
                if (_playerColliders == null || _playerColliders.Length == 0)
                {
                    _resolvePlayerTimer -= Time.deltaTime;
                    if (_resolvePlayerTimer <= 0f) { _resolvePlayerTimer = 1f; CachePlayerColliders(); }
                }
                return;
            }
            _resolvePlayerTimer -= Time.deltaTime;
            if (_resolvePlayerTimer > 0f) return;
            _resolvePlayerTimer = 1f;
            ResolvePlayer();
        }

        private void WarnIfBrainNotOperational()
        {
            if (_warnedBrainNotOperational) return;
            if (playerTarget == null) return;
            if (IsPlayerBrainOperational()) return;
            if (DistanceToPlayer() > detectionRadius) return;
            _warnedBrainNotOperational = true;
            Debug.LogWarning("[EnemyMotherSpider] Player inside the radius, but Brain not operacional.");
        }

        private bool IsPlayerBrainOperational()
        {
            if (playerBrain == null) return true;
            if (!playerBrain.enabled) return false;
            if (!playerBrain.gameObject.activeInHierarchy) return false;
            if (!playerBrain.IsAwakened) return false;
            if (!playerBrain.IsAlive) return false;
            return true;
        }

        private bool HasPlayer() => playerTarget != null && IsPlayerBrainOperational();

        private float DistanceToPlayer()
        {
            if (playerTarget == null) return Mathf.Infinity;
            return Vector3.Distance(transform.position, playerTarget.position);
        }

        private bool IsPlayerInAttackZone() => HasPlayer() && DistanceToPlayer() <= attackZoneRadius;

        private PlayerHealth GetPlayerHealth()
        {
            if (playerBrain != null) return playerBrain.GetComponent<PlayerHealth>();
            if (playerTarget != null) return playerTarget.GetComponent<PlayerHealth>();
            return null;
        }
    }
}

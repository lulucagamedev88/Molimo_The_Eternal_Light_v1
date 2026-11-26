using System.Collections.Generic;
using UnityEngine;

namespace MolimoByLulucaGameDev
{

    public partial class EnemyMotherSpider
    {

        private float _ramDamageCooldownTimer;
        private float _attackHitboxCooldownTimer;
        private readonly Collider[] _breakableHits = new Collider[32];
        private readonly Collider[] _attackHits = new Collider[16];
        private readonly HashSet<IDamageable> _breakableProcessed = new HashSet<IDamageable>();

        private void SetAttackHitboxActive(bool active)
        {
            if (attackHitbox == null) return;
            if (attackHitbox.enabled == active) return;
            attackHitbox.enabled = active;
            if (logAttackHitbox) Debug.Log($"[EnemyMotherSpider] AttackHitbox {(active ? "enabled" : "disabled")}.");
        }

        private void TickAttackHitbox()
        {
            if (attackHitbox == null) return;
            if (!attackHitbox.enabled) return;
            if (_attackHitboxCooldownTimer > 0f) return;

            GetAttackHitboxWorld(out Vector3 center, out float radius);
            int mask = playerLayers.value != 0 ? playerLayers.value : ~0;
            int count = Physics.OverlapSphereNonAlloc(center, radius, _attackHits, mask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider c = _attackHits[i];
                if (c == null) continue;
                PlayerBrain b = c.GetComponentInParent<PlayerBrain>();
                if (b == null) continue;
                if (playerBrain != null && b != playerBrain) continue;
                DealDamageToPlayer(attackHitboxDamagePercent);
                _attackHitboxCooldownTimer = attackHitboxDamageCooldown;
                return;
            }
        }

        private void GetAttackHitboxWorld(out Vector3 center, out float radius)
        {
            center = transform.position + transform.forward;
            radius = 0.5f;
            if (attackHitbox == null) return;

            switch (attackHitbox)
            {
                case SphereCollider s:
                    center = s.transform.TransformPoint(s.center);
                    radius = s.radius * Mathf.Max(Mathf.Abs(s.transform.lossyScale.x), Mathf.Abs(s.transform.lossyScale.y), Mathf.Abs(s.transform.lossyScale.z));
                    break;
                case BoxCollider b:
                    center = b.transform.TransformPoint(b.center);
                    Vector3 and = b.size;
                    radius = Mathf.Max(and.x, and.y, and.z) * 0.5f * Mathf.Max(Mathf.Abs(b.transform.lossyScale.x), Mathf.Abs(b.transform.lossyScale.y), Mathf.Abs(b.transform.lossyScale.z));
                    break;
                case CapsuleCollider c:
                    center = c.transform.TransformPoint(c.center);
                    radius = c.radius * Mathf.Max(Mathf.Abs(c.transform.lossyScale.x), Mathf.Abs(c.transform.lossyScale.z));
                    break;
                default:
                    Bounds bounds = attackHitbox.bounds;
                    center = bounds.center;
                    radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
                    break;
            }
        }

        private void DealDamageToPlayer(float healthPercent)
        {
            PlayerHealth hp = GetPlayerHealth();
            if (hp == null) return;
            float maxHp = hp.MaxHealth;
            if (maxHp <= 0f) return;
            float damage = maxHp * Mathf.Max(0f, healthPercent);
            if (logPlayerDamage) Debug.Log($"[EnemyMotherSpider] damage when the Player: {damage} ({healthPercent:P0}).");
            hp.TakeDamage(damage);
        }

        private void TryRamDamage(Collider other)
        {
            if (other == null) return;
            if (_ramDamageCooldownTimer > 0f) return;
            PlayerBrain b = other.GetComponentInParent<PlayerBrain>();
            if (b == null) return;
            if (playerBrain != null && b != playerBrain) return;
            DealDamageToPlayer(ramDamagePercent);
            _ramDamageCooldownTimer = ramDamageCooldown;
        }

        private void TryDealStompDamage()
        {
            if (!HasPlayer()) return;
            float d = Vector3.Distance(transform.position, playerTarget.position);
            if (d > landAttackRadius) return;
            DealDamageToPlayer(stompDamagePercent);
        }

        private void OnCollisionEnter(Collision collision) => HandleCollision(collision);
        private void OnCollisionStay(Collision collision) => HandleCollision(collision);

        private void HandleCollision(Collision collision)
        {
            if (_state == State.Dead) return;
            if (collision == null || collision.collider == null) return;
            if (breakOnContact) TryDamageBreakable(collision.collider);
            if (_state == State.Chase) TryRamDamage(collision.collider);
        }

        private void TryDamageBreakable(Collider other)
        {
            if (other == null) return;
            if (breakableLayers.value == 0) return;
            IDamageable dmg = other.GetComponentInParent<IDamageable>();
            if (dmg == null) return;

            bool layerMatch = (breakableLayers.value & (1 << other.gameObject.layer)) != 0;
            if (!layerMatch && dmg is Component dmgComp)
                layerMatch = (breakableLayers.value & (1 << dmgComp.gameObject.layer)) != 0;
            if (!layerMatch) return;
            if (!dmg.IsAlive) return;

            dmg.TakeDamage(breakableDamage);
            if (logBreakables) Debug.Log($"[EnemyMotherSpider] Breakable (contacto): {other.name} → {breakableDamage}.");
        }

        private void DamageBreakablesInRadius(Vector3 center, float radius)
        {
            if (breakableLayers.value == 0) return;
            if (radius <= 0f) return;
            int count = Physics.OverlapSphereNonAlloc(center, radius, _breakableHits, breakableLayers, QueryTriggerInteraction.Ignore);
            if (count == 0) return;

            _breakableProcessed.Clear();
            for (int i = 0; i < count; i++)
            {
                Collider c = _breakableHits[i];
                if (c == null) continue;
                IDamageable dmg = c.GetComponentInParent<IDamageable>();
                if (dmg == null) continue;
                if (!dmg.IsAlive) continue;
                if (!_breakableProcessed.Add(dmg)) continue;
                dmg.TakeDamage(breakableDamage);
                if (logBreakables) Debug.Log($"[EnemyMotherSpider] Breakable (stomp): {c.name} → {breakableDamage}.");
            }
        }

        public void TakeDamage(float amount, float knockbackDistance = -1f)
        {
            if (onlyDamageableViaWeakPoint)
            {
                if (belly != null && belly.IsPlayerInside)
                {
                    ApplyDamageFromWeakPoint(amount, knockbackDistance, belly.DamageMultiplier);
                    return;
                }
                if (logDamage) Debug.Log("[EnemyMotherSpider] damage in the body ignored — ataca the belly.");
                return;
            }
            ApplyDamageInternal(amount, knockbackDistance);
        }

        public void ApplyDamageFromWeakPoint(float amount, float knockbackDistance, float multiplier)
        {
            if (amount <= 0f) return;
            if (onlyWeakPointActiveDuringRest && !IsBellyExposed)
            {
                if (logDamage) Debug.Log("[EnemyMotherSpider] point fraco outside the Rest — ignored.");
                return;
            }
            float finalDamage = amount * Mathf.Max(0f, multiplier);
            if (logDamage) Debug.Log($"[EnemyMotherSpider] belly: {amount} × {multiplier} = {finalDamage}");
            GenerateImpulse();
            ApplyDamageInternal(finalDamage, knockbackDistance);
        }

        private void ApplyDamageInternal(float amount, float knockbackDistance)
        {
            if (_isDead) return;
            if (_state == State.Dead) return;
            if (amount <= 0f) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            if (logDamage) Debug.Log($"[EnemyMotherSpider] damage={amount} → {currentHealth}/{maxHealth}");

            float effectiveKnockback = knockbackDistance >= 0f ? knockbackDistance : defaultKnockbackDistance;
            ApplyKnockback(effectiveKnockback);
            PlayHitParticle();
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f)
            {
                if (playHitParticleOnDeath) PlayHitParticle();
                Die();
                return;
            }

            bool shouldStagger = _state != State.Jump && !(_state == State.Rest && restIsUninterruptibleByDamage);
            if (shouldStagger) EnterStagger();
        }

        private void ApplyKnockback(float distance)
        {
            if (distance <= 0f) return;
            if (rb == null || rb.isKinematic) return;

            Vector3 v = rb.linearVelocity;
            v.y = 0f;
            Vector3 dir = v.sqrMagnitude > 0.01f ? -v.normalized : -transform.forward;
            const float distanceToForce = 6f;
            float force = distance * distanceToForce * knockbackMultiplier;
            Vector3 impulse = dir * force;
            impulse.y = knockbackVerticalBias;
            rb.AddForce(impulse, ForceMode.VelocityChange);
        }
    }
}

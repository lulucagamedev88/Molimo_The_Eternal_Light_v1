using UnityEngine;

namespace MolimoByLulucaGameDev
{

    public partial class EnemyMotherSpider
    {

        private void GenerateImpulse()
        {
            if (impulseSource == null) return;
            impulseSource.GenerateImpulse();
        }

        private void PlayHitParticle()
        {
            if (hitParticle == null) return;
            hitParticle.Clear(true);
            hitParticle.Play(true);
        }

        private void EnsureAudioSources()
        {
            if (loopAudioSource == null)
            {
                loopAudioSource = gameObject.AddComponent<AudioSource>();
                loopAudioSource.playOnAwake = false;
                loopAudioSource.loop = true;
                loopAudioSource.spatialBlend = 1f;
            }
            if (sfxAudioSource == null)
            {
                sfxAudioSource = gameObject.AddComponent<AudioSource>();
                sfxAudioSource.playOnAwake = false;
                sfxAudioSource.loop = false;
                sfxAudioSource.spatialBlend = 1f;
            }
        }

        private void PlaySfxOneShot(SoundData sfx)
        {
            if (sfx == null || sfxAudioSource == null) return;
            AudioClip clip = sfx.GetClip();
            if (clip == null) return;
            sfxAudioSource.pitch = sfx.GetPitch();
            sfxAudioSource.minDistance = sfx.minDistance > 0 ? sfx.minDistance : 1f;
            sfxAudioSource.maxDistance = sfx.maxDistance > 0 ? sfx.maxDistance : 35f;
            sfxAudioSource.PlayOneShot(clip, sfx.volume);
        }

        private void PlaySfxOneShotPersistent(SoundData sfx, Vector3 worldPosition)
        {
            if (sfx == null) return;
            AudioClip clip = sfx.GetClip();
            if (clip == null) return;

            float pitch = sfx.GetPitch();

            GameObject go = new GameObject($"[SFX_{clip.name}]");
            go.transform.position = worldPosition;

            AudioSource src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 1f;
            src.pitch = pitch;
            src.volume = sfx.volume;
            src.minDistance = sfx.minDistance > 0 ? sfx.minDistance : 1f;
            src.maxDistance = sfx.maxDistance > 0 ? sfx.maxDistance : 35f;
            src.clip = clip;
            src.Play();

            float lifetime = clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
            Object.Destroy(go, lifetime + 0.15f);
        }

        private void StartLoopSfx(SoundData sfx, AudioSource source)
        {
            if (source == null) return;
            AudioClip clip = sfx != null ? sfx.GetClip() : null;
            if (clip == null) { StopLoopSfx(source); return; }
            if (source.isPlaying && source.clip == clip) return;
            source.clip = clip;
            source.volume = sfx.volume;
            source.pitch = sfx.GetPitch();
            source.minDistance = sfx.minDistance > 0 ? sfx.minDistance : 1f;
            source.maxDistance = sfx.maxDistance > 0 ? sfx.maxDistance : 35f;
            source.loop = true;
            source.Play();
        }

        private void StopLoopSfx(AudioSource source)
        {
            if (source == null) return;
            if (!source.isPlaying && source.clip == null) return;
            source.Stop();
            source.clip = null;
            source.loop = false;
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;
            _state = State.Dead;

            if (_jumpRoutine != null) { StopCoroutine(_jumpRoutine); _jumpRoutine = null; }
            if (_recoveryRoutine != null) { StopCoroutine(_recoveryRoutine); _recoveryRoutine = null; }

            if (belly != null) belly.SetExposed(false);

            SetPlayerCollisionIgnored(true);
            SetAttackHitboxActive(false);

            StopLoopSfx(loopAudioSource);
            _walkSoundPlaying = false;

            PlaySfxOneShotPersistent(deathSfx, transform.position);

            SetBool(_hWalk, false);
            SetTrigger(_hDeath);

            StopHorizontal();
            rb.useGravity = true;

            GenerateImpulse();

            if (spawnDebrisOnDeath) SpawnDebris();
            if (hideVisualsOnDeath) HideAllRenderers();

            OnDeath?.Invoke();
            ScheduleDestroy();
        }

        private void SpawnDebris()
        {
            if (debrisPrefab == null) return;
            Vector3 pos = transform.position + Vector3.up * debrisSpawnYOffset;
            Quaternion rot = debrisInheritsRotation ? transform.rotation : Quaternion.identity;
            Instantiate(debrisPrefab, pos, rot);
            if (logStates) Debug.Log($"[EnemyMotherSpider] Debris spawned in {pos}.");
        }

        private void HideAllRenderers()
        {
            Renderer[] rends = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
                if (rends[i] != null) rends[i].enabled = false;
        }

        private void ScheduleDestroy()
        {
            GameObject target = destroyParentInsteadOfSelf && parentObjectToDestroy != null
                ? parentObjectToDestroy : gameObject;
            if (destroyDelayOnDeath > 0f) Destroy(target, destroyDelayOnDeath);
            else Destroy(target);
            if (!destroyParentInsteadOfSelf && parentObjectToDestroy != null && parentObjectToDestroy != gameObject)
            {
                if (destroyDelayOnDeath > 0f) Destroy(parentObjectToDestroy, destroyDelayOnDeath);
                else Destroy(parentObjectToDestroy);
            }
        }
    }
}

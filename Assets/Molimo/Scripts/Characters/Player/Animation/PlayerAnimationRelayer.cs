using UnityEngine;
using UnityEngine.Events;

namespace MolimoByLulucaGameDev
{
    [DisallowMultipleComponent]

    public class PlayerAnimationRelayer : MonoBehaviour
    {
        #region Inspector — references of combat
        [Header("=== references (combat) ===")]
        [SerializeField] private WeaponHitBox weaponHitBox;
        [SerializeField] private WeaponVFX weaponVFX;
        #endregion

        #region Inspector — Eventos of prologue
        [Header("=== Eventos of prologue (Animation Event) ===")]
        [SerializeField] private UnityEvent onWakeAnimationComplete;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (weaponHitBox == null)
                weaponHitBox = GetComponentInChildren<WeaponHitBox>(true);

            if (weaponVFX == null)
                weaponVFX = GetComponentInChildren<WeaponVFX>(true);

            if (weaponVFX == null)
                weaponVFX = GetComponentInParent<WeaponVFX>(true);
        }
        #endregion

        #region Animation Event — prologue

        public void OnWakeAnimationComplete()
        {
            onWakeAnimationComplete?.Invoke();
        }
        #endregion

        #region Animation Event — combat & damage

        public void SetCustomDamage(float damage)
        {
            if (weaponHitBox != null)
                weaponHitBox.SetCustomDamage(damage);
        }

        public void SetKnockbackDistance(float distance)
        {
            if (weaponHitBox != null)
                weaponHitBox.SetKnockbackDistance(distance);
        }

        public void EnableWeaponHitBox()
        {
            if (weaponHitBox != null)
                weaponHitBox.EnableHit();
        }

        public void EnableWeaponHitBoxWithKnockback(float knockbackDistance)
        {
            if (weaponHitBox != null)
                weaponHitBox.EnableHitWithKnockback(knockbackDistance);
        }

        public void DisableWeaponHitBox()
        {
            if (weaponHitBox != null)
                weaponHitBox.DisableHit();
        }
        #endregion

        #region Animation Event — audio of the sword

        public void PlaySwingSound()
        {
            if (weaponHitBox != null)
                weaponHitBox.PlaySwingSound();
        }

        public void SwordImpact(string impactName)
        {
            if (weaponHitBox != null)
                weaponHitBox.SwordImpact(impactName);
        }
        #endregion

        #region Animation Event — WeaponHitBox VFX

        public void SlashVFX(string vfxName)
        {
            if (weaponHitBox != null)
                weaponHitBox.SlashVFX(vfxName);
        }

        public void StopSlashVFX(string vfxName)
        {
            if (weaponHitBox != null)
                weaponHitBox.StopVFX(vfxName);
        }
        #endregion

        #region Animation Event — SwordTrail

        public void PlaySwordTrailHorizontal()
        {
            if (weaponVFX != null)
                weaponVFX.EnableSwordTrail(WeaponVFX.VFXOrientation.Horizontal, false);
        }

        public void PlaySwordTrailHorizontalReverse()
        {
            if (weaponVFX != null)
                weaponVFX.EnableSwordTrail(WeaponVFX.VFXOrientation.Horizontal, true);
        }

        public void PlaySwordTrailVertical()
        {
            if (weaponVFX != null)
                weaponVFX.EnableSwordTrail(WeaponVFX.VFXOrientation.Vertical, false);
        }

        public void PlaySwordTrailVerticalReverse()
        {
            if (weaponVFX != null)
                weaponVFX.EnableSwordTrail(WeaponVFX.VFXOrientation.Vertical, true);
        }

        public void StopSwordTrail()
        {
            if (weaponVFX != null)
                weaponVFX.DisableSwordTrail();
        }

        public void PlayImpact()
        {
            if (weaponVFX != null)
                weaponVFX.PlayImpact();
        }

        public void StopImpact()
        {
            if (weaponVFX != null)
                weaponVFX.StopImpact();
        }
        #endregion

        #region Animation Event — Block

        public void PlayBlockSound()
        {
            if (weaponHitBox != null)
                weaponHitBox.PlayClashSound();
        }

        public void BlockVFX(string vfxName)
        {
            if (weaponHitBox != null)
                weaponHitBox.BlockVFX(vfxName);
        }

        public void StopBlockVFX(string vfxName)
        {
            if (weaponHitBox != null)
                weaponHitBox.StopBlockVFX(vfxName);
        }
        #endregion
    }
}

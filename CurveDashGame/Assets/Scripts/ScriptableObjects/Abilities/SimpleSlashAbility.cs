using UnityEngine;

namespace STG.CurveDash
{
    [CreateAssetMenu(fileName = "SimpleSlashAbility", menuName = "Curve-Dash/Abilities/Simple Slash")]
    public class SimpleSlashAbility : AbilityData
    {
        public GameObject SlashVFX;
        
        public override void Execute(GameObject user, Vector3 targetPosition)
        {
            Debug.Log($"[Ability] Executing {AbilityName} from {user.name} at {targetPosition}");
            
            if (SlashVFX != null)
            {
                // Tính toán hướng xoay nằm ngang hướng về phía mục tiêu
                Vector3 direction = (targetPosition - user.transform.position);
                direction.y = 0; // Đảm bảo hiệu ứng xoay theo trục ngang song song mặt đất
                Quaternion targetRotation = direction.sqrMagnitude > 0.001f 
                    ? Quaternion.LookRotation(direction.normalized) 
                    : user.transform.rotation;

                VfxSystem.RequestSpawn(SlashVFX, targetPosition, targetRotation);
            }
            
            // Additional logic: Play sound, deal extra damage in AOE, etc.
        }
    }
}


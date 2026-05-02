using UnityEngine;

namespace STG.CurveDash
{
    [CreateAssetMenu(fileName = "SimpleSlashAbility", menuName = "Curve Dash/Abilities/Simple Slash")]
    public class SimpleSlashAbility : AbilityData
    {
        public GameObject SlashVFX;
        
        public override void Execute(GameObject user, Vector3 targetPosition)
        {
            Debug.Log($"[Ability] Executing {AbilityName} from {user.name} at {targetPosition}");
            
            if (SlashVFX != null)
            {
                Instantiate(SlashVFX, targetPosition, Quaternion.identity);
            }
            
            // Additional logic: Play sound, deal extra damage in AOE, etc.
        }
    }
}

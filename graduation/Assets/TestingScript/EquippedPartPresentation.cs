using UnityEngine;

public class EquippedPartPresentation : MonoBehaviour
{
    private const string AttackParameterName = "Attack";
    private static readonly int AttackID = Animator.StringToHash(AttackParameterName);

    #region Serialized Fields
    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Header("Effects")]
    [SerializeField] private GameObject attackEffect;
    #endregion

    #region Public Methods
    /// <summary>
    /// Plays this equipped part's attack animation and effect.
    /// </summary>
    public void PlayAttack()
    {
        if (animator != null)
        {
            animator.SetTrigger(AttackID);
        }

        if (attackEffect != null)
        {
            attackEffect.SetActive(false);
            attackEffect.SetActive(true);
        }
    }
    #endregion
}

using UnityEngine;
using UnityEngine.Rendering;

public class PlayerVignetteManager : MonoBehaviour
{

    [Header("Damage Vignette")]
    [SerializeField] private GameObject damageVignette;
    [SerializeField] private Animator damageVignetteAnimator;

    [SerializeField] private string damageVignetteHitTrigger = "Hit";
    [SerializeField] private string damageVignetteActiveTrigger = "Active";

    [Header("Heal Vignette")]
    [SerializeField] private GameObject healVignette;
    [SerializeField] private Animator healVignetteAnimator;

    [SerializeField] private string healVignetteHealTrigger = "Heal";

    [Header("Thresholds")]
    public float lowHealthDamageVignetteActiveThreshold = 0.3f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    #region Lifecycle

    private void Awake()
    {
        HideAllVignettes();
    }

    #endregion

    #region Triggers

    public void TriggerDamageVignette()
    {
        damageVignette.SetActive(true);
        damageVignetteAnimator.SetTrigger(damageVignetteHitTrigger);
        DebugLog("Triggered damage vignette");
    }

    public void SetDamageVignetteActive(bool isActive)
    {
        damageVignette.SetActive(isActive);
        damageVignetteAnimator.SetBool(damageVignetteActiveTrigger, isActive);
        DebugLog($"Set damage vignette active to {isActive}");
    }


    public void TriggerHealVignette()
    {
        healVignette.SetActive(true);
        healVignetteAnimator.SetTrigger(healVignetteHealTrigger);
        DebugLog("Triggered heal vignette");
    }

    #endregion


    #region Activation

    public void ShowDamageVignette() => damageVignette.SetActive(true);
    public void ShowHealVignette() => healVignette.SetActive(true);

    public void HideDamageVignette() => damageVignette.SetActive(false);
    public void HideHealVignette() => healVignette.SetActive(false);


    public void HideAllVignettes()
    {
        damageVignette.SetActive(false);
        healVignette.SetActive(false);
    }

    #endregion

    #region Debug

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[PlayerVignetteManager] {message}");
    }

    #endregion

}

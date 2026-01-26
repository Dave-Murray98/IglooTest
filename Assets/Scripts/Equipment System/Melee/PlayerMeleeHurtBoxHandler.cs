using UnityEngine;

public class PlayerMeleeHurtBoxHandler : MonoBehaviour
{
    [SerializeField] private GameObject leftHandMeleeHurtBox;
    [SerializeField] private GameObject rightHandMeleeHurtBox;

    [SerializeField] private Vector3 leftHandMeleeHurtBoxDefaultPosition;
    [SerializeField] private Vector3 rightHandMeleeHurtBoxDefaultPosition;

    [SerializeField] private bool enableDebugLogs = false;

    [Header("Audio")]
    [SerializeField] private AudioClip[] meleeAudioClips;

    public void EnableMeleeHurtBox(bool left)
    {
        if (left)
            leftHandMeleeHurtBox.SetActive(true);
        else
            rightHandMeleeHurtBox.SetActive(true);

        if (meleeAudioClips.Length > 0)
            AudioManager.Instance.PlaySound2D(meleeAudioClips[Random.Range(0, meleeAudioClips.Length)], AudioCategory.PlayerSFX);

        DebugLog("Enabled melee hurt box");
    }

    public void DisableMeleeHurtBoxes()
    {
        leftHandMeleeHurtBox.SetActive(false);
        rightHandMeleeHurtBox.SetActive(false);

        DebugLog("Disabled melee hurt boxes");
    }

    public void SetMeleeHurtBoxPosition(Vector3 position, bool left)
    {
        if (left)
            leftHandMeleeHurtBox.transform.localPosition = position;
        else
            rightHandMeleeHurtBox.transform.localPosition = position;

        DebugLog("Set melee hurt box position");
    }

    public void ResetBothMeleeHurtBoxPositions()
    {
        leftHandMeleeHurtBox.transform.localPosition = leftHandMeleeHurtBoxDefaultPosition;
        rightHandMeleeHurtBox.transform.localPosition = rightHandMeleeHurtBoxDefaultPosition;

        DebugLog("Reset both melee hurt box positions");
    }

    public void ResetMeleeHurtBoxPosition(bool left)
    {
        if (left)
            leftHandMeleeHurtBox.transform.localPosition = leftHandMeleeHurtBoxDefaultPosition;
        else
            rightHandMeleeHurtBox.transform.localPosition = rightHandMeleeHurtBoxDefaultPosition;

        DebugLog("Reset melee hurt box position");
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerMeleeHurtBoxHandler] {message}");
        }
    }
}

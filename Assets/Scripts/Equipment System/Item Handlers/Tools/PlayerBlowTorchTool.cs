using UnityEngine;

public class PlayerBlowTorchTool : PlayerTool
{

    [SerializeField] private GameObject torchFire;

    private void Awake()
    {
        torchFire.SetActive(false);
    }

    public override void TriggerToolEffect()
    {
        torchFire.SetActive(true);
    }

    public override void StopToolEffect()
    {
        torchFire.SetActive(false);
    }

    protected override void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[PlayerBlowTorchTool] {message}");
    }
}

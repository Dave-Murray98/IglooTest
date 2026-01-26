using System.Collections;
using UnityEngine;

public class GivePlayerOxygenTanks : MonoBehaviour
{
    [SerializeField] private int oxygenTanksToGive = 3;

    public bool giveOnStart = true;

    [SerializeField] private ItemData oxygenTankItemData;

    [SerializeField] private bool enableDebugLogs = false;

    private void Start()
    {
        if (giveOnStart)
        {
            if (PlayerInventoryManager.Instance != null)
                PlayerInventoryManager.Instance.OnPlayerInventoryLoaded += GiveOxygenTanks;

            GiveOxygenTanks();
        }
    }

    public void GiveOxygenTanks()
    {
        DebugLog("Giving player oxygen tanks...");
        StartCoroutine(WaitForSceneToFinishLoadingBeforeGivingOxygen());
    }

    private IEnumerator WaitForSceneToFinishLoadingBeforeGivingOxygen()
    {
        yield return new WaitForEndOfFrame();
        //first add oxygen tanks to player inventory
        for (int i = 0; i < oxygenTanksToGive; i++)
        {
            PlayerInventoryManager.Instance.AddItem(oxygenTankItemData);
        }

        Debug.Log($"Gave player {oxygenTanksToGive} oxygen tanks.");

        //then get one of those tanks from inventory
        string tankID = PlayerInventoryManager.Instance.GetItemIDByItemData(oxygenTankItemData);

        // then have the player's oxygen tank equip one of the new tanks
        OxygenTankManager.Instance.EquipTank(tankID);

        DebugLog($"Equipped {oxygenTankItemData.itemName} to player.");

        StartCoroutine(UpdateOxygenUICoroutine());

    }

    private IEnumerator UpdateOxygenUICoroutine()
    {
        yield return new WaitForSeconds(0.5f);

        GameManager.Instance.uiManager.UpdateOxygenBar(
        OxygenTankManager.Instance.GetCurrentOxygen(),
        OxygenTankManager.Instance.GetMaxCapacity()
    );
    }

    private void OnDestroy()
    {
        if (PlayerInventoryManager.Instance != null)
            PlayerInventoryManager.Instance.OnPlayerInventoryLoaded -= GiveOxygenTanks;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[GivePlayerOxygenTanks]: {message}");
    }
}

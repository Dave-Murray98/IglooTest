using UnityEngine;
using System;

/// <summary>
/// Result structure for item placement attempts with detailed feedback.
/// </summary>
[Serializable]
public struct ItemPlacementResult
{
    public bool success;
    public string message;
    public Vector2Int position;
    public int rotation;
    public InventoryItemData placedItem;
    public ItemPlacementResult(bool isSuccess, string resultMessage, Vector2Int targetPosition, int targetRotation, InventoryItemData item = null)
    {
        success = isSuccess;
        message = resultMessage;
        position = targetPosition;
        rotation = targetRotation;
        placedItem = item;
    }
}
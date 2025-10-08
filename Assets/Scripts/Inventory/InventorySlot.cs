using System;
using UnityEngine;
/// <summary>
/// Represents a single inventory slot that can hold an item and its quantity
/// This is a data class (no MonoBehaviour needed)
/// </summary>
[System.Serializable]
public class InventorySlot
{
    public InventoryItem item;      // Reference to the item ScriptableObject
    public int quantity;   // How many of this item are in the slot
    
    /// <summary>
    /// Constructor for creating a new slot with an item
    /// </summary>
    public InventorySlot(InventoryItem item, int quantity)
    {
        this.item = item;
        this.quantity = quantity;
    }
    
    /// <summary>
    /// Check if this slot is empty
    /// </summary>
    public bool IsEmpty()
    {
        return item == null || quantity <= 0;
    }
    
    /// <summary>
    /// Check if this slot can accept more of the same item
    /// </summary>
    public bool CanAddMore()
    {
        if (item == null) return true;
        return quantity < item.maxStackSize;
    }
    
    /// <summary>
    /// Add quantity to this slot (respects max stack size)
    /// Returns the amount that couldn't be added
    /// </summary>
    public int AddQuantity(int amount)
    {
        int maxCanAdd = item.maxStackSize - quantity;
        int actuallyAdded = Mathf.Min(amount, maxCanAdd);
        quantity += actuallyAdded;
        return amount - actuallyAdded; // Return overflow
    }
    
    /// <summary>
    /// Remove quantity from this slot
    /// Returns false if not enough quantity
    /// </summary>
    public bool RemoveQuantity(int amount)
    {
        if (quantity >= amount)
        {
            quantity -= amount;
            if (quantity <= 0)
            {
                Clear();
            }
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Clear this slot completely
    /// </summary>
    public void Clear()
    {
        item = null;
        quantity = 0;
    }
}
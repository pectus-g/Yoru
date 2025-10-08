using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }
    
    [Header("Inventory Settings")]
    [SerializeField] private int maxUniqueItems = 20;
    
    [Header("Drop Settings")]
    [SerializeField] private float dropDistance = 1.5f;
    [SerializeField] private LayerMask groundLayer;
    
    private List<InventorySlot> inventorySlots = new List<InventorySlot>();
    
    public System.Action OnInventoryChanged;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadInventory();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public List<InventorySlot> GetInventorySlots()
    {
        return inventorySlots;
    }
    
    public int GetItemCount()
    {
        return inventorySlots.Count;
    }
    
    public bool IsFull()
    {
        return inventorySlots.Count >= maxUniqueItems;
    }
    
    public bool AddItem(InventoryItem item, int quantity = 1)
    {
        if (item == null) return false;
        
        InventorySlot existingSlot = inventorySlots.FirstOrDefault(slot => slot.item.itemID == item.itemID);
        
        if (existingSlot != null)
        {
            int remainingQuantity = existingSlot.AddQuantity(quantity);
            
            if (remainingQuantity > 0)
            {
                if (IsFull())
                {
                    Debug.Log("Inventory is full! Cannot add more unique items.");
                    OnInventoryChanged?.Invoke();
                    SaveInventory();
                    return false;
                }
                
                InventorySlot newSlot = new InventorySlot(item, remainingQuantity);
                inventorySlots.Add(newSlot);
            }
            
            OnInventoryChanged?.Invoke();
            SaveInventory();
            return true;
        }
        else
        {
            if (IsFull())
            {
                Debug.Log($"Inventory is full! Cannot add {item.itemName}. Max unique items: {maxUniqueItems}");
                return false;
            }
            
            InventorySlot newSlot = new InventorySlot(item, quantity);
            inventorySlots.Add(newSlot);
            
            Debug.Log($"Added {quantity}x {item.itemName} to inventory! ({inventorySlots.Count}/{maxUniqueItems} slots used)");
            
            OnInventoryChanged?.Invoke();
            SaveInventory();
            return true;
        }
    }
    
    public bool RemoveItem(InventoryItem item, int quantity = 1)
    {
        if (item == null) return false;
        
        InventorySlot slot = inventorySlots.FirstOrDefault(s => s.item.itemID == item.itemID);
        
        if (slot != null)
        {
            bool success = slot.RemoveQuantity(quantity);
            
            if (slot.IsEmpty())
            {
                inventorySlots.Remove(slot);
                Debug.Log($"Removed last {item.itemName} - slot removed from inventory");
            }
            
            if (success)
            {
                OnInventoryChanged?.Invoke();
                SaveInventory();
            }
            
            return success;
        }
        
        return false;
    }
    
    public bool RemoveItemFromSlot(int slotIndex, int quantity = 1)
    {
        if (slotIndex < 0 || slotIndex >= inventorySlots.Count) return false;
        
        InventorySlot slot = inventorySlots[slotIndex];
        bool result = slot.RemoveQuantity(quantity);
        
        if (slot.IsEmpty())
        {
            inventorySlots.RemoveAt(slotIndex);
        }
        
        if (result)
        {
            OnInventoryChanged?.Invoke();
            SaveInventory();
        }
        
        return result;
    }
    
    public void UseItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= inventorySlots.Count) return;
        
        InventorySlot slot = inventorySlots[slotIndex];
        if (!slot.item.isConsumable) 
        {
            Debug.Log($"{slot.item.itemName} is not consumable!");
            return;
        }
        
        Debug.Log($"Used {slot.item.itemName}! Effect: {slot.item.effectType}");
        
        RemoveItemFromSlot(slotIndex, 1);
    }
    
    public void DropItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= inventorySlots.Count) return;
        
        InventorySlot slot = inventorySlots[slotIndex];
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("Player not found! Make sure player has 'Player' tag.");
            return;
        }
        
        Vector3 dropPosition = player.transform.position + player.transform.forward * dropDistance;
        
        RaycastHit hit;
        if (Physics.Raycast(dropPosition + Vector3.up * 2f, Vector3.down, out hit, 10f, groundLayer))
        {
            dropPosition = hit.point + Vector3.up * 0.5f;
        }
        
        if (slot.item.worldPrefab != null)
        {
            GameObject droppedItem = Instantiate(slot.item.worldPrefab, dropPosition, Quaternion.identity);
            
            ItemPickup pickup = droppedItem.GetComponent<ItemPickup>();
            if (pickup != null)
            {
                pickup.SetItem(slot.item, 1);
            }
        }
        else
        {
            Debug.LogWarning($"Item {slot.item.itemName} has no world prefab assigned!");
        }
        
        RemoveItemFromSlot(slotIndex, 1);
    }
    
    public bool HasItem(InventoryItem item)
    {
        return inventorySlots.Any(slot => slot.item.itemID == item.itemID);
    }
    
    public int GetItemQuantity(InventoryItem item)
    {
        return inventorySlots
            .Where(slot => slot.item.itemID == item.itemID)
            .Sum(slot => slot.quantity);
    }
    
    public void ClearInventory()
    {
        inventorySlots.Clear();
        OnInventoryChanged?.Invoke();
        SaveInventory();
        Debug.Log("Inventory cleared!");
    }
    
    [System.Serializable]
    private class InventorySaveData
    {
        public List<SlotSaveData> slots = new List<SlotSaveData>();
    }
    
    [System.Serializable]
    private class SlotSaveData
    {
        public string itemID;
        public int quantity;
    }
    
    private string SavePath => Path.Combine(Application.persistentDataPath, "inventory.json");
    
    public void SaveInventory()
    {
        InventorySaveData saveData = new InventorySaveData();
        
        foreach (InventorySlot slot in inventorySlots)
        {
            SlotSaveData slotData = new SlotSaveData
            {
                itemID = slot.item.itemID,
                quantity = slot.quantity
            };
            saveData.slots.Add(slotData);
        }
        
        string json = JsonUtility.ToJson(saveData, true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"Inventory saved! {inventorySlots.Count} items stored.");
    }
    
    public void LoadInventory()
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log("No saved inventory found. Starting fresh.");
            return;
        }
        
        try
        {
            string json = File.ReadAllText(SavePath);
            InventorySaveData saveData = JsonUtility.FromJson<InventorySaveData>(json);
            
            inventorySlots.Clear();
            
            foreach (SlotSaveData slotData in saveData.slots)
            {
                InventoryItem item = FindItemByID(slotData.itemID);
                if (item != null)
                {
                    InventorySlot slot = new InventorySlot(item, slotData.quantity);
                    inventorySlots.Add(slot);
                }
            }
            
            OnInventoryChanged?.Invoke();
            Debug.Log($"Inventory loaded! {inventorySlots.Count} items restored.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error loading inventory: " + e.Message);
        }
    }
    
    private InventoryItem FindItemByID(string itemID)
    {
        InventoryItem[] allItems = Resources.LoadAll<InventoryItem>("Items");
        
        foreach (InventoryItem item in allItems)
        {
            if (item.itemID == itemID)
            {
                return item;
            }
        }
        
        Debug.LogWarning($"Item with ID '{itemID}' not found in Resources/Items folder!");
        return null;
    }
}
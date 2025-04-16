using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using MelonLoader;
using System;

namespace REPO_UTILS
{
    /// <summary>
    /// Manages item-related functionality:
    /// - Item ESP (tracking)
    /// - Item information display
    /// - Item value calculation
    /// </summary>
    public class ItemManager
    {
        private Core _core;
        private Transform _levelTransform;

        // Item tracking
        private List<Transform> _items = new List<Transform>();
        private List<GameObject> _itemLineObjects = new List<GameObject>();
        private List<LineRenderer> _itemLineRenderers = new List<LineRenderer>();

        // Item display data
        private List<string> _itemNames = new List<string>();
        private List<float> _itemDistances = new List<float>();

        // Item ESP state
        private bool _itemESPEnabled = true;

        // Constructor
        public ItemManager(Core core)
        {
            _core = core;
        }

        #region Initialization

        public void Initialize(Transform levelTransform)
        {
            MelonLogger.Msg("[ItemManager.Initialize] Called.");
            _levelTransform = levelTransform;
            if (_levelTransform == null) MelonLogger.Warning("  _levelTransform is NULL on Initialize!");

            FindItems();
            CreateLineRenderersForItems();
            MelonLogger.Msg("[ItemManager.Initialize] Finished.");
        }

        public void Reset()
        {
            _levelTransform = null;

            _items.Clear();
            ClearItemESP();

            _itemNames.Clear();
            _itemDistances.Clear();
        }

        public void OnApplicationQuit()
        {
            ClearItemESP();
        }

        #endregion

        #region Update Methods

        public void OnUpdate()
        {
            if (_items.Count > 0)
            {
                UpdateItemList();
                UpdateESPForItems();
                UpdateItemInfo();
            }
        }

        private void FindItems()
        {
            MelonLogger.Msg("[ItemManager.FindItems] Called.");
            _items.Clear(); // Clear before finding

            if (_levelTransform == null)
            {
                 MelonLogger.Warning("  Cannot find items: _levelTransform is null.");
                 return;
            }

            var valuableObjects = _levelTransform.GetComponentsInChildren<MonoBehaviour>()
                .Where(mb => mb.GetType().Name == "ValuableObject")
                .Select(mb => mb.transform)
                .ToList();

            MelonLogger.Msg($"[ItemManager.FindItems] Found {valuableObjects.Count} ValuableObject components under Level.");

            foreach (var item in valuableObjects)
            {
                // if (!_items.Contains(item)) // We cleared _items, so contains check is redundant
                {
                    _items.Add(item);
                }
            }
            MelonLogger.Msg($"[ItemManager.FindItems] Finished. Added {_items.Count} items to list.");
        }

        private void UpdateItemList()
        {
            // Remove destroyed items and their renderers
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] == null)
                {
                    if (i < _itemLineRenderers.Count && _itemLineRenderers[i] != null)
                    {
                        if (i < _itemLineObjects.Count && _itemLineObjects[i] != null)
                        {
                            GameObject.Destroy(_itemLineObjects[i]);
                            _itemLineObjects[i] = null;
                            _itemLineRenderers[i] = null;
                        }
                    }
                }
            }

            // Filter out null items
            _items = _items.Where(item => item != null).ToList();

            // Clean up line renderers
            for (int i = _itemLineObjects.Count - 1; i >= 0; i--)
            {
                if (_itemLineObjects[i] == null)
                {
                    _itemLineObjects.RemoveAt(i);
                    if (i < _itemLineRenderers.Count)
                    {
                        _itemLineRenderers.RemoveAt(i);
                    }
                }
            }
        }

        private void UpdateESPForItems()
        {
            if (!_itemESPEnabled) return;

            Vector3 playerPosition = _core.GetPlayerPosition();

            for (int i = 0; i < _items.Count && i < _itemLineRenderers.Count; i++)
            {
                if (_items[i] != null && _itemLineRenderers[i] != null)
                {
                    Vector3 itemPosition = _items[i].position;
                    DrawLineToTarget(playerPosition, itemPosition, _itemLineRenderers[i]);
                }
            }
        }

        private void UpdateItemInfo()
        {
            _itemNames.Clear();
            _itemDistances.Clear();

            Vector3 playerPosition = _core.GetPlayerPosition();
            List<(string, float)> itemInfoList = new List<(string, float)>();

            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] == null) continue;

                float distance = Vector3.Distance(playerPosition, _items[i].position);
                string itemName = _items[i].gameObject.name;

                itemInfoList.Add((itemName, distance));
            }

            itemInfoList.Sort((a, b) => a.Item2.CompareTo(b.Item2));

            foreach (var info in itemInfoList)
            {
                _itemNames.Add(info.Item1);
                _itemDistances.Add(info.Item2);
            }
        }

        #endregion

        #region Item ESP Methods

        private void CreateLineRenderersForItems()
        {
            foreach (var item in _items)
            {
                GameObject lineObject = new GameObject($"ESP_ItemLine_{_items.IndexOf(item)}");
                _itemLineObjects.Add(lineObject);

                LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
                _itemLineRenderers.Add(lineRenderer);

                lineRenderer.startWidth = 0.005f;
                lineRenderer.endWidth = 0.005f;
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                lineRenderer.startColor = Color.green;
                lineRenderer.endColor = Color.green;
                lineRenderer.positionCount = 2;
            }
        }

        private void DrawLineToTarget(Vector3 startPosition, Vector3 endPosition, LineRenderer lineRenderer)
        {
            if (lineRenderer != null)
            {
                lineRenderer.SetPosition(0, startPosition);
                lineRenderer.SetPosition(1, endPosition);
            }
        }

        public void ToggleItemESP()
        {
            _itemESPEnabled = !_itemESPEnabled;
            SetItemESPVisibility(_itemESPEnabled);
            MelonLogger.Msg($"Item ESP is now {(_itemESPEnabled ? "ON" : "OFF")}");
        }

        public bool IsItemESPEnabled()
        {
            return _itemESPEnabled;
        }

        private void SetItemESPVisibility(bool isVisible)
        {
            foreach (var lineRenderer in _itemLineRenderers)
            {
                if (lineRenderer != null)
                    lineRenderer.enabled = isVisible;
            }
        }

        private void ClearItemESP()
        {
            foreach (var lineObject in _itemLineObjects)
            {
                if (lineObject != null)
                {
                    GameObject.Destroy(lineObject);
                }
            }

            _itemLineObjects.Clear();
            _itemLineRenderers.Clear();
        }

        #endregion

        #region Item Value Management

        public float CalculateTotalItemValue()
        {
            float totalValue = 0f;

            foreach (var item in _items)
            {
                if (item == null) continue;

                MonoBehaviour valuableComponent = item.GetComponents<MonoBehaviour>()
                    .FirstOrDefault(c => c.GetType().Name == "ValuableObject");

                if (valuableComponent != null)
                {
                    // Try to get the current value (not original value)
                    FieldInfo dollarValueField = valuableComponent.GetType().GetField("dollarValueCurrent",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (dollarValueField != null)
                    {
                        object value = dollarValueField.GetValue(valuableComponent);

                        if (value is float floatValue)
                        {
                            totalValue += floatValue;
                        }
                        else if (value is int intValue)
                        {
                            totalValue += intValue;
                        }
                        else if (value is double doubleValue)
                        {
                            totalValue += (float)doubleValue;
                        }
                    }
                }
            }

            return totalValue;
        }

        // New method to set the value of the closest item
        public void MaxValueClosestItem()
        {
            MelonLogger.Msg("Attempting to maximize value of closest item...");

            if (_items == null || _items.Count == 0)
            {
                MelonLogger.Warning("No items found to modify.");
                return;
            }

            Vector3 playerPosition = _core.GetPlayerPosition();
            Transform closestItem = null;
            float minDistanceSqr = float.MaxValue;

            // Find the closest valid item
            foreach (Transform itemTransform in _items)
            {
                if (itemTransform == null) continue;

                float distanceSqr = (itemTransform.position - playerPosition).sqrMagnitude;
                if (distanceSqr < minDistanceSqr)
                {
                    minDistanceSqr = distanceSqr;
                    closestItem = itemTransform;
                }
            }

            if (closestItem == null)
            {
                MelonLogger.Warning("Could not determine the closest item.");
                return;
            }

            MelonLogger.Msg($"Closest item found: {closestItem.name} at distance {Mathf.Sqrt(minDistanceSqr):F2}m");

            // Get the ValuableObject component
            MonoBehaviour valuableComponent = closestItem.GetComponent("ValuableObject") as MonoBehaviour;
            if (valuableComponent == null)
            {
                MelonLogger.Error($"Closest item '{closestItem.name}' does not have a ValuableObject component.");
                return;
            }

            // Use DollarValueSetRPC method instead of setting field
            string methodName = "DollarValueSetRPC";
            MethodInfo valueSetMethod = valuableComponent.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(float) }, null);

            if (valueSetMethod == null)
            {
                MelonLogger.Error($"Could not find method '{methodName}(float)' on {valuableComponent.GetType().Name} for item '{closestItem.name}'.");
                return;
            }

            // Set the value to 999999 using the method
            try
            {
                float newValue = 999999f;
                MelonLogger.Msg($"Attempting to call {methodName}({newValue}) on '{closestItem.name}'...");
                valueSetMethod.Invoke(valuableComponent, new object[] { newValue });
                MelonLogger.Msg($"Successfully called {methodName} on '{closestItem.name}'.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to call method '{methodName}' for item '{closestItem.name}': {ex.Message}");
            }
        }

        // New method to set all item values to 1
        public void MakeAllItemsCheap()
        {
            MelonLogger.Msg("Attempting to make all items cheap (value=1)...");

            GameObject levelGenerator = GameObject.Find("Level Generator");
            if (levelGenerator == null)
            {
                MelonLogger.Error("Could not find 'Level Generator' GameObject.");
                return;
            }

            Transform itemsParent = levelGenerator.transform.Find("Items");
            if (itemsParent == null)
            {
                MelonLogger.Error("Could not find 'Items' child transform under 'Level Generator'.");
                return;
            }

            int itemsProcessed = 0;
            int itemsModified = 0;
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            foreach (Transform itemChild in itemsParent)
            {
                itemsProcessed++;
                if (itemChild == null) continue;

                // Get the ItemAttributes component
                MonoBehaviour itemAttributes = itemChild.GetComponent("ItemAttributes") as MonoBehaviour;
                if (itemAttributes == null)
                {
                    // MelonLogger.Warning($"Item '{itemChild.name}' does not have an ItemAttributes component. Skipping.");
                    continue; // Skip if component not found
                }

                // --- Revert: Find and set the 'value' field directly ---
                FieldInfo valueField = itemAttributes.GetType().GetField("value", flags);
                if (valueField == null)
                {
                    // MelonLogger.Warning($"Could not find 'value' field on ItemAttributes for item '{itemChild.name}'. Skipping.");
                    continue; // Skip if field not found
                }

                // Set the value to 1
                try
                {
                    // Ensure the field is an int before setting
                    if (valueField.FieldType == typeof(int))
                    {
                        valueField.SetValue(itemAttributes, 1);
                        itemsModified++;
                    }
                    else
                    {
                         MelonLogger.Warning($"Value field on '{itemChild.name}' is not an int ({valueField.FieldType}). Skipping.");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Failed to set value for item '{itemChild.name}': {ex.Message}");
                }
            }

            MelonLogger.Msg($"Cheap items process complete. Processed: {itemsProcessed}, Modified: {itemsModified}.");
        }

        // New method to complete all extraction points
        public void CompleteExtractionPoints()
        {
            MelonLogger.Msg("Attempting to complete all extraction points...");

            GameObject levelGenerator = GameObject.Find("Level Generator");
            if (levelGenerator == null)
            {
                MelonLogger.Error("Could not find 'Level Generator' GameObject.");
                return;
            }

            Transform levelTransform = levelGenerator.transform.Find("Level");
            if (levelTransform == null)
            {
                MelonLogger.Error("Could not find 'Level' child transform under 'Level Generator'.");
                return;
            }

            // Find all descendant GameObjects named "Extraction Point" under the Level transform
            List<GameObject> extractionPointGameObjects = levelTransform.GetComponentsInChildren<Transform>(true) // Include inactive
                .Where(t => t.gameObject.name == "Extraction Point")
                .Select(t => t.gameObject)
                .ToList();

            if (extractionPointGameObjects.Count == 0)
            {
                MelonLogger.Warning("No GameObjects named 'Extraction Point' found under Level Generator/Level.");
                return;
            }

            MelonLogger.Msg($"Found {extractionPointGameObjects.Count} GameObjects named 'Extraction Point'.");

            int pointsModified = 0;
            foreach (var epGameObject in extractionPointGameObjects)
            {
                 // Now get the ExtractionPoint component from this specific GameObject
                 MonoBehaviour epComponent = epGameObject.GetComponent("ExtractionPoint") as MonoBehaviour;
                 if (epComponent == null)
                 {
                     MelonLogger.Warning($"GameObject '{epGameObject.name}' found, but it does not have an ExtractionPoint component. Skipping.");
                    continue;
                 }

                try
                {
                    Type epType = epComponent.GetType();
                    // Find the nested State enum type
                    Type stateEnumType = epType.GetNestedType("State", BindingFlags.Public | BindingFlags.NonPublic);
                    if (stateEnumType == null || !stateEnumType.IsEnum)
                    {
                        MelonLogger.Error($"Could not find nested enum 'State' in {epType.FullName} for {epComponent.gameObject.name}.");
                        continue;
                    }

                    // Get the "Complete" value from the enum
                    object completeStateValue;
                    try
                    {
                        completeStateValue = Enum.Parse(stateEnumType, "Complete", true); // Case-insensitive
                    }
                    catch (ArgumentException)
                    {
                        MelonLogger.Error($"Enum value 'Complete' not found in {stateEnumType.FullName} for {epComponent.gameObject.name}.");
                        continue;
                    }

                    // Find the StateSet method that takes the State enum
                    MethodInfo stateSetMethod = epType.GetMethod("StateSet", new Type[] { stateEnumType });
                    if (stateSetMethod == null)
                    {
                         // Also try finding StateSetRPC as a fallback?
                         stateSetMethod = epType.GetMethod("StateSetRPC", new Type[] { stateEnumType });
                        if (stateSetMethod == null)
                        {
                             MelonLogger.Error($"Could not find method 'StateSet({stateEnumType.Name})' or 'StateSetRPC({stateEnumType.Name})' in {epType.FullName} for {epComponent.gameObject.name}.");
                            continue;
                        }
                         MelonLogger.Msg($"Using StateSetRPC method for {epComponent.gameObject.name}.");
                    }

                    // Call the StateSet method with the Complete state
                    MelonLogger.Msg($"Attempting to call {stateSetMethod.Name}({completeStateValue}) on {epComponent.gameObject.name}...");
                    stateSetMethod.Invoke(epComponent, new object[] { completeStateValue });
                    pointsModified++;
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Failed to process extraction point '{epComponent.gameObject.name}': {ex.Message}");
                }
            }

            MelonLogger.Msg($"Extraction point completion process finished. Modified: {pointsModified}/{extractionPointGameObjects.Count}");
        }

        #endregion

        #region Getters

        public List<string> GetItemNames()
        {
            return _itemNames;
        }

        public List<float> GetItemDistances()
        {
            return _itemDistances;
        }

        public int GetItemCount()
        {
            return _items.Count;
        }

        #endregion
    }
}
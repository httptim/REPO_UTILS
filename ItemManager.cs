using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using MelonLoader;

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
            _levelTransform = levelTransform;

            FindItems();
            CreateLineRenderersForItems();
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
            if (_levelTransform == null) return;

            var valuableObjects = _levelTransform.GetComponentsInChildren<MonoBehaviour>()
                .Where(mb => mb.GetType().Name == "ValuableObject")
                .Select(mb => mb.transform)
                .ToList();

            MelonLogger.Msg($"Found {valuableObjects.Count} valuable items in the level");

            foreach (var item in valuableObjects)
            {
                if (!_items.Contains(item))
                {
                    _items.Add(item);
                }
            }
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
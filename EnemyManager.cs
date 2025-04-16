using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System;
using MelonLoader;

namespace REPO_UTILS
{
    /// <summary>
    /// Manages enemy-related functionality:
    /// - Enemy ESP (tracking)
    /// - Enemy information display
    /// - Enemy manipulation (kill, etc.)
    /// </summary>
    public class EnemyManager
    {
        private Core _core;
        private Transform _enemiesParent;

        // Enemy tracking
        private List<Transform> _enemies = new List<Transform>();
        private List<GameObject> _lineObjects = new List<GameObject>();
        private List<LineRenderer> _lineRenderers = new List<LineRenderer>();
        private List<bool> _enemyWasActive = new List<bool>();
        private List<string> _enemyTypes = new List<string>();

        // Enemy display data
        private List<string> _enemyNames = new List<string>();
        private List<float> _enemyDistances = new List<float>();
        private List<Transform> _sortedEnemies = new List<Transform>(); // Enemies in the same order as UI

        // Enemy ESP state
        private bool _enemyESPEnabled = true;

        // Constructor
        public EnemyManager(Core core)
        {
            _core = core;
        }

        #region Initialization

        public void Initialize(Transform enemiesParent)
        {
            MelonLogger.Msg("[EnemyManager.Initialize] Called.");
            _enemiesParent = enemiesParent;
            if (_enemiesParent == null) MelonLogger.Warning("  _enemiesParent is NULL on Initialize!");

            FindEnemies();
            CreateLineRenderersForEnemies();
            MelonLogger.Msg("[EnemyManager.Initialize] Finished.");
        }

        public void Reset()
        {
            _enemiesParent = null;

            _enemies.Clear();
            _enemyTypes.Clear();
            ClearEnemyESP();

            _enemyWasActive.Clear();
            _enemyNames.Clear();
            _enemyDistances.Clear();
            _sortedEnemies.Clear();
        }

        public void OnApplicationQuit()
        {
            ClearEnemyESP();
        }

        #endregion

        #region Update Methods

        public void OnUpdate()
        {
            if (_enemies.Count > 0 && _lineRenderers.Count > 0)
            {
                UpdateEnemyList();
                UpdateESPForEnemies();
                UpdateEnemyInfo();
            }
        }

        private void UpdateEnemyList()
        {
            if (_enemiesParent == null) return;

            // Get all children of the enemies parent that are not the parent itself
            Transform[] currentEnemies = _enemiesParent.GetComponentsInChildren<Transform>(true)
                .Where(t => t != _enemiesParent && t.name.Contains("Enemy")).ToArray();

            // Add any new enemies to our list
            foreach (Transform enemy in currentEnemies)
            {
                if (!_enemies.Contains(enemy))
                {
                    _enemies.Add(enemy);
                    _enemyWasActive.Add(true);
                    CreateLineRendererForEnemy(enemy);
                }
            }

            // Remove any destroyed enemies
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                if (_enemies[i] == null)
                {
                    _enemies.RemoveAt(i);
                    if (i < _enemyWasActive.Count)
                        _enemyWasActive.RemoveAt(i);
                }
            }
        }

        private void UpdateESPForEnemies()
        {
            if (!_enemyESPEnabled) return;

            Vector3 playerPosition = _core.GetPlayerPosition();

            for (int i = 0; i < _enemies.Count && i < _lineRenderers.Count; i++)
            {
                if (_enemies[i] == null)
                {
                    if (i < _lineRenderers.Count && _lineRenderers[i] != null)
                    {
                        _lineRenderers[i].enabled = false;
                    }
                    continue;
                }

                Transform enable = _enemies[i].Find("Enable");
                if (enable == null) continue;

                bool isActive = enable != null && enable.gameObject.activeInHierarchy;

                if (i < _enemyWasActive.Count)
                {
                    _enemyWasActive[i] = isActive;
                }

                if (!isActive)
                {
                    if (i < _lineRenderers.Count && _lineRenderers[i] != null)
                    {
                        _lineRenderers[i].enabled = false;
                    }
                    continue;
                }

                Transform controller = enable.Find("Controller");
                if (controller == null) continue;

                if (i < _lineRenderers.Count && _lineRenderers[i] != null)
                {
                    _lineRenderers[i].enabled = true;
                    Vector3 enemyPosition = controller.position;
                    DrawLineToTarget(playerPosition, enemyPosition, _lineRenderers[i]);
                }
            }
        }

        private void UpdateEnemyInfo()
        {
            _enemyNames.Clear();
            _enemyDistances.Clear();
            _sortedEnemies.Clear();

            if (_core.GetPlayerPosition() == null) return;
            Vector3 playerPosition = _core.GetPlayerPosition();
            List<(Transform enemy, string name, float distance, bool active)> enemyInfoList = new List<(Transform, string, float, bool)>();

            // Collect info for all valid enemies
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i] == null) continue;

                Transform enable = _enemies[i].Find("Enable");
                if (enable == null) continue;

                bool isActive = enable.gameObject.activeInHierarchy;

                Transform controller = enable.Find("Controller");
                if (controller == null) continue;

                float distance = Vector3.Distance(playerPosition, controller.position);
                string enemyName = _enemies[i].gameObject.name;

                // Store the enemy transform with all the other info
                enemyInfoList.Add((_enemies[i], enemyName, distance, isActive));
            }

            // Sort by distance
            enemyInfoList.Sort((a, b) => a.distance.CompareTo(b.distance));

            // Build the UI lists from sorted data
            foreach (var info in enemyInfoList)
            {
                string displayName = info.active ? info.name : $"[INACTIVE] {info.name}";

                _enemyNames.Add(displayName);
                _enemyDistances.Add(info.distance);
                _sortedEnemies.Add(info.enemy);
            }
        }

        #endregion

        #region Enemy ESP Methods

        private void CreateLineRendererForEnemy(Transform enemy)
        {
            GameObject lineObject = new GameObject($"ESP_LineRenderer_{_enemies.IndexOf(enemy)}");
            _lineObjects.Add(lineObject);

            LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
            _lineRenderers.Add(lineRenderer);

            lineRenderer.startWidth = 0.01f;
            lineRenderer.endWidth = 0.01f;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = Color.red;
            lineRenderer.endColor = Color.red;
            lineRenderer.positionCount = 2;
        }

        private void CreateLineRenderersForEnemies()
        {
            foreach (var enemy in _enemies)
            {
                CreateLineRendererForEnemy(enemy);
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

        public void ToggleEnemyESP()
        {
            _enemyESPEnabled = !_enemyESPEnabled;
            SetEnemyESPVisibility(_enemyESPEnabled);
            MelonLogger.Msg($"Enemy ESP is now {(_enemyESPEnabled ? "ON" : "OFF")}");
        }

        public bool IsEnemyESPEnabled()
        {
            return _enemyESPEnabled;
        }

        private void SetEnemyESPVisibility(bool isVisible)
        {
            for (int i = 0; i < _lineRenderers.Count; i++)
            {
                if (_lineRenderers[i] != null)
                {
                    bool enemyActive = i < _enemyWasActive.Count && _enemyWasActive[i];
                    _lineRenderers[i].enabled = isVisible && enemyActive;
                }
            }
        }

        private void ClearEnemyESP()
        {
            foreach (var lineObject in _lineObjects)
            {
                if (lineObject != null)
                {
                    GameObject.Destroy(lineObject);
                }
            }

            _lineObjects.Clear();
            _lineRenderers.Clear();
        }

        #endregion

        #region Enemy Manipulation

        public void KillEnemy(int uiIndex)
        {
            // Check if the UI index is valid
            if (uiIndex < 0 || uiIndex >= _sortedEnemies.Count) return;

            Transform enemy = _sortedEnemies[uiIndex];
            if (enemy == null) return;

            // Validate the path to the EnemyHealth component
            Transform enable = enemy.Find("Enable");
            if (enable == null) return;

            Transform controller = enable.Find("Controller");
            if (controller == null) return;

            // Look for the EnemyHealth component on the controller
            MonoBehaviour enemyHealth = controller.GetComponents<MonoBehaviour>()
                .FirstOrDefault(c => c.GetType().Name == "EnemyHealth");

            if (enemyHealth != null)
            {
                // Call the Hurt method with a fixed large damage value and zero vector as direction
                MethodInfo hurtMethod = enemyHealth.GetType().GetMethod("Hurt",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (hurtMethod != null)
                {
                    try
                    {
                        Vector3 zeroDirection = Vector3.zero;
                        // Use 1000 as damage to ensure the enemy dies
                        hurtMethod.Invoke(enemyHealth, new object[] { 1000, zeroDirection });

                        // Force a refresh of the enemy lists
                        UpdateEnemyInfo();
                        return;
                    }
                    catch { }
                }

                // Fallback: Set health directly to 0
                FieldInfo healthField = enemyHealth.GetType().GetField("health",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (healthField != null)
                {
                    try
                    {
                        healthField.SetValue(enemyHealth, 0);

                        // Force a refresh of the enemy lists
                        UpdateEnemyInfo();
                        return;
                    }
                    catch { }
                }
            }
        }

        #endregion

        #region Getters

        public List<string> GetEnemyNames()
        {
            return _enemyNames;
        }

        public List<float> GetEnemyDistances()
        {
            return _enemyDistances;
        }

        public List<Transform> GetSortedEnemies()
        {
            return _sortedEnemies;
        }

        public int GetEnemyCount()
        {
            return _enemies.Count;
        }

        #endregion

        private void FindEnemies()
        {
            MelonLogger.Msg("[EnemyManager.FindEnemies] Called.");
            _enemies.Clear(); // Clear before finding
            _enemyTypes.Clear();
            // Should also clear _lineObjects / _lineRenderers here?
            // ClearEnemyESP(); // Might be better to call this

            if (_enemiesParent == null)
            {
                MelonLogger.Warning("  Cannot find enemies: _enemiesParent is null.");
                return;
            }

            // Find all direct children (assuming enemies are direct children)
            List<Transform> potentialEnemies = new List<Transform>();
             for (int i = 0; i < _enemiesParent.childCount; i++)
             {
                 potentialEnemies.Add(_enemiesParent.GetChild(i));
             }
            
            MelonLogger.Msg($"[EnemyManager.FindEnemies] Found {potentialEnemies.Count} potential enemy transforms under Enemies parent.");

            // Add filtering/validation if needed here (e.g., check for specific components)
            foreach (var enemy in potentialEnemies)
            {
                 if (enemy != null) // Basic null check
                 {
                     _enemies.Add(enemy);
                     _enemyTypes.Add(enemy.name); // Use name as type for now
                 }
            }
            MelonLogger.Msg($"[EnemyManager.FindEnemies] Finished. Added {_enemies.Count} enemies to list.");
        }
    }
}
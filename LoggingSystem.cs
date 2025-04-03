using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.IO;
using System;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine.SceneManagement;
using System.Linq;

namespace REPO_UTILS
{
    /// <summary>
    /// Advanced logging system that analyzes game components (player, enemies, items, world objects),
    /// and saves logs to separate files for better organization.
    /// </summary>
    public class LoggingSystem
    {
        private Core _core;
        private bool _loggingEnabled = false;
        private float _logInterval = 10f; // Log update interval in seconds
        private object _loggingCoroutine;

        // Base directory for logs
        private string _logsBaseDir;

        // Separate log files for different game components
        private Dictionary<LogCategory, string> _logFilePaths = new Dictionary<LogCategory, string>();

        // Enum to categorize different types of logs
        public enum LogCategory
        {
            Player,
            Enemies,
            Items,
            WorldObjects,
            System,
            Structure,      // For general game structure
            Miscellaneous   // For objects that don't fit other categories
        }

        // Tracking structures for components and their values
        private HashSet<int> _loggedGameObjects = new HashSet<int>();
        private Dictionary<LogCategory, HashSet<int>> _categoryLoggedObjects = new Dictionary<LogCategory, HashSet<int>>();
        private HashSet<int> _loggedComponents = new HashSet<int>();
        private Dictionary<string, HashSet<string>> _loggedMethods = new Dictionary<string, HashSet<string>>();
        private Dictionary<string, HashSet<string>> _loggedFields = new Dictionary<string, HashSet<string>>();
        private Dictionary<string, HashSet<string>> _loggedProperties = new Dictionary<string, HashSet<string>>();
        private Dictionary<string, Dictionary<string, object>> _fieldValues = new Dictionary<string, Dictionary<string, object>>();
        private Dictionary<string, Dictionary<string, object>> _propertyValues = new Dictionary<string, Dictionary<string, object>>();

        // Specific known fields we're interested in
        private readonly string[] _speedRelatedTerms = new string[] {
            "MoveSpeed" // The specific field we know exists
        };

        private readonly string[] _staminaRelatedTerms = new string[] {
            "EnergyCurrent", "EnergyStart" // The specific fields we know exist
        };

        private readonly string[] _jumpRelatedTerms = new string[] {
            "JumpExtra", "JumpExtraCurrent" // The specific fields we know exist
        };

        // Constructor
        public LoggingSystem(Core core)
        {
            _core = core;

            // Initialize log file paths
            InitializeLogPaths();

            // Initialize category-specific tracking
            foreach (LogCategory category in Enum.GetValues(typeof(LogCategory)))
            {
                _categoryLoggedObjects[category] = new HashSet<int>();
            }
        }

        #region Log File Management

        private void InitializeLogPaths()
        {
            // Set up base directory for logs
            _logsBaseDir = Path.Combine(MelonEnvironment.UserDataDirectory, "MelonLoader", "Logs", "REPO_UTILS");

            // Create the directory if it doesn't exist
            if (!Directory.Exists(_logsBaseDir))
            {
                Directory.CreateDirectory(_logsBaseDir);
            }

            // Initialize log file paths for each category
            _logFilePaths[LogCategory.Player] = Path.Combine(_logsBaseDir, "Player_Components.log");
            _logFilePaths[LogCategory.Enemies] = Path.Combine(_logsBaseDir, "Enemy_Components.log");
            _logFilePaths[LogCategory.Items] = Path.Combine(_logsBaseDir, "Item_Components.log");
            _logFilePaths[LogCategory.WorldObjects] = Path.Combine(_logsBaseDir, "World_Objects.log");
            _logFilePaths[LogCategory.System] = Path.Combine(_logsBaseDir, "System_Events.log");
            _logFilePaths[LogCategory.Structure] = Path.Combine(_logsBaseDir, "Game_Structure.log");
            _logFilePaths[LogCategory.Miscellaneous] = Path.Combine(_logsBaseDir, "Misc_Objects.log");

            // Log the initialization to the system log
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string initMessage = $"=== REPO_UTILS Logging System Initialized at {timestamp} ===\n" +
                                $"Log files directory: {_logsBaseDir}\n";

            File.WriteAllText(_logFilePaths[LogCategory.System], initMessage);
        }

        public string GetLogFilePath(LogCategory category)
        {
            if (_logFilePaths.ContainsKey(category))
            {
                return _logFilePaths[category];
            }

            return _logFilePaths[LogCategory.System]; // Default to system log
        }

        /// <summary>
        /// Gets the file path for the game structure log
        /// </summary>
        public string GetStructureLogFilePath()
        {
            return GetLogFilePath(LogCategory.Structure);
        }

        /// <summary>
        /// Write a message to a specific log file category
        /// </summary>
        private void WriteToLog(LogCategory category, string content, bool append = true)
        {
            try
            {
                string logPath = GetLogFilePath(category);

                if (append)
                {
                    File.AppendAllText(logPath, content);
                }
                else
                {
                    File.WriteAllText(logPath, content);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to write to {category} log: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears all logs or a specific log category
        /// </summary>
        public void ClearLogs(LogCategory? specificCategory = null)
        {
            if (specificCategory.HasValue)
            {
                if (_logFilePaths.ContainsKey(specificCategory.Value))
                {
                    File.WriteAllText(_logFilePaths[specificCategory.Value],
                        $"=== REPO_UTILS {specificCategory.Value} Log Cleared at {DateTime.Now} ===\n");
                    MelonLogger.Msg($"Cleared {specificCategory.Value} log file");

                    // Clear the tracking for this category
                    if (_categoryLoggedObjects.ContainsKey(specificCategory.Value))
                    {
                        _categoryLoggedObjects[specificCategory.Value].Clear();
                    }
                }
            }
            else
            {
                // Clear all logs
                foreach (var category in _logFilePaths.Keys)
                {
                    File.WriteAllText(_logFilePaths[category],
                        $"=== REPO_UTILS {category} Log Cleared at {DateTime.Now} ===\n");

                    // Clear the tracking for each category
                    if (_categoryLoggedObjects.ContainsKey(category))
                    {
                        _categoryLoggedObjects[category].Clear();
                    }
                }

                // Reset global tracking
                _loggedGameObjects.Clear();
                _loggedComponents.Clear();

                MelonLogger.Msg("Cleared all log files");
            }
        }

        #endregion

        #region Public Methods

        public void SetLoggingEnabled(bool enabled)
        {
            // If it was enabled and we're disabling it
            if (_loggingEnabled && !enabled)
            {
                StopLogging(); // Make sure to stop any ongoing logging
            }

            _loggingEnabled = enabled;

            if (enabled)
            {
                string message = $"Component logging enabled at {DateTime.Now}\n";
                WriteToLog(LogCategory.System, message);
                MelonLogger.Msg($"Component logging enabled. Log files in: {_logsBaseDir}");
                StartLogging();
            }
            else
            {
                string message = $"Component logging disabled at {DateTime.Now}\n";
                WriteToLog(LogCategory.System, message);
                MelonLogger.Msg("Component logging disabled");
            }
        }

        public string GetLogsBaseDir()
        {
            return _logsBaseDir;
        }

        public void StartLogging()
        {
            // Only start logging if it's enabled
            if (_loggingEnabled)
            {
                StopLogging(); // Stop any existing logging coroutine
                _loggingCoroutine = MelonCoroutines.Start(LoggingRoutine());

                string message = $"Started component monitoring at {DateTime.Now}\n";
                WriteToLog(LogCategory.System, message);
                MelonLogger.Msg("Started game component monitoring");
            }
        }

        public void StopLogging()
        {
            if (_loggingCoroutine != null)
            {
                MelonCoroutines.Stop(_loggingCoroutine);
                _loggingCoroutine = null;

                string message = $"Stopped component monitoring at {DateTime.Now}\n";
                WriteToLog(LogCategory.System, message);
            }
        }

        public void ToggleLogging()
        {
            _loggingEnabled = !_loggingEnabled;
            MelonLogger.Msg($"Component logging is now {(_loggingEnabled ? "enabled" : "disabled")}");
            MelonLogger.Msg($"Log files located in: {_logsBaseDir}");

            if (_loggingEnabled)
            {
                // Find player and log all components if we haven't already
                GameObject playerObj = GameObject.Find("PlayerAvatar(Clone)");
                if (playerObj != null)
                {
                    // Log the entire player structure immediately on toggle
                    LogPlayerComponents(playerObj.transform);

                    // Attempt to find and log enemies
                    LogEnemies();

                    // Attempt to find and log items
                    LogItems();

                    // Attempt to find and log world objects
                    LogWorldObjects();

                    // Log game structure
                    LogGameStructure();

                    // Log all scene objects that might have been missed
                    LogAllSceneObjects();
                }
                else
                {
                    string errorMsg = $"=== REPO_UTILS Component Logging Error at {DateTime.Now} ===\n\nCould not find PlayerAvatar(Clone)\n";
                    WriteToLog(LogCategory.System, errorMsg);
                    MelonLogger.Error("Could not find PlayerAvatar(Clone) to log components");

                    StartLogging(); // Still start monitoring in case player appears later
                }
            }
            else
            {
                StopLogging();
            }
        }

        public bool IsLoggingEnabled()
        {
            return _loggingEnabled;
        }

        /// <summary>
        /// Logs a GameObject and its components to the specified category log with full detail
        /// </summary>
        public void LogGameObjectToCategory(GameObject gameObject, LogCategory category, string customHeader = null)
        {
            if (gameObject == null)
            {
                MelonLogger.Error($"Cannot log to {category} log: GameObject is null");
                return;
            }

            // Skip if this GameObject has already been logged to this category
            if (_categoryLoggedObjects[category].Contains(gameObject.GetInstanceID()))
            {
                return;
            }

            StringBuilder sb = new StringBuilder();

            // Add header if provided
            if (!string.IsNullOrEmpty(customHeader))
            {
                sb.AppendLine(customHeader);
            }
            else
            {
                sb.AppendLine($"=== {category} GameObject: {gameObject.name} at {DateTime.Now} ===\n");
            }

            // Log the complete hierarchy of this GameObject
            sb.AppendLine($"=== COMPLETE HIERARCHY FOR {gameObject.name} ===");
            LogGameObjectHierarchy(gameObject, sb, 0);

            // Log all components in detail for this GameObject and its children
            sb.AppendLine($"\n=== DETAILED COMPONENT ANALYSIS FOR {gameObject.name} ===");
            LogGameObjectAndChildrenInDetail(gameObject, sb, 0);

            // Mark this GameObject as logged for this category
            _categoryLoggedObjects[category].Add(gameObject.GetInstanceID());

            // Also mark it in the global tracking
            _loggedGameObjects.Add(gameObject.GetInstanceID());

            // Write to the appropriate log file
            WriteToLog(category, sb.ToString(), true);
            MelonLogger.Msg($"Logged {gameObject.name} to {category} log with full detail");
        }

        /// <summary>
        /// Logs a GameObject hierarchy and all component details for all children recursively
        /// </summary>
        private void LogGameObjectAndChildrenInDetail(GameObject obj, StringBuilder sb, int depth)
        {
            if (obj == null) return;

            string indent = new string(' ', depth * 2);
            sb.AppendLine($"{indent}GameObject: {obj.name} (Active: {obj.activeInHierarchy})");

            // Log transform information
            Transform transform = obj.transform;
            sb.AppendLine($"{indent}  Transform:");
            sb.AppendLine($"{indent}    Position: {transform.position}");
            sb.AppendLine($"{indent}    Rotation: {transform.rotation.eulerAngles}");
            sb.AppendLine($"{indent}    Local Scale: {transform.localScale}");

            // Log all components in detail
            LogAllComponentsInDetail(obj, sb, depth + 1);

            // Process all children
            foreach (Transform child in obj.transform)
            {
                LogGameObjectAndChildrenInDetail(child.gameObject, sb, depth + 1);
            }
        }

        public void LogPlayerComponents(Transform playerTransform)
        {
            if (playerTransform == null)
            {
                MelonLogger.Error("Cannot log player components: playerTransform is null");
                return;
            }

            MelonLogger.Msg($"Logging player components to: {GetLogFilePath(LogCategory.Player)}");

            // Create or clear the player log file with a header
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"=== REPO_UTILS Player Component Logging Started at {DateTime.Now} ===\n");

            // Log the entire GameObject hierarchy
            sb.AppendLine("=== COMPLETE PLAYER HIERARCHY ===");
            LogGameObjectHierarchy(playerTransform.gameObject, sb, 0);

            MelonLogger.Msg("Logged entire player hierarchy");

            // Find the player controller
            Transform playerController = playerTransform.Find("Player Avatar Controller");

            if (!_loggingEnabled || playerController == null) return;

            // Log all components for the player and all its children in detail
            sb.AppendLine("\n=== DETAILED PLAYER COMPONENT ANALYSIS ===");
            LogGameObjectAndChildrenInDetail(playerTransform.gameObject, sb, 0);

            // Mark all player GameObjects as logged for the Player category
            MarkGameObjectAndChildrenAsLogged(playerTransform.gameObject, LogCategory.Player);

            // Write to player log file
            WriteToLog(LogCategory.Player, sb.ToString(), false);
            MelonLogger.Msg($"Initial player component analysis written to log file");

            // Start the logging routine to monitor changes
            _loggingEnabled = true;
            StartLogging();
        }

        /// <summary>
        /// Recursively marks a GameObject and all its children as logged for a specific category
        /// </summary>
        private void MarkGameObjectAndChildrenAsLogged(GameObject obj, LogCategory category)
        {
            if (obj == null) return;

            int instanceId = obj.GetInstanceID();
            _categoryLoggedObjects[category].Add(instanceId);
            _loggedGameObjects.Add(instanceId);

            // Mark all children as well
            foreach (Transform child in obj.transform)
            {
                MarkGameObjectAndChildrenAsLogged(child.gameObject, category);
            }
        }

        /// <summary>
        /// Logs the overall game structure, including scene hierarchy, managers, and systems
        /// </summary>
        public void LogGameStructure()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"=== REPO_UTILS Game Structure Logging at {DateTime.Now} ===\n");

            Scene activeScene = SceneManager.GetActiveScene();
            sb.AppendLine($"Active Scene: {activeScene.name}");
            sb.AppendLine($"Build Index: {activeScene.buildIndex}");
            sb.AppendLine($"Path: {activeScene.path}");
            sb.AppendLine($"Root Object Count: {activeScene.rootCount}");

            // Log application info
            sb.AppendLine("\n=== APPLICATION INFO ===");
            sb.AppendLine($"Product Name: {Application.productName}");
            sb.AppendLine($"Version: {Application.version}");
            sb.AppendLine($"Unity Version: {Application.unityVersion}");
            sb.AppendLine($"Platform: {Application.platform}");
            sb.AppendLine($"System Language: {Application.systemLanguage}");
            sb.AppendLine($"Is Editor: {Application.isEditor}");
            sb.AppendLine($"Is Playing: {Application.isPlaying}");
            sb.AppendLine($"Target Framerate: {Application.targetFrameRate}");

            // Log render settings
            sb.AppendLine("\n=== RENDER SETTINGS ===");
            sb.AppendLine($"Fog Enabled: {RenderSettings.fog}");
            sb.AppendLine($"Ambient Light: {RenderSettings.ambientLight}");
            sb.AppendLine($"Skybox Material: {(RenderSettings.skybox ? RenderSettings.skybox.name : "None")}");

            // Log camera info
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                sb.AppendLine("\n=== MAIN CAMERA ===");
                LogGameObjectToCategory(mainCamera.gameObject, LogCategory.Structure,
                    "=== MAIN CAMERA DETAILED COMPONENT ANALYSIS ===");
            }

            // Log physics settings
            sb.AppendLine("\n=== PHYSICS SETTINGS ===");
            sb.AppendLine($"Gravity: {Physics.gravity}");
            sb.AppendLine($"Default Contact Offset: {Physics.defaultContactOffset}");
            sb.AppendLine($"Sleep Threshold: {Physics.sleepThreshold}");
            sb.AppendLine($"Bounce Threshold: {Physics.bounceThreshold}");
            sb.AppendLine($"Default Solver Iterations: {Physics.defaultSolverIterations}");

            // Log key game objects
            sb.AppendLine("\n=== KEY GAME OBJECTS ===");
            GameObject playerObj = GameObject.Find("PlayerAvatar(Clone)");
            sb.AppendLine($"Player Avatar: {(playerObj != null ? "Found" : "Not Found")}");

            GameObject levelGenerator = GameObject.Find("Level Generator");
            sb.AppendLine($"Level Generator: {(levelGenerator != null ? "Found" : "Not Found")}");

            if (levelGenerator != null)
            {
                Transform levelTransform = levelGenerator.transform.Find("Level");
                sb.AppendLine($"  Level: {(levelTransform != null ? "Found" : "Not Found")}");

                Transform enemiesParent = levelGenerator.transform.Find("Enemies");
                sb.AppendLine($"  Enemies Parent: {(enemiesParent != null ? "Found" : "Not Found")}");

                if (enemiesParent != null)
                {
                    sb.AppendLine($"    Enemy Count: {enemiesParent.childCount}");
                }

                // Log the Level Generator in detail (only if it hasn't been logged already)
                if (!_categoryLoggedObjects[LogCategory.Structure].Contains(levelGenerator.GetInstanceID()))
                {
                    LogGameObjectToCategory(levelGenerator, LogCategory.Structure,
                        "=== LEVEL GENERATOR DETAILED COMPONENT ANALYSIS ===");
                }
            }

            // Count objects by tag
            sb.AppendLine("\n=== OBJECTS BY TAG ===");
            string[] commonTags = new string[]
            {
                "Player", "Enemy", "Item", "Weapon", "Trigger", "MainCamera",
                "Ground", "Door", "Wall", "Untagged"
            };

            foreach (string tag in commonTags)
            {
                try
                {
                    GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(tag);
                    sb.AppendLine($"Tag '{tag}': {taggedObjects.Length} objects");

                    // Log a sample of each tag (first one found)
                    if (taggedObjects.Length > 0 && tag != "Untagged")
                    {
                        sb.AppendLine($"  Sample object with tag '{tag}': {taggedObjects[0].name}");

                        // Log the first object of each tag in detail
                        if (!_categoryLoggedObjects[LogCategory.Structure].Contains(taggedObjects[0].GetInstanceID()))
                        {
                            LogGameObjectToCategory(taggedObjects[0], LogCategory.Structure,
                                $"=== SAMPLE '{tag}' OBJECT DETAILED COMPONENT ANALYSIS ===");
                        }
                    }
                }
                catch
                {
                    // Tag might not exist in this game
                    sb.AppendLine($"Tag '{tag}': Not used in this game");
                }
            }

            // Try to find important managers
            sb.AppendLine("\n=== IMPORTANT MANAGERS ===");

            try
            {
                // Find any objects with "Manager" in their name
                GameObject[] managerObjects = GameObject.FindObjectsOfType<GameObject>()
                    .Where(go => go.name.Contains("Manager"))
                    .ToArray();

                sb.AppendLine($"Found {managerObjects.Length} objects with 'Manager' in their name:");
                foreach (GameObject manager in managerObjects)
                {
                    sb.AppendLine($"  - {manager.name}");

                    // Log each manager in detail
                    if (!_categoryLoggedObjects[LogCategory.Structure].Contains(manager.GetInstanceID()))
                    {
                        LogGameObjectToCategory(manager, LogCategory.Structure,
                            $"=== '{manager.name}' MANAGER DETAILED COMPONENT ANALYSIS ===");
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error while looking for managers: {ex.Message}");
            }

            // Write to the structure log file
            WriteToLog(LogCategory.Structure, sb.ToString(), false);
            MelonLogger.Msg($"Game structure analysis written to structure log file");
        }

        /// <summary>
        /// Log enemies and their components to the enemies log file
        /// </summary>
        public void LogEnemies()
        {
            if (_core.EnemyManager == null || _core.EnemyManager.GetEnemyCount() == 0)
            {
                MelonLogger.Msg("No enemies found to log");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"=== REPO_UTILS Enemy Component Logging Started at {DateTime.Now} ===\n");

            // Get enemies from the EnemyManager
            List<Transform> enemies = _core.EnemyManager.GetSortedEnemies();
            List<string> enemyNames = _core.EnemyManager.GetEnemyNames();

            sb.AppendLine($"Found {enemies.Count} enemies:");

            // Log summary first
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] == null) continue;

                string name = (i < enemyNames.Count) ? enemyNames[i] : $"Enemy {i}";
                float distance = (i < _core.EnemyManager.GetEnemyDistances().Count)
                    ? _core.EnemyManager.GetEnemyDistances()[i]
                    : 0f;

                sb.AppendLine($"{i + 1}. {name} - Distance: {distance:F2}m");
            }

            // Write the summary to the enemy log file
            WriteToLog(LogCategory.Enemies, sb.ToString(), false);

            // Log each enemy with full detail
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] == null) continue;

                string name = (i < enemyNames.Count) ? enemyNames[i] : $"Enemy {i}";

                // Skip if this enemy has already been logged
                if (_categoryLoggedObjects[LogCategory.Enemies].Contains(enemies[i].gameObject.GetInstanceID()))
                {
                    continue;
                }

                // Log this enemy with full detail
                LogGameObjectToCategory(enemies[i].gameObject, LogCategory.Enemies,
                    $"=== ENEMY: {name} DETAILED COMPONENT ANALYSIS ===");
            }

            MelonLogger.Msg($"Enemy component analysis written to log file");
        }

        /// <summary>
        /// Log items and their components to the items log file
        /// </summary>
        public void LogItems()
        {
            if (_core.ItemManager == null || _core.ItemManager.GetItemCount() == 0)
            {
                MelonLogger.Msg("No items found to log");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"=== REPO_UTILS Item Component Logging Started at {DateTime.Now} ===\n");

            // Use the available item data from ItemManager
            List<string> itemNames = _core.ItemManager.GetItemNames();

            sb.AppendLine($"Found {itemNames.Count} items:");

            // Log summary of items
            for (int i = 0; i < itemNames.Count; i++)
            {
                sb.AppendLine($"Item {i + 1}: {itemNames[i]} - Distance: {_core.ItemManager.GetItemDistances()[i]:F2}m");
            }

            // Calculate and log total item value
            float totalValue = _core.ItemManager.CalculateTotalItemValue();
            sb.AppendLine($"\nTotal Item Value: ${totalValue:N0}");

            // Write summary to the items log file
            WriteToLog(LogCategory.Items, sb.ToString(), false);

            // Find all items in the scene - using reflection to access private _items field of ItemManager
            Type itemManagerType = _core.ItemManager.GetType();
            FieldInfo itemsField = itemManagerType.GetField("_items",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (itemsField != null)
            {
                List<Transform> items = (List<Transform>)itemsField.GetValue(_core.ItemManager);

                // Log each item with full detail
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] == null) continue;

                    string name = (i < itemNames.Count) ? itemNames[i] : $"Item {i}";

                    // Skip if this item has already been logged
                    if (_categoryLoggedObjects[LogCategory.Items].Contains(items[i].gameObject.GetInstanceID()))
                    {
                        continue;
                    }

                    // Log this item with full detail
                    LogGameObjectToCategory(items[i].gameObject, LogCategory.Items,
                        $"=== ITEM: {name} DETAILED COMPONENT ANALYSIS ===");
                }
            }

            MelonLogger.Msg($"Item component analysis written to log file");
        }

        /// <summary>
        /// Log world objects (walls, doors, etc.) to the world objects log file
        /// </summary>
        public void LogWorldObjects()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"=== REPO_UTILS World Objects Logging Started at {DateTime.Now} ===\n");

            // Find the Level Generator
            GameObject levelGenerator = GameObject.Find("Level Generator");
            if (levelGenerator == null)
            {
                sb.AppendLine("Could not find Level Generator");
                WriteToLog(LogCategory.WorldObjects, sb.ToString(), false);
                MelonLogger.Msg("No world objects found to log");
                return;
            }

            // Find the Level transform
            Transform levelTransform = levelGenerator.transform.Find("Level");
            if (levelTransform == null)
            {
                sb.AppendLine("Could not find Level transform in Level Generator");
                WriteToLog(LogCategory.WorldObjects, sb.ToString(), false);
                return;
            }

            // First, log high-level stats
            int childCount = levelTransform.childCount;
            sb.AppendLine($"Level contains {childCount} root objects\n");

            // Log walls, doors, and other world objects
            int wallCount = 0;
            int doorCount = 0;
            int miscCount = 0;

            Dictionary<string, int> objectTypes = new Dictionary<string, int>();
            Dictionary<string, List<GameObject>> categorizedObjects = new Dictionary<string, List<GameObject>>();

            // Find all children of the level
            foreach (Transform child in levelTransform)
            {
                string name = child.name.ToLower();
                GameObject childObj = child.gameObject;

                // Categorize by name
                if (name.Contains("wall"))
                {
                    wallCount++;
                    if (!categorizedObjects.ContainsKey("walls"))
                        categorizedObjects["walls"] = new List<GameObject>();
                    categorizedObjects["walls"].Add(childObj);
                }
                else if (name.Contains("door"))
                {
                    doorCount++;
                    if (!categorizedObjects.ContainsKey("doors"))
                        categorizedObjects["doors"] = new List<GameObject>();
                    categorizedObjects["doors"].Add(childObj);
                }
                else
                {
                    miscCount++;
                    if (!categorizedObjects.ContainsKey("misc"))
                        categorizedObjects["misc"] = new List<GameObject>();
                    categorizedObjects["misc"].Add(childObj);
                }

                // Count by type
                string typeName = child.name;
                if (typeName.Contains("("))
                {
                    // Remove instance suffix like (1), (2), etc.
                    typeName = typeName.Substring(0, typeName.IndexOf("(")).Trim();
                }

                if (objectTypes.ContainsKey(typeName))
                {
                    objectTypes[typeName]++;
                }
                else
                {
                    objectTypes[typeName] = 1;
                }
            }

            // Log counts
            sb.AppendLine($"World Object Counts:");
            sb.AppendLine($"- Walls: {wallCount}");
            sb.AppendLine($"- Doors: {doorCount}");
            sb.AppendLine($"- Misc Objects: {miscCount}");

            // Log type breakdown
            sb.AppendLine("\nObject Types Breakdown:");
            foreach (var kvp in objectTypes)
            {
                sb.AppendLine($"- {kvp.Key}: {kvp.Value}");
            }

            // Write summary to world objects log
            WriteToLog(LogCategory.WorldObjects, sb.ToString(), false);

            // Log sample objects from each category with full detail
            foreach (var category in categorizedObjects.Keys)
            {
                var objects = categorizedObjects[category];
                if (objects.Count > 0)
                {
                    // Log the first object of each category
                    GameObject sampleObj = objects[0];

                    // Skip if already logged
                    if (_categoryLoggedObjects[LogCategory.WorldObjects].Contains(sampleObj.GetInstanceID()))
                    {
                        continue;
                    }

                    // Log with full detail
                    LogGameObjectToCategory(sampleObj, LogCategory.WorldObjects,
                        $"=== SAMPLE {category.ToUpper()} OBJECT: {sampleObj.name} DETAILED COMPONENT ANALYSIS ===");

                    // For larger categories, also log some additional samples
                    if (objects.Count > 10)
                    {
                        // Log middle object
                        GameObject middleObj = objects[objects.Count / 2];
                        if (!_categoryLoggedObjects[LogCategory.WorldObjects].Contains(middleObj.GetInstanceID()))
                        {
                            LogGameObjectToCategory(middleObj, LogCategory.WorldObjects,
                                $"=== ADDITIONAL {category.ToUpper()} SAMPLE: {middleObj.name} DETAILED COMPONENT ANALYSIS ===");
                        }

                        // Log last object
                        GameObject lastObj = objects[objects.Count - 1];
                        if (!_categoryLoggedObjects[LogCategory.WorldObjects].Contains(lastObj.GetInstanceID()))
                        {
                            LogGameObjectToCategory(lastObj, LogCategory.WorldObjects,
                                $"=== ADDITIONAL {category.ToUpper()} SAMPLE: {lastObj.name} DETAILED COMPONENT ANALYSIS ===");
                        }
                    }
                }
            }

            MelonLogger.Msg($"World objects analysis written to log file");
        }

        /// <summary>
        /// Performs a comprehensive scene search to find and log objects that don't fit into standard categories
        /// </summary>
        public void LogAllSceneObjects()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"=== REPO_UTILS Full Scene Objects Logging Started at {DateTime.Now} ===\n");

            // Find all root objects in the scene
            List<GameObject> rootObjects = new List<GameObject>();
            Scene activeScene = SceneManager.GetActiveScene();

            sb.AppendLine($"Active Scene: {activeScene.name}");
            sb.AppendLine($"Build Index: {activeScene.buildIndex}");
            sb.AppendLine($"Path: {activeScene.path}");
            sb.AppendLine($"Is Loaded: {activeScene.isLoaded}");

            // Get all root objects
            activeScene.GetRootGameObjects(rootObjects);
            sb.AppendLine($"Root GameObjects in scene: {rootObjects.Count}\n");

            // Keep track of objects we've already logged elsewhere
            HashSet<int> alreadyLoggedObjects = new HashSet<int>(_loggedGameObjects);

            // Track statistics 
            int totalObjectsLogged = 0;
            int miscObjectsFound = 0;
            Dictionary<string, int> objectTypeCount = new Dictionary<string, int>();

            // Log summary of root objects
            sb.AppendLine("Root Object Summary:");
            foreach (GameObject rootObj in rootObjects)
            {
                // Count objects in this hierarchy
                int objectsInHierarchy = 0;
                CountObjectsInHierarchy(rootObj.transform, ref objectsInHierarchy);

                sb.AppendLine($"- {rootObj.name}: {objectsInHierarchy} objects in hierarchy");

                // Collect unique types with counts
                string rootTypeName = rootObj.name;
                if (objectTypeCount.ContainsKey(rootTypeName))
                    objectTypeCount[rootTypeName]++;
                else
                    objectTypeCount[rootTypeName] = 1;

                // Check if already logged elsewhere
                bool alreadyLogged = alreadyLoggedObjects.Contains(rootObj.GetInstanceID());

                if (!alreadyLogged)
                {
                    miscObjectsFound++;
                    totalObjectsLogged += objectsInHierarchy;
                }
            }

            // Summary section
            sb.AppendLine("\n=== SCENE OBJECTS SUMMARY ===");
            sb.AppendLine($"Total objects in scene: {CountSceneObjects()}");
            sb.AppendLine($"Objects logged in detail: {totalObjectsLogged}");
            sb.AppendLine($"Miscellaneous root objects found: {miscObjectsFound}");

            sb.AppendLine("\nObject Types Breakdown:");
            foreach (var kvp in objectTypeCount.OrderByDescending(kvp => kvp.Value))
            {
                sb.AppendLine($"- {kvp.Key}: {kvp.Value}");
            }

            // Write the summary to the Miscellaneous log file
            WriteToLog(LogCategory.Miscellaneous, sb.ToString(), false);

            // Log each unlogged root object with full detail
            foreach (GameObject rootObj in rootObjects)
            {
                // Skip logging objects we know are already covered by other categories
                if (rootObj.name == "PlayerAvatar(Clone)" ||
                    rootObj.name == "Level Generator" ||
                    rootObj.name.Contains("Enemy"))
                {
                    continue;
                }

                // Skip if already logged
                if (_loggedGameObjects.Contains(rootObj.GetInstanceID()))
                {
                    continue;
                }

                // Log with full detail
                LogGameObjectToCategory(rootObj, LogCategory.Miscellaneous,
                    $"=== MISCELLANEOUS ROOT OBJECT: {rootObj.name} DETAILED COMPONENT ANALYSIS ===");
            }

            MelonLogger.Msg($"Full scene object analysis written to miscellaneous log file");
        }

        #endregion

        #region Private Methods

        private IEnumerator LoggingRoutine()
        {
            MelonLogger.Msg($"Starting component monitoring - checking every {_logInterval} seconds");
            WriteToLog(LogCategory.System, $"Starting monitoring cycle - interval: {_logInterval} seconds\n");

            // Main logging loop
            while (_loggingEnabled)
            {
                try
                {
                    // Log player data
                    MonitorPlayerComponents();

                    // Log enemy data
                    MonitorEnemyComponents();

                    // Log item data
                    MonitorItemComponents();

                    // Log world object data (less frequently)
                    if (Time.frameCount % 5 == 0) // Every 5th cycle
                    {
                        MonitorWorldObjects();
                    }

                    // Look for new or unclassified objects (very infrequently)
                    if (Time.frameCount % 30 == 0) // Every 30th cycle
                    {
                        MonitorMiscellaneousObjects();
                    }

                    MelonLogger.Msg($"Logged component states at {DateTime.Now}");
                }
                catch (Exception ex)
                {
                    string errorMsg = $"Error during logging cycle: {ex.Message}\n{ex.StackTrace}\n";
                    WriteToLog(LogCategory.System, errorMsg);
                    MelonLogger.Error($"Error during logging cycle: {ex.Message}");
                }

                // Wait for the next logging cycle
                yield return new WaitForSeconds(_logInterval);
            }
        }

        /// <summary>
        /// Monitors for new scene objects that don't fit in other categories
        /// </summary>
        private void MonitorMiscellaneousObjects()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            List<GameObject> rootObjects = new List<GameObject>();
            activeScene.GetRootGameObjects(rootObjects);

            StringBuilder sb = new StringBuilder();
            string sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            sb.AppendLine($"=== Miscellaneous Objects Logging Session: {sessionId} ===\n");

            bool foundNewObjects = false;

            foreach (GameObject rootObj in rootObjects)
            {
                // Skip known categorized objects
                if (rootObj.name == "PlayerAvatar(Clone)" ||
                    rootObj.name == "Level Generator" ||
                    rootObj.name.Contains("Enemy"))
                {
                    continue;
                }

                // Check if this object has been logged before
                if (!_loggedGameObjects.Contains(rootObj.GetInstanceID()))
                {
                    foundNewObjects = true;
                    sb.AppendLine($"New root object found: {rootObj.name}");

                    // Log this new object with full detail
                    LogGameObjectToCategory(rootObj, LogCategory.Miscellaneous,
                        $"=== NEW MISCELLANEOUS OBJECT: {rootObj.name} DETAILED COMPONENT ANALYSIS ===");
                }
            }

            if (foundNewObjects)
            {
                sb.AppendLine($"Total scene objects: {CountSceneObjects()}");
                WriteToLog(LogCategory.Miscellaneous, sb.ToString(), true);
            }
        }

        private void MonitorPlayerComponents()
        {
            GameObject playerObj = GameObject.Find("PlayerAvatar(Clone)");
            if (playerObj != null)
            {
                // Initialize a StringBuilder for this logging cycle
                StringBuilder sb = new StringBuilder();

                // Create a session ID to group logs from this cycle
                string sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                sb.AppendLine($"=== Player Logging Session: {sessionId} ===\n");

                // Check for changes in all components throughout the player hierarchy
                LogAllComponentChanges(playerObj, sb);

                // Only write to the log if there were actual changes
                if (sb.Length > 100) // More than just the header
                {
                    WriteToLog(LogCategory.Player, sb.ToString(), true);
                }
            }
        }

        private void MonitorEnemyComponents()
        {
            if (_core.EnemyManager == null || _core.EnemyManager.GetEnemyCount() == 0)
            {
                return;
            }

            StringBuilder sb = new StringBuilder();
            string sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            sb.AppendLine($"=== Enemy Logging Session: {sessionId} ===\n");

            // Get current enemy data
            List<string> enemyNames = _core.EnemyManager.GetEnemyNames();
            List<float> enemyDistances = _core.EnemyManager.GetEnemyDistances();

            sb.AppendLine($"Current Enemy Count: {enemyNames.Count}");

            for (int i = 0; i < enemyNames.Count; i++)
            {
                sb.AppendLine($"- {enemyNames[i]}: {enemyDistances[i]:F2}m");
            }

            // Only write if there are enemies to log
            if (enemyNames.Count > 0)
            {
                WriteToLog(LogCategory.Enemies, sb.ToString(), true);
            }

            // Check for any state changes in enemy components
            List<Transform> enemies = _core.EnemyManager.GetSortedEnemies();
            bool foundChanges = false;

            foreach (Transform enemy in enemies)
            {
                if (enemy == null) continue;

                StringBuilder changeSb = new StringBuilder();
                if (CheckGameObjectChanges(enemy.gameObject, changeSb))
                {
                    foundChanges = true;
                    WriteToLog(LogCategory.Enemies,
                        $"\n=== STATE CHANGES DETECTED FOR {enemy.name} ===\n{changeSb.ToString()}\n",
                        true);
                }
            }

            if (foundChanges)
            {
                MelonLogger.Msg("Detected and logged enemy component state changes");
            }
        }

        private void MonitorItemComponents()
        {
            if (_core.ItemManager == null || _core.ItemManager.GetItemCount() == 0)
            {
                return;
            }

            StringBuilder sb = new StringBuilder();
            string sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            sb.AppendLine($"=== Item Logging Session: {sessionId} ===\n");

            // Get current item data
            List<string> itemNames = _core.ItemManager.GetItemNames();
            List<float> itemDistances = _core.ItemManager.GetItemDistances();
            float totalValue = _core.ItemManager.CalculateTotalItemValue();

            sb.AppendLine($"Current Item Count: {itemNames.Count}");
            sb.AppendLine($"Total Item Value: ${totalValue:N0}");

            for (int i = 0; i < itemNames.Count; i++)
            {
                sb.AppendLine($"- {itemNames[i]}: {itemDistances[i]:F2}m");
            }

            // Only write if there are items to log
            if (itemNames.Count > 0)
            {
                WriteToLog(LogCategory.Items, sb.ToString(), true);
            }

            // Check for any state changes in item components
            Type itemManagerType = _core.ItemManager.GetType();
            FieldInfo itemsField = itemManagerType.GetField("_items",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (itemsField != null)
            {
                List<Transform> items = (List<Transform>)itemsField.GetValue(_core.ItemManager);
                bool foundChanges = false;

                foreach (Transform item in items)
                {
                    if (item == null) continue;

                    StringBuilder changeSb = new StringBuilder();
                    if (CheckGameObjectChanges(item.gameObject, changeSb))
                    {
                        foundChanges = true;
                        WriteToLog(LogCategory.Items,
                            $"\n=== STATE CHANGES DETECTED FOR {item.name} ===\n{changeSb.ToString()}\n",
                            true);
                    }
                }

                if (foundChanges)
                {
                    MelonLogger.Msg("Detected and logged item component state changes");
                }
            }
        }

        private void MonitorWorldObjects()
        {
            GameObject levelGenerator = GameObject.Find("Level Generator");
            if (levelGenerator == null) return;

            Transform levelTransform = levelGenerator.transform.Find("Level");
            if (levelTransform == null) return;

            StringBuilder sb = new StringBuilder();
            string sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            sb.AppendLine($"=== World Objects Logging Session: {sessionId} ===\n");

            // Count active/inactive objects
            int totalObjects = 0;
            int activeObjects = 0;
            foreach (Transform child in levelTransform)
            {
                totalObjects++;
                if (child.gameObject.activeInHierarchy)
                {
                    activeObjects++;
                }
            }

            sb.AppendLine($"World Objects Status:");
            sb.AppendLine($"- Total Objects: {totalObjects}");
            sb.AppendLine($"- Active Objects: {activeObjects}");
            sb.AppendLine($"- Inactive Objects: {totalObjects - activeObjects}");

            // Write world object updates
            WriteToLog(LogCategory.WorldObjects, sb.ToString(), true);

            // Check for any significant state changes in world objects
            bool foundChanges = false;

            // Check a few sample world objects for changes
            foreach (Transform child in levelTransform)
            {
                if (child == null) continue;

                // Only check objects that are in the "doors" category or have active state changes
                string name = child.name.ToLower();
                if (name.Contains("door") || name.Contains("trap") || name.Contains("trigger"))
                {
                    StringBuilder changeSb = new StringBuilder();
                    if (CheckGameObjectChanges(child.gameObject, changeSb))
                    {
                        foundChanges = true;
                        WriteToLog(LogCategory.WorldObjects,
                            $"\n=== STATE CHANGES DETECTED FOR {child.name} ===\n{changeSb.ToString()}\n",
                            true);
                    }
                }
            }

            if (foundChanges)
            {
                MelonLogger.Msg("Detected and logged world object state changes");
            }
        }

        /// <summary>
        /// Recursively counts objects in a hierarchy without logging them
        /// </summary>
        private void CountObjectsInHierarchy(Transform parent, ref int count)
        {
            count++; // Count this object

            foreach (Transform child in parent)
            {
                CountObjectsInHierarchy(child, ref count);
            }
        }

        /// <summary>
        /// Counts all objects in the active scene
        /// </summary>
        private int CountSceneObjects()
        {
            int count = 0;
            GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            return allObjects.Length;
        }

        private void LogGameObjectHierarchy(GameObject obj, StringBuilder sb, int depth)
        {
            if (obj == null) return;

            string indent = new string(' ', depth * 2);
            sb.AppendLine($"{indent}- GameObject: {obj.name} (Active: {obj.activeInHierarchy})");

            // Track this GameObject as logged
            _loggedGameObjects.Add(obj.GetInstanceID());

            // Log all components
            Component[] components = obj.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component != null)
                {
                    sb.AppendLine($"{indent}  Component: {component.GetType().Name}");

                    // If this is a Renderer, include material info
                    if (component is Renderer renderer)
                    {
                        Material[] materials = renderer.materials;
                        if (materials != null && materials.Length > 0)
                        {
                            sb.AppendLine($"{indent}    Materials: {materials.Length}");
                            for (int i = 0; i < materials.Length; i++)
                            {
                                if (materials[i] != null)
                                    sb.AppendLine($"{indent}      {i}: {materials[i].name} (Shader: {materials[i].shader.name})");
                            }
                        }
                    }

                    // If this is a Collider, include collider info
                    if (component is Collider collider)
                    {
                        sb.AppendLine($"{indent}    Is Trigger: {collider.isTrigger}");
                        sb.AppendLine($"{indent}    Is Enabled: {collider.enabled}");
                    }
                }
            }

            // Log children recursively
            foreach (Transform child in obj.transform)
            {
                LogGameObjectHierarchy(child.gameObject, sb, depth + 1);
            }
        }

        private void LogAllComponentsInDetail(GameObject obj, StringBuilder sb, int depth = 1)
        {
            // Log components on this GameObject
            string indent = new string(' ', depth * 2);
            Component[] allComponents = obj.GetComponents<Component>();

            sb.AppendLine($"{indent}GameObject: {obj.name} has {allComponents.Length} components:");

            // Log basic Transform info
            Transform transform = obj.transform;
            sb.AppendLine($"{indent}  Transform:");
            sb.AppendLine($"{indent}    Position: {transform.position}");
            sb.AppendLine($"{indent}    Rotation: {transform.rotation.eulerAngles}");
            sb.AppendLine($"{indent}    Local Scale: {transform.localScale}");

            // Log parent info if available
            if (transform.parent != null)
            {
                sb.AppendLine($"{indent}    Parent: {transform.parent.name}");
            }

            // Log child count
            sb.AppendLine($"{indent}    Child Count: {transform.childCount}");

            // Now log all MonoBehaviour components in detail
            MonoBehaviour[] components = obj.GetComponents<MonoBehaviour>();
            foreach (var component in components)
            {
                LogComponentInDetail(component, sb, depth + 1);
            }

            // Also log non-MonoBehaviour components
            foreach (var component in allComponents)
            {
                if (component is not MonoBehaviour && !(component is Transform))
                {
                    LogNonMonoBehaviourComponent(component, sb, depth + 1);
                }
            }
        }

        /// <summary>
        /// Logs details for non-MonoBehaviour components like Renderers, Colliders, etc.
        /// </summary>
        private void LogNonMonoBehaviourComponent(Component component, StringBuilder sb, int depth)
        {
            if (component == null) return;

            string indent = new string(' ', depth * 2);
            Type type = component.GetType();

            sb.AppendLine($"\n{indent}• Component: {type.FullName}");

            // Check for specific component types and log their relevant properties

            // Renderer components
            if (component is Renderer renderer)
            {
                sb.AppendLine($"{indent}  Renderer Properties:");
                sb.AppendLine($"{indent}    Enabled: {renderer.enabled}");
                sb.AppendLine($"{indent}    Cast Shadows: {renderer.shadowCastingMode}");
                sb.AppendLine($"{indent}    Receive Shadows: {renderer.receiveShadows}");

                Material[] materials = renderer.materials;
                if (materials != null && materials.Length > 0)
                {
                    sb.AppendLine($"{indent}    Materials: {materials.Length}");
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] != null)
                        {
                            sb.AppendLine($"{indent}      Material {i}: {materials[i].name}");
                            sb.AppendLine($"{indent}        Shader: {materials[i].shader.name}");

                            // Try to log main texture
                            try
                            {
                                if (materials[i].mainTexture != null)
                                    sb.AppendLine($"{indent}        Main Texture: {materials[i].mainTexture.name}");
                            }
                            catch { }

                            // Try to log main color
                            try
                            {
                                if (materials[i].HasProperty("_Color"))
                                    sb.AppendLine($"{indent}        Main Color: {materials[i].color}");
                            }
                            catch { }
                        }
                    }
                }
            }

            // Collider components
            if (component is Collider collider)
            {
                sb.AppendLine($"{indent}  Collider Properties:");
                sb.AppendLine($"{indent}    Enabled: {collider.enabled}");
                sb.AppendLine($"{indent}    Is Trigger: {collider.isTrigger}");

                if (collider is BoxCollider boxCollider)
                {
                    sb.AppendLine($"{indent}    Type: Box Collider");
                    sb.AppendLine($"{indent}    Center: {boxCollider.center}");
                    sb.AppendLine($"{indent}    Size: {boxCollider.size}");
                }
                else if (collider is SphereCollider sphereCollider)
                {
                    sb.AppendLine($"{indent}    Type: Sphere Collider");
                    sb.AppendLine($"{indent}    Center: {sphereCollider.center}");
                    sb.AppendLine($"{indent}    Radius: {sphereCollider.radius}");
                }
                else if (collider is CapsuleCollider capsuleCollider)
                {
                    sb.AppendLine($"{indent}    Type: Capsule Collider");
                    sb.AppendLine($"{indent}    Center: {capsuleCollider.center}");
                    sb.AppendLine($"{indent}    Radius: {capsuleCollider.radius}");
                    sb.AppendLine($"{indent}    Height: {capsuleCollider.height}");
                    sb.AppendLine($"{indent}    Direction: {capsuleCollider.direction}");
                }
                else if (collider is MeshCollider meshCollider)
                {
                    sb.AppendLine($"{indent}    Type: Mesh Collider");
                    sb.AppendLine($"{indent}    Convex: {meshCollider.convex}");
                    sb.AppendLine($"{indent}    Mesh: {(meshCollider.sharedMesh != null ? meshCollider.sharedMesh.name : "None")}");
                }
            }

            // Rigidbody components
            if (component is Rigidbody rigidbody)
            {
                sb.AppendLine($"{indent}  Rigidbody Properties:");
                sb.AppendLine($"{indent}    Mass: {rigidbody.mass}");
                sb.AppendLine($"{indent}    Drag: {rigidbody.drag}");
                sb.AppendLine($"{indent}    Angular Drag: {rigidbody.angularDrag}");
                sb.AppendLine($"{indent}    Use Gravity: {rigidbody.useGravity}");
                sb.AppendLine($"{indent}    Is Kinematic: {rigidbody.isKinematic}");
                sb.AppendLine($"{indent}    Interpolation: {rigidbody.interpolation}");
                sb.AppendLine($"{indent}    Collision Detection: {rigidbody.collisionDetectionMode}");
                sb.AppendLine($"{indent}    Constraints: {rigidbody.constraints}");
            }

            // AudioSource components
            if (component is AudioSource audioSource)
            {
                sb.AppendLine($"{indent}  AudioSource Properties:");
                sb.AppendLine($"{indent}    Enabled: {audioSource.enabled}");
                sb.AppendLine($"{indent}    Clip: {(audioSource.clip != null ? audioSource.clip.name : "None")}");
                sb.AppendLine($"{indent}    Volume: {audioSource.volume}");
                sb.AppendLine($"{indent}    Pitch: {audioSource.pitch}");
                sb.AppendLine($"{indent}    Play On Awake: {audioSource.playOnAwake}");
                sb.AppendLine($"{indent}    Loop: {audioSource.loop}");
            }

            // Light components
            if (component is Light light)
            {
                sb.AppendLine($"{indent}  Light Properties:");
                sb.AppendLine($"{indent}    Enabled: {light.enabled}");
                sb.AppendLine($"{indent}    Type: {light.type}");
                sb.AppendLine($"{indent}    Color: {light.color}");
                sb.AppendLine($"{indent}    Intensity: {light.intensity}");
                sb.AppendLine($"{indent}    Range: {light.range}");
                sb.AppendLine($"{indent}    Spot Angle: {light.spotAngle}");
                sb.AppendLine($"{indent}    Shadows: {light.shadows}");
            }

            // For any other component type, try to log using reflection
            if (!(component is Renderer) && !(component is Collider) &&
                !(component is Rigidbody) && !(component is AudioSource) && !(component is Light))
            {
                // Log fields
                FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                if (fields.Length > 0)
                {
                    sb.AppendLine($"{indent}  Public Fields:");
                    foreach (var field in fields)
                    {
                        try
                        {
                            object value = field.GetValue(component);
                            string valueStr = FormatValue(value);
                            sb.AppendLine($"{indent}    {field.Name} ({field.FieldType.Name}): {valueStr}");
                        }
                        catch
                        {
                            sb.AppendLine($"{indent}    {field.Name} ({field.FieldType.Name}): [Error reading value]");
                        }
                    }
                }

                // Log properties
                PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                if (properties.Length > 0)
                {
                    sb.AppendLine($"{indent}  Public Properties:");
                    foreach (var property in properties)
                    {
                        if (property.CanRead)
                        {
                            try
                            {
                                object value = property.GetValue(component, null);
                                string valueStr = FormatValue(value);
                                sb.AppendLine($"{indent}    {property.Name} ({property.PropertyType.Name}): {valueStr}");
                            }
                            catch
                            {
                                sb.AppendLine($"{indent}    {property.Name} ({property.PropertyType.Name}): [Error reading value]");
                            }
                        }
                    }
                }
            }
        }

        private void LogComponentInDetail(MonoBehaviour component, StringBuilder sb, int indent)
        {
            if (component == null) return;

            string indentation = new string(' ', indent * 2);
            Type type = component.GetType();

            // Log component type
            sb.AppendLine($"\n{indentation}• Component: {type.FullName}");

            // Log inheritance hierarchy
            sb.AppendLine($"{indentation}  Inheritance Hierarchy:");
            Type baseType = type.BaseType;
            string hierarchyIndent = indentation + "    ";
            while (baseType != null && baseType != typeof(object))
            {
                sb.AppendLine($"{hierarchyIndent}{baseType.FullName}");
                baseType = baseType.BaseType;
                hierarchyIndent += "  ";
            }

            // Check if this component might be related to movement, stamina, or jump
            bool isSpeedRelated = IsSpeedRelated(type.Name);
            bool isStaminaRelated = IsStaminaRelated(type.Name);
            bool isJumpRelated = IsJumpRelated(type.Name);

            if (isSpeedRelated)
                sb.AppendLine($"{indentation}  [NOTED] This appears to be a movement/speed-related component!");
            if (isStaminaRelated)
                sb.AppendLine($"{indentation}  [NOTED] This appears to be a stamina/energy-related component!");
            if (isJumpRelated)
                sb.AppendLine($"{indentation}  [NOTED] This appears to be a jump-related component!");

            // Remember that we've seen this component
            _loggedComponents.Add(component.GetInstanceID());

            // Create component key for tracking
            string componentKey = component.GetType().Name;

            // Initialize dictionaries for this component if needed
            if (!_loggedMethods.ContainsKey(componentKey))
                _loggedMethods[componentKey] = new HashSet<string>();
            if (!_loggedFields.ContainsKey(componentKey))
                _loggedFields[componentKey] = new HashSet<string>();
            if (!_loggedProperties.ContainsKey(componentKey))
                _loggedProperties[componentKey] = new HashSet<string>();
            if (!_fieldValues.ContainsKey(componentKey))
                _fieldValues[componentKey] = new Dictionary<string, object>();
            if (!_propertyValues.ContainsKey(componentKey))
                _propertyValues[componentKey] = new Dictionary<string, object>();

            // Log fields
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (fields.Length > 0)
            {
                sb.AppendLine($"{indentation}  Fields:");
                foreach (var field in fields)
                {
                    try
                    {
                        object value = field.GetValue(component);
                        string valueStr = FormatValue(value);

                        // Focus special attention on our known important fields
                        bool fieldIsSpeedRelated = isSpeedRelated || field.Name == "MoveSpeed" ||
                                                  _speedRelatedTerms.Any(term => field.Name.Contains(term));

                        bool fieldIsStaminaRelated = isStaminaRelated ||
                                                    field.Name == "EnergyCurrent" ||
                                                    field.Name == "EnergyStart" ||
                                                    _staminaRelatedTerms.Any(term => field.Name.Contains(term));

                        bool fieldIsJumpRelated = isJumpRelated ||
                                                 field.Name == "JumpExtra" ||
                                                 field.Name == "JumpExtraCurrent" ||
                                                 _jumpRelatedTerms.Any(term => field.Name.Contains(term));

                        string note = "";
                        if (fieldIsSpeedRelated)
                            note += " [SPEED-RELATED]";
                        if (fieldIsStaminaRelated)
                            note += " [STAMINA/ENERGY-RELATED]";
                        if (fieldIsJumpRelated)
                            note += " [JUMP-RELATED]";

                        sb.AppendLine($"{indentation}    {field.Name} ({field.FieldType.Name}): {valueStr}{note}");

                        // Remember this field and its value
                        _loggedFields[componentKey].Add(field.Name);
                        _fieldValues[componentKey][field.Name] = value;
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"{indentation}    {field.Name} ({field.FieldType.Name}): [Error reading value: {ex.Message}]");
                        _loggedFields[componentKey].Add(field.Name);
                    }
                }
            }

            // Log properties
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (properties.Length > 0)
            {
                sb.AppendLine($"{indentation}  Properties:");
                foreach (var property in properties)
                {
                    if (property.CanRead)
                    {
                        try
                        {
                            object value = property.GetValue(component, null);
                            string valueStr = FormatValue(value);

                            // Focus special attention on our known important properties
                            bool propertyIsSpeedRelated = isSpeedRelated || property.Name == "MoveSpeed" ||
                                                        _speedRelatedTerms.Any(term => property.Name.Contains(term));

                            bool propertyIsStaminaRelated = isStaminaRelated ||
                                                        property.Name == "EnergyCurrent" ||
                                                        property.Name == "EnergyStart" ||
                                                        _staminaRelatedTerms.Any(term => property.Name.Contains(term));

                            bool propertyIsJumpRelated = isJumpRelated ||
                                                      property.Name == "JumpExtra" ||
                                                      property.Name == "JumpExtraCurrent" ||
                                                      _jumpRelatedTerms.Any(term => property.Name.Contains(term));

                            string note = "";
                            if (propertyIsSpeedRelated)
                                note += " [SPEED-RELATED]";
                            if (propertyIsStaminaRelated)
                                note += " [STAMINA/ENERGY-RELATED]";
                            if (propertyIsJumpRelated)
                                note += " [JUMP-RELATED]";

                            sb.AppendLine($"{indentation}    {property.Name} ({property.PropertyType.Name}): {valueStr}{note}");

                            // Remember this property and its value
                            _loggedProperties[componentKey].Add(property.Name);
                            _propertyValues[componentKey][property.Name] = value;
                        }
                        catch (Exception ex)
                        {
                            sb.AppendLine($"{indentation}    {property.Name} ({property.PropertyType.Name}): [Error reading value: {ex.Message}]");
                            _loggedProperties[componentKey].Add(property.Name);
                        }
                    }
                    else
                    {
                        sb.AppendLine($"{indentation}    {property.Name} ({property.PropertyType.Name}): [Write-only]");
                        _loggedProperties[componentKey].Add(property.Name);
                    }
                }
            }

            // Log methods
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (methods.Length > 0)
            {
                sb.AppendLine($"{indentation}  Methods:");
                foreach (var method in methods)
                {
                    string methodSignature = GetMethodSignature(method);

                    // Focus special attention on methods related to our known fields
                    bool methodIsSpeedRelated = isSpeedRelated ||
                                             method.Name.Contains("MoveSpeed") ||
                                             method.Name.Contains("Speed") ||
                                             _speedRelatedTerms.Any(term => method.Name.Contains(term));

                    bool methodIsStaminaRelated = isStaminaRelated ||
                                               method.Name.Contains("Energy") ||
                                               method.Name.Contains("Stamina") ||
                                               _staminaRelatedTerms.Any(term => method.Name.Contains(term));

                    bool methodIsJumpRelated = isJumpRelated ||
                                            method.Name.Contains("Jump") ||
                                            _jumpRelatedTerms.Any(term => method.Name.Contains(term));

                    string note = "";
                    if (methodIsSpeedRelated)
                        note += " [SPEED-RELATED]";
                    if (methodIsStaminaRelated)
                        note += " [STAMINA/ENERGY-RELATED]";
                    if (methodIsJumpRelated)
                        note += " [JUMP-RELATED]";

                    sb.AppendLine($"{indentation}    {methodSignature}{note}");

                    // Remember this method
                    _loggedMethods[componentKey].Add(methodSignature);
                }
            }

            // Log interfaces implemented by this component
            Type[] interfaces = type.GetInterfaces();
            if (interfaces.Length > 0)
            {
                sb.AppendLine($"{indentation}  Implemented Interfaces:");
                foreach (var interfaceType in interfaces)
                {
                    sb.AppendLine($"{indentation}    {interfaceType.FullName}");
                }
            }
        }

        private void LogAllComponentChanges(GameObject obj, StringBuilder sb)
        {
            bool hasPlayerChanges = LogPlayerComponentChanges(obj, sb);

            if (!hasPlayerChanges)
            {
                // Only add this if no other content was added to the StringBuilder
                if (sb.Length < 100)
                {
                    sb.AppendLine("No new player information to log.");
                }
            }
        }

        private bool LogPlayerComponentChanges(GameObject playerObj, StringBuilder sb)
        {
            bool anyChanges = false;

            // Check components on the player avatar
            MonoBehaviour[] avatarComponents = playerObj.GetComponents<MonoBehaviour>();
            foreach (var component in avatarComponents)
            {
                if (component != null && CheckComponentChanges(component, sb))
                {
                    anyChanges = true;
                }
            }

            // Check components on all children recursively
            foreach (Transform child in playerObj.transform)
            {
                if (CheckGameObjectChanges(child.gameObject, sb))
                {
                    anyChanges = true;
                }
            }

            return anyChanges;
        }

        private bool CheckGameObjectChanges(GameObject obj, StringBuilder sb)
        {
            if (obj == null) return false;

            bool anyChanges = false;

            // Check components on this GameObject
            MonoBehaviour[] components = obj.GetComponents<MonoBehaviour>();
            foreach (var component in components)
            {
                if (component != null && CheckComponentChanges(component, sb))
                {
                    anyChanges = true;
                }
            }

            // Check children recursively
            foreach (Transform child in obj.transform)
            {
                if (CheckGameObjectChanges(child.gameObject, sb))
                {
                    anyChanges = true;
                }
            }

            return anyChanges;
        }

        private bool CheckComponentChanges(MonoBehaviour component, StringBuilder sb)
        {
            if (component == null) return false;

            // If we haven't seen this component before, log it completely
            if (!_loggedComponents.Contains(component.GetInstanceID()))
            {
                sb.AppendLine($"\nNew component found: {component.GetType().FullName} on {component.gameObject.name}");
                LogComponentInDetail(component, sb, 1);
                return true;
            }

            string componentKey = component.GetType().Name;
            bool anyChanges = false;
            bool headerWritten = false;

            // Check for field changes
            if (_loggedFields.ContainsKey(componentKey))
            {
                Type type = component.GetType();
                FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                foreach (var field in fields)
                {
                    // Only check fields we've seen before
                    if (_loggedFields[componentKey].Contains(field.Name))
                    {
                        try
                        {
                            object currentValue = field.GetValue(component);

                            // Check if the value has changed
                            if (_fieldValues[componentKey].ContainsKey(field.Name))
                            {
                                object oldValue = _fieldValues[componentKey][field.Name];

                                // Compare values
                                if (!AreValuesEqual(oldValue, currentValue))
                                {
                                    // Write component header if not already written
                                    if (!headerWritten)
                                    {
                                        sb.AppendLine($"\n• Component: {componentKey} on {component.gameObject.name} has changes:");
                                        headerWritten = true;
                                    }

                                    string oldValueStr = FormatValue(oldValue);
                                    string newValueStr = FormatValue(currentValue);

                                    // Focus special attention on our known fields
                                    bool isSpeedRelated = field.Name == "MoveSpeed" ||
                                                         _speedRelatedTerms.Any(term => field.Name.Contains(term));

                                    bool isStaminaRelated = field.Name == "EnergyCurrent" ||
                                                           field.Name == "EnergyStart" ||
                                                           _staminaRelatedTerms.Any(term => field.Name.Contains(term));

                                    bool isJumpRelated = field.Name == "JumpExtra" ||
                                                        field.Name == "JumpExtraCurrent" ||
                                                        _jumpRelatedTerms.Any(term => field.Name.Contains(term));

                                    string note = "";
                                    if (isSpeedRelated)
                                        note += " [SPEED-RELATED]";
                                    if (isStaminaRelated)
                                        note += " [STAMINA/ENERGY-RELATED]";
                                    if (isJumpRelated)
                                        note += " [JUMP-RELATED]";

                                    sb.AppendLine($"  Field: {field.Name} changed from {oldValueStr} to {newValueStr}{note}");

                                    // Update the stored value
                                    _fieldValues[componentKey][field.Name] = currentValue;
                                    anyChanges = true;
                                }
                            }
                            else
                            {
                                // First time seeing this field value, store it
                                _fieldValues[componentKey][field.Name] = currentValue;
                            }
                        }
                        catch (Exception) { }
                    }
                }
            }

            // Check for property changes
            if (_loggedProperties.ContainsKey(componentKey))
            {
                Type type = component.GetType();
                PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                foreach (var property in properties)
                {
                    // Only check properties we've seen before that can be read
                    if (property.CanRead && _loggedProperties[componentKey].Contains(property.Name))
                    {
                        try
                        {
                            object currentValue = property.GetValue(component, null);

                            // Check if the value has changed
                            if (_propertyValues[componentKey].ContainsKey(property.Name))
                            {
                                object oldValue = _propertyValues[componentKey][property.Name];

                                // Compare values
                                if (!AreValuesEqual(oldValue, currentValue))
                                {
                                    // Write component header if not already written
                                    if (!headerWritten)
                                    {
                                        sb.AppendLine($"\n• Component: {componentKey} on {component.gameObject.name} has changes:");
                                        headerWritten = true;
                                    }

                                    string oldValueStr = FormatValue(oldValue);
                                    string newValueStr = FormatValue(currentValue);

                                    // Focus special attention on our known properties
                                    bool isSpeedRelated = property.Name == "MoveSpeed" ||
                                                         _speedRelatedTerms.Any(term => property.Name.Contains(term));

                                    bool isStaminaRelated = property.Name == "EnergyCurrent" ||
                                                           property.Name == "EnergyStart" ||
                                                           _staminaRelatedTerms.Any(term => property.Name.Contains(term));

                                    bool isJumpRelated = property.Name == "JumpExtra" ||
                                                        property.Name == "JumpExtraCurrent" ||
                                                        _jumpRelatedTerms.Any(term => property.Name.Contains(term));

                                    string note = "";
                                    if (isSpeedRelated)
                                        note += " [SPEED-RELATED]";
                                    if (isStaminaRelated)
                                        note += " [STAMINA/ENERGY-RELATED]";
                                    if (isJumpRelated)
                                        note += " [JUMP-RELATED]";

                                    sb.AppendLine($"  Property: {property.Name} changed from {oldValueStr} to {newValueStr}{note}");

                                    // Update the stored value
                                    _propertyValues[componentKey][property.Name] = currentValue;
                                    anyChanges = true;
                                }
                            }
                            else
                            {
                                // First time seeing this property value, store it
                                _propertyValues[componentKey][property.Name] = currentValue;
                            }
                        }
                        catch (Exception) { }
                    }
                }
            }

            return anyChanges;
        }

        private bool IsSpeedRelated(string name)
        {
            // Check for our known fields and other speed-related terms
            return name.Contains("MoveSpeed") ||
                   name.Contains("Speed") ||
                   name.Contains("Movement") ||
                   name.Contains("Velocity");
        }

        private bool IsStaminaRelated(string name)
        {
            // Check for our known fields and other stamina-related terms
            return name.Contains("Energy") ||
                   name.Contains("Stamina") ||
                   name.Contains("Endurance");
        }

        private bool IsJumpRelated(string name)
        {
            // Check for our known fields and other jump-related terms
            return name.Contains("Jump");
        }

        private string GetMethodSignature(MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            string paramList = string.Join(", ", Array.ConvertAll(parameters, p => $"{p.ParameterType.Name} {p.Name}"));
            return $"{method.ReturnType.Name} {method.Name}({paramList})";
        }

        private string FormatValue(object value)
        {
            if (value == null)
                return "null";

            if (value is float f)
                return f.ToString("F3");

            if (value is double d)
                return d.ToString("F3");

            if (value is Vector2 v2)
                return $"({v2.x:F2}, {v2.y:F2})";

            if (value is Vector3 v3)
                return $"({v3.x:F2}, {v3.y:F2}, {v3.z:F2})";

            if (value is Color color)
                return $"RGBA({color.r:F2}, {color.g:F2}, {color.b:F2}, {color.a:F2})";

            if (value is Quaternion q)
                return $"({q.x:F2}, {q.y:F2}, {q.z:F2}, {q.w:F2})";

            if (value is Array array)
            {
                if (array.Length <= 5)
                {
                    // For small arrays, show all elements
                    return $"[{string.Join(", ", Array.ConvertAll(array as object[], o => FormatValue(o)))}]";
                }
                else
                {
                    // For larger arrays, show length and first few elements
                    return $"Array[{array.Length}] [{string.Join(", ", Array.ConvertAll(array as object[], o => FormatValue(o)).Take(3))}...]";
                }
            }

            if (value is IList list)
            {
                if (list.Count <= 5)
                {
                    // For small lists, show all elements
                    string elements = "";
                    for (int i = 0; i < list.Count; i++)
                    {
                        elements += (i > 0 ? ", " : "") + FormatValue(list[i]);
                    }
                    return $"[{elements}]";
                }
                else
                {
                    // For larger lists, show count and first few elements
                    string elements = "";
                    for (int i = 0; i < 3; i++)
                    {
                        elements += (i > 0 ? ", " : "") + FormatValue(list[i]);
                    }
                    return $"List[{list.Count}] [{elements}...]";
                }
            }

            if (value is IDictionary dict)
            {
                return $"Dictionary[{dict.Count} entries]";
            }

            if (value is GameObject go)
            {
                return $"GameObject \"{go.name}\"";
            }

            if (value is Component comp)
            {
                return $"{comp.GetType().Name} on \"{comp.gameObject.name}\"";
            }

            return value.ToString();
        }

        private bool AreValuesEqual(object a, object b)
        {
            if (a == null && b == null)
                return true;

            if (a == null || b == null)
                return false;

            if (a is float f1 && b is float f2)
                return Mathf.Approximately(f1, f2);

            if (a is double d1 && b is double d2)
                return Math.Abs(d1 - d2) < 0.0001;

            if (a is Vector3 v1 && b is Vector3 v2)
                return Vector3.Distance(v1, v2) < 0.001f;

            if (a is Vector2 v2a && b is Vector2 v2b)
                return Vector2.Distance(v2a, v2b) < 0.001f;

            if (a is Quaternion q1 && b is Quaternion q2)
                return Quaternion.Angle(q1, q2) < 0.001f;

            if (a is Color c1 && b is Color c2)
                return ((Color)a) == ((Color)b);

            return a.Equals(b);
        }

        #endregion
    }
}
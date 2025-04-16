using UnityEngine;
using MelonLoader;
using System.Linq;
using UnityEngine.SceneManagement;
using System.Collections;
using MelonLoader.Utils;

[assembly: MelonInfo(typeof(REPO_UTILS.Core), "REPO_UTILS", "1.0.0", "thultz", null)]
[assembly: MelonGame("semiwork", "REPO")]

namespace REPO_UTILS
{
    /// <summary>
    /// Core class for the REPO_UTILS mod.
    /// This mod adds ESP (visual tracking) for enemies, items, and players,
    /// along with player health management features and various utility functions.
    /// </summary>
    public class Core : MelonMod
    {
        // Manager instances
        public PlayerManager PlayerManager { get; private set; }
        public EnemyManager EnemyManager { get; private set; }
        public ItemManager ItemManager { get; private set; }
        public UIManager UIManager { get; private set; }
        public LoggingSystem Logger { get; private set; }

        // Core references
        private Camera mainCamera;
        private Transform playerAvatar;
        private Transform playerController;
        private Transform enemiesParent;
        private Transform levelTransform;

        // Store reference to the coroutine handle (object for MelonCoroutines)
        private object _findPlayerCoroutine = null; // Changed type from Coroutine to object

        #region MelonLoader Lifecycle Methods

        public override void OnApplicationStart()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.activeSceneChanged += OnSceneChanged;

            PlayerManager = new PlayerManager(this);
            EnemyManager = new EnemyManager(this);
            ItemManager = new ItemManager(this);
            UIManager = new UIManager(this);
            Logger = new LoggingSystem(this);
            Logger.SetLoggingEnabled(false);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            MelonLogger.Msg($"===== [CORE] OnSceneLoaded - SCENE: {scene.name}, MODE: {mode} =====");
            // Reset all systems for the new scene
            MelonLogger.Msg("[CORE] OnSceneLoaded: Calling ResetAllSystems...");
            ResetAllSystems();
            MelonLogger.Msg("[CORE] OnSceneLoaded: Calling InitializeESP...");
            InitializeESP();

            // Subscribe GUI handler
             MelonLogger.Msg("[CORE] OnSceneLoaded: Subscribing OnGUI...");
            MelonEvents.OnGUI.Unsubscribe(UIManager.DrawGUI);
            MelonEvents.OnGUI.Subscribe(UIManager.DrawGUI, 100);

            // Do NOT start the logger automatically
            // Logger.StartLogging(); -- Remove or comment out this line
            MelonLogger.Msg("[CORE] OnSceneLoaded: Finished.");
        }

        private void OnSceneChanged(Scene oldScene, Scene newScene)
        {
             MelonLogger.Msg($"===== [CORE] OnSceneChanged - FROM: {oldScene.name}, TO: {newScene.name} =====");
            // Clean up GUI subscription
             MelonLogger.Msg("[CORE] OnSceneChanged: Unsubscribing OnGUI...");
            MelonEvents.OnGUI.Unsubscribe(UIManager.DrawGUI);

            // Reset and prepare for new scene
            MelonLogger.Msg("[CORE] OnSceneChanged: Calling ResetAllSystems...");
            ResetAllSystems();
             // Stop existing coroutine? 
             if (_findPlayerCoroutine != null)
             {
                  MelonLogger.Msg("[CORE] OnSceneChanged: Explicitly stopping previous FindPlayerWithDelay coroutine...");
                  try
                  {
                       MelonCoroutines.Stop(_findPlayerCoroutine); // Use MelonCoroutines.Stop
                  }
                  catch (Exception ex)
                  {
                       MelonLogger.Warning($"[CORE] OnSceneChanged: Exception while stopping coroutine (likely already finished): {ex.GetType().Name} - {ex.Message}");
                  }
                  _findPlayerCoroutine = null;
             }
             MelonLogger.Msg("[CORE] OnSceneChanged: Starting FindPlayerWithDelay coroutine...");
             _findPlayerCoroutine = MelonCoroutines.Start(FindPlayerWithDelay()); // Assign object handle
             MelonLogger.Msg("[CORE] OnSceneChanged: Finished.");
        }

        public override void OnUpdate()
        {
            if (playerController == null) return;

            // Update managers
            PlayerManager.OnUpdate();
            EnemyManager.OnUpdate();
            ItemManager.OnUpdate();

            // Check for key presses
            CheckKeyPresses();
        }

        public override void OnApplicationQuit()
        {
            // Unsubscribe from events
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnSceneChanged;
            MelonEvents.OnGUI.Unsubscribe(UIManager.DrawGUI);

            // Clean up managers
            PlayerManager.OnApplicationQuit();
            EnemyManager.OnApplicationQuit();
            ItemManager.OnApplicationQuit();
            Logger.StopLogging();
        }

        #endregion

        #region Initialization Methods

        private void ResetAllSystems()
        {
            // Reset core references
            playerAvatar = null;
            playerController = null;
            enemiesParent = null;
            levelTransform = null;

            // Reset managers
            PlayerManager.Reset();
            EnemyManager.Reset();
            ItemManager.Reset();
            UIManager.Reset();
        }

        private void InitializeESP()
        {
            // Get reference to main camera
            mainCamera = Camera.main;

            // Initialize managers
            UIManager.Initialize();

            // Start player finding coroutine
            MelonCoroutines.Start(FindPlayerWithDelay());
        }

        private IEnumerator FindPlayerWithDelay()
        {
            MelonLogger.Msg("Starting player/level search coroutine...");
            bool initialized = false;
            int attempt = 0;

            // Keep trying until all essential objects are found
            while (!initialized)
            {
                attempt++;
                MelonLogger.Msg($"Initialization attempt #{attempt}...");

                 // Reset references before each attempt?
                 // playerAvatar = null; 
                 // playerController = null;
                 // enemiesParent = null;
                 // levelTransform = null;
                 // GameObject levelGenerator = null; // Reset local variable too

                // --- Find Player --- 
                if (playerAvatar == null)
                {
                    playerAvatar = GameObject.Find("PlayerAvatar(Clone)")?.transform;
                }

                if (playerAvatar == null)
                {
                    MelonLogger.Msg("PlayerAvatar(Clone) not found yet...");
                    yield return new WaitForSeconds(1.5f); // Wait before next major attempt
                    continue; // Restart the loop
                }
                // Found Player Avatar, now find controller
                MelonLogger.Msg("   Found PlayerAvatar(Clone).");

                if (playerController == null)
                {
                    playerController = playerAvatar.Find("Player Avatar Controller");
                }

                if (playerController == null)
                {
                    MelonLogger.Msg("   Player Avatar Controller not found yet (child of PlayerAvatar)...");
                     playerAvatar = null; // Reset avatar if controller not found, might be incomplete object
                    yield return new WaitForSeconds(1.0f); // Wait slightly less
                    continue;
                }
                MelonLogger.Msg("   Found Player Avatar Controller.");

                // Optional: Check child count if needed
                // if (playerAvatar.childCount != 8) { ... yield return ... continue; }

                // --- Find Level Generator and its Children --- 
                GameObject levelGenerator = GameObject.Find("Level Generator");
                if (levelGenerator == null)
                {
                    MelonLogger.Msg("Level Generator not found yet...");
                    yield return new WaitForSeconds(1.5f);
                    continue;
                }
                 MelonLogger.Msg("   Found Level Generator.");

                if (enemiesParent == null)
                {
                     enemiesParent = levelGenerator.transform.Find("Enemies");
                }

                if (enemiesParent == null)
                {
                    MelonLogger.Msg("   Enemies transform not found yet (child of Level Generator)...");
                    yield return new WaitForSeconds(1.0f);
                    continue;
                }
                MelonLogger.Msg("   Found Enemies transform.");

                if (levelTransform == null)
                {
                    levelTransform = levelGenerator.transform.Find("Level");
                }

                if (levelTransform == null)
                {
                    MelonLogger.Msg("   Level transform not found yet (child of Level Generator)...");
                    yield return new WaitForSeconds(1.0f);
                    continue;
                }
                 MelonLogger.Msg("   Found Level transform.");

                // --- All Found - Proceed with Initialization --- 
                 MelonLogger.Msg("All required objects found! Initializing managers...");

                 // Initialize all managers with scene references
                 PlayerManager.Initialize(playerAvatar, playerController);
                 EnemyManager.Initialize(enemiesParent);
                 ItemManager.Initialize(levelTransform);

                 // Start logging player components for analysis - THIS IS THE IMPORTANT PART
                 Logger.LogPlayerComponents(playerController);

                 MelonLogger.Msg("ESP system initialized successfully after {attempt} attempts.");
                 initialized = true; // Set flag to exit the loop
            }
            // Coroutine finishes after successful initialization
        }

        #endregion

        #region Input Handling

        // Add this method to the Core class in the Input Handling section
        private void CheckKeyPresses()
        {
            if (Input.GetKeyDown(KeyCode.Semicolon))
            {
                PlayerManager.ToggleGodMode();
            }

            if (Input.GetKeyDown(KeyCode.L))
            {
                EnemyManager.ToggleEnemyESP();
            }

            if (Input.GetKeyDown(KeyCode.I))
            {
                ItemManager.ToggleItemESP();
            }

            if (Input.GetKeyDown(KeyCode.P))
            {
                PlayerManager.TogglePlayerESP();
            }

            if (Input.GetKeyDown(KeyCode.K))
            {
                // Use the new method instead of directly toggling
                Logger.SetLoggingEnabled(!Logger.IsLoggingEnabled());
            }

            // Add a new key for structure logging (J key)
            if (Input.GetKeyDown(KeyCode.J))
            {
                Logger.LogGameStructure();
                MelonLogger.Msg($"Game structure logged to {Logger.GetStructureLogFilePath()}");
            }
        }

        #endregion

        #region Helper Methods

        // Helper to get the main camera
        public Camera GetMainCamera() => mainCamera;

        // Helper to get player positions
        public Vector3 GetPlayerPosition() => playerController.position + new Vector3(0, 1f, 0);

        // New method to trigger max value on closest item
        public void MaxValueClosestItem()
        {
             ItemManager.MaxValueClosestItem();
        }

        // New method to trigger making all items cheap
        public void MakeAllItemsCheap()
        {
            ItemManager.MakeAllItemsCheap(); // Delegate to ItemManager
        }

        // New method to trigger completing extraction points
        public void CompleteExtractionPoints()
        {
            ItemManager.CompleteExtractionPoints(); // Delegate to ItemManager
        }

        // New method for manual refresh
        public void ManualRefresh()
        {
            MelonLogger.Msg("===== [CORE] ManualRefresh Requested ====");

            // 1. Stop existing initialization coroutine if running
            if (_findPlayerCoroutine != null)
            {
                MelonLogger.Msg("[CORE] ManualRefresh: Stopping existing FindPlayerWithDelay coroutine...");
                 try
                 {
                    MelonCoroutines.Stop(_findPlayerCoroutine); // Use MelonCoroutines.Stop
                 }
                 catch (Exception ex)
                 {
                     MelonLogger.Warning($"[CORE] ManualRefresh: Exception while stopping coroutine (likely already finished): {ex.GetType().Name} - {ex.Message}");
                 }
                _findPlayerCoroutine = null;
            }
            else
            {
                MelonLogger.Msg("[CORE] ManualRefresh: No existing FindPlayerWithDelay coroutine found to stop.");
            }

            // 2. Reset all systems (clears references and manager states)
            MelonLogger.Msg("[CORE] ManualRefresh: Calling ResetAllSystems...");
            ResetAllSystems(); // This implicitly calls ClearPlayerESP, ClearItemESP, ClearEnemyESP via manager Reset methods
            MelonLogger.Msg("  -> ESP lines cleared as part of ResetAllSystems.");

            // 3. Start the initialization coroutine again
            // InitializeESP(); // Don't call this directly, let the coroutine handle manager init
            MelonLogger.Msg("[CORE] ManualRefresh: Starting FindPlayerWithDelay coroutine...");
            _findPlayerCoroutine = MelonCoroutines.Start(FindPlayerWithDelay()); // Assign object handle
            MelonLogger.Msg("  -> FindPlayerWithDelay started, will recreate ESP lines upon finding objects.");

            MelonLogger.Msg("===== [CORE] ManualRefresh Finished ====");
        }

        #endregion
    }
}
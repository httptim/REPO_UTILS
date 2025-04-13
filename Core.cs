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
            MelonLogger.Msg($"Scene loaded: {scene.name}");

            // Reset all systems for the new scene
            ResetAllSystems();
            InitializeESP();

            // Subscribe GUI handler
            MelonEvents.OnGUI.Unsubscribe(UIManager.DrawGUI);
            MelonEvents.OnGUI.Subscribe(UIManager.DrawGUI, 100);

            // Do NOT start the logger automatically
            // Logger.StartLogging(); -- Remove or comment out this line
        }

        private void OnSceneChanged(Scene oldScene, Scene newScene)
        {
            MelonLogger.Msg($"Scene changed from {oldScene.name} to {newScene.name}");

            // Clean up GUI subscription
            MelonEvents.OnGUI.Unsubscribe(UIManager.DrawGUI);

            // Reset and prepare for new scene
            ResetAllSystems();
            MelonCoroutines.Start(FindPlayerWithDelay());
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
            yield return new WaitForSeconds(3f);

            playerAvatar = GameObject.Find("PlayerAvatar(Clone)")?.transform;
            if (playerAvatar == null)
            {
                MelonLogger.Msg("Could not find PlayerAvatar(Clone)");
                yield break;
            }

            playerController = playerAvatar.Find("Player Avatar Controller");
            if (playerController == null)
            {
                MelonLogger.Msg("Could not find Player Avatar Controller");
                yield break;
            }

            if (playerAvatar.childCount != 8)
            {
                MelonLogger.Msg($"Player avatar has {playerAvatar.childCount} children, expected 8");
                yield break;
            }

            GameObject levelGenerator = GameObject.Find("Level Generator");
            if (levelGenerator == null)
            {
                MelonLogger.Msg("Could not find Level Generator");
                yield break;
            }

            enemiesParent = levelGenerator.transform.Find("Enemies");
            if (enemiesParent == null)
            {
                MelonLogger.Msg("Could not find Enemies transform");
                yield break;
            }

            levelTransform = levelGenerator.transform.Find("Level");
            if (levelTransform == null)
            {
                MelonLogger.Msg("Could not find Level transform");
                yield break;
            }

            // Initialize all managers with scene references
            PlayerManager.Initialize(playerAvatar, playerController);
            EnemyManager.Initialize(enemiesParent);
            ItemManager.Initialize(levelTransform);

            // Start logging player components for analysis - THIS IS THE IMPORTANT PART
            Logger.LogPlayerComponents(playerController);

            MelonLogger.Msg("ESP system initialized successfully");
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

        #endregion
    }
}
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System;
using MelonLoader;

namespace REPO_UTILS
{
    public class PlayerManager
    {
        private Core _core;
        private Transform _playerAvatar;
        private Transform _playerController;
        private MonoBehaviour _playerHealth;
        private MonoBehaviour _playerStamina;
        private MonoBehaviour _playerMovement;
        private Dictionary<string, FieldInfo> _staminaFields = new Dictionary<string, FieldInfo>();
        private bool _godModeToggled = false;
        private bool _playerESPEnabled = true;
        private List<Transform> _otherPlayers = new List<Transform>();
        private List<MonoBehaviour> _playerAvatarComponents = new List<MonoBehaviour>();
        private List<MonoBehaviour> _playerHealthComponents = new List<MonoBehaviour>();
        private List<GameObject> _playerLineObjects = new List<GameObject>();
        private List<LineRenderer> _playerLineRenderers = new List<LineRenderer>();

        // Store original values for restoring when god mode is turned off
        private Dictionary<string, object> _originalValues = new Dictionary<string, object>();

        public PlayerManager(Core core)
        {
            _core = core;
            FindPlayerComponents();
            FindOtherPlayers();
        }

        public void Initialize(Transform playerAvatar, Transform playerController)
        {
            _playerAvatar = playerAvatar;
            _playerController = playerController;
            FindPlayerComponents();
            FindOtherPlayers();
        }

        public void Reset()
        {
            _playerAvatar = null;
            _playerController = null;
            _playerHealth = null;
            _playerStamina = null;
            _playerMovement = null;
            ClearPlayerESP();
            _otherPlayers.Clear();
            _playerAvatarComponents.Clear();
            _playerHealthComponents.Clear();
            _staminaFields.Clear();
            _originalValues.Clear();
        }

        public void OnApplicationQuit()
        {
            ClearPlayerESP();
        }

        private void FindPlayerComponents()
        {
            if (_playerController == null) return;
            MonoBehaviour[] components = _playerController.GetComponents<MonoBehaviour>();
            foreach (var component in components)
            {
                string typeName = component.GetType().Name;
                if (typeName == "PlayerHealth")
                {
                    _playerHealth = component;
                }
                else if (typeName == "PlayerStamina" || typeName.Contains("Stamina"))
                {
                    _playerStamina = component;
                    CacheStaminaFields(component);
                }
                else if (typeName == "PlayerMovement" || typeName == "CharacterController" ||
                         typeName.Contains("Movement") || typeName.Contains("Controller"))
                {
                    _playerMovement = component;
                }
            }
        }

        private void CacheStaminaFields(MonoBehaviour component)
        {
            // Direct access to the known energy/stamina fields
            FieldInfo energyCurrentField = component.GetType().GetField("EnergyCurrent",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            FieldInfo energyStartField = component.GetType().GetField("EnergyStart",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (energyCurrentField != null)
            {
                string key = "EnergyCurrent";
                _staminaFields[key] = energyCurrentField;
            }

            if (energyStartField != null)
            {
                string key = "EnergyStart";
                _staminaFields[key] = energyStartField;
            }
        }

        private void FindOtherPlayers()
        {
            _otherPlayers.Clear();
            _playerAvatarComponents.Clear();
            _playerHealthComponents.Clear();
            ClearPlayerESP();

            GameObject[] allPlayerAvatars = GameObject.FindObjectsOfType<GameObject>()
                .Where(go => go.name == "PlayerAvatar(Clone)" && go.transform != _playerAvatar)
                .ToArray();

            foreach (var playerObj in allPlayerAvatars)
            {
                Transform playerTransform = playerObj.transform;
                _otherPlayers.Add(playerTransform);

                Transform otherPlayerController = playerTransform.Find("Player Avatar Controller");
                MonoBehaviour healthComponent = null;
                MonoBehaviour avatarComponent = null;

                if (otherPlayerController != null)
                {
                    healthComponent = otherPlayerController.GetComponent("PlayerHealth") as MonoBehaviour;
                    avatarComponent = otherPlayerController.GetComponent("PlayerAvatar") as MonoBehaviour;
                }
                else
                {
                    MelonLogger.Warning($"Could not find 'Player Avatar Controller' for player object: {playerObj.name}");
                }

                _playerHealthComponents.Add(healthComponent);
                _playerAvatarComponents.Add(avatarComponent);

                CreateLineRendererForPlayer(playerTransform);
            }
            MelonLogger.Msg($"Found {_otherPlayers.Count} other players.");
        }

        public void OnUpdate()
        {
            if (_godModeToggled)
            {
                ApplyGodModeEffects();
            }

            if (_playerESPEnabled && _otherPlayers.Count > 0)
            {
                UpdatePlayerESP();
            }
        }

        private void UpdatePlayerESP()
        {
            if (_playerController == null) return;

            Vector3 playerPosition = _core.GetPlayerPosition();

            for (int i = 0; i < _otherPlayers.Count && i < _playerLineRenderers.Count; i++)
            {
                if (_otherPlayers[i] != null && _playerLineRenderers[i] != null)
                {
                    Transform otherPlayerController = _otherPlayers[i].Find("Player Avatar Controller");
                    if (otherPlayerController != null)
                    {
                        Vector3 otherPlayerPosition = otherPlayerController.position;
                        DrawLineToTarget(playerPosition, otherPlayerPosition, _playerLineRenderers[i]);
                    }
                }
            }

            if (Time.frameCount % 300 == 0)
            {
                FindOtherPlayers();
            }
        }

        public void ToggleGodMode()
        {
            if (_playerHealth == null) return;

            FieldInfo godModeField = _playerHealth.GetType().GetField("godMode",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (godModeField == null) return;

            bool currentGodMode = (bool)godModeField.GetValue(_playerHealth);

            // Toggle god mode
            _godModeToggled = !currentGodMode;
            godModeField.SetValue(_playerHealth, _godModeToggled);

            if (_godModeToggled)
            {
                // Store original values and apply god mode effects
                StoreOriginalValues();
                ApplyGodModeEffects();
            }
            else
            {
                // Restore original values
                RestoreOriginalValues();
            }

            MelonLogger.Msg($"God Mode: {(_godModeToggled ? "ENABLED" : "DISABLED")}");
        }

        private void StoreOriginalValues()
        {
            _originalValues.Clear();

            // Find the PlayerController component (assuming it's on the main player object or controller transform)
            // This might need adjustment based on the actual game object structure
            MonoBehaviour playerControllerComponent = _playerMovement; // Use the cached PlayerMovement/Controller component

            if (playerControllerComponent != null && playerControllerComponent.GetType().Name.Contains("Controller")) // Check if it's likely the PlayerController
            {
                // Store SprintSpeed
                FieldInfo sprintSpeedField = playerControllerComponent.GetType().GetField("SprintSpeed",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (sprintSpeedField != null)
                {
                    _originalValues["SprintSpeed"] = sprintSpeedField.GetValue(playerControllerComponent);
                    MelonLogger.Msg($"Stored original SprintSpeed: {_originalValues["SprintSpeed"]}");
                }
                 else
                {
                     MelonLogger.Warning("Could not find 'SprintSpeed' field to store original value.");
                 }

                // Remove the old MoveSpeed storing logic if it exists elsewhere,
                // or comment it out if keeping for reference:
                /*
                FieldInfo moveSpeedField = playerControllerComponent.GetType().GetField("MoveSpeed", ...);
                if (moveSpeedField != null)
                {
                    _originalValues["MoveSpeed"] = moveSpeedField.GetValue(playerControllerComponent);
                }
                */
            }
             else
            {
                 MelonLogger.Warning("Could not find PlayerController component to store original values.");
            }

            // Keep other original value storing logic if necessary (e.g., for health/stamina)
            // ...
        }

        private void RestoreOriginalValues()
        {
             MonoBehaviour playerControllerComponent = _playerMovement;

            if (playerControllerComponent != null && playerControllerComponent.GetType().Name.Contains("Controller"))
            {
                // Restore SprintSpeed
                if (_originalValues.ContainsKey("SprintSpeed"))
                {
                    FieldInfo sprintSpeedField = playerControllerComponent.GetType().GetField("SprintSpeed",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (sprintSpeedField != null)
                    {
                        try
                        {
                            sprintSpeedField.SetValue(playerControllerComponent, _originalValues["SprintSpeed"]);
                             MelonLogger.Msg($"Restored original SprintSpeed: {_originalValues["SprintSpeed"]}");
                        }
                        catch (Exception ex)
                        {
                             MelonLogger.Error($"Failed to restore SprintSpeed: {ex.Message}");
                        }
                    }
                     else
                     {
                          MelonLogger.Warning("Could not find 'SprintSpeed' field to restore original value.");
                     }
                }

                // Remove or comment out old MoveSpeed restoration
                 /*
                 if (_originalValues.ContainsKey("MoveSpeed"))
                 {
                     FieldInfo moveSpeedField = playerControllerComponent.GetType().GetField("MoveSpeed", ...);
                     if (moveSpeedField != null)
                     {
                         moveSpeedField.SetValue(playerControllerComponent, _originalValues["MoveSpeed"]);
                     }
                 }
                 */
            }
            else
            {
                 MelonLogger.Warning("Could not find PlayerController component to restore original values.");
            }

             // Keep other restore logic
             // ...
        }


        private void ApplyGodModeEffects()
        {
             MonoBehaviour playerControllerComponent = _playerMovement;

            if (playerControllerComponent != null && playerControllerComponent.GetType().Name.Contains("Controller"))
            {
                 // Apply enhanced SprintSpeed
                 FieldInfo sprintSpeedField = playerControllerComponent.GetType().GetField("SprintSpeed",
                     BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                 if (sprintSpeedField != null)
                 {
                     try
                     {
                         sprintSpeedField.SetValue(playerControllerComponent, 8f); // Set SprintSpeed to 8f
                         // MelonLogger.Msg("Applied enhanced SprintSpeed (8f)."); // Optional: Log spammy
                     }
                     catch (Exception ex)
                     {
                         MelonLogger.Error($"Failed to apply enhanced SprintSpeed: {ex.Message}");
                     }
                 }
                  else
                  {
                     // Log only if the field wasn't found initially during StoreOriginalValues maybe?
                     // MelonLogger.Warning("Could not find 'SprintSpeed' field to apply god mode effect.");
                  }

                 // Remove or comment out old MoveSpeed modification
                 /*
                 FieldInfo moveSpeedField = playerControllerComponent.GetType().GetField("MoveSpeed", ...);
                 if (moveSpeedField != null)
                 {
                      // Logic to modify MoveSpeed removed
                 }
                 */
            }
             else
            {
                 // MelonLogger.Warning("Could not find PlayerController component to apply god mode effects."); // Spammy
            }

             // Keep other god mode effects (like infinite health/stamina)
             ApplyInfiniteStamina(); // Make sure this is still called if needed
             // Add health setting logic here if required by god mode
             // ...
        }

        public void ApplyInfiniteStamina()
        {
            if (_playerStamina == null || _staminaFields.Count == 0)
            {
                // MelonLogger.Warning("Cannot apply infinite stamina: PlayerStamina component or fields not found."); // Potentially spammy
                return;
            }

            try
            {
                if (_staminaFields.TryGetValue("EnergyCurrent", out FieldInfo energyCurrentField))
                {
                    // Set current energy/stamina to 100
                    if (energyCurrentField.FieldType == typeof(float))
                        energyCurrentField.SetValue(_playerStamina, 100f);
                    else if (energyCurrentField.FieldType == typeof(int))
                        energyCurrentField.SetValue(_playerStamina, 100);
                    // Add other type checks if necessary
                }

                // Optionally, also set the max/start energy to 100 if desired
                // if (_staminaFields.TryGetValue("EnergyStart", out FieldInfo energyStartField))
                // {
                //     if (energyStartField.FieldType == typeof(float))
                //         energyStartField.SetValue(_playerStamina, 100f);
                //     else if (energyStartField.FieldType == typeof(int))
                //         energyStartField.SetValue(_playerStamina, 100);
                // }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error applying infinite stamina: {ex.Message}");
                // Consider disabling this feature or logging once if errors persist
                _playerStamina = null; // Stop trying if it errors
            }
        }

        public void HealSelf()
        {
            if (_playerHealth != null)
            {
                CallHealMethod(_playerHealth, 10, false);
                MelonLogger.Msg("Attempted to heal self.");
            }
            else
            {
                MelonLogger.Warning("Cannot HealSelf: PlayerHealth component not found for local player.");
            }
        }

        public void ReviveSelf()
        {
            if (_playerHealth != null)
            {
                CallHealMethod(_playerHealth, 100, false);
                MelonLogger.Msg("Attempted to revive self.");
            }
            else
            {
                MelonLogger.Warning("Cannot ReviveSelf: PlayerHealth component not found for local player.");
            }
        }

        public void HealOtherPlayer(int playerIndex)
        {
            if (playerIndex >= 0 && playerIndex < _playerHealthComponents.Count)
            {
                MonoBehaviour targetHealth = _playerHealthComponents[playerIndex];
                if (targetHealth != null)
                {
                    CallHealMethod(targetHealth, 10, false);
                    MelonLogger.Msg($"Attempted to heal player {playerIndex}.");
                }
                else
                {
                    MelonLogger.Warning($"Cannot HealOtherPlayer: PlayerHealth component not found for player index {playerIndex}.");
                }
            }
            else
            {
                MelonLogger.Warning($"Cannot HealOtherPlayer: Invalid player index {playerIndex}.");
            }
        }

        public void ReviveOtherPlayer(int playerIndex)
        {
            if (playerIndex >= 0 && playerIndex < _playerHealthComponents.Count)
            {
                MonoBehaviour targetHealth = _playerHealthComponents[playerIndex];
                if (targetHealth != null)
                {
                    CallHealMethod(targetHealth, 100, false);
                    MelonLogger.Msg($"Attempted to revive player {playerIndex}.");
                }
                else
                {
                    MelonLogger.Warning($"Cannot ReviveOtherPlayer: PlayerHealth component not found for player index {playerIndex}.");
                }
            }
            else
            {
                MelonLogger.Warning($"Cannot ReviveOtherPlayer: Invalid player index {playerIndex}.");
            }
        }

        private void CallHealMethod(MonoBehaviour healthComponent, int amount, bool useEffect)
        {
            if (healthComponent == null) return;

            try
            {
                MethodInfo healMethod = healthComponent.GetType().GetMethod("Heal", new Type[] { typeof(int), typeof(bool) });
                if (healMethod != null)
                {
                    healMethod.Invoke(healthComponent, new object[] { amount, useEffect });
                }
                else
                {
                    MelonLogger.Warning($"Heal(int, bool) method not found on {healthComponent.GetType().Name}.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error calling Heal method: {ex.Message}");
            }
        }

        public void TogglePlayerESP()
        {
            _playerESPEnabled = !_playerESPEnabled;
            SetPlayerESPVisibility(_playerESPEnabled);
        }

        public bool IsPlayerESPEnabled()
        {
            return _playerESPEnabled;
        }

        private void SetPlayerESPVisibility(bool isVisible)
        {
            foreach (var lineRenderer in _playerLineRenderers)
            {
                if (lineRenderer != null)
                    lineRenderer.enabled = isVisible;
            }
        }

        private void CreateLineRendererForPlayer(Transform player)
        {
            GameObject lineObject = new GameObject($"ESP_PlayerLine_{_otherPlayers.IndexOf(player)}");
            _playerLineObjects.Add(lineObject);

            LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
            _playerLineRenderers.Add(lineRenderer);

            lineRenderer.startWidth = 0.01f;
            lineRenderer.endWidth = 0.01f;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = Color.blue;
            lineRenderer.endColor = Color.blue;
            lineRenderer.positionCount = 2;
        }

        private void DrawLineToTarget(Vector3 startPosition, Vector3 endPosition, LineRenderer lineRenderer)
        {
            if (lineRenderer != null)
            {
                lineRenderer.SetPosition(0, startPosition);
                lineRenderer.SetPosition(1, endPosition);
            }
        }

        private void ClearPlayerESP()
        {
            foreach (var lineObject in _playerLineObjects)
            {
                if (lineObject != null)
                {
                    GameObject.Destroy(lineObject);
                }
            }

            _playerLineObjects.Clear();
            _playerLineRenderers.Clear();
        }

        public bool IsPlayerAlive(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= _playerHealthComponents.Count || _playerHealthComponents[playerIndex] == null)
                return false;

            try
            {
                FieldInfo deadField = _playerHealthComponents[playerIndex].GetType().GetField("dead", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (deadField != null && deadField.FieldType == typeof(bool))
                {
                    return !(bool)deadField.GetValue(_playerHealthComponents[playerIndex]);
                }

                int health = GetPlayerHealth(playerIndex);
                return health > 0;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error checking if player {playerIndex} is alive: {ex.Message}");
                return false;
            }
        }

        public int GetPlayerHealth(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= _playerHealthComponents.Count || _playerHealthComponents[playerIndex] == null)
                return 0;

            try
            {
                FieldInfo healthField = _playerHealthComponents[playerIndex].GetType().GetField("health", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (healthField != null)
                {
                    if (typeof(IConvertible).IsAssignableFrom(healthField.FieldType))
                    {
                        object value = healthField.GetValue(_playerHealthComponents[playerIndex]);
                        return Convert.ToInt32(value);
                    }
                }

                PropertyInfo healthProperty = _playerHealthComponents[playerIndex].GetType().GetProperty("Health", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (healthProperty != null && healthProperty.PropertyType == typeof(int))
                {
                    return (int)healthProperty.GetValue(_playerHealthComponents[playerIndex]);
                }

                MelonLogger.Warning($"Could not find 'health' field or property for player {playerIndex}. Returning 0.");
                return 0;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting health for player {playerIndex}: {ex.Message}");
                return 0;
            }
        }

        public string GetPlayerName(int playerIndex)
        {
            if (playerIndex >= 0 && playerIndex < _playerAvatarComponents.Count)
            {
                MonoBehaviour avatarComponent = _playerAvatarComponents[playerIndex];
                if (avatarComponent != null)
                {
                    try
                    {
                        FieldInfo nameField = avatarComponent.GetType().GetField("playerName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (nameField != null && nameField.FieldType == typeof(string))
                        {
                            return (string)nameField.GetValue(avatarComponent);
                        }
                        else
                        {
                            MelonLogger.Warning($"'playerName' field not found or not string on PlayerAvatar for index {playerIndex}.");
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"Error getting playerName for player {playerIndex}: {ex.Message}");
                    }
                }
                else
                {
                    // MelonLogger.Warning($"PlayerAvatar component is null for player index {playerIndex}.");
                }
            }
            else
            {
                MelonLogger.Warning($"Invalid player index {playerIndex} for GetPlayerName.");
            }
            return $"Player {playerIndex + 1}";
        }

        public bool IsGodModeEnabled()
        {
            return _godModeToggled;
        }

        public List<Transform> GetOtherPlayers()
        {
            return _otherPlayers;
        }
    }
}
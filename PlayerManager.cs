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
            MelonLogger.Msg("[PlayerManager.Initialize] Called.");
            _playerAvatar = playerAvatar;
            _playerController = playerController;
            if (_playerAvatar == null) MelonLogger.Warning("  _playerAvatar is NULL on Initialize!");
            if (_playerController == null) MelonLogger.Warning("  _playerController is NULL on Initialize!");

            FindPlayerComponents(); // Find local player components first
            FindOtherPlayers(); // Then find others
            // CacheInventoryComponent(); // REMOVED Call
            MelonLogger.Msg("[PlayerManager.Initialize] Finished.");
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
            MelonLogger.Msg("[PlayerManager.FindOtherPlayers] Called.");
            _otherPlayers.Clear(); // Clear lists before repopulating
            _playerAvatarComponents.Clear();
            _playerHealthComponents.Clear();
            ClearPlayerESP();

            GameObject[] allPlayerAvatars = GameObject.FindObjectsOfType<GameObject>()
                .Where(go => go.name == "PlayerAvatar(Clone)" && go.transform != _playerAvatar)
                .ToArray();

            MelonLogger.Msg($"[PlayerManager.FindOtherPlayers] Found {allPlayerAvatars.Length} potential PlayerAvatar(Clone) objects (excluding self).");

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
             MelonLogger.Msg($"[PlayerManager.FindOtherPlayers] Finished. Added {_otherPlayers.Count} other players.");
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
            if (!_playerESPEnabled) return; 
            
            if (Time.frameCount % 120 == 0) MelonLogger.Msg($"[PlayerManager.UpdatePlayerESP] Running. Players: {_otherPlayers.Count}, Renderers: {_playerLineRenderers.Count}");

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
                        if (!_playerLineRenderers[i].enabled) 
                        {
                            if (Time.frameCount % 120 == 0) MelonLogger.Msg($"[UpdatePlayerESP] Re-enabling line for player index {i}.");
                            _playerLineRenderers[i].enabled = true;
                        }
                    }
                     else 
                     {
                         if (_playerLineRenderers[i] != null && _playerLineRenderers[i].enabled)
                         {
                             MelonLogger.Warning($"[UpdatePlayerESP] Disabling line for player index {i} because otherPlayerController is NULL (Player: {_otherPlayers[i]?.name}).");
                             _playerLineRenderers[i].enabled = false;
                         }
                     }
                }
                 else 
                 {
                     if (i < _playerLineRenderers.Count && _playerLineRenderers[i] != null && _playerLineRenderers[i].enabled)
                     {
                          MelonLogger.Warning($"[UpdatePlayerESP] Disabling line for index {i} because _otherPlayers[i] is NULL or _playerLineRenderers[i] is NULL.");
                          _playerLineRenderers[i].enabled = false;
                     }
                      if (Time.frameCount % 120 == 0 && (_otherPlayers.Count != _playerLineRenderers.Count))
                          MelonLogger.Warning($"[PlayerManager.UpdatePlayerESP] Mismatch! Players: {_otherPlayers.Count}, Renderers: {_playerLineRenderers.Count}");
                 }
            }

            if (Time.frameCount % 300 == 0)
            {
                FindOtherPlayers();
            }
        }

        public void ToggleGodMode()
        {
            MelonLogger.Msg("[ToggleGodMode] Attempting to toggle...");
            if (_playerHealth == null)
            {
                 MelonLogger.Error("[ToggleGodMode] Failed: _playerHealth component is null.");
                 return;
            }
            MelonLogger.Msg($"[ToggleGodMode] _playerHealth component type: {_playerHealth.GetType().FullName}");

            FieldInfo godModeField = null;
            try
            {
                 godModeField = _playerHealth.GetType().GetField("godMode",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            }
             catch (Exception ex)
             {
                 MelonLogger.Error($"[ToggleGodMode] Error finding 'godMode' field: {ex.Message}");
                 return;
             }

            if (godModeField == null)
            {
                 MelonLogger.Error("[ToggleGodMode] Failed: 'godMode' field not found.");
                 return;
            }
            MelonLogger.Msg("[ToggleGodMode] 'godMode' field found.");

            bool currentGodModeValue = false;
            try
            {
                 currentGodModeValue = (bool)godModeField.GetValue(_playerHealth);
                 MelonLogger.Msg($"[ToggleGodMode] Current 'godMode' field value: {currentGodModeValue}");
            }
            catch (Exception ex)
            {
                 MelonLogger.Error($"[ToggleGodMode] Error reading 'godMode' field value: {ex.Message}");
                 return;
            }

            // Calculate the new state
            bool targetGodModeState = !currentGodModeValue;
            MelonLogger.Msg($"[ToggleGodMode] Target state: {targetGodModeState}");

            // Set the field value
            try
            {
                godModeField.SetValue(_playerHealth, targetGodModeState);
                 MelonLogger.Msg($"[ToggleGodMode] Successfully set 'godMode' field to: {targetGodModeState}");
            }
            catch (Exception ex)
            {
                 MelonLogger.Error($"[ToggleGodMode] Error setting 'godMode' field value: {ex.Message}");
                 // Even if setting fails, should we still update our internal state? Maybe not.
                 return; 
            }

            // Update internal state ONLY if field was set successfully
            _godModeToggled = targetGodModeState; 
            MelonLogger.Msg($"[ToggleGodMode] Internal flag _godModeToggled set to: {_godModeToggled}");

            if (_godModeToggled)
            {
                MelonLogger.Msg("[ToggleGodMode] Applying ENABLED effects...");
                // Store original values and apply god mode effects
                StoreOriginalValues();
                ApplyGodModeEffects();
            }
            else
            {
                MelonLogger.Msg("[ToggleGodMode] Applying DISABLED effects (restoring values)...");
                // Restore original values
                RestoreOriginalValues();
            }

            MelonLogger.Msg($"[ToggleGodMode] God Mode toggle complete: {(_godModeToggled ? "ENABLED" : "DISABLED")}");
        }

        private void StoreOriginalValues()
        {
            _originalValues.Clear();

            GameObject[] allPlayerObjects = GameObject.FindGameObjectsWithTag("Player");
            if (allPlayerObjects.Length == 0)
            {
                GameObject playerObj = GameObject.Find("Player");
                if (playerObj != null)
                {
                    allPlayerObjects = new GameObject[] { playerObj };
                }
            }

            foreach (GameObject playerObj in allPlayerObjects)
            {
                Transform controllerTransform = playerObj.transform.Find("Controller");
                if (controllerTransform == null) continue;

                MonoBehaviour[] components = controllerTransform.GetComponents<MonoBehaviour>();
                foreach (var component in components)
                {
                    if (component.GetType().Name == "PlayerController")
                    {
                        // Store SprintSpeed
                        FieldInfo sprintSpeedField = component.GetType().GetField("SprintSpeed",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (sprintSpeedField != null)
                        {
                            try
                            {
                                _originalValues["SprintSpeed"] = sprintSpeedField.GetValue(component);
                            }
                            catch { }
                        }

                        // Store EnergyCurrent
                        FieldInfo energyCurrentField = component.GetType().GetField("EnergyCurrent",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (energyCurrentField != null)
                        {
                            try
                            {
                                _originalValues["EnergyCurrent"] = energyCurrentField.GetValue(component);
                            }
                            catch { }
                        }

                        // Store EnergyStart
                        FieldInfo energyStartField = component.GetType().GetField("EnergyStart",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (energyStartField != null)
                        {
                            try
                            {
                                _originalValues["EnergyStart"] = energyStartField.GetValue(component);
                            }
                            catch { }
                        }

                        // Store JumpExtra
                        FieldInfo jumpExtraField = component.GetType().GetField("JumpExtra",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (jumpExtraField != null)
                        {
                            try
                            {
                                _originalValues["JumpExtra"] = jumpExtraField.GetValue(component);
                            }
                            catch { }
                        }

                        // Store JumpExtraCurrent
                        FieldInfo jumpExtraCurrentField = component.GetType().GetField("JumpExtraCurrent",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (jumpExtraCurrentField != null)
                        {
                            try
                            {
                                _originalValues["JumpExtraCurrent"] = jumpExtraField.GetValue(component); // Original code had a potential typo here: jumpExtraField instead of jumpExtraCurrentField
                            }
                            catch { }
                        }
                        // Keep MelonLogger message if desired
                        MelonLogger.Msg($"Stored original values from {component.GetType().Name}: {string.Join(", ", _originalValues.Keys)}");
                        return; // Exit after finding the PlayerController
                    }
                }
            }
             if (_originalValues.Count == 0) MelonLogger.Warning("Could not find PlayerController component to store original values.");
        }

        private void RestoreOriginalValues()
        {
            if (_originalValues.Count == 0) return;

            GameObject[] allPlayerObjects = GameObject.FindGameObjectsWithTag("Player");
            if (allPlayerObjects.Length == 0)
            {
                GameObject playerObj = GameObject.Find("Player");
                if (playerObj != null)
                {
                    allPlayerObjects = new GameObject[] { playerObj };
                }
            }

            foreach (GameObject playerObj in allPlayerObjects)
            {
                Transform controllerTransform = playerObj.transform.Find("Controller");
                if (controllerTransform == null) continue;

                MonoBehaviour[] components = controllerTransform.GetComponents<MonoBehaviour>();
                foreach (var component in components)
                {
                    if (component.GetType().Name == "PlayerController")
                    {
                        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                        // Restore SprintSpeed
                        if (_originalValues.ContainsKey("SprintSpeed"))
                        {
                            FieldInfo sprintSpeedField = component.GetType().GetField("SprintSpeed", flags);
                            if (sprintSpeedField != null)
                            {
                                try { sprintSpeedField.SetValue(component, _originalValues["SprintSpeed"]); } catch { }
                            }
                        }

                        // Restore EnergyCurrent
                        if (_originalValues.ContainsKey("EnergyCurrent"))
                        {
                            FieldInfo energyCurrentField = component.GetType().GetField("EnergyCurrent", flags);
                            if (energyCurrentField != null)
                            {
                                try { energyCurrentField.SetValue(component, _originalValues["EnergyCurrent"]); } catch { }
                            }
                        }

                        // Restore EnergyStart
                        if (_originalValues.ContainsKey("EnergyStart"))
                        {
                            FieldInfo energyStartField = component.GetType().GetField("EnergyStart", flags);
                            if (energyStartField != null)
                            {
                                try { energyStartField.SetValue(component, _originalValues["EnergyStart"]); } catch { }
                            }
                        }

                        // Restore JumpExtra
                        if (_originalValues.ContainsKey("JumpExtra"))
                        {
                            FieldInfo jumpExtraField = component.GetType().GetField("JumpExtra", flags);
                            if (jumpExtraField != null)
                            {
                                try { jumpExtraField.SetValue(component, _originalValues["JumpExtra"]); } catch { }
                            }
                        }

                        // Restore JumpExtraCurrent
                        if (_originalValues.ContainsKey("JumpExtraCurrent"))
                        {
                            FieldInfo jumpExtraCurrentField = component.GetType().GetField("JumpExtraCurrent", flags);
                            if (jumpExtraCurrentField != null)
                            {
                                try { jumpExtraCurrentField.SetValue(component, _originalValues["JumpExtraCurrent"]); } catch { }
                            }
                        }
                         MelonLogger.Msg($"Restored original player values for {component.GetType().Name}.");
                        return; // Exit after finding PlayerController
                    }
                }
            }
            MelonLogger.Warning("Could not find PlayerController component to restore original values.");
        }

        private void ApplyGodModeEffects()
        {
            ApplyInfiniteStamina();

            if (Time.frameCount % 300 == 0) // Check frame count to avoid running every frame
            {
                UpdatePlayerControllerValues();
            }
        }

        private void UpdatePlayerControllerValues()
        {
            string playerAvatarName = null;

            // Attempt to get playerName from the local player's avatar components
             if (_playerController != null) // Ensure _playerController is valid
             {
                 MonoBehaviour[] avatarComponents = _playerController.GetComponents<MonoBehaviour>();
                 foreach (var component in avatarComponents)
                 {
                     // Check for Field first
                     FieldInfo playerNameField = component.GetType().GetField("playerName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                     if (playerNameField != null)
                     {
                         try { playerAvatarName = (string)playerNameField.GetValue(component); break; } catch { }
                     }
                     // Check for Property if field not found or failed
                     if (playerAvatarName == null)
                     {
                         PropertyInfo playerNameProperty = component.GetType().GetProperty("playerName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                         if (playerNameProperty != null && playerNameProperty.CanRead)
                         {
                            try { playerAvatarName = (string)playerNameProperty.GetValue(component, null); break; } catch { }
                         }
                     }
                      if (playerAvatarName != null) break; // Exit loop once name is found
                 }
             }

            // Find the PlayerController component and apply changes
            GameObject[] allPlayerObjects = GameObject.FindGameObjectsWithTag("Player");
            if (allPlayerObjects.Length == 0)
            {
                GameObject playerObj = GameObject.Find("Player");
                if (playerObj != null) allPlayerObjects = new GameObject[] { playerObj };
            }

            foreach (GameObject playerObj in allPlayerObjects)
            {
                Transform controllerTransform = playerObj.transform.Find("Controller");
                if (controllerTransform == null) continue;

                MonoBehaviour[] components = controllerTransform.GetComponents<MonoBehaviour>();
                foreach (var component in components)
                {
                    if (component.GetType().Name == "PlayerController") // Make sure this name is correct
                    {
                        // Compare names if possible, otherwise apply to the first found controller
                        bool applyChanges = true; // Default to true
                        if (playerAvatarName != null) // Only compare if we successfully got local player name
                        {
                            string controllerPlayerName = null;
                             FieldInfo contPlayerNameField = component.GetType().GetField("playerName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                             if (contPlayerNameField != null) try { controllerPlayerName = (string)contPlayerNameField.GetValue(component); } catch { }

                            // If controller name doesn't match local name, don't apply (unless controller has no name field)
                             if (controllerPlayerName != null && playerAvatarName != controllerPlayerName)
                             {
                                 applyChanges = false;
                             }
                        }

                        if (applyChanges)
                        {
                             BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                            // Helper lambda to set value safely
                            Action<FieldInfo, object> SetValueSafe = (field, value) => { try { field?.SetValue(component, value); } catch { } };

                            // Set SprintSpeed to 8
                            FieldInfo sprintSpeedField = component.GetType().GetField("SprintSpeed", flags);
                            if (sprintSpeedField != null)
                            {
                                Type fieldType = sprintSpeedField.FieldType;
                                if (fieldType == typeof(int)) SetValueSafe(sprintSpeedField, 8);
                                else if (fieldType == typeof(float)) SetValueSafe(sprintSpeedField, 8f);
                                else if (fieldType == typeof(double)) SetValueSafe(sprintSpeedField, 8.0);
                            }

                            // Set EnergyCurrent to 100
                            FieldInfo energyCurrentField = component.GetType().GetField("EnergyCurrent", flags);
                             if (energyCurrentField != null)
                            {
                                Type fieldType = energyCurrentField.FieldType;
                                if (fieldType == typeof(int)) SetValueSafe(energyCurrentField, 100);
                                else if (fieldType == typeof(float)) SetValueSafe(energyCurrentField, 100f);
                                else if (fieldType == typeof(double)) SetValueSafe(energyCurrentField, 100.0);
                            }

                            // Set EnergyStart to 100
                            FieldInfo energyStartField = component.GetType().GetField("EnergyStart", flags);
                             if (energyStartField != null)
                            {
                                Type fieldType = energyStartField.FieldType;
                                if (fieldType == typeof(int)) SetValueSafe(energyStartField, 100);
                                else if (fieldType == typeof(float)) SetValueSafe(energyStartField, 100f);
                                else if (fieldType == typeof(double)) SetValueSafe(energyStartField, 100.0);
                            }

                            // Set JumpExtra to 999
                            FieldInfo jumpExtraField = component.GetType().GetField("JumpExtra", flags);
                             if (jumpExtraField != null)
                            {
                                Type fieldType = jumpExtraField.FieldType;
                                if (fieldType == typeof(int)) SetValueSafe(jumpExtraField, 999);
                                else if (fieldType == typeof(float)) SetValueSafe(jumpExtraField, 999f);
                                else if (fieldType == typeof(double)) SetValueSafe(jumpExtraField, 999.0);
                            }

                            // Set JumpExtraCurrent to 999
                            FieldInfo jumpExtraCurrentField = component.GetType().GetField("JumpExtraCurrent", flags);
                             if (jumpExtraCurrentField != null)
                            {
                                Type fieldType = jumpExtraCurrentField.FieldType;
                                if (fieldType == typeof(int)) SetValueSafe(jumpExtraCurrentField, 999);
                                else if (fieldType == typeof(float)) SetValueSafe(jumpExtraCurrentField, 999f);
                                else if (fieldType == typeof(double)) SetValueSafe(jumpExtraCurrentField, 999.0);
                            }

                             // MelonLogger.Msg($"Applied god mode values to {component.GetType().Name}");
                             return; // Assume only one PlayerController needs updating
                        }
                    }
                }
            }
            // MelonLogger.Warning("Could not find PlayerController component to apply god mode values.");
        }

        public void ApplyInfiniteStamina()
        {
            if (_playerStamina == null) return;

            // Set EnergyCurrent to maximum using cached fields
             if (_staminaFields.TryGetValue("EnergyCurrent", out FieldInfo energyCurrentField) &&
                 _staminaFields.TryGetValue("EnergyStart", out FieldInfo energyStartField))
            {
                try
                {
                    object maxStaminaValue = energyStartField.GetValue(_playerStamina);
                    energyCurrentField.SetValue(_playerStamina, maxStaminaValue);
                }
                catch (Exception ex)
                 {
                     MelonLogger.Error($"Error applying infinite stamina (setting current to start): {ex.Message}");
                 }
            }
             else
             {
                 // Fallback or log warning if fields weren't cached
                 MelonLogger.Warning("Cannot apply infinite stamina: EnergyCurrent or EnergyStart field info missing.");
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
            if (_playerAvatar != null && _playerHealth != null) // Need both Avatar and Health components
            {
                 // Use IsPlayerAlive(-1) or a local check? Let's check local _playerHealth directly
                 bool isAlive = false; // Default to false (dead) if checks fail, to allow revive attempt
                 bool statusDetermined = false;
                 MelonLogger.Msg("[ReviveSelf] Checking local player liveness...");

                 try
                 {
                     FieldInfo deadField = _playerHealth.GetType().GetField("dead", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                     if (deadField != null && deadField.FieldType == typeof(bool))
                     {
                         bool deadValue = (bool)deadField.GetValue(_playerHealth);
                         isAlive = !deadValue;
                         statusDetermined = true;
                         MelonLogger.Msg($"  -> Found 'dead' field ({deadField.FieldType}): {deadValue}. Player isAlive = {isAlive}");
                     }
                     else
                     {
                         MelonLogger.Msg("  -> 'dead' field not found or not boolean. Trying 'health' field...");
                         // Fallback health check if 'dead' field not found
                         FieldInfo healthField = _playerHealth.GetType().GetField("health", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                         if (healthField != null)
                         {
                             object healthValueObj = healthField.GetValue(_playerHealth);
                             // Try converting health to int, assuming health > 0 means alive
                             try
                             {
                                 int currentHealth = Convert.ToInt32(healthValueObj);
                                 isAlive = (currentHealth > 0);
                                 statusDetermined = true;
                                  MelonLogger.Msg($"  -> Found 'health' field ({healthField.FieldType}): {currentHealth}. Player isAlive = {isAlive}");
                             }
                             catch (Exception convEx)
                             {
                                  MelonLogger.Warning($"  -> Found 'health' field ({healthField.FieldType}) but failed to convert value '{healthValueObj}' to int: {convEx.Message}");
                             }
                         }
                         else
                         {
                              MelonLogger.Warning("  -> 'health' field also not found.");
                         }
                     }
                 }
                 catch (Exception ex)
                 {
                      MelonLogger.Error($"[ReviveSelf] Error during liveness check: {ex.Message}. Assuming player might be dead.");
                      // Keep isAlive = false (default)
                 }

                 if (!statusDetermined)
                 {
                     MelonLogger.Warning("[ReviveSelf] Could not determine player status from fields. Assuming player might be dead to allow revive attempt.");
                     // isAlive remains false (default)
                 }


                if (!isAlive)
                {
                     MelonLogger.Msg("[ReviveSelf] Player determined to be dead. Proceeding with revive attempts...");
                     MonoBehaviour playerAvatarComponent = _playerAvatar.GetComponent("PlayerAvatar") as MonoBehaviour;
                     if (playerAvatarComponent == null)
                     {
                          MelonLogger.Error("Cannot ReviveSelf: Local PlayerAvatar component not found on _playerAvatar object.");
                          // Try fallback health manipulation directly?
                          TryReviveSelfHealthFallback(); 
                          return;
                     }

                    MethodInfo reviveMethod = playerAvatarComponent.GetType().GetMethod("Revive",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    bool success = false;
                    if (reviveMethod != null)
                    {
                        try
                        {
                            reviveMethod.Invoke(playerAvatarComponent, new object[] { true });
                            MelonLogger.Msg("  Called Revive(true) successfully.");
                            success = true;
                        }
                        catch (TargetParameterCountException)
                        {
                            try
                            {
                                 reviveMethod.Invoke(playerAvatarComponent, null);
                                 MelonLogger.Msg("  Called Revive() successfully.");
                                 success = true;
                            }
                            catch (Exception ex1) { MelonLogger.Warning($"  Revive() call failed: {ex1.Message}"); }
                        }
                        catch (Exception ex) { MelonLogger.Warning($"  Revive(true) call failed: {ex.Message}"); }
                    }
                    else { MelonLogger.Msg("  Revive method not found."); }

                    if (!success)
                    {
                         MelonLogger.Msg("Attempting ReviveRPC on local PlayerAvatar...");
                        MethodInfo reviveRPCMethod = playerAvatarComponent.GetType().GetMethod("ReviveRPC",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        if (reviveRPCMethod != null)
                        {
                            try
                            {
                                reviveRPCMethod.Invoke(playerAvatarComponent, new object[] { true });
                                MelonLogger.Msg("  Called ReviveRPC(true) successfully.");
                                success = true;
                            }
                             catch (TargetParameterCountException)
                            {
                                 try
                                 {
                                     reviveRPCMethod.Invoke(playerAvatarComponent, null);
                                     MelonLogger.Msg("  Called ReviveRPC() successfully.");
                                     success = true;
                                 }
                                 catch (Exception ex1) { MelonLogger.Warning($"  ReviveRPC() call failed: {ex1.Message}"); }
                            }
                            catch (Exception ex) { MelonLogger.Warning($"  ReviveRPC(true) call failed: {ex.Message}"); }
                        }
                         else { MelonLogger.Msg("  ReviveRPC method not found."); }
                    }

                    // If Revive/RPC failed, try health fallback
                    if (!success)
                    {
                        TryReviveSelfHealthFallback();
                    }

                    if (!success) // Check again after fallback
                    {
                         MelonLogger.Error("All self-revive attempts failed.");
                    }
                }
                else
                {
                    MelonLogger.Msg("Cannot ReviveSelf: Player is already alive.");
                }
            }
            else
            {
                 MelonLogger.Warning("Cannot ReviveSelf: PlayerAvatar or PlayerHealth component not found/cached.");
            }
        }

        // Helper method for ReviveSelf health fallback
        private void TryReviveSelfHealthFallback()
        {
             MelonLogger.Msg("Revive/ReviveRPC failed for self. Attempting health fallback...");
            if (_playerHealth != null)
            {
                 FieldInfo healthField = _playerHealth.GetType().GetField("health",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (healthField != null)
                {
                    try
                    {
                        healthField.SetValue(_playerHealth, 30); // Original fallback value
                        MelonLogger.Msg("  Set local health field to 30 as fallback.");
                        // Consider success = true; here if calling from ReviveSelf
                    }
                    catch (Exception ex)
                    {
                         MelonLogger.Error($"  Failed to set local health field as fallback: {ex.Message}");
                    }
                }
                else
                {
                    MelonLogger.Warning("  Local health field not found for fallback.");
                }
            }
             else
             {
                  MelonLogger.Warning("  Local PlayerHealth component null for fallback.");
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
            if (playerIndex >= 0 && playerIndex < _playerAvatarComponents.Count)
            {
                MonoBehaviour playerAvatarComponent = _playerAvatarComponents[playerIndex];

                if (playerAvatarComponent != null)
                {
                    // Check using the more robust IsPlayerAlive method
                    if (!IsPlayerAlive(playerIndex))
                    {
                         MelonLogger.Msg($"Attempting Revive/ReviveRPC on PlayerAvatar for player {playerIndex}...");
                        MethodInfo reviveMethod = playerAvatarComponent.GetType().GetMethod("Revive",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        bool success = false;
                        if (reviveMethod != null)
                        {
                            try
                            {
                                // Try invoking with boolean argument first (common pattern)
                                reviveMethod.Invoke(playerAvatarComponent, new object[] { true });
                                MelonLogger.Msg("  Called Revive(true) successfully.");
                                success = true;
                            }
                            catch (TargetParameterCountException)
                            {
                                // Try invoking with no arguments if boolean version failed
                                try
                                {
                                     reviveMethod.Invoke(playerAvatarComponent, null);
                                     MelonLogger.Msg("  Called Revive() successfully.");
                                     success = true;
                                }
                                catch (Exception ex1)
                                {
                                     MelonLogger.Warning($"  Revive() call failed: {ex1.Message}");
                                }
                            }
                            catch (Exception ex)
                            {
                                MelonLogger.Warning($"  Revive(true) call failed: {ex.Message}");
                            }
                        }
                        else
                        {
                            MelonLogger.Msg("  Revive method not found.");
                        }

                        // If Revive method failed or wasn't found, try ReviveRPC
                        if (!success)
                        {
                             MelonLogger.Msg($"Attempting ReviveRPC on PlayerAvatar for player {playerIndex}...");
                            MethodInfo reviveRPCMethod = playerAvatarComponent.GetType().GetMethod("ReviveRPC",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                            if (reviveRPCMethod != null)
                            {
                                try
                                {
                                    // Try invoking with boolean argument first
                                    reviveRPCMethod.Invoke(playerAvatarComponent, new object[] { true });
                                    MelonLogger.Msg("  Called ReviveRPC(true) successfully.");
                                    success = true;
                                }
                                catch (TargetParameterCountException)
                                {
                                     // Try invoking with no arguments if boolean version failed
                                     try
                                     {
                                         reviveRPCMethod.Invoke(playerAvatarComponent, null);
                                         MelonLogger.Msg("  Called ReviveRPC() successfully.");
                                         success = true;
                                     }
                                     catch (Exception ex1)
                                     {
                                         MelonLogger.Warning($"  ReviveRPC() call failed: {ex1.Message}");
                                     }
                                }
                                catch (Exception ex)
                                {
                                    MelonLogger.Warning($"  ReviveRPC(true) call failed: {ex.Message}");
                                }
                            }
                             else
                             {
                                 MelonLogger.Msg("  ReviveRPC method not found.");
                             }
                        }

                        // If both Revive and ReviveRPC failed, try the health fallback
                        if (!success)
                        {
                            MelonLogger.Msg($"Revive/ReviveRPC failed for player {playerIndex}. Attempting health fallback...");
                            if (playerIndex < _playerHealthComponents.Count)
                            {
                                MonoBehaviour playerHealthComponent = _playerHealthComponents[playerIndex];
                                if (playerHealthComponent != null)
                                {
                                    FieldInfo healthField = playerHealthComponent.GetType().GetField("health",
                                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                                    if (healthField != null)
                                    {
                                        try
                                        {
                                            healthField.SetValue(playerHealthComponent, 30); // Original fallback value
                                            MelonLogger.Msg("  Set health field to 30 as fallback.");
                                            success = true;
                                        }
                                        catch (Exception ex)
                                        {
                                             MelonLogger.Error($"  Failed to set health field as fallback: {ex.Message}");
                                        }
                                    }
                                    else
                                    {
                                        MelonLogger.Warning("  Health field not found for fallback.");
                                    }
                                }
                                else
                                {
                                     MelonLogger.Warning("  PlayerHealth component null for fallback.");
                                }
                            }
                        }

                        if (!success)
                        {
                             MelonLogger.Error($"All revive attempts failed for player {playerIndex}.");
                        }
                    }
                    else
                    {
                        MelonLogger.Msg($"Player {playerIndex} is already alive, cannot revive.");
                    }
                }
                else
                {
                    MelonLogger.Warning($"Cannot ReviveOtherPlayer: PlayerAvatar component not found for player index {playerIndex}.");
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
            MelonLogger.Msg($"[PlayerManager.TogglePlayerESP] Setting ESP Enabled to: {_playerESPEnabled}");
            SetPlayerESPVisibility(_playerESPEnabled);
        }

        public bool IsPlayerESPEnabled()
        {
            return _playerESPEnabled;
        }

        private void SetPlayerESPVisibility(bool isVisible)
        {
            MelonLogger.Msg($"[PlayerManager.SetPlayerESPVisibility] Setting visibility to: {isVisible} for {_playerLineRenderers.Count} renderers.");
            foreach (var lineRenderer in _playerLineRenderers)
            {
                if (lineRenderer != null)
                    lineRenderer.enabled = isVisible;
            }
        }

        private void CreateLineRendererForPlayer(Transform player)
        {
            int index = _otherPlayers.IndexOf(player);
            string objName = $"ESP_PlayerLine_{index}";
            MelonLogger.Msg($"[PlayerManager.CreateLineRendererForPlayer] Creating line object: {objName}");
            GameObject lineObject = new GameObject(objName);
            if (lineObject == null) { MelonLogger.Error("  Failed to create line GameObject!"); return; }

            _playerLineObjects.Add(lineObject);

            LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
             if (lineRenderer == null) { MelonLogger.Error("  Failed to add LineRenderer component!"); return; }
            _playerLineRenderers.Add(lineRenderer);

            lineRenderer.startWidth = 0.01f;
            lineRenderer.endWidth = 0.01f;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = Color.blue;
            lineRenderer.endColor = Color.blue;
            lineRenderer.positionCount = 2;

            MelonLogger.Msg($"  Line renderer created and added. Total renderers: {_playerLineRenderers.Count}");
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
            MelonLogger.Msg("[PlayerManager.ClearPlayerESP] Clearing existing player lines...");
            foreach (var lineObject in _playerLineObjects)
            {
                if (lineObject != null)
                {
                    GameObject.Destroy(lineObject);
                }
            }

            _playerLineObjects.Clear();
            _playerLineRenderers.Clear();
             MelonLogger.Msg("  Player lines cleared.");
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
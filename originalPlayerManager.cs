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
            GameObject[] allPlayerAvatars = GameObject.FindObjectsOfType<GameObject>()
                .Where(go => go.name == "PlayerAvatar(Clone)" && go.transform.childCount == 10)
                .ToArray();

            foreach (var playerObj in allPlayerAvatars)
            {
                if (playerObj.transform == _playerAvatar) continue;

                Transform playerTransform = playerObj.transform;
                if (!_otherPlayers.Contains(playerTransform))
                {
                    _otherPlayers.Add(playerTransform);

                    Transform playerController = playerTransform.Find("Player Avatar Controller");
                    if (playerController != null)
                    {
                        MonoBehaviour[] components = playerController.GetComponents<MonoBehaviour>();
                        MonoBehaviour healthComponent = null;
                        MonoBehaviour avatarComponent = null;

                        foreach (var component in components)
                        {
                            string typeName = component.GetType().Name;
                            if (typeName == "PlayerHealth")
                            {
                                healthComponent = component;
                            }
                            else if (typeName == "PlayerAvatar")
                            {
                                avatarComponent = component;
                            }
                        }

                        _playerHealthComponents.Add(healthComponent);
                        _playerAvatarComponents.Add(avatarComponent);
                    }
                    else
                    {
                        _playerHealthComponents.Add(null);
                        _playerAvatarComponents.Add(null);
                    }

                    CreateLineRendererForPlayer(playerTransform);
                }
            }
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
                        // Store MoveSpeed
                        FieldInfo moveSpeedField = component.GetType().GetField("MoveSpeed",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (moveSpeedField != null)
                        {
                            try
                            {
                                _originalValues["MoveSpeed"] = moveSpeedField.GetValue(component);
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
                                _originalValues["JumpExtraCurrent"] = jumpExtraField.GetValue(component);
                            }
                            catch { }
                        }
                    }
                }
            }
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
                        // Restore MoveSpeed
                        if (_originalValues.ContainsKey("MoveSpeed"))
                        {
                            FieldInfo moveSpeedField = component.GetType().GetField("MoveSpeed",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (moveSpeedField != null)
                            {
                                try
                                {
                                    moveSpeedField.SetValue(component, _originalValues["MoveSpeed"]);
                                }
                                catch { }
                            }
                        }

                        // Restore EnergyCurrent
                        if (_originalValues.ContainsKey("EnergyCurrent"))
                        {
                            FieldInfo energyCurrentField = component.GetType().GetField("EnergyCurrent",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (energyCurrentField != null)
                            {
                                try
                                {
                                    energyCurrentField.SetValue(component, _originalValues["EnergyCurrent"]);
                                }
                                catch { }
                            }
                        }

                        // Restore EnergyStart
                        if (_originalValues.ContainsKey("EnergyStart"))
                        {
                            FieldInfo energyStartField = component.GetType().GetField("EnergyStart",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (energyStartField != null)
                            {
                                try
                                {
                                    energyStartField.SetValue(component, _originalValues["EnergyStart"]);
                                }
                                catch { }
                            }
                        }

                        // Restore JumpExtra
                        if (_originalValues.ContainsKey("JumpExtra"))
                        {
                            FieldInfo jumpExtraField = component.GetType().GetField("JumpExtra",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (jumpExtraField != null)
                            {
                                try
                                {
                                    jumpExtraField.SetValue(component, _originalValues["JumpExtra"]);
                                }
                                catch { }
                            }
                        }

                        // Restore JumpExtraCurrent
                        if (_originalValues.ContainsKey("JumpExtraCurrent"))
                        {
                            FieldInfo jumpExtraCurrentField = component.GetType().GetField("JumpExtraCurrent",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (jumpExtraCurrentField != null)
                            {
                                try
                                {
                                    jumpExtraCurrentField.SetValue(component, _originalValues["JumpExtraCurrent"]);
                                }
                                catch { }
                            }
                        }
                    }
                }
            }

            MelonLogger.Msg("Restored original player values");
        }

        private void ApplyGodModeEffects()
        {
            ApplyInfiniteStamina();

            if (Time.frameCount % 300 == 0)
            {
                UpdatePlayerControllerValues();
            }
        }

        private void UpdatePlayerControllerValues()
        {
            string playerAvatarName = null;

            MonoBehaviour[] avatarComponents = _playerController.GetComponents<MonoBehaviour>();
            foreach (var component in avatarComponents)
            {
                FieldInfo playerNameField = component.GetType().GetField("playerName",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (playerNameField != null)
                {
                    try
                    {
                        playerAvatarName = (string)playerNameField.GetValue(component);
                        break;
                    }
                    catch { }
                }

                PropertyInfo playerNameProperty = component.GetType().GetProperty("playerName",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (playerNameProperty != null && playerNameProperty.CanRead)
                {
                    try
                    {
                        playerAvatarName = (string)playerNameProperty.GetValue(component, null);
                        break;
                    }
                    catch { }
                }
            }

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
                        string controllerPlayerName = null;
                        FieldInfo playerNameField = component.GetType().GetField("playerName",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        if (playerNameField != null)
                        {
                            try
                            {
                                controllerPlayerName = (string)playerNameField.GetValue(component);
                            }
                            catch { }
                        }

                        if ((playerAvatarName == null || controllerPlayerName == null || playerAvatarName == controllerPlayerName))
                        {
                            // Directly set the MoveSpeed field to 10 (instead of 5)
                            FieldInfo moveSpeedField = component.GetType().GetField("MoveSpeed",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (moveSpeedField != null)
                            {
                                try
                                {
                                    Type fieldType = moveSpeedField.FieldType;

                                    if (fieldType == typeof(int) || fieldType == typeof(Int32))
                                    {
                                        moveSpeedField.SetValue(component, 10);
                                    }
                                    else if (fieldType == typeof(float) || fieldType == typeof(Single))
                                    {
                                        moveSpeedField.SetValue(component, 10f);
                                    }
                                    else if (fieldType == typeof(double))
                                    {
                                        moveSpeedField.SetValue(component, 10.0);
                                    }
                                }
                                catch { }
                            }

                            // Directly set EnergyCurrent
                            FieldInfo energyCurrentField = component.GetType().GetField("EnergyCurrent",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (energyCurrentField != null)
                            {
                                try
                                {
                                    Type fieldType = energyCurrentField.FieldType;

                                    if (fieldType == typeof(int) || fieldType == typeof(Int32))
                                    {
                                        energyCurrentField.SetValue(component, 9999);
                                    }
                                    else if (fieldType == typeof(float) || fieldType == typeof(Single))
                                    {
                                        energyCurrentField.SetValue(component, 9999f);
                                    }
                                    else if (fieldType == typeof(double))
                                    {
                                        energyCurrentField.SetValue(component, 9999.0);
                                    }
                                }
                                catch { }
                            }

                            // Directly set EnergyStart
                            FieldInfo energyStartField = component.GetType().GetField("EnergyStart",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (energyStartField != null)
                            {
                                try
                                {
                                    Type fieldType = energyStartField.FieldType;

                                    if (fieldType == typeof(int) || fieldType == typeof(Int32))
                                    {
                                        energyStartField.SetValue(component, 9999);
                                    }
                                    else if (fieldType == typeof(float) || fieldType == typeof(Single))
                                    {
                                        energyStartField.SetValue(component, 9999f);
                                    }
                                    else if (fieldType == typeof(double))
                                    {
                                        energyStartField.SetValue(component, 9999.0);
                                    }
                                }
                                catch { }
                            }

                            // Directly set JumpExtra
                            FieldInfo jumpExtraField = component.GetType().GetField("JumpExtra",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (jumpExtraField != null)
                            {
                                try
                                {
                                    Type fieldType = jumpExtraField.FieldType;

                                    if (fieldType == typeof(int) || fieldType == typeof(Int32))
                                    {
                                        jumpExtraField.SetValue(component, 999);
                                    }
                                    else if (fieldType == typeof(float) || fieldType == typeof(Single))
                                    {
                                        jumpExtraField.SetValue(component, 999f);
                                    }
                                    else if (fieldType == typeof(double))
                                    {
                                        jumpExtraField.SetValue(component, 999.0);
                                    }
                                }
                                catch { }
                            }

                            // Directly set JumpExtraCurrent
                            FieldInfo jumpExtraCurrentField = component.GetType().GetField("JumpExtraCurrent",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (jumpExtraCurrentField != null)
                            {
                                try
                                {
                                    Type fieldType = jumpExtraCurrentField.FieldType;

                                    if (fieldType == typeof(int) || fieldType == typeof(Int32))
                                    {
                                        jumpExtraCurrentField.SetValue(component, 999);
                                    }
                                    else if (fieldType == typeof(float) || fieldType == typeof(Single))
                                    {
                                        jumpExtraCurrentField.SetValue(component, 999f);
                                    }
                                    else if (fieldType == typeof(double))
                                    {
                                        jumpExtraCurrentField.SetValue(component, 999.0);
                                    }
                                }
                                catch { }
                            }

                            return;
                        }
                    }
                }
            }
        }

        public void ApplyInfiniteStamina()
        {
            if (_playerStamina == null) return;

            // Set EnergyCurrent to maximum
            FieldInfo energyCurrentField = _staminaFields["EnergyCurrent"];
            FieldInfo energyStartField = _staminaFields["EnergyStart"];

            if (energyCurrentField != null && energyStartField != null)
            {
                try
                {
                    // Get the max stamina value from EnergyStart field
                    object maxStaminaValue = energyStartField.GetValue(_playerStamina);

                    // Set the current stamina to max
                    energyCurrentField.SetValue(_playerStamina, maxStaminaValue);
                }
                catch { }
            }
        }

        public void HealPlayer(int playerIndex)
        {
            if (playerIndex >= 0 && playerIndex < _playerHealthComponents.Count)
            {
                MonoBehaviour playerHealthComponent = _playerHealthComponents[playerIndex];

                if (playerHealthComponent != null)
                {
                    FieldInfo currentHealthField = playerHealthComponent.GetType().GetField("health",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (currentHealthField != null)
                    {
                        int currentHealth = (int)currentHealthField.GetValue(playerHealthComponent);

                        if (currentHealth > 0)
                        {
                            int addHealth = 10;
                            if (currentHealth + addHealth > 100)
                            {
                                addHealth = 100 - currentHealth;
                            }

                            int newHealth = currentHealth + addHealth;
                            currentHealthField.SetValue(playerHealthComponent, newHealth);
                        }
                    }
                }
            }
        }

        public void KillPlayer(int playerIndex)
        {
            if (playerIndex >= 0 && playerIndex < _playerHealthComponents.Count)
            {
                MonoBehaviour playerHealthComponent = _playerHealthComponents[playerIndex];

                if (playerHealthComponent != null)
                {
                    MethodInfo killMethod = playerHealthComponent.GetType().GetMethod("TakeDamage",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (killMethod != null)
                    {
                        try
                        {
                            try
                            {
                                FieldInfo currentHealthField = playerHealthComponent.GetType().GetField("health",
                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                                int currentHealth = 100;
                                if (currentHealthField != null)
                                {
                                    currentHealth = (int)currentHealthField.GetValue(playerHealthComponent);
                                }

                                killMethod.Invoke(playerHealthComponent, new object[] { currentHealth + 1000 });
                                return;
                            }
                            catch { }
                        }
                        catch { }
                    }

                    MethodInfo dieMethod = playerHealthComponent.GetType().GetMethod("Die",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (dieMethod != null)
                    {
                        try
                        {
                            try
                            {
                                dieMethod.Invoke(playerHealthComponent, null);
                                return;
                            }
                            catch
                            {
                                dieMethod.Invoke(playerHealthComponent, new object[] { true });
                                return;
                            }
                        }
                        catch { }
                    }

                    FieldInfo playerHealthField = playerHealthComponent.GetType().GetField("health",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (playerHealthField != null)
                    {
                        playerHealthField.SetValue(playerHealthComponent, 0);
                    }
                }
            }
        }

        public void RevivePlayer(int playerIndex)
        {
            if (playerIndex >= 0 && playerIndex < _playerAvatarComponents.Count)
            {
                MonoBehaviour playerAvatarComponent = _playerAvatarComponents[playerIndex];

                if (playerAvatarComponent != null)
                {
                    if (!IsPlayerAlive(playerIndex))
                    {
                        MethodInfo reviveMethod = playerAvatarComponent.GetType().GetMethod("Revive",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        if (reviveMethod != null)
                        {
                            try
                            {
                                reviveMethod.Invoke(playerAvatarComponent, new object[] { true });
                            }
                            catch
                            {
                                MethodInfo reviveRPCMethod = playerAvatarComponent.GetType().GetMethod("ReviveRPC",
                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                                if (reviveRPCMethod != null)
                                {
                                    try
                                    {
                                        reviveRPCMethod.Invoke(playerAvatarComponent, new object[] { true });
                                    }
                                    catch
                                    {
                                        if (playerIndex < _playerHealthComponents.Count)
                                        {
                                            MonoBehaviour playerHealthComponent = _playerHealthComponents[playerIndex];
                                            if (playerHealthComponent != null)
                                            {
                                                FieldInfo healthField = playerHealthComponent.GetType().GetField("health",
                                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                                                if (healthField != null)
                                                {
                                                    healthField.SetValue(playerHealthComponent, 30);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
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
            if (playerIndex >= 0 && playerIndex < _playerHealthComponents.Count)
            {
                MonoBehaviour playerHealthComponent = _playerHealthComponents[playerIndex];

                if (playerHealthComponent != null)
                {
                    FieldInfo healthField = playerHealthComponent.GetType().GetField("health",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (healthField != null)
                    {
                        int currentHealth = (int)healthField.GetValue(playerHealthComponent);
                        return currentHealth > 0;
                    }
                }
            }

            return false;
        }

        public int GetPlayerHealth(int playerIndex)
        {
            if (playerIndex >= 0 && playerIndex < _playerHealthComponents.Count)
            {
                MonoBehaviour playerHealthComponent = _playerHealthComponents[playerIndex];

                if (playerHealthComponent != null)
                {
                    FieldInfo healthField = playerHealthComponent.GetType().GetField("health",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (healthField != null)
                    {
                        return (int)healthField.GetValue(playerHealthComponent);
                    }
                }
            }

            return 0;
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
using MelonLoader;
using UnityEngine;

namespace REPO_UTILS
{
    /// <summary>
    /// Manages the mod's user interface including:
    /// - Main window UI
    /// - Toggle controls
    /// - Player, enemy, and item lists
    /// </summary>
    public class UIManager
    {
        private Core _core;

        // Window parameters
        private int _rightPadding = 20;
        private Rect _mainWindowRect;
        private float _mainWindowWidth = 350;
        private float _minWindowHeight = 140;

        // UI state
        private bool _showEnemyList = true;
        private bool _showItemList = true;
        private bool _showPlayerList = true;
        private Vector2 _enemyListScrollPos = Vector2.zero;
        private Vector2 _itemListScrollPos = Vector2.zero;
        private Vector2 _playerListScrollPos = Vector2.zero;

        // Constructor
        public UIManager(Core core)
        {
            _core = core;
        }

        #region Initialization

        public void Initialize()
        {
            _mainWindowRect = new Rect(
                Screen.width - _mainWindowWidth - _rightPadding,
                100,
                _mainWindowWidth,
                _minWindowHeight
            );
        }

        public void Reset()
        {
            _showEnemyList = true;
            _showItemList = true;
            _showPlayerList = true;
            _enemyListScrollPos = Vector2.zero;
            _itemListScrollPos = Vector2.zero;
            _playerListScrollPos = Vector2.zero;
        }

        #endregion

        #region GUI Drawing

        public void DrawGUI()
        {
            // Calculate dynamic heights based on what's shown
            float toggleSectionHeight = 125f; // Increased height for Heal/Revive Self buttons
            float playerSectionHeight = _showPlayerList ? 200f : 25f;
            float enemySectionHeight = _showEnemyList ? 170f : 25f;
            float itemSectionHeight = _showItemList ? 170f : 25f;

            float totalHeight =
                toggleSectionHeight +
                playerSectionHeight +
                enemySectionHeight +
                itemSectionHeight +
                100f; // Extra padding

            float yPosition = (Screen.height - totalHeight) / 2;
            if (yPosition < 20) yPosition = 20;

            _mainWindowRect = new Rect(
                Screen.width - _mainWindowWidth - _rightPadding,
                yPosition,
                _mainWindowWidth,
                totalHeight
            );

            GUI.Box(_mainWindowRect, "R.E.P.O UTILS");

            float currentY = 25;

            DrawStatusToggles(_mainWindowRect.x + 10, _mainWindowRect.y + currentY);
            currentY += toggleSectionHeight;

            DrawPlayerSection(_mainWindowRect.x, _mainWindowRect.y, currentY, playerSectionHeight);
            currentY += playerSectionHeight;

            DrawEnemySection(_mainWindowRect.x, _mainWindowRect.y, currentY, enemySectionHeight);
            currentY += enemySectionHeight;

            DrawItemSection(_mainWindowRect.x, _mainWindowRect.y, currentY, itemSectionHeight);

            // Display logging status at the bottom
            GUI.Label(new Rect(_mainWindowRect.x + 10, _mainWindowRect.y + totalHeight - 20, 330, 20),
                     $"Logging: {(_core.Logger.IsLoggingEnabled() ? "ON" : "OFF")} (Toggle: K)");
        }

        private void DrawStatusToggles(float x, float y)
        {
            // God Mode toggle
            GUI.Label(new Rect(x, y, 80, 20), "God Mode:");
            if (GUI.Button(new Rect(x + 90, y, 60, 20), _core.PlayerManager.IsGodModeEnabled() ? "ON" : "OFF"))
            {
                _core.PlayerManager.ToggleGodMode();
            }
            GUI.Label(new Rect(x + 160, y, 80, 20), "(;)");

            // Enemy ESP toggle
            GUI.Label(new Rect(x, y + 25, 80, 20), "Enemy ESP:");
            if (GUI.Button(new Rect(x + 90, y + 25, 60, 20), _core.EnemyManager.IsEnemyESPEnabled() ? "ON" : "OFF"))
            {
                _core.EnemyManager.ToggleEnemyESP();
            }
            GUI.Label(new Rect(x + 160, y + 25, 80, 20), "(L)");

            // Item ESP toggle
            GUI.Label(new Rect(x, y + 50, 80, 20), "Item ESP:");
            if (GUI.Button(new Rect(x + 90, y + 50, 60, 20), _core.ItemManager.IsItemESPEnabled() ? "ON" : "OFF"))
            {
                _core.ItemManager.ToggleItemESP();
            }
            GUI.Label(new Rect(x + 160, y + 50, 80, 20), "(I)");

            // Player ESP toggle
            GUI.Label(new Rect(x, y + 75, 80, 20), "Player ESP:");
            if (GUI.Button(new Rect(x + 90, y + 75, 60, 20), _core.PlayerManager.IsPlayerESPEnabled() ? "ON" : "OFF"))
            {
                _core.PlayerManager.TogglePlayerESP();
            }
            GUI.Label(new Rect(x + 160, y + 75, 80, 20), "(P)");

            // Heal Self button
            if (GUI.Button(new Rect(x, y + 100, 100, 20), "Heal Self"))
            {
                 _core.PlayerManager.HealSelf();
            }

            // Revive Self button
            if (GUI.Button(new Rect(x + 110, y + 100, 100, 20), "Revive Self"))
            {
                 _core.PlayerManager.ReviveSelf();
            }

            // Give Gun button
            if (GUI.Button(new Rect(x + 220, y + 100, 50, 20), "Gun"))
            {
                _core.PlayerManager.GiveTranqGun();
            }
        }

        private void DrawPlayerSection(float baseX, float baseY, float currentY, float sectionHeight)
        {
            // Draw section header with toggle button
            GUI.Label(new Rect(baseX + 10, baseY + currentY, 200, 20), "Players:");
            if (GUI.Button(new Rect(baseX + _mainWindowRect.width - 70, baseY + currentY, 60, 20),
                          _showPlayerList ? "Hide" : "Show"))
            {
                _showPlayerList = !_showPlayerList;
            }
            currentY += 25;

            // Draw player list if visible
            if (_showPlayerList)
            {
                float playerListHeight = sectionHeight - 25f;
                DrawPlayerList(baseX + 10, baseY + currentY, _mainWindowRect.width - 20, playerListHeight);
            }
        }

        private void DrawEnemySection(float baseX, float baseY, float currentY, float sectionHeight)
        {
            // Draw section header with toggle button
            GUI.Label(new Rect(baseX + 10, baseY + currentY, 200, 20), "Enemies:");
            if (GUI.Button(new Rect(baseX + _mainWindowRect.width - 70, baseY + currentY, 60, 20),
                          _showEnemyList ? "Hide" : "Show"))
            {
                _showEnemyList = !_showEnemyList;
            }
            currentY += 25;

            // Draw enemy list if visible
            if (_showEnemyList)
            {
                float enemyListHeight = sectionHeight - 25f;
                DrawEnemyList(baseX + 10, baseY + currentY, _mainWindowRect.width - 20, enemyListHeight);
            }
        }

        private void DrawItemSection(float baseX, float baseY, float currentY, float sectionHeight)
        {
            // Show total value in the items label
            float totalValue = _core.ItemManager.CalculateTotalItemValue();
            GUI.Label(new Rect(baseX + 10, baseY + currentY, 280, 20),
                     $"Items: (Total Value: ${totalValue:N0})");

            if (GUI.Button(new Rect(baseX + _mainWindowRect.width - 70, baseY + currentY, 60, 20),
                          _showItemList ? "Hide" : "Show"))
            {
                _showItemList = !_showItemList;
            }
            currentY += 25;

            // Draw item list if visible
            if (_showItemList)
            {
                float itemListHeight = sectionHeight - 25f;
                DrawItemList(baseX + 10, baseY + currentY, _mainWindowRect.width - 20, itemListHeight);
            }
        }

        private void DrawPlayerList(float x, float y, float width, float height)
        {
            GUI.Box(new Rect(x, y, width, height), "");

            // Column headers
            GUI.Box(new Rect(x, y, width, 25), "");
            GUI.Label(new Rect(x + 5, y + 5, 100, 20), "Player");
            GUI.Label(new Rect(x + 100, y + 5, 60, 20), "Status");
            GUI.Label(new Rect(x + 160, y + 5, 40, 20), "HP");
            GUI.Label(new Rect(x + 210, y + 5, 130, 20), "Actions");

            var players = _core.PlayerManager.GetOtherPlayers();

            Rect viewRect = new Rect(x, y + 25, width, height - 25);
            Rect contentRect = new Rect(0, 0, width - 20, players.Count * 30);

            _playerListScrollPos = GUI.BeginScrollView(viewRect, _playerListScrollPos, contentRect);

            for (int i = 0; i < players.Count; i++)
            {
                bool isAlive = _core.PlayerManager.IsPlayerAlive(i);
                int playerHealth = _core.PlayerManager.GetPlayerHealth(i);
                string statusText = isAlive ? "Alive" : "Dead";
                string playerName = _core.PlayerManager.GetPlayerName(i); // Get player name

                GUI.Label(new Rect(5, i * 30, 90, 20), playerName); // Use player name

                GUIStyle statusStyle = new GUIStyle(GUI.skin.label);
                statusStyle.normal.textColor = isAlive ? Color.green : Color.red;
                GUI.Label(new Rect(100, i * 30, 60, 20), statusText, statusStyle);

                GUI.Label(new Rect(160, i * 30, 40, 20), $"{playerHealth}");

                // Heal button - only enabled if player is alive and below max health
                GUI.enabled = isAlive && playerHealth < 100;
                if (GUI.Button(new Rect(210, i * 30, 40, 20), "Heal"))
                {
                    _core.PlayerManager.HealOtherPlayer(i); // Call HealOtherPlayer
                }

                // Revive button - only enabled if player is dead
                GUI.enabled = !isAlive;
                if (GUI.Button(new Rect(255, i * 30, 50, 20), "Revive"))
                {
                    _core.PlayerManager.ReviveOtherPlayer(i); // Call ReviveOtherPlayer
                }

                GUI.enabled = true; // Reset GUI enabled state
            }

            if (players.Count == 0)
            {
                GUI.Label(new Rect(5, 0, width - 30, 20), "No other players detected");
            }

            GUI.EndScrollView();
        }

        private void DrawEnemyList(float x, float y, float width, float height)
        {
            GUI.Box(new Rect(x, y, width, height), "");

            var enemyNames = _core.EnemyManager.GetEnemyNames();
            var enemyDistances = _core.EnemyManager.GetEnemyDistances();

            _enemyListScrollPos = GUI.BeginScrollView(
                new Rect(x, y, width, height),
                _enemyListScrollPos,
                new Rect(0, 0, width - 20, enemyNames.Count * 20)
            );

            for (int i = 0; i < enemyNames.Count; i++)
            {
                // Draw enemy name and distance
                GUI.Label(new Rect(5, i * 20, width - 90, 20),
                          $"{enemyNames[i]} - {enemyDistances[i]:0.00}m");

                // Add kill button
                if (GUI.Button(new Rect(width - 80, i * 20, 60, 20), "Kill"))
                {
                    _core.EnemyManager.KillEnemy(i);
                }
            }

            if (enemyNames.Count == 0)
            {
                GUI.Label(new Rect(5, 0, width - 30, 20), "No enemies detected");
            }

            GUI.EndScrollView();
        }

        private void DrawItemList(float x, float y, float width, float height)
        {
            GUI.Box(new Rect(x, y, width, height), "");

            var itemNames = _core.ItemManager.GetItemNames();
            var itemDistances = _core.ItemManager.GetItemDistances();

            _itemListScrollPos = GUI.BeginScrollView(
                new Rect(x, y, width, height),
                _itemListScrollPos,
                new Rect(0, 0, width - 20, itemNames.Count * 20)
            );

            for (int i = 0; i < itemNames.Count; i++)
            {
                GUI.Label(new Rect(5, i * 20, width - 30, 20),
                          $"{itemNames[i]} - {itemDistances[i]:0.00}m");
            }

            if (itemNames.Count == 0)
            {
                GUI.Label(new Rect(5, 0, width - 30, 20), "No items detected");
            }

            GUI.EndScrollView();
        }

        #endregion
    }
}
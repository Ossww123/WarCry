using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles player information display such as name, health, and palette/color
/// </summary>
public class PlayerStatsDisplay : MonoBehaviour
{
    [Header("Player Info UI")]
    public TMP_Text playerNameText;

    [Header("Health UI")]
    public Slider healthBar;
    public TMP_Text healthValueText;

    // Events for UI updates
    public static event Action<float, float> OnPlayerHealthChanged;
    public static event Action<string> OnPlayerNameChanged;
    public static event Action<Palettes> OnTeamColorChanged;

    private GameObject playerUnit;

    public void Initialize(GameObject playerUnit)
    {
        this.playerUnit = playerUnit;
        RegisterEventHandlers();
        FetchInitialPlayerData();
    }

    private void RegisterEventHandlers()
    {
        OnPlayerHealthChanged += UpdateHealth;
        OnPlayerNameChanged += UpdatePlayerName;
        OnTeamColorChanged += UpdateTeamColor;
    }

    private void UnregisterEventHandlers()
    {
        OnPlayerHealthChanged -= UpdateHealth;
        OnPlayerNameChanged -= UpdatePlayerName;
        OnTeamColorChanged -= UpdateTeamColor;
    }

    private void OnDestroy()
    {
        UnregisterEventHandlers();
    }

    /// <summary>
    /// Updates the UI elements to their initial states based on the player object
    /// </summary>
    private void FetchInitialPlayerData()
    {
        if (playerUnit == null)
        {
            return;
        }

        // Get name
        string playerName = "";
        if (LocalPlayerLocator.TryFindForUsername(playerUnit, out playerName))
        {
            UpdatePlayerName(playerName);
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Initial player name set: {playerName}");
        }

        // Get palette/color
        Unit unitComponent = playerUnit.GetComponent<Unit>();
        if (unitComponent != null && healthBar != null && healthBar.fillRect != null)
        {
            Palettes palette = (Palettes)unitComponent.palette;
            UpdateTeamColor(palette);
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Initial player palette set: {palette}");
        }

        // Get health
        TheOneAndOnlyStats stats = playerUnit.GetComponent<TheOneAndOnlyStats>();
        if (stats != null)
        {
            UpdateHealth(stats.maxHealth, stats.currentHealth);
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] Initial health set: {stats.maxHealth}/{stats.currentHealth}");
        }
    }

    private void UpdateHealth(float maxHealth, float currentHealth)
    {
        if (healthBar != null)
        {
            healthBar.maxValue = maxHealth;
            healthBar.value = currentHealth;
        }
        
        if (healthValueText != null)
        {
            healthValueText.text = $"{currentHealth:F0}/{maxHealth:F0}";
        }
    }

    private void UpdateTeamColor(Palettes palette)
    {
        if (healthBar != null && healthBar.fillRect != null)
        {
            healthBar.fillRect.GetComponent<Image>().color = 
                PalettesManager.GetColor(palette);
        }
    }

    private void UpdatePlayerName(string playerName)
    {
        if (playerNameText != null)
        {
            playerNameText.text = playerName;
        }
    }

    public static void TriggerHealthChanged(float maxHealth, float currentHealth)
    {
        OnPlayerHealthChanged?.Invoke(maxHealth, currentHealth);
    }

    public static void TriggerPlayerNameChanged(string name)
    {
        OnPlayerNameChanged?.Invoke(name);
    }

    public static void TriggerPlayerPaletteChanged(Palettes palette)
    {
        OnTeamColorChanged?.Invoke(palette);
    }
}
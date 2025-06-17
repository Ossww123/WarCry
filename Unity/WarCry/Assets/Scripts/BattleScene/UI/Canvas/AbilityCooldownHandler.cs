using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles the display and updating of ability cooldowns in the UI
/// </summary>
public class AbilityCooldownHandler : MonoBehaviour
{
    [Header("Ability UI")]
    private Abilities playerAbilities;

    [Header("Ability 1")]
    public Image abilityMaskImage1;

    public Text abilityText1;

    [Header("Ability 2")]
    public Image abilityMaskImage2;

    public Text abilityText2;

    [Header("Ability 3")]
    public Image abilityMaskImage3;

    public Text abilityText3;

    [Header("Ability 4")]
    public Image abilityMaskImage4;

    public Text abilityText4;

    // Events for UI updates
    public static event Action<float> OnAbility1CooldownChanged;
    public static event Action<float> OnAbility2CooldownChanged;
    public static event Action<float> OnAbility3CooldownChanged;
    public static event Action<float> OnAbility4CooldownChanged;

    public void Initialize(Abilities abilities)
    {
        playerAbilities = abilities;
        SubscribeEvents();
    }

    private void SubscribeEvents()
    {
        // Subscribe to UI events
        OnAbility1CooldownChanged += UpdateAbility1Cooldown;
        OnAbility2CooldownChanged += UpdateAbility2Cooldown;
        OnAbility3CooldownChanged += UpdateAbility3Cooldown;
        OnAbility4CooldownChanged += UpdateAbility4Cooldown;
    }

    private void UnsubscribeEvents()
    {
        // Unsubscribe from UI events
        OnAbility1CooldownChanged -= UpdateAbility1Cooldown;
        OnAbility2CooldownChanged -= UpdateAbility2Cooldown;
        OnAbility3CooldownChanged -= UpdateAbility3Cooldown;
        OnAbility4CooldownChanged -= UpdateAbility4Cooldown;
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }
    private void UpdateCooldownVisuals(ref float currentCooldown, float maxCooldown,
        ref bool isCooldown, Image skillImage, Text skillText)
    {
        if (isCooldown)
        {
            skillImage.gameObject.SetActive(true);
            skillText.gameObject.SetActive(true);
            if (currentCooldown <= 0f)
            {
                if (skillImage != null)
                {
                    skillImage.fillAmount = 0f;
                }

                if (skillText != null)
                {
                    skillText.text = "";
                }
            }
            else
            {
                if (skillImage != null)
                {
                    skillImage.fillAmount = currentCooldown / maxCooldown;
                }

                if (skillText != null)
                {
                    skillText.text = Mathf.Ceil(currentCooldown)
                        .ToString();
                }
            }
        }
        else
        {
            skillImage.gameObject.SetActive(false);
            skillText.gameObject.SetActive(false);
        }
    }

    private void UpdateAbility1Cooldown(float cooldown)
    {
        var ability1Cooldown = playerAbilities.ability1Cooldown;
        var isAbility1Cooldown = playerAbilities.isAbility1Cooldown;
        UpdateCooldownVisuals(ref cooldown, ability1Cooldown, ref isAbility1Cooldown,
            abilityMaskImage1, abilityText1);
    }

    private void UpdateAbility2Cooldown(float cooldown)
    {
        var ability2Cooldown = playerAbilities.ability2Cooldown;
        var isAbility2Cooldown = playerAbilities.isAbility2Cooldown;
        UpdateCooldownVisuals(ref cooldown, ability2Cooldown, ref isAbility2Cooldown,
            abilityMaskImage2, abilityText2);
    }

    private void UpdateAbility3Cooldown(float cooldown)
    {
        var ability3Cooldown = playerAbilities.ability3Cooldown;
        var isAbility3Cooldown = playerAbilities.isAbility3Cooldown;
        UpdateCooldownVisuals(ref cooldown, ability3Cooldown, ref isAbility3Cooldown,
            abilityMaskImage3, abilityText3);
    }

    private void UpdateAbility4Cooldown(float cooldown)
    {
        var ability4Cooldown = playerAbilities.ability4Cooldown;
        var isAbility4Cooldown = playerAbilities.isAbility4Cooldown;
        UpdateCooldownVisuals(ref cooldown, ability4Cooldown, ref isAbility4Cooldown,
            abilityMaskImage4, abilityText4);
    }


    public static void TriggerAbility1CooldownChanged(float cooldown)
    {
        OnAbility1CooldownChanged?.Invoke(cooldown);
    }

    public static void TriggerAbility2CooldownChanged(float cooldown)
    {
        OnAbility2CooldownChanged?.Invoke(cooldown);
    }

    public static void TriggerAbility3CooldownChanged(float cooldown)
    {
        OnAbility3CooldownChanged?.Invoke(cooldown);
    }

    public static void TriggerAbility4CooldownChanged(float cooldown)
    {
        OnAbility4CooldownChanged?.Invoke(cooldown);
    }
}
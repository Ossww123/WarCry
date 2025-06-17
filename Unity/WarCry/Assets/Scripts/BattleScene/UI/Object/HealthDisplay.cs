using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HealthDisplay : MonoBehaviour
{
    public Slider healthBar;
    public Unit unit;
    public TheOneAndOnlyStats unitStats;

    private void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        // Set color from Unit's palette
        if (unit != null)
        {
            SetSlider3DColor(PalettesManager.GetColor(unit.palette));
        }

        // Initialize health from Stats
        if (unitStats != null)
        {
            Start3DSlider(unitStats.maxHealth);
            Update3DSlider(unitStats.currentHealth);
        }
    }

    public void SetSlider3DColor(Color color)
    {
        if (healthBar?.fillRect != null)
        {
            healthBar.fillRect.GetComponent<Image>()
                .color = color;
        }
    }

    public void Start3DSlider(float maxValue)
    {
        healthBar.maxValue = maxValue;
        healthBar.value = maxValue;
    }

    public void Update3DSlider(float value)
    {
        healthBar.value = value;
    }
}
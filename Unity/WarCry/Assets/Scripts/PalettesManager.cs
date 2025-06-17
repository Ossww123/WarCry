using System;
using UnityEngine;

public static class PalettesManager
{
    /// <summary>
    /// A static array of predefined colors representing a color palette.
    /// Each color is defined using RGBA values and corresponds to a specific
    /// named palette listed in the <see cref="Palettes"/> enumeration.
    /// </summary>
    public static readonly Color[] colors = new[]
    {
        new Color(0x33 / 255f, 0x4E / 255f, 0xFF / 255f, 1f), // Blue
        new Color(0xFF / 255f, 0x48 / 255f, 0x13 / 255f, 1f), // Red
        new Color(0x7E / 255f, 0x77 / 255f, 0x85 / 255f, 1f), // Black
        new Color(0x46 / 255f, 0x82 / 255f, 0xFF / 255f, 1f), // LightBlue
        new Color(0x80 / 255f, 0x3F / 255f, 0x19 / 255f, 1f), // Brown
        new Color(0x00 / 255f, 0xB0 / 255f, 0x1D / 255f, 1f), // Green
        new Color(0x07 / 255f, 0xFF / 255f, 0xA1 / 255f, 1f), // Turquoise
        new Color(0xFF / 255f, 0x77 / 255f, 0xF8 / 255f, 1f), // Pink
        new Color(0xB1 / 255f, 0x76 / 255f, 0xFF / 255f, 1f), // Purple
        new Color(0xFF / 255f, 0x7E / 255f, 0x0E / 255f, 1f), // Tan
        new Color(0xDB / 255f, 0xED / 255f, 0xFF / 255f, 1f), // White
        new Color(0xFF / 255f, 0xC4 / 255f, 0x03 / 255f, 1f) // Yellow
    };

    public static Color GetColor(Palettes palette)
    {
        var index = (Int32)palette;
        if (0 <= index && index < colors.Length)
        {
            return colors[index];
        }
        throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of range");
    }

    public static Color GetColor(Int32 index)
    {
        if (0 <= index && index < colors.Length)
        {
            return colors[index];
        }
        throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of range");
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public static class SplineToolsIntegrationsManager
    {
        public static readonly HashSet<string> ActiveIntegrations = new HashSet<string>();

        [DidReloadScripts]
        public static void RefreshIntegrations()
        {
#if UNITY_2018_3_OR_NEWER
            UpdateIntegration("R.A.M 2019", "RamSpline, Assembly-CSharp", "ST_RAM_2019");
#else
            UpdateIntegration("R.A.M", "RamSpline, Assembly-CSharp", "ST_RAM");
#endif
            UpdateIntegration("MapMagic 1", "MapMagic.MapMagic, Assembly-CSharp", "ST_MM_1");
            UpdateIntegration("MapMagic 2 Core", " MapMagic.GUI.SettingsWindow, MapMagic.Settings", "ST_MM_2");
            UpdateIntegration("MapMagic 2 Biomes", " MapMagic.Nodes.Biomes.BiomesSet200, MapMagic", "ST_MM_2_BIOMES");

            UpdateIntegration("MapMagic 2 Colors", " MapMagic.Nodes.GUI.GeneratorDraw, MapMagic.Editor", "getThirdPartyGeneratorColors", "ST_MM_2_COLORS");
        }

        public static void UpdateIntegration(string name, string script, string keyword)
        {
            var available = Type.GetType(script) != null;

            if (available) EnableKeyword(name, keyword);
            else DisableKeyword(name, keyword);
        }

        public static void UpdateIntegration(string name, string script, string field, string keyword)
        {
            var type = Type.GetType(script);

            if (type == null) DisableKeyword(name, keyword);
            else
            {
                var available = type.GetFields().Any(f => f.Name == field);

                if (available) EnableKeyword(name, keyword);
                else DisableKeyword(name, keyword);
            }

        }

        public static void EnableKeyword(string name, string keyword)
        {
            ActiveIntegrations.Add(name);

            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);

            if (!symbols.Contains(keyword + ";") && !symbols.EndsWith(keyword))
            {
                symbols += (symbols.Length != 0 ? ";" : "") + keyword;
                Debug.Log("SplineTools: Found " + name + " in the project");

                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, symbols);
            }
        }


        public static void DisableKeyword(string name, string keyword)
        {
            ActiveIntegrations.Remove(name);

            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);

            if (symbols.Contains(keyword + ";") || symbols.EndsWith(keyword))
            {
                symbols = symbols.Replace(keyword, "");
                symbols = symbols.Replace(";;", ";");
                Debug.Log("SplineTools: " + name + " no longer found in the project");
            }

            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, symbols);
        }
    }
}

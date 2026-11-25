using System.IO;
using UnityEditor;
using UnityEngine;
using ZombieCity.City.Core;

namespace ZombieCity.City.EditorTools
{
    /// <summary>
    /// Editor menu stub for OSM import (full pipeline in later TASK-001 steps).
    /// </summary>
    public static class ImportOsmMenu
    {
        const string MenuPath = "ZombieCity/City/Import OSM...";

        static string PrefsKey => "ZombieCity.ImportOsmPath." + Application.dataPath.GetHashCode();

        [MenuItem(MenuPath)]
        static void ImportOsm()
        {
            Debug.Log($"[City] defaults: cellSize={new CityBakeConfig().cellSize}");

            string startDir = Application.dataPath + "/Source";
            string prefsPath = EditorPrefs.GetString(PrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(prefsPath))
            {
                string prefsDir = Path.GetDirectoryName(prefsPath);
                if (!string.IsNullOrEmpty(prefsDir) && Directory.Exists(prefsDir))
                {
                    startDir = prefsDir;
                }
            }

            string path = EditorUtility.OpenFilePanel("Select OSM", startDir, "osm");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            EditorPrefs.SetString(PrefsKey, path);
            Debug.Log($"[City] Import OSM stub selected: {path}");
        }
    }
}

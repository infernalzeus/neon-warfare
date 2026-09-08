using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NW.Editor
{
    /// <summary>
    /// NW → Play Battle  — creates an empty Battle scene (if absent),
    /// registers it as build scene 0, and enters Play Mode.
    /// </summary>
    public static class BattleSceneSetup
    {
        const string SCENE_PATH = "Assets/_Project/Scenes/Battle.unity";

        [MenuItem("NW/Play Battle %F5")]
        public static void PlayBattle()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // Create the Battle scene file if it doesn't exist yet
            if (!System.IO.File.Exists(SCENE_PATH))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, SCENE_PATH);
                AssetDatabase.Refresh();
                Debug.Log($"[NW] Created {SCENE_PATH}");
            }

            // Ensure Battle.unity is build scene 0
            EnsureInBuildSettings(SCENE_PATH);

            // Open the Battle scene then enter Play Mode
            EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        static void EnsureInBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            bool found = scenes.Any(s => s.path == path);
            if (!found)
            {
                scenes.Insert(0, new EditorBuildSettingsScene(path, true));
                // Move all others after index 0
                for (int i = 1; i < scenes.Count; i++)
                    scenes[i] = new EditorBuildSettingsScene(scenes[i].path, scenes[i].enabled);
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log($"[NW] Added {path} as build scene 0");
            }
            else
            {
                // Make sure it's enabled
                for (int i = 0; i < scenes.Count; i++)
                    if (scenes[i].path == path && !scenes[i].enabled)
                    {
                        scenes[i] = new EditorBuildSettingsScene(path, true);
                        EditorBuildSettings.scenes = scenes.ToArray();
                    }
            }
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TwelveTails.Gameplay
{
    public sealed class ChapterMenu : MonoBehaviour
    {
        public static string ChapterScene(int chapterNumber) => chapterNumber switch
        {
            1 => "CarronHarvest",
            _ => throw new ArgumentOutOfRangeException(nameof(chapterNumber), "That chapter is not playable yet.")
        };

        public void LoadChapter(int chapterNumber)
        {
            var sceneName = ChapterScene(chapterNumber);
            Debug.Log($"Loading Chapter {chapterNumber}: {sceneName}");
            SceneManager.LoadScene(sceneName);
        }

        private void OnGUI()
        {
            var width = Mathf.Min(520f, Screen.width - 40f);
            var area = new Rect((Screen.width - width) * .5f, Mathf.Max(40f, Screen.height * .18f), width, 420f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("12 Tails Offline", new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                alignment = TextAnchor.MiddleCenter
            });
            GUILayout.Space(24f);
            if (GUILayout.Button("Chapter 1 - Carron Hunt", GUILayout.Height(64f))) LoadChapter(1);
            GUILayout.Space(12f);
            GUI.enabled = false;
            for (var chapter = 2; chapter <= 12; chapter++)
                GUILayout.Button($"Chapter {chapter} - In development", GUILayout.Height(24f));
            GUI.enabled = true;
            GUILayout.EndArea();
        }
    }
}
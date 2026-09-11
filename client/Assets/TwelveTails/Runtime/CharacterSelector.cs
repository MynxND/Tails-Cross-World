using UnityEngine;

namespace TwelveTails.Gameplay
{
    public sealed class CharacterSelector : MonoBehaviour
    {
        private const string PreferenceKey = "selected-character-v1";
        private int selected;
        private GameObject visual = null!;

        public string SelectedId => CharacterRoster.Ids[selected];

        private void Awake()
        {
            var renderer = GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false;
            Select(PlayerPrefs.GetString(PreferenceKey, CharacterRoster.Ids[0]));
        }

        public void Select(string characterId)
        {
            selected = CharacterRoster.IndexOf(characterId);
            if (visual != null) Destroy(visual);
            visual = ProceduralCharacter.Create(CharacterRoster.Ids[selected], transform);
            PlayerPrefs.SetString(PreferenceKey, CharacterRoster.Ids[selected]);
            PlayerPrefs.Save();
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, Screen.height - 205, 710, 185), GUI.skin.box);
            GUILayout.Label($"Choose Tail: {SelectedId.ToUpperInvariant()} - {CharacterRoster.Classes[selected]}");
            for (var row = 0; row < 2; row++)
            {
                GUILayout.BeginHorizontal();
                for (var column = 0; column < 6; column++)
                {
                    var index = row * 6 + column;
                    var label = CharacterRoster.Ids[index];
                    if (GUILayout.Button(label, GUILayout.Width(110), GUILayout.Height(48))) Select(label);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }
    }
}

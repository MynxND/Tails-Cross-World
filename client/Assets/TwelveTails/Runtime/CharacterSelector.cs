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
            var title = ToTitleCase(CharacterRoster.Ids[selected]);
            var prefab = Resources.Load<GameObject>($"OriginalCharacters/{title}");
            if (prefab == null) prefab = Resources.Load<GameObject>($"Characters/{title}");
            visual = prefab != null ? Instantiate(prefab, transform) : ProceduralCharacter.Create(CharacterRoster.Ids[selected], transform);
            visual.name = "Character Visual";
            var legacyAnimation = visual.GetComponentInChildren<Animation>(true);
            if (legacyAnimation != null && legacyAnimation.GetComponent<LegacyAnimationDriver>() == null)
                legacyAnimation.gameObject.AddComponent<LegacyAnimationDriver>();
            PlayerPrefs.SetString(PreferenceKey, CharacterRoster.Ids[selected]);
            PlayerPrefs.Save();
        }

        private static string ToTitleCase(string value) => char.ToUpperInvariant(value[0]) + value.Substring(1);

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

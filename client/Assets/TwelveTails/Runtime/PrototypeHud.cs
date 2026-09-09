using UnityEngine;

namespace TwelveTails.Gameplay
{
    public sealed class PrototypeHud : MonoBehaviour
    {
        [SerializeField] private QuestProgress quest = null!;
        public void Configure(QuestProgress progress) => quest = progress;

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.box) { fontSize = 20, alignment = TextAnchor.MiddleLeft };
            GUI.Box(new Rect(20, 20, 520, 90), $"WASD: Move    Space: Attack\n{quest.ObjectiveText}", style);
        }
    }
}

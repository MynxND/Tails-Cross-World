using UnityEngine;

namespace TwelveTails.Gameplay
{
    public sealed class PrototypeHud : MonoBehaviour
    {
        [SerializeField] private QuestProgress quest = null!;
        [SerializeField] private PlayerProgress progress = null!;
        [SerializeField] private SaveCoordinator saves = null!;
        public void Configure(QuestProgress questProgress, PlayerProgress playerProgress, SaveCoordinator saveCoordinator)
        {
            quest = questProgress;
            progress = playerProgress;
            saves = saveCoordinator;
        }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.box) { fontSize = 20, alignment = TextAnchor.MiddleLeft };
            GUI.Box(new Rect(20, 20, 650, 135),
                $"WASD: Move    Space: Attack    F5: Save    F9: Load\n{quest.ObjectiveText}\nEXP: {progress.Experience}    Potions: {progress.PotionCount}    Save: {saves.LastStatus}", style);
        }
    }
}

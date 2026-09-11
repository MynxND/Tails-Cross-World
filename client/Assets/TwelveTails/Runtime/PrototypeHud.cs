using UnityEngine;

namespace TwelveTails.Gameplay
{
    public sealed class PrototypeHud : MonoBehaviour
    {
        [SerializeField] private QuestProgress quest = null!;
        [SerializeField] private PlayerProgress progress = null!;
        [SerializeField] private SaveCoordinator saves = null!;
        [SerializeField] private MupoHerdMission mupoMission = null!;
        [SerializeField] private DefeatAndProtectMission protectMission = null!;
        public void Configure(QuestProgress questProgress, PlayerProgress playerProgress, SaveCoordinator saveCoordinator)
        {
            quest = questProgress;
            progress = playerProgress;
            saves = saveCoordinator;
        }

        public void ConfigureMupo(MupoHerdMission mission, PlayerProgress playerProgress, SaveCoordinator saveCoordinator)
        {
            mupoMission = mission;
            progress = playerProgress;
            saves = saveCoordinator;
        }

        public void ConfigureProtect(DefeatAndProtectMission mission, PlayerProgress playerProgress, SaveCoordinator saveCoordinator)
        {
            protectMission = mission;
            progress = playerProgress;
            saves = saveCoordinator;
        }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.box) { fontSize = 20, alignment = TextAnchor.MiddleLeft };
            var objective = protectMission != null
                ? protectMission.ObjectiveText
                : mupoMission != null ? mupoMission.ObjectiveText : quest.ObjectiveText;
            GUI.Box(new Rect(20, 20, 650, 135),
                $"WASD: Move    Space: Attack    F5: Save    F9: Load\n{objective}\nEXP: {progress.Experience}    Potions: {progress.PotionCount}    Save: {saves.LastStatus}", style);
        }
    }
}

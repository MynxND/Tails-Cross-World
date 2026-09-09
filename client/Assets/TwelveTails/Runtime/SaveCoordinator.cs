using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TwelveTails.Gameplay
{
    public sealed class SaveCoordinator : MonoBehaviour
    {
        [SerializeField] private PlayerProgress progress = null!;
        [SerializeField] private QuestProgress quest = null!;
        public string LastStatus { get; private set; } = "No save loaded";
        public string SavePath => Path.Combine(Application.persistentDataPath, "prototype-save-v1.json");

        public void Configure(PlayerProgress playerProgress, QuestProgress questProgress)
        {
            progress = playerProgress;
            quest = questProgress;
        }

        private void Start()
        {
            if (File.Exists(SavePath)) Load();
        }

        private void Update()
        {
            if (Keyboard.current?.f5Key.wasPressedThisFrame == true) Save();
            if (Keyboard.current?.f9Key.wasPressedThisFrame == true) Load();
        }

        public void Save()
        {
            ProgressSave.Write(SavePath, new ProgressSaveData
            {
                experience = progress.Experience,
                potionCount = progress.PotionCount,
                questComplete = quest.IsComplete,
                rewardClaimed = progress.RewardClaimed
            });
            LastStatus = "Saved";
        }

        public void Load()
        {
            try
            {
                var data = ProgressSave.Read(SavePath);
                progress.Restore(data.experience, data.potionCount, data.rewardClaimed);
                quest.Restore(data.questComplete);
                LastStatus = "Loaded";
            }
            catch (System.Exception exception)
            {
                LastStatus = "Load rejected: " + exception.Message;
                Debug.LogWarning(LastStatus);
            }
        }
    }
}

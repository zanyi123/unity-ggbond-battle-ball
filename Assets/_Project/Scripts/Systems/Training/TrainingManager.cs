using UnityEngine;
using System.Collections.Generic;

namespace BattleBall.Systems.Training
{
    public class TrainingManager : MonoBehaviour
    {
        public static TrainingManager Instance { get; private set; }

        // ===== 常量 =====
        public const float TRAIN_BONUS_PER_TIME = 1.0f;

        // ===== UI 兼容: 精确匹配 PreparationUI / BaseSystem 调用形式 =====
        public virtual int GetFieldLevel() { return 1; }
        public virtual int GetFieldLevel(string field) { return 1; }
        public virtual Dictionary<string, float> GetFieldUpgradeCost(int fieldLevel) { return new Dictionary<string, float>(); }
        public virtual Dictionary<string, float> GetFieldUpgradeCost(string field) { return new Dictionary<string, float>(); }
        public virtual Dictionary<string, float> GetTrainCost() { return new Dictionary<string, float>(); }
        public virtual Dictionary<string, float> GetTrainCost(string field, string stat) { return new Dictionary<string, float>(); }
        public virtual float GetStatMax(string fieldOrChar, string stat) { return 100f; }
        public virtual bool CanTrain(string charId, string field, string stat) { return true; }
        public virtual bool CanTrain(string charId, string stat) { return true; }
        public virtual bool TrainStat(string charId, string field, string stat) { return true; }
        public virtual bool TrainStat(string charId, string stat) { return true; }
        public virtual bool UpgradeField() { return true; }
        public virtual bool UpgradeField(string charId) { return true; }
        public virtual bool UpgradeField(string charId, string field) { return true; }
        public virtual Dictionary<string, object> GetTrainingData(int playerIdx) { return new Dictionary<string, object>(); }
        public virtual List<Dictionary<string, object>> GetAllTrainings() { return new List<Dictionary<string, object>>(); }
        public virtual bool StartTraining(int playerIdx, string trainingId) { return true; }
        public virtual float GetProgress(int playerIdx) { return 0f; }

        protected virtual void Awake() {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        protected virtual void OnDestroy() { if (Instance == this) Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance == null)
            {
                var go = new GameObject("TrainingManager");
                go.AddComponent<TrainingManager>();
            }
        }
    }
}

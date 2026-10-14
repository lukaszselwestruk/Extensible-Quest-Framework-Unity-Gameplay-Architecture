using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using I2.Loc;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "Quest", menuName = "ScriptableObjects/Quests/Quest")]
public class Quest : DatabaseElement<ScriptableObject>
{
    public event Action<Quest, AQuestTask> OnQuestTaskCompleted;
    public event Action<Quest, AQuestTask> OnQuestTaskProgress;

    public bool IsSideQuest => _isSideQuest;

    public LocalizedString Title;
    public LocalizedString Description;
    public List<Quest> NextQuests = new();
    [SerializeField] private bool _isSideQuest = false;
    [field: SerializeField] public PadlockList ConditionalLocks = new();
    [SerializeField] private List<AQuestReward> _questActivationBehaviours;
    [SerializeField] private List<AQuestTask> _questTasks;
    [SerializeField] private List<AQuestReward> _questRewards;

    public bool IsQuestActive { get; set; } = false;

    public string GUID { get; set; }

    [HideInInspector] public List<AQuestTask> QuestTasks = new();
    [HideInInspector] public List<AQuestReward> QuestRewards = new();

    private bool _activated = false;

#if UNITY_EDITOR
    [Button("Test quest creation")]
    private void TestQuests()
    {
        Debug.Log($"Testing quest \"{Title}\"...");
        //StartQuest();
        QuestTasks = new();
        QuestTasks = new();
        QuestRewards = new();
        Debug.Log("Testing finished");
    }

    [Button("Cleanup quest")]
    private void CleanupQuests()
    {
        QuestTasks = new();
        QuestRewards = new();
    }
#endif

    public void StartQuest(QuestManager questManager)
    {
        QuestTasks.Clear();
        if (_questTasks == null || questManager == null) return;

        foreach (AQuestTask qt in _questTasks)
        {
            if (qt == null) continue;
            AQuestTask questTask = Instantiate(qt);
            questTask.PrepareTask(QuestTaskProgress);
            questTask.SetTaskID(questManager.GetUniqueTaskID(this));
            QuestTasks.Add(questTask);
        }
    }

    public void InitializeQuestRewards()
    {
        QuestRewards.Clear();
        if (_questRewards == null) return;

        foreach (AQuestReward qr in _questRewards)
        {
            if (qr == null) continue;
            AQuestReward questReward = Instantiate(qr);
            QuestRewards.Add(questReward);
        }
    }

    public void CheckIfTasksHasAlreadyBeenDone()
    {
        foreach (AQuestTask qt in QuestTasks)
        {
            if (qt.ActionHasAlreadyBeDone())
            {
                qt.ForceCompleteTask();
                OnQuestTaskCompleted?.Invoke(this, qt);
            }
        }
    }

    public bool IsQuestCompleted(ActionStat actionStat)
    {
        bool isQuestCompleted = true;

        foreach (AQuestTask qt in QuestTasks)
        {
            if (qt == null) continue;
            if (qt.IsCompleted) continue;
            if (actionStat == null && !qt.Optional)
            {
                isQuestCompleted = false;
                continue;
            }

            bool taskCompleted = qt.TryCompleteTask(actionStat);

            if (!taskCompleted && !qt.Optional)
            {
                isQuestCompleted = false;
                continue;
            }

            if (!qt.IsCompleted) continue;

            qt.GetRewards();
            OnQuestTaskCompleted?.Invoke(this, qt);
        }

        return isQuestCompleted;
    }

    public void CompleteNextTask()
    {
        foreach (AQuestTask qt in QuestTasks)
        {
            if (qt == null) continue;
            if (qt.IsCompleted)
                continue;

            qt.ForceCompleteTask();
            OnQuestTaskCompleted?.Invoke(this, qt);
            return;
        }
    }

    private void QuestTaskProgress(AQuestTask questTask)
    {
        OnQuestTaskProgress?.Invoke(this, questTask);
    }

    public void Initialize(SaveQuest saveQuest)
    {
        if (saveQuest == null || saveQuest.QuestTasks == null)
        {
            Debug.LogError($"Quest {name} has invalid save data");
            return;
        }

        _activated = saveQuest.Activated;

        for (int i = 0; i < QuestTasks.Count; ++i)
        {
            if (QuestTasks.Count != saveQuest.QuestTasks.Count)
            {
                Debug.LogError($"Loaded quest has different amount of tasks than our task");
                continue;
                Debug.LogError($"Loaded quest {name} has a different number of tasks than the current quest definition");
                return;
            }

            for (int i = 0; i < QuestTasks.Count; ++i)
            {
                if (QuestTasks[i] == null || saveQuest.QuestTasks[i] == null) continue;
                QuestTasks[i].Initialize(saveQuest.QuestTasks[i]);
            }

            IsQuestCompleted(null);
        }

    public SaveQuest GetQuestSaveData()
    {
        SaveQuest saveQuest = new();

        saveQuest.Activated = _activated;

        foreach (AQuestTask qt in QuestTasks)
            saveQuest.QuestTasks.Add(qt.GetQuestTaskSaveData());

        return saveQuest;
    }

    public IEnumerator AwardPlayer(MonoBehaviour emptyMonoBehaviour)
    {
        foreach (AQuestReward qr in QuestRewards)
            yield return emptyMonoBehaviour.StartCoroutine(qr.AwardPlayer());
    }

    public IEnumerator StartQuest(MonoBehaviour emptyMonoBehaviour)
    {
        if (emptyMonoBehaviour == null) yield break;

        if (_activated)
        {
            Debug.LogWarning("Quest has already been activated");
            //yield break;
            yield break;
        }

        _activated = true;

        if (_questActivationBehaviours == null) yield break;

        foreach (AQuestReward activationBehaviour in _questActivationBehaviours)
        {
            if (activationBehaviour == null)
            {
                Debug.LogError($"Quest {name} has a null activation behaviour");
                continue;
            }
            yield return emptyMonoBehaviour.StartCoroutine(activationBehaviour.AwardPlayer());
        }
    }

    public void Reset()
    {
        if (QuestTasks != null)
        {
            foreach (AQuestTask quest in QuestTasks)
            {
                if (quest == null) continue;
                if (quest.QuestTargetFinder) quest.QuestTargetFinder.Deactivate();
            }
        }

        IsQuestActive = false;
        OnQuestTaskCompleted = null;
        OnQuestTaskProgress = null;
        QuestTasks = new();
        QuestRewards = new();

        _activated = false;
    }

    public override List<string> GetPath()
    {
        return new();
    }
}
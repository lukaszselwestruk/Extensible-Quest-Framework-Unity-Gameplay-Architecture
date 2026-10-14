using System;
using System.Collections.Generic;
using UnityEngine;
using I2.Loc;

[CreateAssetMenu(fileName = "QuestTask Group", menuName = "ScriptableObjects/Quests/Tasks/Group")]
public class QuestTaskGroup : AQuestTask
{
    [SerializeField] List<AQuestTask> _tasks;
    [SerializeField] int _requiredFinishedTasks;
    [SerializeField] bool _showCounter;

    [HideInInspector] public List<AQuestTask> Tasks = new();

    private int _currentlyFinished = 0;

    public override void PrepareTask(Action<AQuestTask> OnTaskProgress)
    {
        base.PrepareTask(OnTaskProgress);
        Tasks.Clear();
        _currentlyFinished = 0;
        IsCompleted = false;

        if (_tasks == null) return;

        foreach (AQuestTask qt in _tasks)
        {
            if (qt == null) continue;
            AQuestTask task = Instantiate(qt);
            task.PrepareTask(OnTaskProgress);
            Tasks.Add(task);
        }
    }

    public override bool TryCompleteTask(ActionStat actionStat = null)
    {
        foreach (AQuestTask qt in Tasks)
        {
            if (qt == null)
            {
                Debug.LogError($"QuestTaskGroup {name} Task is null");
                continue;
            }

            if (qt.IsCompleted)
                continue;
            bool wasCompleted = qt.IsCompleted;
            qt.TryCompleteTask(actionStat);

            if (qt.TryCompleteTask(actionStat))
                if (!wasCompleted && qt.IsCompleted)
                {
                    ++_currentlyFinished;
                    InvokeOnTaskProgress();
                    qt.InvokeOnTaskProgress();
                }
        }

        if (_currentlyFinished >= _requiredFinishedTasks)
        {
            ClearOnTaskProgress();
            IsCompleted = true;
        }

        return IsCompleted;
    }

    public override string GetCounter()
    {
        return _showCounter ? $"({_currentlyFinished}/{_requiredFinishedTasks})" : "";
    }

    public override void Initialize(ASaveQuestTask saveQuestTask)
    {
        SaveQuestTaskGroupValue value = saveQuestTask as SaveQuestTaskGroupValue;
        if (value?.Value == null)
        {
            Debug.LogError($"QuestTaskGroup {name} has invalid save data");
            return;
        }

        _currentlyFinished = 0;
        IsCompleted = false;

        for (int i = 0; i < Tasks.Count; ++i)
        {
            if (Tasks[i] == null)
            {
                Debug.LogError($"QuestTaskGroup {name} Task is null");
                continue;
            }
            if (i >= value.Value.Count || value.Value[i] == null)
            {
                Debug.LogError($"QuestTaskGroup {name} has incomplete save data");
                continue;
            }

            Tasks[i].Initialize(value.Value[i]);

            if (Tasks[i].TryCompleteTask())
                ++_currentlyFinished;
        }

        IsCompleted = _currentlyFinished >= _requiredFinishedTasks;
    }

    public override ASaveQuestTask GetQuestTaskSaveData()
    {
        List<ASaveQuestTask> saveForTasks = new();

        foreach (AQuestTask task in Tasks)
            saveForTasks.Add(task.GetQuestTaskSaveData());

        return new SaveQuestTaskGroupValue(saveForTasks);
    }

    public override bool CompareActionStatData(AActionStatData data = null)
    {
        return false;
    }
}

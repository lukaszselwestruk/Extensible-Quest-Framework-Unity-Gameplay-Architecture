using UnityEngine;
using I2.Loc;
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

public abstract class AQuestTask : SerializedScriptableObject
{
    public event Action<AQuestTask> OnTaskProgress;

    [SerializeField] protected ActionStat _usedActionStat;
    [SerializeField] protected LocalizedString _description;
    [SerializeField] protected LocalizedString _helpText;
    [SerializeField] protected bool _searchForAlreadyDoneActions;
    [SerializeField] private AQuestReward[] _questReward = new AQuestReward[0];
    [SerializeField] private bool _optional;
    [SerializeField] protected StateRecogniser _stateRecogniser;

    public int TaskID { get; private set; }

    public AQuestTargetFinder QuestTargetFinder;
    public bool IsCompleted { get; protected set; } = false;
    public bool Optional => _optional;

    public virtual void PrepareTask(Action<AQuestTask> OnTaskProgress) =>
        this.OnTaskProgress += OnTaskProgress;

    public virtual void ForceCompleteTask()
    {
        IsCompleted = true;
    }

    public abstract bool TryCompleteTask(ActionStat actionStat = null);
    private bool CompareActionStatData(List<AActionStatData> datas = null)
    {
        if (datas == null) return false;

        foreach (AActionStatData statData in datas)
        {
            if (statData == null) continue;
            if (CompareActionStatData(statData)) return true;
        }
        return false;
    }

    public abstract bool CompareActionStatData(AActionStatData data = null);
    public virtual bool ActionHasAlreadyBeDone()
    {
        if (_stateRecogniser != null && _stateRecogniser.CompareState(SceneServiceLocator.Instance)) return true;
        SceneServiceLocator serviceLocator = SceneServiceLocator.Instance;
        if (_stateRecogniser != null && serviceLocator != null && _stateRecogniser.CompareState(serviceLocator)) return true;

        if (!_searchForAlreadyDoneActions) return false;
        SceneServiceLocator serviceLocator = SceneServiceLocator.Instance;
        if (serviceLocator == null) return false;

        if (serviceLocator.TryGetServiceLocatorComponent(out StatsManager statManager))
        {
            if (_usedActionStat == null) return false;
            if (!statManager.Statistics.ContainsKey(_usedActionStat.Guid)) return false;
            return CompareActionStatData(statManager.Statistics[_usedActionStat.Guid]);
        }
        return false;
    }

    public void SetTaskID(int taskID) => TaskID = taskID;

    public virtual LocalizedString GetQuestTaskDescription() =>
        _description;

    public virtual LocalizedString GetQuestTaskHelp() =>
        _helpText;

    public abstract string GetCounter();

    public abstract void Initialize(ASaveQuestTask saveQuestTask);
    public abstract ASaveQuestTask GetQuestTaskSaveData();

    public void InvokeOnTaskProgress() =>
        OnTaskProgress?.Invoke(this);

    public void ClearOnTaskProgress() =>
        OnTaskProgress = null;

    public void GetRewards()
    {
        MonoBehaviour monoBehaviour = SceneServiceLocator.Instance;
        if (monoBehaviour == null) return;

        for (int i = 0; i < _questReward.Length; i++)
        {
            if (_questReward[i] != null)
                monoBehaviour.StartCoroutine(_questReward[i].AwardPlayer());
        }
    }

    public bool IsValid()
    {
        if (_usedActionStat == null) return false;
        return true;
    }
}

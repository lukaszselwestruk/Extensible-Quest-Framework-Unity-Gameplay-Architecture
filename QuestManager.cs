using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using Sirenix.OdinInspector;

public class QuestManager : MonoBehaviour, IServiceLocatorComponent, IManager, IQuestManager, ISaveableSync<SaveData>, IAwake, IStartable, IMapTaskReceiver
{
    public DatabaseElement<Quest> Database => _database;
    public List<Quest> QuestInProgress => _questsInProgress;

    [ServiceLocatorComponent] private StatsManager _actionStatsManager;
    [ServiceLocatorComponent] private WindowManager _windowManager;
    [ServiceLocatorComponent] private DifficultyLevelManager _difficultyLevelManager;

    [SerializeField] private QuestUIController _questUIController;
    [SerializeField] private DatabaseElement<Quest> _database;
    [SerializeField] private Quest _startQuest;
    [SerializeField] private MapPinBlockerSO[] _sideQuestBlockers;
    [SerializeField] private PadlockList _tutorialPadlocks;

    public bool Enabled => true;

    public ServiceLocator MyServiceLocator { get; set; }
    public Action OnProgressChanged { get; set; }

    public event Action<Quest> OnQuestStarted;
    public event Action<Quest> OnQuestActivated;
    public event Action<Quest> OnQuestCompleted;
    public event Action<Quest> OnQuestAborted;
    public event Action<Quest, AQuestTask> OnQuestTaskCompleted;
    public event Action<Quest, AQuestTask> OnQuestTaskProgressed;

    private List<Quest> _questsInProgress = new();
    private List<Quest> _questsCompleted = new();
    private Quest _questActive;
    private QuestsSaveData _questSaveData;
    private bool _isConfigured;

    public void CustomReset()
    {
        OnQuestStarted = null;
        OnQuestActivated = null;
        OnQuestCompleted = null;
        OnQuestAborted = null;
        OnQuestTaskCompleted = null;

        _questsInProgress?.ForEach(q => q?.Reset());
        _questsCompleted?.ForEach(q => q?.Reset());

        _questsInProgress = new();
        _questsCompleted = new();
        _questActive = null;
    }

    public void CustomAwake()
    {
        CustomReset();
        _database.EntryList.ForEach(quest => quest.Reset());
        _difficultyLevelManager.SubscribeToSettingChanges(SettingName.DisableMissions, ChangedDifficulty);
        if (_database != null && _database.EntryList != null)
            _database.EntryList.ForEach(quest => quest?.Reset());

        _difficultyLevelManager?.SubscribeToSettingChanges(SettingName.DisableMissions, ChangedDifficulty);
    }

    public bool SideQuestAlreadyActive()
    {
        foreach (Quest quest in _questsInProgress)
        {
            if (quest.IsSideQuest) return true;
        }

        return false;
    }

    [Button]
    private void AddQuest(Quest quest)
    {
        AddQuest(quest, true);
    }

    [Button]
    public void ChangeActiveQuestToNext()
    {
        ActivateQuest(_questsInProgress[(_questsInProgress.IndexOf(_questActive) + 1) % _questsInProgress.Count]);
        if (_questsInProgress.Count == 0) return;

        int activeQuestIndex = _questsInProgress.IndexOf(_questActive);
        if (activeQuestIndex < 0) activeQuestIndex = 0;
        ActivateQuest(_questsInProgress[(activeQuestIndex + 1) % _questsInProgress.Count]);
    }

    public void CustomStart()
    {
        _windowManager.CreateWindow(_questUIController);
    }

    public void Configure()
    {
        if (_isConfigured || _actionStatsManager == null) return;
        _actionStatsManager.OnStatisticAdded += CheckQuestsInProgress;
        _isConfigured = true;
    }

    private void OnDestroy()
    {
        if (_isConfigured && _actionStatsManager != null)
            _actionStatsManager.OnStatisticAdded -= CheckQuestsInProgress;
        _difficultyLevelManager.UnSubscribeToSettingChanges(SettingName.DisableMissions, ChangedDifficulty);
        _difficultyLevelManager?.UnSubscribeToSettingChanges(SettingName.DisableMissions, ChangedDifficulty);
    }

    public void Initialize(SaveData save)
    {
        if (!NetworkServer.active) return;

        Configure();

        if (save == null)
        {
            AddQuest(_startQuest, true);
            return;
        }

        if (save.Quests == null) return;
        if (save.DifficultySettingsSaveData.DifficultyBool.ContainsKey(SettingName.DisableMissions) &&
        if (save.DifficultySettingsSaveData != null && save.DifficultySettingsSaveData.DifficultyBool.ContainsKey(SettingName.DisableMissions) &&
            save.DifficultySettingsSaveData.DifficultyBool[SettingName.DisableMissions]) return;

        InitializeQuests(save.Quests);
    }

    public void ReInitialize()
    {
        InitializeQuests(_questSaveData);
    }

    private void InitializeQuests(QuestsSaveData save)
    {
        if (save == null) return;
        _questSaveData = save;
        if (save.InProgressQuests == null || save.CompletedQuests == null) return;

        foreach (SaveQuest saveQuest in save.InProgressQuests)
        {
            if (saveQuest == null) continue;
            Quest quest = _database.GetEntryOrDefault(saveQuest.Guid);

            if (quest == null)
                continue;

            if (quest == null) continue;
            if (!_questsInProgress.Contains(quest))
                AddQuest(quest, save.ActiveQuestGuid == saveQuest.Guid);

            quest.Initialize(saveQuest);
        }

        foreach (SaveQuest saveQuest in save.CompletedQuests)
        {
            if (saveQuest == null) continue;
            Quest quest = _database.GetEntryOrDefault(saveQuest.Guid);

            if (quest == null)
                continue;

            if (quest == null) continue;
            quest.Initialize(saveQuest);

            if (!_questsCompleted.Contains(quest))
                _questsCompleted.Add(quest);
        }
    }

    public SaveData CollectData(SaveData data)
    {
        if (data == null) return null;
        QuestsSaveData questsSaveData = new();

        foreach (Quest quest in _questsInProgress)
        {
            SaveQuest saveQuest = quest.GetQuestSaveData();
            saveQuest.Guid = _database.GetGuidOfElement(quest);
            questsSaveData.InProgressQuests.Add(saveQuest);

            if (quest == _questActive)
                questsSaveData.ActiveQuestGuid = saveQuest.Guid;
        }

        foreach (Quest quest in _questsCompleted)
        {
            SaveQuest saveQuest = quest.GetQuestSaveData();
            saveQuest.Guid = _database.GetGuidOfElement(quest);
            questsSaveData.CompletedQuests.Add(saveQuest);
        }

        data.Quests = questsSaveData;
        return data;
    }

    public void AddQuest(string questGuid, bool setQuestActive = false, bool allowClient = false)
    {
        Quest quest = _database.GetEntry(questGuid);
        AddQuest(quest, setQuestActive, allowClient);
    }

    public void AddQuest(Quest quest, bool setQuestActive = false, bool allowClient = false)
    {
        if (quest == null)
        {
            Debug.LogWarning("Quest you want to add is null");
            return;
        }

        if (!allowClient && !NetworkServer.active) return;

        if (!_database.EntryList.Contains(quest))
            if (_database == null || _database.EntryList == null || !_database.EntryList.Contains(quest))
            {
                Debug.LogError($"Quest \"{quest.Title}\" is not in entry list of the manager");
                return;
            }

        if (_questsInProgress.Contains(quest))
        {
            Debug.Log($"Quest \"{quest.Title}\" already started");
            return;
        }

        if (_questsCompleted.Contains(quest))
        {
            Debug.LogWarning($"Quest \"{quest.Title}\" has already been done, but starting it again");
        }

        quest.GUID = _database.GetGuidOfElement(quest);

        quest.Reset();
        quest.StartQuest(this);
        quest.OnQuestTaskCompleted += QuestTaskCompleted;
        quest.OnQuestTaskProgress += QuestTaskProgress;

        _questsInProgress.Add(quest);
        OnQuestStarted?.Invoke(quest);

        if (setQuestActive)
            ActivateQuest(quest);

        StartCoroutine(quest.StartQuest((MonoBehaviour)this));
        quest.CheckIfTasksHasAlreadyBeenDone();
        CheckQuestsInProgress(null);
    }

    public void ActivateQuest(Quest quest)
    {
        if (!_questsInProgress.Contains(quest))
        {
            Debug.Log($"Quest \"{quest}\" is not yet started");
            return;
        }

        if (_questActive != null)
        {
            _questActive.IsQuestActive = false;
        }

        _questActive = quest;
        _questActive.IsQuestActive = true;

        OnQuestActivated?.Invoke(quest);
    }

    private void CheckQuestsInProgress(ActionStat actionStat)
    {
        StartCoroutine(CheckQuestsInProgressCoroutine(actionStat));
    }

    private IEnumerator CheckQuestsInProgressCoroutine(ActionStat actionStat)
    {
        for (int i = _questsInProgress.Count - 1; i >= 0; --i)
        {
            Quest q = _questsInProgress[i];

            if (!q.IsQuestCompleted(actionStat))
                continue;

            yield return CompleteQuest(q);
        }

        if (_questActive == null && _questsInProgress.Count > 0)
            ActivateQuest(_questsInProgress[0]);
    }

    public void CompleteNextTask()
    {
        if (_questActive == null) return;
        _questActive.CompleteNextTask();

        if (_questActive.IsQuestCompleted(null))
            CompleteActiveQuest();
    }

    public void CompleteActiveQuest() => StartCoroutine(CompleteQuest(_questActive));
    private IEnumerator CompleteQuest(Quest q)
    {
        if (q == null) yield break;

        q.OnQuestTaskProgress -= QuestTaskProgress;
        q.OnQuestTaskCompleted -= QuestTaskCompleted;

        _questsInProgress.Remove(q);
        _questsCompleted.Add(q);
        OnQuestCompleted?.Invoke(q);
        yield return q.AwardPlayer(this);

        yield return new WaitForSeconds(1);// StartCoroutine(q.AwardPlayer(this));
        yield return new WaitForSeconds(1);
        bool correctlySetNextQuest = false;
        if (q.NextQuests.Count == 0) correctlySetNextQuest = true;
        foreach (Quest newQuest in q.NextQuests)
        {
            if (newQuest == null)
            {
                Debug.LogError($"Some quest is null in {q.name}");
                continue;
            }
            if (newQuest.ConditionalLocks.IsAnyPadlockLocked()) continue;
            AddQuest(newQuest);
            correctlySetNextQuest = true;
        }

        if (!correctlySetNextQuest)
            AddQuest(q.NextQuests[0]);

        if (q == _questActive)
        {
            q.IsQuestActive = false;
            _questActive = null;
        }

        if (_questActive == null && _questsInProgress.Count > 0)
            ActivateQuest(_questsInProgress[0]);
    }

    private void QuestTaskProgress(Quest quest, AQuestTask questTask)
    {
        OnQuestTaskProgressed?.Invoke(quest, questTask);
    }

    private void QuestTaskCompleted(Quest quest, AQuestTask questTask)
    {
        OnQuestTaskCompleted?.Invoke(quest, questTask);
    }


    public int GetUniqueTaskID(Quest newQuest)
    {
        int taskID = 0;

        foreach (AQuestTask aqTask in newQuest.QuestTasks)
        {
            if (aqTask.TaskID <= taskID) continue;
            taskID = aqTask.TaskID;
        }

        foreach (Quest quest in _questsInProgress)
        {
            foreach (AQuestTask aqTask in quest.QuestTasks)
            {
                if (aqTask.TaskID <= taskID) continue;
                taskID = aqTask.TaskID;
            }
        }

        return taskID + 1;
    }

    public Quest GetActiveQuest() =>
        _questActive;

    public void ReceiveAwards(List<AQuestReward> rewards)
    {
        foreach (AQuestReward reward in rewards)
            StartCoroutine(reward.AwardPlayer());
    }

    public void ChangedDifficulty(object misisonsDisabled)
    {
        if (!Utilities.TryCast(misisonsDisabled, out bool disabled)) return;
        if (!disabled) return;
        DisableQuests();
        _tutorialPadlocks.UnlockAll();
    }

    public void DisableQuests()
    {
        _questsInProgress.Clear();
        OnQuestCompleted?.Invoke(_questActive);
        _questActive = null;
    }

    public void AbortCurrentQuest() => AbortQuest(_questActive);

    public void AbortQuest(string guid)
    {
        Quest q = _database.GetEntryOrDefault(guid);
        if (q == default) return;
        AbortQuest(q);
    }

    public void AbortQuest(Quest q)
    {
        if (q == null) return;

        _questsInProgress.Remove(q);
        OnQuestAborted?.Invoke(q);

        if (q == _questActive)
        {
            q.IsQuestActive = false;
            _questActive = null;
        }

        if (_questActive == null && _questsInProgress.Count > 0)
            ActivateQuest(_questsInProgress[0]);
        else
            _questActive = null;
    }

    public void ReceiveMapTask(MapTask mapTask)
    {
    }

    public IMapPinBlocker[] CanReceiveTask(MapTask mapTask)
    {
        if (SideQuestAlreadyActive()) { return _sideQuestBlockers; }
        return null;
    }

    public void InformAboutIncommingTask(MapTask mapTask)
    {
    }
}

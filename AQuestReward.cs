using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

public abstract class AQuestReward : SerializedScriptableObject
{
    [Button("Reward Player")]
    private void Test()
    {
        if (!SceneServiceLocator.InstanceExists)
        {
            Debug.LogError($"Couldn't reward player because there is no {typeof(SceneServiceLocator)}");
            return;
        }

        SceneServiceLocator.Instance.StartCoroutine(AwardPlayer());
    }

    public abstract IEnumerator AwardPlayer();
}

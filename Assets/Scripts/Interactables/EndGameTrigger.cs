using System.Collections;
using UnityEngine;

public class EndGameTrigger : QuestTrigger
{

    [SerializeField] float delayToEndGame = 15f;

    protected override void CompleteQuest()
    {
        base.CompleteQuest();
        StartCoroutine(EndGameCoroutine());
    }

    private IEnumerator EndGameCoroutine()
    {
        yield return new WaitForSeconds(delayToEndGame);
        GameManager.Instance.OnPlayerWin();
    }

}

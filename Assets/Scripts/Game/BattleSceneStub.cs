using TMPro;
using UnityEngine;

// BattleSceneStub — 战斗占位场景：显示匹配结果（计划 4 替换为真实战斗）
public class BattleSceneStub : MonoBehaviour
{
    [SerializeField] private TMP_Text infoText;
    private void Start()
    {
        infoText.text = $"Room {BattleContext.RoomId}\nOpponent UID {BattleContext.OpponentUid} (ELO {BattleContext.OpponentElo})\n\nBattle scene (Plan 4)";
    }
}

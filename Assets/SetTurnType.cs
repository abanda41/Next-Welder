using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

public class SetTurnType : MonoBehaviour
{
    public SnapTurnProvider snapTurn;
    public ContinuousTurnProvider continuousTurn;

    public void SetTypeFromIndex(int index)
    {
        if (index == 0)
        {
            snapTurn.enabled = false;
            continuousTurn.enabled = true;
        }
        else if (index == 1)
        {
            snapTurn.enabled = true;
            continuousTurn.enabled = false;
        }
    }
}

using UnityEngine;

namespace Game
{
    public class CheatManager : MonoBehaviour
    {
        #region Cheat Key Codes

        private const KeyCode COMPLETE_LEVEL_CHEAT_KEY = KeyCode.O;
        private const KeyCode FAIL_LEVEL_CHEAT_KEY = KeyCode.F;

        #endregion

        #region Cheat Methods

        private void CompleteLevel(bool isSuccess)
        {
            GameManager.Instance.GameplayController.CHEAT_FinishGameplay(isSuccess);
        }

        #endregion

        #region Unity Events Functions

        private void Update()
        {
            if (Input.GetKeyDown(COMPLETE_LEVEL_CHEAT_KEY))
            {
                CompleteLevel(true);
            }
            else if (Input.GetKeyDown(FAIL_LEVEL_CHEAT_KEY))
            {
                CompleteLevel(false);
            }
        }

        #endregion
    }
}
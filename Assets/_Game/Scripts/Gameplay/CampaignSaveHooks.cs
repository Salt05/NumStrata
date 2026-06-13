using NumStrata.Data;
using NumStrata.UI;
using UnityEngine;

namespace NumStrata.Gameplay
{
    /// <summary>
    /// Bridges gameplay events to LocalDataManager campaign APIs.
    /// </summary>
    public static class CampaignSaveHooks
    {
        public static void EvaluateTemporaryOutcomeAfterBoardChange()
        {
            if (!LevelLoader.IsLevelActive) return;
            if (LocalDataManager.Instance == null) return;

            int remainingTiles = TileCounter.Instance != null
                ? TileCounter.Instance.CountRemainingPlayableTiles()
                : 0;

            if (remainingTiles == 0)
            {
                bool isChallenge = PlayerPrefs.GetInt("IsChallengeMode", 0) == 1;
                string modeName = isChallenge ? "CHALLENGE" : "CAMPAIGN";
                Debug.Log($"[CampaignSaveHooks] WIN: {modeName} board cleared.");

                LocalDataManager.Instance.CompleteCampaignLevel();
                LevelLoader.IsLevelActive = false;

                // Show Win Screen Popup
                if (GameplayUIManager.Instance != null)
                {
                    GameplayUIManager.Instance.ShowWinScreen();
                }
                return;
            }

            int helperUsesLeft = 3;
            if (HelperManager.Instance != null)
            {
                helperUsesLeft = Mathf.Max(0, 3 - HelperManager.Instance.GetLevelHelperUses());
                if (HelperManager.Instance.hasFreeHelperUsage)
                {
                    helperUsesLeft++;
                }
            }

            if (remainingTiles + helperUsesLeft < 4)
            {
                Debug.LogWarning(
                    $"[CampaignSaveHooks] LOSE: remainingTiles={remainingTiles}, helperUsesLeft={helperUsesLeft} (Sum={remainingTiles + helperUsesLeft} < 4).");
                LocalDataManager.Instance.FailCampaignLevel();
                LevelLoader.IsLevelActive = false;

                // Show Lose Screen Popup
                if (GameplayUIManager.Instance != null)
                {
                    GameplayUIManager.Instance.ShowLoseScreen();
                }
            }
        }

        public static void NotifyLevelFailed()
        {
            if (!LevelLoader.IsLevelActive) return;
            LocalDataManager.Instance?.FailCampaignLevel();
            LevelLoader.IsLevelActive = false;

            // Show Lose Screen Popup
            if (GameplayUIManager.Instance != null)
            {
                GameplayUIManager.Instance.ShowLoseScreen();
            }
        }
    }
}

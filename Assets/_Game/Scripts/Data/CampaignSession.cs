using System;
using System.Text.RegularExpressions;

namespace NumStrata.Data
{
    /// <summary>
    /// Truyền level campaign giữa MainMenu và scene Gameplay.
    /// </summary>
    public static class CampaignSession
    {
        public const string PendingLevelIdKey = "PendingLevelId";
        public const string CampaignResourcesFolder = "Campaign";
        public const string StreakResourcesFolder = "Streak";
        private static readonly Regex LevelIndexSuffix = new Regex(@"(\d+)$", RegexOptions.Compiled);

        public static string GetCampaignResourcePath(string levelId)
        {
            // Kiểm tra xem có đang ở chế độ Challenge không để lấy đúng thư mục Resources
            bool isChallenge = UnityEngine.PlayerPrefs.GetInt("IsChallengeMode", 0) == 1;
            string folder = isChallenge ? StreakResourcesFolder : CampaignResourcesFolder;
            
            return $"{folder}/{levelId}";
        }

        public static string IndexToLevelId(int index)
        {
            return $"campaign_{Math.Max(1, index):D4}";
        }

        public static int LevelIdToIndex(string levelId)
        {
            if (string.IsNullOrEmpty(levelId)) return 0;

            Match match = LevelIndexSuffix.Match(levelId);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int index))
            {
                return index;
            }

            return 0;
        }

        public static void NormalizeCampaignPointer(CampaignSaveData campaign)
        {
            if (campaign == null) return;

            if (campaign.currentLevelIndex < 1)
            {
                campaign.currentLevelIndex = 1;
            }

            string expectedId = IndexToLevelId(campaign.currentLevelIndex);
            if (string.IsNullOrEmpty(campaign.currentLevelId) ||
                LevelIdToIndex(campaign.currentLevelId) != campaign.currentLevelIndex)
            {
                campaign.currentLevelId = expectedId;
            }
        }
    }
}

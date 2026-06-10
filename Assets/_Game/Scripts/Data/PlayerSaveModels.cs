using System;
using System.Collections.Generic;

namespace NumStrata.Data
{
    /// <summary>
    /// Schema cũ (v0) — chỉ dùng trong migration, không ghi mới.
    /// </summary>
    [Serializable]
    public class LegacyLevelProgress
    {
        public string levelId;
        public string state;
        public int attempts;
        public int helperUses;
    }

    public static class SaveSchemaVersions
    {
        public const int PlayerDataVersion = 1;
        public const int SessionResumeVersion = 1;
    }

    [Serializable]
    public class PlayerData
    {
        public int saveVersion = SaveSchemaVersions.PlayerDataVersion;
        public long lastModifiedAt;
        public bool isDirtyCloud;

        public string playerId;
        public string displayName;
        public string avatarUrl;
        public long createdAt;

        public int gold;
        public int totalStreak;
        public string streakIconId;

        public PlayerShield shield = new PlayerShield();
        public CampaignSaveData campaign = new CampaignSaveData();
        public DailySaveData daily = new DailySaveData();

        /// <summary>
        /// Chỉ đọc khi migrate v0 → v1. Không dùng sau khi save lại.
        /// JsonUtility vẫn deserialize field này nếu tồn tại trong JSON cũ.
        /// </summary>
        public List<LegacyLevelProgress> campaignProgress = new List<LegacyLevelProgress>();
    }

    [Serializable]
    public class PlayerShield
    {
        public bool hasShield;
        public long lastShieldConsumedAt;
        public long nextShieldRegenAt;
    }

    [Serializable]
    public class CampaignSaveData
    {
        public int currentLevelIndex = 1;
        public string currentLevelId = "campaign_0001";
        public bool hasActiveRun;
    }

    [Serializable]
    public class DailySaveData
    {
        public string currentWeekId; // YYYY_wWW
        public int currentStreakCount; // 0-7
        public List<int> completedDays = new List<int>(); // 1=Mon, 7=Sun
        public bool usedShieldThisWeek;
    }

    [Serializable]
    public class SessionResume
    {
        public int saveVersion = SaveSchemaVersions.SessionResumeVersion;
        public string activeLevelId;
        public int levelHelperUses;
        public long savedAt;
        public List<TileSaveState> tiles = new List<TileSaveState>();
    }

    [Serializable]
    public class TileSaveState
    {
        public string location;
        public int gridX;
        public int gridY;
        public int layerId;
        public int slotIndex;
        public string tileType;
        public int numberValue;
        public string operatorValue;
        public bool isMystery;
    }
}

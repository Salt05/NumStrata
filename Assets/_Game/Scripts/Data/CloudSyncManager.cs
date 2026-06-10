using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;

namespace NumStrata.Data
{
    public class CloudSyncManager : MonoBehaviour
    {
        public static CloudSyncManager Instance { get; private set; }

        public event Action<bool> OnConnectionStatusChanged;
        public event Action<string> OnSyncStatusChanged;

        private FirebaseAuth auth;
        private FirebaseFirestore db;
        public FirebaseFirestore Db => db;
        private FirebaseUser currentUser;
        
        [Header("Google Sign-In Settings")]
#if GOOGLE_SIGNIN_ENABLED
        [SerializeField] private string webClientId = "278330878957-rmgalaml6eltgg4j76t2jplituu4s29p.apps.googleusercontent.com";
#endif
        
        private bool isFirebaseInitialized = false;
        private bool isConnecting = false;
        private bool isSyncing = false;

        public bool IsConnected => isFirebaseInitialized && currentUser != null && Application.internetReachability != NetworkReachability.NotReachable;
        public string UserUid => currentUser?.UserId;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            InitializeFirebaseSDK();
        }

        private void InitializeFirebaseSDK()
        {
            if (isConnecting) return;
            isConnecting = true;
            OnSyncStatusChanged?.Invoke("Initializing Firebase...");

            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                isConnecting = false;
                var dependencyStatus = task.Result;
                if (dependencyStatus == DependencyStatus.Available)
                {
                    auth = FirebaseAuth.DefaultInstance;
                    db = FirebaseFirestore.DefaultInstance;
                    isFirebaseInitialized = true;
                    Debug.Log("[CloudSyncManager] Firebase initialized successfully.");
                    
                    // Lắng nghe thay đổi trạng thái auth (nếu có)
                    auth.StateChanged += OnAuthStateChanged;
                    
                    // Tiến hành xác thực ẩn danh
                    AuthenticateAnonymously();
                }
                else
                {
                    isFirebaseInitialized = false;
                    string errorMsg = $"Could not resolve all Firebase dependencies: {dependencyStatus}";
                    Debug.LogError($"[CloudSyncManager] {errorMsg}");
                    OnSyncStatusChanged?.Invoke("Firebase initialization failed.");
                }
            });
        }

        private void OnAuthStateChanged(object sender, EventArgs e)
        {
            if (auth.CurrentUser != currentUser)
            {
                currentUser = auth.CurrentUser;
                bool signedIn = currentUser != null;
                if (signedIn)
                {
                    Debug.Log($"[CloudSyncManager] Authenticated as: {currentUser.UserId}");
                    OnConnectionStatusChanged?.Invoke(true);
                }
                else
                {
                    Debug.Log("[CloudSyncManager] Signed out.");
                    OnConnectionStatusChanged?.Invoke(false);
                }
            }
        }

        private void AuthenticateAnonymously()
        {
            if (!isFirebaseInitialized) return;

            if (auth.CurrentUser != null)
            {
                currentUser = auth.CurrentUser;
                Debug.Log($"[CloudSyncManager] Already signed in as: {currentUser.UserId}");
                SyncWithCloud();
                return;
            }

            OnSyncStatusChanged?.Invoke("Authenticating...");
            auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    Debug.LogError($"[CloudSyncManager] Anonymous auth failed: {task.Exception}");
                    OnSyncStatusChanged?.Invoke("Authentication failed.");
                    OnConnectionStatusChanged?.Invoke(false);
                    return;
                }

                currentUser = task.Result.User;
                Debug.Log($"[CloudSyncManager] Authenticated successfully as Anonymous: {currentUser.UserId}");
                
                // Tiến hành đồng bộ sau khi xác thực thành công
                SyncWithCloud();
            });
        }

        public void SignInWithGoogleReal(Action<bool, string> callback)
        {
#if GOOGLE_SIGNIN_ENABLED
            if (Application.isEditor)
            {
                Debug.LogWarning("[CloudSyncManager] Google Sign-In real popup is not supported in Unity Editor. Using Mock instead.");
                MockGoogleSignIn("Google Gamer Demo", "data:image/png;base64,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", callback);
                return;
            }

            // Khởi tạo cấu hình cho Google Sign-In
            var configuration = new Google.GoogleSignInConfiguration
            {
                WebClientId = webClientId,
                RequestIdToken = true,
                RequestEmail = true,
                RequestProfile = true
            };
            Google.GoogleSignIn.Configuration = configuration;

            Debug.Log("[CloudSyncManager] Starting Google Sign-In...");
            Google.GoogleSignIn.DefaultInstance.SignIn().ContinueWithOnMainThread((System.Threading.Tasks.Task<Google.GoogleSignInUser> task) =>
            {
                if (task.IsCanceled)
                {
                    callback?.Invoke(false, "Google Sign-In canceled.");
                }
                else if (task.IsFaulted)
                {
                    callback?.Invoke(false, "Google Sign-In failed: " + task.Exception);
                }
                else
                {
                    // Lấy Token của Google
                    string idToken = task.Result.IdToken;
                    // Tiến hành đăng nhập Firebase bằng Token này
                    SignInWithGoogle(idToken, null, callback);
                }
            });
#else
            Debug.LogWarning("[CloudSyncManager] GOOGLE_SIGNIN_ENABLED is not defined. Using Mock Google Login instead.");
            // Tự động mock nếu chưa cấu hình SDK
            MockGoogleSignIn("guest", "data:image/png;base64,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", callback);
#endif
        }

        public void SignInWithGoogle(string idToken, string accessToken, Action<bool, string> callback)
        {
            if (!isFirebaseInitialized)
            {
                callback?.Invoke(false, "Firebase not initialized.");
                return;
            }

            Credential credential = GoogleAuthProvider.GetCredential(idToken, accessToken);
            
            if (currentUser != null && currentUser.IsAnonymous)
            {
                currentUser.LinkWithCredentialAsync(credential).ContinueWithOnMainThread((System.Threading.Tasks.Task task) =>
                {
                    if (task.IsCanceled || task.IsFaulted)
                    {
                        Debug.LogError($"[CloudSyncManager] Link with Google failed: {task.Exception}");
                        SignInDirectlyWithCredential(credential, callback);
                    }
                    else
                    {
                        currentUser = auth.CurrentUser;
                        UpdateUserDataFromGoogle(currentUser);
                        Debug.Log("[CloudSyncManager] Anonymous account linked with Google successfully.");
                        callback?.Invoke(true, "Linked successfully.");
                        SyncWithCloud();
                    }
                });
            }
            else
            {
                SignInDirectlyWithCredential(credential, callback);
            }
        }

        private void SignInDirectlyWithCredential(Credential credential, Action<bool, string> callback)
        {
            auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread((System.Threading.Tasks.Task task) =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    Debug.LogError($"[CloudSyncManager] Sign in with Google failed: {task.Exception}");
                    callback?.Invoke(false, task.Exception?.Message ?? "Sign in failed.");
                }
                else
                {
                    currentUser = auth.CurrentUser;
                    UpdateUserDataFromGoogle(currentUser);
                    Debug.Log($"[CloudSyncManager] Signed in with Google successfully: {currentUser.UserId}");
                    callback?.Invoke(true, "Signed in successfully.");
                    SyncWithCloud();
                }
            });
        }

        private void UpdateUserDataFromGoogle(FirebaseUser user)
        {
            if (LocalDataManager.Instance.CurrentPlayer != null && user != null)
            {
                string displayName = user.DisplayName;
                string avatarUrl = user.PhotoUrl?.ToString();

                // Nếu các trường root bị trống (thường gặp khi link tài khoản lần đầu), kiểm tra trong ProviderData
                if (user.ProviderData != null)
                {
                    foreach (var profile in user.ProviderData)
                    {
                        if (profile.ProviderId == "google.com")
                        {
                            if (string.IsNullOrEmpty(displayName))
                            {
                                displayName = profile.DisplayName;
                            }
                            if (string.IsNullOrEmpty(avatarUrl))
                            {
                                avatarUrl = profile.PhotoUrl?.ToString();
                            }
                            break;
                        }
                    }
                }

                // Nếu vẫn trống, ta gán tạm email hoặc ID làm tên để không bị trống giao diện
                if (string.IsNullOrEmpty(displayName))
                {
                    displayName = user.Email;
                }
                if (string.IsNullOrEmpty(displayName))
                {
                    displayName = "Google User";
                }

                LocalDataManager.Instance.CurrentPlayer.displayName = displayName;
                LocalDataManager.Instance.CurrentPlayer.avatarUrl = avatarUrl ?? "";
                LocalDataManager.Instance.CurrentPlayer.playerId = user.UserId;
                LocalDataManager.Instance.MarkPlayerDirty();
                LocalDataManager.Instance.FlushPlayerData();
            }
        }

        public void MockGoogleSignIn(string mockName, string mockAvatarUrl, Action<bool, string> callback)
        {
            if (LocalDataManager.Instance.CurrentPlayer == null)
            {
                callback?.Invoke(false, "Local player data not loaded.");
                return;
            }

            LocalDataManager.Instance.CurrentPlayer.displayName = mockName;
            LocalDataManager.Instance.CurrentPlayer.avatarUrl = mockAvatarUrl;
            LocalDataManager.Instance.MarkPlayerDirty();
            LocalDataManager.Instance.FlushPlayerData();

            Debug.Log($"[CloudSyncManager] Mock Google Sign-In successful. Name: {mockName}, Avatar: {mockAvatarUrl}");
            callback?.Invoke(true, "Mock Sign-in successful.");
            
            if (IsConnected)
            {
                SyncWithCloud();
            }
        }

        /// <summary>
        /// Đồng bộ 2 chiều (Pull dữ liệu từ cloud về, so sánh timestamp để Merge, sau đó Push ngược lên nếu local mới hơn).
        /// </summary>
        public void SyncWithCloud()
        {
            if (isSyncing || !IsConnected)
            {
                if (!IsConnected)
                {
                    Debug.LogWarning("[CloudSyncManager] Cannot sync: No connection or not authenticated.");
                    OnSyncStatusChanged?.Invoke("Sync skipped (Offline)");
                }
                return;
            }

            isSyncing = true;
            OnSyncStatusChanged?.Invoke("Syncing...");
            Debug.Log("[CloudSyncManager] Starting Sync with Cloud...");

            DocumentReference docRef = db.Collection("users").Document(currentUser.UserId);
            docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    isSyncing = false;
                    Debug.LogError($"[CloudSyncManager] Pull failed: {task.Exception}");
                    OnSyncStatusChanged?.Invoke("Sync failed.");
                    return;
                }

                DocumentSnapshot snapshot = task.Result;
                PlayerData localData = LocalDataManager.Instance.CurrentPlayer;

                if (localData == null)
                {
                    isSyncing = false;
                    Debug.LogError("[CloudSyncManager] Local PlayerData is null. Cannot merge.");
                    OnSyncStatusChanged?.Invoke("Sync failed: No local data.");
                    return;
                }

                if (snapshot.Exists)
                {
                    // Lấy JSON data từ cloud document
                    if (snapshot.TryGetValue("dataJson", out string remoteJson) && !string.IsNullOrEmpty(remoteJson))
                    {
                        try
                        {
                            PlayerData remoteData = JsonUtility.FromJson<PlayerData>(remoteJson);
                            MergeAndResolve(localData, remoteData);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[CloudSyncManager] Error parsing remote data: {ex.Message}. Overwriting cloud.");
                            // Nếu lỗi parse, đè bản local lên cloud
                            ForcePush(localData);
                        }
                    }
                    else
                    {
                        // Document tồn tại nhưng không có trường dataJson
                        Debug.LogWarning("[CloudSyncManager] Cloud document empty. Forcing push local.");
                        ForcePush(localData);
                    }
                }
                else
                {
                    // Tài khoản mới chưa có data trên cloud -> Đẩy local hiện tại lên
                    Debug.Log("[CloudSyncManager] Cloud document does not exist. Initializing cloud save.");
                    ForcePush(localData);
                }
            });
        }

        private void MergeAndResolve(PlayerData local, PlayerData remote)
        {
            Debug.Log($"[CloudSyncManager] Merging. Local timestamp: {local.lastModifiedAt}, Cloud: {remote.lastModifiedAt}");

            if (remote.lastModifiedAt > local.lastModifiedAt)
            {
                // Dữ liệu cloud mới hơn -> Nhận dữ liệu cloud
                Debug.Log("[CloudSyncManager] Cloud data is newer. Pulling cloud data.");
                LocalDataManager.Instance.UpdatePlayerFromCloud(remote);
                isSyncing = false;
                OnSyncStatusChanged?.Invoke("Sync Completed (Pull)");
            }
            else if (local.lastModifiedAt > remote.lastModifiedAt || local.isDirtyCloud)
            {
                // Dữ liệu local mới hơn hoặc local dirty -> Đẩy lên cloud
                Debug.Log("[CloudSyncManager] Local data is newer or dirty. Pushing to cloud.");
                PushToCloudInternal(local);
            }
            else
            {
                // Bằng nhau và không dirty -> Đồng bộ hoàn tất
                Debug.Log("[CloudSyncManager] Local and Cloud are in sync.");
                isSyncing = false;
                // Cập nhật lại flag local
                if (local.isDirtyCloud)
                {
                    local.isDirtyCloud = false;
                    LocalDataManager.Instance.FlushPlayerDataIfDirty();
                }
                OnSyncStatusChanged?.Invoke("Sync Completed (In Sync)");
            }
        }

        /// <summary>
        /// Đẩy dữ liệu local lên cloud chỉ khi local dirty (isDirtyCloud == true).
        /// </summary>
        public void PushToCloud()
        {
            PlayerData localData = LocalDataManager.Instance.CurrentPlayer;
            if (localData == null || !localData.isDirtyCloud) return;

            if (!IsConnected)
            {
                Debug.Log("[CloudSyncManager] Offline; save will sync when online.");
                return;
            }

            PushToCloudInternal(localData);
        }

        private void ForcePush(PlayerData data)
        {
            PushToCloudInternal(data);
        }

        private void PushToCloudInternal(PlayerData data)
        {
            if (!IsConnected)
            {
                isSyncing = false;
                return;
            }

            // Gán lại playerId trùng với Firebase UID để đồng bộ và dễ quản lý
            if (data.playerId != currentUser.UserId)
            {
                data.playerId = currentUser.UserId;
            }

            // Đảm bảo cập nhật timestamp trước khi push
            data.lastModifiedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            data.isDirtyCloud = false;

            string json = JsonUtility.ToJson(data, false);
            Dictionary<string, object> docData = new Dictionary<string, object>
            {
                { "lastModifiedAt", data.lastModifiedAt },
                { "gold", data.gold },
                { "currentLevelIndex", data.campaign?.currentLevelIndex ?? 1 },
                { "totalStreak", data.totalStreak },
                { "displayName", data.displayName ?? "" },
                { "avatarUrl", data.avatarUrl ?? "" },
                { "dataJson", json }
            };

            DocumentReference docRef = db.Collection("users").Document(currentUser.UserId);
            docRef.SetAsync(docData).ContinueWithOnMainThread(task =>
            {
                isSyncing = false;
                if (task.IsCanceled || task.IsFaulted)
                {
                    Debug.LogError($"[CloudSyncManager] Push failed: {task.Exception}");
                    // Phục hồi lại flag dirty để thử lại sau
                    data.isDirtyCloud = true;
                    LocalDataManager.Instance.FlushPlayerDataIfDirty();
                    OnSyncStatusChanged?.Invoke("Push failed.");
                }
                else
                {
                    Debug.Log("[CloudSyncManager] Data pushed successfully to Cloud.");
                    // Lưu lại local để update flag isDirtyCloud = false
                    LocalDataManager.Instance.FlushPlayerData();
                    OnSyncStatusChanged?.Invoke("Sync Completed (Push)");
                }
            });
        }

        public bool IsGoogleConnected()
        {
#if GOOGLE_SIGNIN_ENABLED
            return currentUser != null && !currentUser.IsAnonymous;
#else
            if (LocalDataManager.Instance != null && LocalDataManager.Instance.CurrentPlayer != null)
            {
                string name = LocalDataManager.Instance.CurrentPlayer.displayName;
                return !string.IsNullOrEmpty(name) && !name.StartsWith("Player_");
            }
            return false;
#endif
        }

        public void SignOut()
        {
            if (auth != null)
            {
                auth.SignOut();
                Debug.Log("[CloudSyncManager] User signed out from Firebase.");
            }

            if (LocalDataManager.Instance != null && LocalDataManager.Instance.CurrentPlayer != null)
            {
                LocalDataManager.Instance.CurrentPlayer.displayName = "Player_" + UnityEngine.Random.Range(1000, 9999);
                LocalDataManager.Instance.CurrentPlayer.avatarUrl = "";
                LocalDataManager.Instance.MarkPlayerDirty();
                LocalDataManager.Instance.FlushPlayerData();
            }

            AuthenticateAnonymously();
        }

        private void OnDestroy()
        {
            if (auth != null)
            {
                auth.StateChanged -= OnAuthStateChanged;
            }
        }
    }
}

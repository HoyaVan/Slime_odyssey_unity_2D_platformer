using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class AdminManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] TextMeshProUGUI feedbackText; // Feedback 텍스트
    [SerializeField] Button gameStartButton; // GameStartButton - 게임으로 돌아가기

    [Header("User List")]
    [SerializeField] TextMeshProUGUI userListText; // UserList > Content의 TextMeshProUGUI (직접 할당)

    [Header("Point History")]
    [SerializeField] TextMeshProUGUI pointHistoryText; // PointHistory > Content의 TextMeshProUGUI (직접 할당)

    [Header("User Details (Optional - 필요시 추가)")]
    [SerializeField] TextMeshProUGUI selectedUserNameText;
    [SerializeField] TextMeshProUGUI selectedUserPointsText;
    [SerializeField] TMP_InputField pointsInputField;
    [SerializeField] Button updatePointsButton;
    [SerializeField] TMP_InputField addPointsInputField;
    [SerializeField] Button addPointsButton;

    [Header("Server Configuration")]
    [SerializeField] string serverUrl = "http://localhost:3000";

    [Header("Scene Names")]
    [SerializeField] string gameSceneName = "Map1";

    private List<UserData> users = new List<UserData>();
    private UserData selectedUser = null;
    private string sessionId;
    private string currentUserId;

    void Start()
    {
        sessionId = PlayerPrefs.GetString("SessionId", string.Empty);
        currentUserId = PlayerPrefs.GetString("PlayerID", "Unknown");
        string userRole = PlayerPrefs.GetString("UserRole", "player");

        // 어드민이 아니면 메인 메뉴로 돌아가기
        if (userRole != "admin")
        {
            Debug.LogWarning("Access denied: Admin only");
            SceneManager.LoadScene("Main");
            return;
        }

        // UI 초기화
        if (feedbackText != null)
        {
            feedbackText.text = $"Welcome, Admin: {currentUserId}";
        }

        if (gameStartButton != null)
        {
            gameStartButton.onClick.AddListener(() =>
            {
                if (AudioPlayer.Instance != null) AudioPlayer.Instance.PlayButtonClick();
                SceneManager.LoadScene(gameSceneName);
            });
        }

        if (updatePointsButton != null)
        {
            updatePointsButton.onClick.AddListener(() =>
            {
                if (AudioPlayer.Instance != null) AudioPlayer.Instance.PlayButtonClick();
                UpdateUserPoints();
            });
        }

        if (addPointsButton != null)
        {
            addPointsButton.onClick.AddListener(() =>
            {
                if (AudioPlayer.Instance != null) AudioPlayer.Instance.PlayButtonClick();
                AddUserPoints();
            });
        }

        // 사용자 목록 로드
        LoadUsers();
    }

    public void LoadUsers()
    {
        StartCoroutine(FetchUsers());
    }

    private IEnumerator FetchUsers()
    {
        if (feedbackText != null)
        {
            feedbackText.text = "Loading users...";
        }

        string url = serverUrl + "/game/admin/users";

        UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("Authorization", $"Session {sessionId}");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success && request.responseCode == 200)
        {
            UsersResponse response = JsonUtility.FromJson<UsersResponse>(request.downloadHandler.text);
            if (response != null && response.users != null)
            {
                users = new List<UserData>(response.users);
                DisplayUsers();

                if (feedbackText != null)
                {
                    feedbackText.text = $"Loaded {users.Count} users";
                }
            }
        }
        else
        {
            Debug.LogError($"Failed to load users: {request.error} (Status: {request.responseCode})");
            if (feedbackText != null)
            {
                feedbackText.text = $"Error: Failed to load users";
            }

            if (request.responseCode == 401)
            {
                Debug.LogError("Unauthorized: Please login again");
                SceneManager.LoadScene("Main");
            }
        }

        request.Dispose();
    }

    void DisplayUsers()
    {
        // 사용자 목록을 하나의 텍스트로 합쳐서 표시
        if (userListText != null)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            for (int i = 0; i < users.Count; i++)
            {
                UserData user = users[i];
                sb.AppendLine($"{i + 1}. {user.ID} ({user.role}) - Total: {user.total_points} pts | Highest: {user.highest_points} pts");
            }

            userListText.text = sb.ToString();
        }

        // 전체 포인트 기록 자동 로드
        LoadAllHistory();
    }

    void SelectUser(UserData user)
    {
        selectedUser = user;

        if (selectedUserNameText != null) selectedUserNameText.text = $"User: {user.ID}";
        if (selectedUserPointsText != null) selectedUserPointsText.text = $"Total Points: {user.total_points} | Highest Points: {user.highest_points}";
        if (pointsInputField != null) pointsInputField.text = user.total_points.ToString();
        if (addPointsInputField != null) addPointsInputField.text = "0";

        if (feedbackText != null)
        {
            feedbackText.text = $"Selected: {user.ID} ({user.role}) - Total: {user.total_points} pts | Highest: {user.highest_points} pts";
        }
    }

    void UpdateUserPoints()
    {
        if (selectedUser == null)
        {
            if (feedbackText != null) feedbackText.text = "Please select a user first";
            return;
        }

        if (pointsInputField != null && int.TryParse(pointsInputField.text, out int points))
        {
            if (points < 0)
            {
                if (feedbackText != null) feedbackText.text = "Points cannot be negative";
                return;
            }
            StartCoroutine(UpdatePoints(selectedUser.index_id, points));
        }
        else
        {
            if (feedbackText != null) feedbackText.text = "Invalid points value";
        }
    }

    void AddUserPoints()
    {
        if (selectedUser == null)
        {
            if (feedbackText != null) feedbackText.text = "Please select a user first";
            return;
        }

        if (addPointsInputField != null && int.TryParse(addPointsInputField.text, out int points))
        {
            if (points == 0)
            {
                if (feedbackText != null) feedbackText.text = "Points amount cannot be zero";
                return;
            }
            StartCoroutine(AddPoints(selectedUser.index_id, points));
        }
        else
        {
            if (feedbackText != null) feedbackText.text = "Invalid points value";
        }
    }

    void LoadAllHistory()
    {
        StartCoroutine(FetchAllHistory());
    }

    void LoadUserHistory(int userId)
    {
        StartCoroutine(FetchUserHistory(userId));
    }

    private IEnumerator FetchAllHistory()
    {
        string url = serverUrl + "/game/admin/points/all-history?limit=100";

        UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("Authorization", $"Session {sessionId}");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success && request.responseCode == 200)
        {
            AllHistoryResponse response = JsonUtility.FromJson<AllHistoryResponse>(request.downloadHandler.text);
            if (response != null && response.history != null)
            {
                DisplayAllHistory(response.history);
            }
        }
        else
        {
            Debug.LogError($"Failed to load all history: {request.error} (Status: {request.responseCode})");
            if (pointHistoryText != null)
            {
                pointHistoryText.text = "Failed to load history";
            }
        }

        request.Dispose();
    }

    private IEnumerator FetchUserHistory(int userId)
    {
        string url = serverUrl + $"/game/admin/points/{userId}/history";

        UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("Authorization", $"Session {sessionId}");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success && request.responseCode == 200)
        {
            HistoryResponse response = JsonUtility.FromJson<HistoryResponse>(request.downloadHandler.text);
            if (response != null && response.history != null)
            {
                DisplayHistory(response.history);
            }
        }
        else
        {
            Debug.LogError($"Failed to load history: {request.error} (Status: {request.responseCode})");
        }

        request.Dispose();
    }

    void DisplayAllHistory(AllHistoryItem[] history)
    {
        // 전체 포인트 기록을 하나의 텍스트로 합쳐서 표시 (최신순)
        if (pointHistoryText != null)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            if (history == null || history.Length == 0)
            {
                pointHistoryText.text = "No history available.";
                return;
            }

            foreach (AllHistoryItem item in history)
            {
                string date = item.created_at ?? item.transaction_date ?? "Unknown";
                int points = item.point_num;
                string userId = item.user_id ?? "Unknown";
                string sign = points > 0 ? "+" : "";
                string colorTag = points > 0 ? "<color=green>" : points < 0 ? "<color=red>" : "";
                string endColorTag = points != 0 ? "</color>" : "";

                sb.AppendLine($"{date} | {userId}: {colorTag}{sign}{points}{endColorTag}");
            }

            pointHistoryText.text = sb.ToString();
        }
    }

    void DisplayHistory(HistoryItem[] history)
    {
        // 특정 사용자의 히스토리를 하나의 텍스트로 합쳐서 표시
        if (pointHistoryText != null)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            if (history == null || history.Length == 0)
            {
                pointHistoryText.text = "No history available.";
                return;
            }

            foreach (HistoryItem item in history)
            {
                string date = item.created_at ?? item.transaction_date ?? "Unknown";
                int points = item.point_num;
                string sign = points > 0 ? "+" : "";
                string colorTag = points > 0 ? "<color=green>" : points < 0 ? "<color=red>" : "";
                string endColorTag = points != 0 ? "</color>" : "";

                sb.AppendLine($"{date}: {colorTag}{sign}{points}{endColorTag}");
            }

            pointHistoryText.text = sb.ToString();
        }
    }

    private IEnumerator UpdatePoints(int userId, int points)
    {
        if (feedbackText != null)
        {
            feedbackText.text = "Updating points...";
        }

        string url = serverUrl + $"/game/admin/points/{userId}";

        // PUT 요청은 JSON으로 전송
        string jsonData = $"{{\"points\": {points}}}";
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);

        UnityWebRequest request = new UnityWebRequest(url, "PUT");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Authorization", $"Session {sessionId}");
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success && request.responseCode == 200)
        {
            Debug.Log($"Points updated successfully for user {userId}");
            if (feedbackText != null)
            {
                feedbackText.text = $"Points updated successfully!";
            }
            LoadUsers(); // 목록 새로고침
            if (selectedUser != null)
            {
                selectedUser.total_points = points;
                if (selectedUserPointsText != null) selectedUserPointsText.text = $"Current Points: {points}";
            }
        }
        else
        {
            Debug.LogError($"Failed to update points: {request.error} (Status: {request.responseCode})");
            if (feedbackText != null)
            {
                feedbackText.text = $"Error: Failed to update points";
            }
        }

        request.Dispose();
    }

    private IEnumerator AddPoints(int userId, int points)
    {
        if (feedbackText != null)
        {
            feedbackText.text = "Adding points...";
        }

        string url = serverUrl + $"/game/admin/points/{userId}/add";

        WWWForm form = new WWWForm();
        form.AddField("amount", points.ToString());

        UnityWebRequest request = UnityWebRequest.Post(url, form);
        request.SetRequestHeader("Authorization", $"Session {sessionId}");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success && request.responseCode == 200)
        {
            PointsResponse response = JsonUtility.FromJson<PointsResponse>(request.downloadHandler.text);
            if (response != null)
            {
                Debug.Log($"Points added successfully. New total: {response.totalPoints}");
                if (feedbackText != null)
                {
                    feedbackText.text = $"Points added! New total: {response.totalPoints}";
                }
                LoadUsers(); // 목록 새로고침
                if (selectedUser != null)
                {
                    selectedUser.total_points = response.totalPoints;
                    if (selectedUserPointsText != null) selectedUserPointsText.text = $"Current Points: {response.totalPoints}";
                    if (pointsInputField != null) pointsInputField.text = response.totalPoints.ToString();
                }
                // 전체 히스토리 새로고침
                LoadAllHistory();
            }
        }
        else
        {
            Debug.LogError($"Failed to add points: {request.error} (Status: {request.responseCode})");
            if (feedbackText != null)
            {
                feedbackText.text = $"Error: Failed to add points";
            }
        }

        request.Dispose();
    }

    [System.Serializable]
    public class UserData
    {
        public int index_id;
        public string ID;
        public string role;
        public int total_points;
        public int highest_points;
        public string created_at;
    }

    [System.Serializable]
    public class UsersResponse
    {
        public UserData[] users;
    }

    [System.Serializable]
    public class PointsResponse
    {
        public string message;
        public int totalPoints;
    }

    [System.Serializable]
    public class HistoryResponse
    {
        public int userId;
        public HistoryItem[] history;
    }

    [System.Serializable]
    public class HistoryItem
    {
        public int user_point_id;
        public int point_id;
        public int point_num;
        public string created_at;
        public string transaction_date;
    }

    [System.Serializable]
    public class AllHistoryResponse
    {
        public int total;
        public AllHistoryItem[] history;
    }

    [System.Serializable]
    public class AllHistoryItem
    {
        public int user_point_id;
        public int point_id;
        public int point_num;
        public string created_at;
        public string transaction_date;
        public string user_id;
        public int user_index_id;
    }
}

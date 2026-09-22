using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class AdminSceneManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] TextMeshProUGUI welcomeText;
    [SerializeField] Button backToGameButton;
    [SerializeField] Button refreshButton;

    [Header("User List")]
    [SerializeField] Transform userListContainer;
    [SerializeField] GameObject userItemPrefab;

    [Header("User Details Panel")]
    [SerializeField] GameObject userDetailsPanel;
    [SerializeField] TextMeshProUGUI selectedUserNameText;
    [SerializeField] TextMeshProUGUI selectedUserRoleText;
    [SerializeField] TextMeshProUGUI selectedUserPointsText;
    [SerializeField] TextMeshProUGUI selectedUserCreatedAtText;

    [Header("Point Management")]
    [SerializeField] TMP_InputField setPointsInputField;
    [SerializeField] Button setPointsButton;
    [SerializeField] TMP_InputField addPointsInputField;
    [SerializeField] Button addPointsButton;
    [SerializeField] Button viewHistoryButton;

    [Header("History Panel")]
    [SerializeField] GameObject historyPanel;
    [SerializeField] Transform historyListContainer;
    [SerializeField] GameObject historyItemPrefab;
    [SerializeField] Button closeHistoryButton;

    [Header("Server Configuration")]
    [SerializeField] string serverUrl = "http://localhost:3000";

    [Header("Scene Names")]
    [SerializeField] string mainMenuSceneName = "Main";
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
            SceneManager.LoadScene(mainMenuSceneName);
            return;
        }

        // UI 초기화
        if (welcomeText != null)
        {
            welcomeText.text = $"Welcome, Admin: {currentUserId}";
        }

        if (backToGameButton != null)
        {
            backToGameButton.onClick.AddListener(() =>
            {
                if (AudioPlayer.Instance != null) AudioPlayer.Instance.PlayButtonClick();
                SceneManager.LoadScene(gameSceneName);
            });
        }

        if (refreshButton != null)
        {
            refreshButton.onClick.AddListener(() =>
            {
                if (AudioPlayer.Instance != null) AudioPlayer.Instance.PlayButtonClick();
                LoadUsers();
            });
        }

        if (setPointsButton != null)
        {
            setPointsButton.onClick.AddListener(() =>
            {
                if (AudioPlayer.Instance != null) AudioPlayer.Instance.PlayButtonClick();
                SetUserPoints();
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

        if (viewHistoryButton != null)
        {
            viewHistoryButton.onClick.AddListener(() =>
            {
                if (AudioPlayer.Instance != null) AudioPlayer.Instance.PlayButtonClick();
                ViewUserHistory();
            });
        }

        if (closeHistoryButton != null)
        {
            closeHistoryButton.onClick.AddListener(() =>
            {
                if (AudioPlayer.Instance != null) AudioPlayer.Instance.PlayButtonClick();
                if (historyPanel != null) historyPanel.SetActive(false);
            });
        }

        if (userDetailsPanel != null) userDetailsPanel.SetActive(false);
        if (historyPanel != null) historyPanel.SetActive(false);

        // 사용자 목록 로드
        LoadUsers();
    }

    public void LoadUsers()
    {
        StartCoroutine(FetchUsers());
    }

    private IEnumerator FetchUsers()
    {
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
            }
        }
        else
        {
            Debug.LogError($"Failed to load users: {request.error} (Status: {request.responseCode})");
            if (request.responseCode == 401)
            {
                Debug.LogError("Unauthorized: Please login again");
                SceneManager.LoadScene(mainMenuSceneName);
            }
        }

        request.Dispose();
    }

    void DisplayUsers()
    {
        // 기존 항목 제거
        if (userListContainer != null)
        {
            foreach (Transform child in userListContainer)
            {
                Destroy(child.gameObject);
            }
        }

        // 사용자 목록 표시
        foreach (UserData user in users)
        {
            if (userItemPrefab != null && userListContainer != null)
            {
                GameObject item = Instantiate(userItemPrefab, userListContainer);

                // 텍스트 찾기
                TextMeshProUGUI[] texts = item.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length > 0)
                {
                    texts[0].text = $"{user.ID}";
                }
                if (texts.Length > 1)
                {
                    texts[1].text = $"Role: {user.role} | Total Points: {user.total_points} | Highest Points: {user.highest_points}";
                }

                Button itemButton = item.GetComponent<Button>();
                if (itemButton == null)
                {
                    itemButton = item.GetComponentInChildren<Button>();
                }

                if (itemButton != null)
                {
                    UserData userCopy = user; // 클로저 문제 방지
                    itemButton.onClick.AddListener(() =>
                    {
                        if (AudioPlayer.Instance != null) AudioPlayer.Instance.PlayButtonClick();
                        SelectUser(userCopy);
                    });
                }
            }
        }
    }

    void SelectUser(UserData user)
    {
        selectedUser = user;
        if (userDetailsPanel != null) userDetailsPanel.SetActive(true);

        if (selectedUserNameText != null) selectedUserNameText.text = $"User: {user.ID}";
        if (selectedUserRoleText != null) selectedUserRoleText.text = $"Role: {user.role}";
        if (selectedUserPointsText != null) selectedUserPointsText.text = $"Total Points: {user.total_points} | Highest Points: {user.highest_points}";
        if (selectedUserCreatedAtText != null && !string.IsNullOrEmpty(user.created_at))
        {
            selectedUserCreatedAtText.text = $"Created: {user.created_at}";
        }

        if (setPointsInputField != null) setPointsInputField.text = user.total_points.ToString();
        if (addPointsInputField != null) addPointsInputField.text = "0";
    }

    void SetUserPoints()
    {
        if (selectedUser == null) return;

        if (setPointsInputField != null && int.TryParse(setPointsInputField.text, out int points))
        {
            if (points < 0)
            {
                Debug.LogWarning("Points cannot be negative");
                return;
            }
            StartCoroutine(UpdatePoints(selectedUser.index_id, points));
        }
        else
        {
            Debug.LogWarning("Invalid points value");
        }
    }

    void AddUserPoints()
    {
        if (selectedUser == null) return;

        if (addPointsInputField != null && int.TryParse(addPointsInputField.text, out int points))
        {
            if (points == 0)
            {
                Debug.LogWarning("Points amount cannot be zero");
                return;
            }
            StartCoroutine(AddPoints(selectedUser.index_id, points));
        }
        else
        {
            Debug.LogWarning("Invalid points value");
        }
    }

    void ViewUserHistory()
    {
        if (selectedUser == null) return;
        StartCoroutine(LoadUserHistory());
    }

    private IEnumerator UpdatePoints(int userId, int points)
    {
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
        }

        request.Dispose();
    }

    private IEnumerator AddPoints(int userId, int points)
    {
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
                LoadUsers(); // 목록 새로고침
                if (selectedUser != null)
                {
                    selectedUser.total_points = response.totalPoints;
                    if (selectedUserPointsText != null) selectedUserPointsText.text = $"Current Points: {response.totalPoints}";
                    if (setPointsInputField != null) setPointsInputField.text = response.totalPoints.ToString();
                }
            }
        }
        else
        {
            Debug.LogError($"Failed to add points: {request.error} (Status: {request.responseCode})");
        }

        request.Dispose();
    }

    private IEnumerator LoadUserHistory()
    {
        if (selectedUser == null) yield break;

        string url = serverUrl + $"/game/admin/points/{selectedUser.index_id}/history";

        UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("Authorization", $"Session {sessionId}");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success && request.responseCode == 200)
        {
            HistoryResponse response = JsonUtility.FromJson<HistoryResponse>(request.downloadHandler.text);
            if (response != null && response.history != null)
            {
                DisplayHistory(response.history);
                if (historyPanel != null) historyPanel.SetActive(true);
            }
        }
        else
        {
            Debug.LogError($"Failed to load history: {request.error} (Status: {request.responseCode})");
        }

        request.Dispose();
    }

    void DisplayHistory(HistoryItem[] history)
    {
        if (historyListContainer == null) return;

        // 기존 항목 제거
        foreach (Transform child in historyListContainer)
        {
            Destroy(child.gameObject);
        }

        // 히스토리 표시
        foreach (HistoryItem item in history)
        {
            if (historyItemPrefab != null)
            {
                GameObject historyItem = Instantiate(historyItemPrefab, historyListContainer);

                TextMeshProUGUI[] texts = historyItem.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length > 0)
                {
                    string date = item.created_at ?? item.transaction_date ?? "Unknown";
                    texts[0].text = date;
                }
                if (texts.Length > 1)
                {
                    int points = item.point_num;
                    string sign = points > 0 ? "+" : "";
                    texts[1].text = $"{sign}{points}";
                    texts[1].color = points > 0 ? Color.green : Color.red;
                }
            }
        }
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
}


using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using System.Collections;

public class LoginManager : MonoBehaviour
{
    [Header("Input Fields")]
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] TMP_InputField id_input;
    [SerializeField] TMP_InputField password_input;

    [Header("Server Configuration")]
    [SerializeField] string serverUrl = "http://localhost:3000";

    [Header("Game Scene Options")]
    [SerializeField] string gameSceneName = "Map1";
    [SerializeField] string adminSceneName = "Admin"; // 어드민 전용 씬 이름

    // Store user session data
    private static UserSession currentSession;
    private const string SessionCookieKey = "SessionCookie";

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Enable cookie handling for session persistence
        // UnityWebRequest.ClearCookieCache();

        // Optional: Check server status on start
        StartCoroutine(CheckServerStatus());
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void PressedButton()
    {
        // Login button pressed
        if (id_input != null && password_input != null)
        {
            StartCoroutine(LoginUser());
        }
        else
        {
            if (label != null)
            {
                label.text = "Error: Input fields not assigned!";
            }
        }
    }

    // Check server status (optional)
    public IEnumerator CheckServerStatus()
    {
        string url = serverUrl;
        UnityWebRequest request = UnityWebRequest.Get(url);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Server is running: " + request.downloadHandler.text);
        }
        else
        {
            Debug.LogError("Server connection failed: " + request.error);
            if (label != null)
            {
                label.text = "Server connection failed!";
            }
        }
    }

    // Login user
    public IEnumerator LoginUser()
    {
        // Validate inputs
        if (string.IsNullOrEmpty(id_input.text) || string.IsNullOrEmpty(password_input.text))
        {
            if (label != null)
            {
                label.text = "Please enter both ID and password!";
            }
            yield break;
        }

        string url = serverUrl; // POST to root for Unity compatibility
        WWWForm form = new WWWForm();

        // FIX: Use .text property instead of literal strings
        form.AddField("id", id_input.text);
        form.AddField("password", password_input.text);

        UnityWebRequest request = null;

        try
        {
            request = UnityWebRequest.Post(url, form);
            // Enable cookie handling for session persistence (MongoDB sessions)
            request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");

            yield return request.SendWebRequest();

            // Get response data (even for errors, server may return JSON)
            string responseText = request.downloadHandler?.text ?? "";
            long responseCode = request.responseCode;

            Debug.Log($"Login Response Code: {responseCode}, Text: {responseText}");

            // Try to parse JSON response (works for both success and error responses)
            LoginResponse response = ParseLoginResponse(responseText);

            // 디버깅: 응답 파싱 확인
            if (response != null)
            {
                Debug.Log($"Parsed response - message: {response.message}, sessionId: {response.sessionId ?? "null"}");
            }
            else
            {
                Debug.LogError("Failed to parse login response as JSON");
            }

            if (request.result == UnityWebRequest.Result.Success && responseCode >= 200 && responseCode < 300)
            {
                // Success response
                if (response != null)
                {
                    if (response.message != null)
                    {
                        if (label != null)
                        {
                            label.text = response.message;
                        }

                        if (response.message.Contains("successful"))
                        {
                            Debug.Log("Login successful! Total Points: " + response.totalPoints);

                            // 서버에서 받은 세션 ID 저장 (Authorization 헤더로 사용)
                            if (!string.IsNullOrEmpty(response.sessionId))
                            {
                                // 세션 ID를 그대로 저장 (쿠키 형식 불필요)
                                PlayerPrefs.SetString("SessionId", response.sessionId);
                                PlayerPrefs.Save();
                                Debug.Log($"✓ Session ID saved: {response.sessionId.Substring(0, Mathf.Min(30, response.sessionId.Length))}...");
                            }
                            else
                            {
                                Debug.LogError("✗ Session ID is NULL or empty in login response!");
                            }

                            // 세션 메모리에 저장
                            string userRole = response.role ?? "player";
                            currentSession = new UserSession
                            {
                                userId = id_input.text,
                                userIndexId = response.userIndexId,
                                totalPoints = response.totalPoints,
                                isLoggedIn = true,
                                role = userRole
                            };

                            // 로컬(PlayerPrefs)에 저장 - 씬이 바뀌어도 유지됨
                            PlayerPrefs.SetString("PlayerID", currentSession.userId);
                            PlayerPrefs.SetInt("UserIndexId", currentSession.userIndexId);
                            PlayerPrefs.SetInt("TotalPoints", currentSession.totalPoints);
                            PlayerPrefs.SetString("UserRole", userRole); // 역할 저장
                            PlayerPrefs.Save();  // 즉시 디스크에 쓰기

                            Debug.Log($"User Role: {userRole}");

                            // 잠깐 메세지 보여주기
                            yield return new WaitForSeconds(1f);

                            // 게임 씬으로 이동
                            StartGame();
                        }
                    }
                    else if (response.error != null)
                    {
                        // Show user-friendly error message from server
                        string friendlyMessage = GetFriendlyErrorMessage(response.error);
                        if (label != null)
                        {
                            label.text = friendlyMessage;
                        }
                    }
                }
            }
            else
            {
                // Error response - try to get error from JSON first
                string friendlyMessage = "";

                if (response != null && !string.IsNullOrEmpty(response.error))
                {
                    // Server returned JSON error message
                    friendlyMessage = GetFriendlyErrorMessage(response.error);
                }
                else
                {
                    // No JSON error, use connection error handler
                    friendlyMessage = GetFriendlyConnectionError(request);
                }

                if (label != null)
                {
                    label.text = friendlyMessage;
                }
                Debug.LogError($"Login failed: {request.error} (Status: {responseCode})");
            }
        }
        finally
        {
            if (request != null)
            {
                request.Dispose();
            }
        }
    }

    // Helper method to parse login response (moved outside try-catch to avoid yield issue)
    private LoginResponse ParseLoginResponse(string responseText)
    {
        try
        {
            return JsonUtility.FromJson<LoginResponse>(responseText);
        }
        catch
        {
            // If not JSON, try to show friendly message
            if (label != null)
            {
                label.text = "Unable to connect to server. Please try again.";
            }
            return null;
        }
    }

    // Get user-friendly error message from server error
    private string GetFriendlyErrorMessage(string error)
    {
        if (string.IsNullOrEmpty(error))
        {
            return "An error occurred. Please try again.";
        }

        // Map server error messages to user-friendly ones
        string errorLower = error.ToLower();

        if (errorLower.Contains("invalid") || errorLower.Contains("incorrect") || errorLower.Contains("wrong"))
        {
            return "Invalid ID or password. Please check your credentials and try again.";
        }
        else if (errorLower.Contains("required") || errorLower.Contains("missing"))
        {
            return "Please enter both ID and password.";
        }
        else if (errorLower.Contains("not found") || errorLower.Contains("does not exist"))
        {
            return "User not found. Please check your ID and try again.";
        }
        else if (errorLower.Contains("server error") || errorLower.Contains("internal"))
        {
            return "Server error occurred. Please try again in a moment.";
        }
        else if (errorLower.Contains("timeout") || errorLower.Contains("timed out"))
        {
            return "Connection timed out. Please check your internet and try again.";
        }
        else
        {
            // Return the error message if it's already user-friendly, otherwise show generic message
            return error.Length > 50 ? "An error occurred. Please try again." : error;
        }
    }

    // Get user-friendly connection error message
    private string GetFriendlyConnectionError(UnityWebRequest request)
    {
        if (request == null)
        {
            return "Unable to connect to server. Please check your internet connection.";
        }

        string error = request.error ?? "";
        string errorLower = error.ToLower();
        long responseCode = request.responseCode;

        // Handle specific HTTP status codes
        if (responseCode == 400)
        {
            return "Invalid request. Please check your input and try again.";
        }
        else if (responseCode == 401)
        {
            return "Invalid ID or password. Please check your credentials.";
        }
        else if (responseCode == 404)
        {
            return "Server not found. Please check the server URL.";
        }
        else if (responseCode == 500)
        {
            return "Server error occurred. Please try again in a moment.";
        }
        else if (responseCode == 503)
        {
            return "Server is temporarily unavailable. Please try again later.";
        }

        // Handle connection errors
        if (errorLower.Contains("cannot resolve") || errorLower.Contains("name resolution"))
        {
            return "Cannot connect to server. Please check your internet connection.";
        }
        else if (errorLower.Contains("connection refused") || errorLower.Contains("refused"))
        {
            return "Server is not responding. Please make sure the server is running.";
        }
        else if (errorLower.Contains("timeout") || errorLower.Contains("timed out"))
        {
            return "Connection timed out. Please try again.";
        }
        else if (errorLower.Contains("network") || errorLower.Contains("unreachable"))
        {
            return "Network error. Please check your internet connection.";
        }
        else if (string.IsNullOrEmpty(error))
        {
            return "Unable to connect to server. Please try again.";
        }

        // Default friendly message
        return "Connection error. Please check your internet and try again.";
    }

    // Start the game scene (ONLY called after successful login)
    private void StartGame()
    {
        // Double-check that user is logged in before starting game
        if (currentSession == null || !currentSession.isLoggedIn)
        {
            Debug.LogError("Cannot start game - user is not logged in!");
            if (label != null)
            {
                label.text = "Error: Authentication required!";
            }
            return;
        }

        Debug.Log("Starting game scene for user: " + currentSession.userId);

        // 어드민인 경우 어드민 씬으로 이동
        string userRole = currentSession.role ?? PlayerPrefs.GetString("UserRole", "player");
        if (userRole == "admin" && !string.IsNullOrEmpty(adminSceneName))
        {
            Debug.Log("Admin detected, loading admin scene: " + adminSceneName);
            SceneManager.LoadScene(adminSceneName);
            return;
        }

        // 일반 사용자는 게임 씬으로 이동
        // Load specific scene by name
        if (!string.IsNullOrEmpty(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
        }
        // Load by build index if gameSceneName is a number
        else if (int.TryParse(gameSceneName, out int sceneIndex))
        {
            SceneManager.LoadScene(sceneIndex);
        }
        else
        {
            Debug.LogWarning("Game scene not configured! Please assign gameSceneName in the Inspector.");
            if (label != null)
            {
                label.text = "Game scene not configured!";
            }
        }
    }


    // Get current session (for use in other scripts)
    public static UserSession GetCurrentSession()
    {
        return currentSession;
    }

    // Clear session (for logout)
    public void ClearSession()
    {
        currentSession = null;
        UnityWebRequest.ClearCookieCache(); // Clear MongoDB session cookies
        PlayerPrefs.DeleteKey("SessionId"); // 세션 ID 삭제
        PlayerPrefs.Save();
        Debug.Log("Session cleared (Session ID removed)");
    }

    // Helper class for JSON parsing
    [System.Serializable]
    public class LoginResponse
    {
        public string message;
        public string error;
        public int totalPoints;
        public int userIndexId;
        public string sessionId; // 서버에서 세션 ID를 응답 본문에 포함
        public string role; // 사용자 역할 (player 또는 admin)
    }

    // User session data class
    [System.Serializable]
    public class UserSession
    {
        public string userId;
        public int userIndexId;
        public int totalPoints;
        public bool isLoggedIn;
        public string role; // 사용자 역할
    }
}

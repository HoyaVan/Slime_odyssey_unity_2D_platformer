using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameLogic : MonoBehaviour
{
    [Header("Input Fields")]
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] TMP_InputField id_input;
    [SerializeField] TMP_InputField password_input;

    [Header("Server Configuration")]
    [SerializeField] string serverUrl = "http://localhost:3000";

    [Header("Game Scene Options")]
    [SerializeField] string gameSceneName = "Map1"; 
    [SerializeField] bool useRandomScene = false; // If true, uses GoToNewScene component
    [SerializeField] GoToNewScene goToNewSceneComponent; // Reference to GoToNewScene script (optional)

    // Store user session data
    private static UserSession currentSession;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Enable cookie handling for session persistence
        UnityWebRequest.ClearCookieCache();
        
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
                        
                        // If login successful, store session and start game
                        if (response.message.Contains("successful"))
                        {
                            Debug.Log("Login successful! Total Points: " + response.totalPoints);
                            
                            // Store session data in memory for current game session
                            // NOTE: The actual session is stored in MongoDB server-side
                            // UnityWebRequest automatically handles session cookies for you
                            currentSession = new UserSession
                            {
                                userId = id_input.text,
                                userIndexId = response.userIndexId,
                                totalPoints = response.totalPoints,
                                isLoggedIn = true
                            };
                            
                            // Wait a moment to show success message
                            yield return new WaitForSeconds(1f);
                            
                            // Start the game scene
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
        
        // Option 1: Use GoToNewScene component if available and enabled
        if (useRandomScene && goToNewSceneComponent != null)
        {
            Debug.Log("Using GoToNewScene component to load random scene");
            goToNewSceneComponent.GoToRandomSceneFromList();
        }
        // Option 2: Load specific scene by name
        else if (!string.IsNullOrEmpty(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
        }
        // Option 3: Load by build index if gameSceneName is a number
        else if (int.TryParse(gameSceneName, out int sceneIndex))
        {
            SceneManager.LoadScene(sceneIndex);
        }
        else
        {
            Debug.LogWarning("Game scene not configured! Please assign gameSceneName or goToNewSceneComponent in the Inspector.");
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
        Debug.Log("Session cleared (MongoDB session cookie removed)");
    }

    // Helper class for JSON parsing
    [System.Serializable]
    public class LoginResponse
    {
        public string message;
        public string error;
        public int totalPoints;
        public int userIndexId;
    }

    // User session data class
    [System.Serializable]
    public class UserSession
    {
        public string userId;
        public int userIndexId;
        public int totalPoints;
        public bool isLoggedIn;
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class CreateAccountForm : MonoBehaviour
{
    [Header("Account Fields")]
    [SerializeField] TMP_InputField usernameInput;
    [SerializeField] TMP_InputField passwordInput;
    [SerializeField] TMP_Dropdown roleDropdown;  // Role selection: Player or Admin

    [Header("Feedback")]
    [SerializeField] TextMeshProUGUI feedbackLabel;  // For displaying success/error messages

    [Header("Server Configuration")]
    [SerializeField] string serverUrl = "http://localhost:3000";

    // Response structure matching backend
    [System.Serializable]
    private class RegisterResponse
    {
        public string message;
        public string error;
        public int userIndexId;
        public string role;
    }

    // Called by the Create Account button
    public void OnCreateAccountClicked()
    {
        if (usernameInput == null || passwordInput == null)
        {
            ShowFeedback("Error: Input fields not assigned!", true);
            return;
        }

        string username = usernameInput.text;
        string password = passwordInput.text;

        // Validate inputs
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowFeedback("Please enter both username and password!", true);
            return;
        }

        // Always use "player" role - admin accounts cannot be created through registration
        string selectedRole = "player";

        Debug.Log($"Creating account: {username}, Role: {selectedRole} (admin creation not allowed)");

        // Start registration coroutine (always passes "player" role)
        StartCoroutine(RegisterUser(username, password, selectedRole));
    }

    // Register user with server (always creates as "player" role)
    private IEnumerator RegisterUser(string username, string password, string selectedRole)
    {
        string url = serverUrl + "/auth/register";
        WWWForm form = new WWWForm();
        form.AddField("id", username);  // Backend expects "id" field (must match backend)
        form.AddField("password", password);  // Backend expects "password" field (must match backend)
        // Note: role field is not sent - backend will always create as "player"
        // form.AddField("role", selectedRole);  // Removed - backend enforces "player" role
        // Optional: Add name field if you want to store display name
        // form.AddField("name", username);

        UnityWebRequest request = null;

        try
        {
            request = UnityWebRequest.Post(url, form);
            request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");

            yield return request.SendWebRequest();

            // Get response data (even for errors, server may return JSON)
            string responseText = request.downloadHandler?.text ?? "";
            long responseCode = request.responseCode;

            Debug.Log($"Register Response Code: {responseCode}, Text: {responseText}");

            // Try to parse JSON response
            RegisterResponse response = ParseRegisterResponse(responseText);

            if (request.result == UnityWebRequest.Result.Success && responseCode >= 200 && responseCode < 300)
            {
                // Success response
                if (response != null && !string.IsNullOrEmpty(response.message))
                {
                    ShowFeedback(response.message, false);
                    Debug.Log($"Registration successful! User ID: {response.userIndexId}, Role: {response.role}");

                    // Optional: Clear form after successful registration
                    ClearForm();

                    // Optional: Switch to login scene or show login prompt
                    // You can add scene transition logic here if needed
                }
                else
                {
                    ShowFeedback("Registration completed, but no confirmation received.", false);
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

                ShowFeedback(friendlyMessage, true);
                // Use LogWarning instead of LogError to prevent game pause on expected errors
                Debug.LogWarning($"Registration failed: {request.error} (Status: {responseCode})");
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

    // Helper method to parse registration response
    private RegisterResponse ParseRegisterResponse(string responseText)
    {
        try
        {
            return JsonUtility.FromJson<RegisterResponse>(responseText);
        }
        catch
        {
            // If not JSON, show friendly message
            if (feedbackLabel != null)
            {
                feedbackLabel.text = "Unable to process server response. Please try again.";
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

        if (errorLower.Contains("already exists") || errorLower.Contains("already taken"))
        {
            return "This username is already taken. Please choose a different one.";
        }
        else if (errorLower.Contains("required") || errorLower.Contains("missing"))
        {
            return "Please enter both username and password.";
        }
        else if (errorLower.Contains("password") && errorLower.Contains("requirement"))
        {
            return "Password must be at least 10 characters and include upper, lower, number, and symbol.";
        }
        else if (errorLower.Contains("invalid") && errorLower.Contains("role"))
        {
            return "Invalid role selected. Please choose Player or Admin.";
        }
        else if (errorLower.Contains("invalid"))
        {
            return "Invalid input. Please check your information and try again.";
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
        else if (responseCode == 409) // Conflict (user already exists)
        {
            return "This username is already taken. Please choose a different one.";
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

    // Show feedback message to user
    private void ShowFeedback(string message, bool isError)
    {
        if (feedbackLabel != null)
        {
            feedbackLabel.text = message;
            // Optional: Change color based on error/success
            // feedbackLabel.color = isError ? Color.red : Color.green;
        }
        Debug.Log(isError ? $"Error: {message}" : $"Success: {message}");
    }

    // Clear form fields
    private void ClearForm()
    {
        if (usernameInput != null) usernameInput.text = "";
        if (passwordInput != null) passwordInput.text = "";
        if (roleDropdown != null) roleDropdown.value = 0; // Reset to first option (Player)
    }

    // Hide/disable role dropdown since admin creation is not allowed
    private void InitializeRoleDropdown()
    {
        if (roleDropdown != null)
        {
            // Hide the dropdown - all accounts are created as "player"
            roleDropdown.gameObject.SetActive(false);
        }
    }

    void Start()
    {
        // Hide role dropdown - admin accounts cannot be created through registration
        InitializeRoleDropdown();
    }
}


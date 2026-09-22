using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Scenes")]
    [SerializeField] string mainMenuSceneName = "Main";  // 메인 메뉴 씬 이름
    [SerializeField] string[] gameSceneNames = new string[2];  // 게임 씬 이름들 (2개)

    [Header("UI")]
    [SerializeField] TextMeshProUGUI scoreText;
    [SerializeField] TextMeshProUGUI lifeText;
    [SerializeField] TextMeshProUGUI accumulatedScoreText;  // 누적 점수 표시
    [SerializeField] TextMeshProUGUI coinProgressText;  // 코인 진행 상황 표시 (예: "5/10")

    [Header("Result UI")]
    [SerializeField] GameObject resultPanel;

    [SerializeField] TextMeshProUGUI resultIDText;
    [SerializeField] TextMeshProUGUI resultScoreText;
    [SerializeField] TextMeshProUGUI resultCoinText;
    [SerializeField] TextMeshProUGUI resultTitleText;
    [SerializeField] UnityEngine.UI.Button nextLevelButton;  // 다음 레벨 버튼 (클리어 시에만 표시)
    [SerializeField] UnityEngine.UI.Button restartButton;  // 재시작 버튼
    [SerializeField] UnityEngine.UI.Button endGameButton;  // 게임 종료 버튼

    [Header("Player Info")]
    string playerID = "Unknown";

    [Header("Server Configuration")]
    [SerializeField] string serverUrl = "http://localhost:3000";

    [Header("Coin Goal")]
    [SerializeField] int coinsToClear = 10;
    int collectedCoins = 0;
    bool isCleared = false;
    bool isGameOver = false;   // 죽어서 끝났는지 표시
    bool scoreSubmitted = false;  // 점수 제출 여부 체크 (중복 제출 방지)

    int score = 0;
    int life = 3;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        playerID = PlayerPrefs.GetString("PlayerID", "Unknown");

        if (resultPanel != null)
            resultPanel.SetActive(false);

        Time.timeScale = 1f;   // 혹시 이전 씬에서 0으로 멈춰 있으면 초기화

        // 게임 시작 시 점수 제출 플래그 리셋
        scoreSubmitted = false;

        // 새 씬 시작 시: 현재 씬 점수는 0부터 시작 (누적 점수는 유지)
        score = 0;
        collectedCoins = 0;  // 새 씬 시작 시 코인도 리셋
        Debug.Log($"Starting new scene. Pending score from previous scenes: {PlayerPrefs.GetInt("PendingScore", 0)}");

        UpdateUI();
    }

    // ---------------- Score ----------------
    public void AddScore(int amount)
    {
        if (isCleared || isGameOver) return;   // 이미 끝난 뒤엔 점수 X
        if (amount <= 0) return;  // 잘못된 점수 값 방지

        score += amount;
        UpdateUI();
    }

    // ---------------- Life ----------------
    public void ReduceLife(int amount)
    {
        if (isCleared || isGameOver) return;   // 이미 끝난 뒤엔 무시

        life -= amount;
        if (life < 0) life = 0;
        UpdateUI();

        // 🔹 라이프가 0이 되면 게임오버 처리
        if (life <= 0)
        {
            isGameOver = true;
            ShowResult();
        }
    }

    // ---------------- Coins ----------------
    public void RegisterCoinCollected()
    {
        if (isCleared || isGameOver) return;

        collectedCoins++;
        UpdateUI();

        if (collectedCoins >= coinsToClear)
        {
            isCleared = true;
            Debug.Log($"Stage Cleared! Coins: {collectedCoins}/{coinsToClear}, Score: {score}");
            ShowResult();
        }
    }

    void ShowResult()
    {
        if (resultPanel != null)
            resultPanel.SetActive(true);

        // 게임 종료 사운드
        if (AudioPlayer.Instance != null)
        {
            AudioPlayer.Instance.PlayEndGame();
        }

        // 제목 (죽었는지 / 클리어인지)
        if (resultTitleText != null)
        {
            if (isGameOver)
                resultTitleText.text = "You Died...";
            else
                resultTitleText.text = "Stage Clear!";
        }

        // ID
        if (resultIDText != null)
            resultIDText.text = $"{playerID}";

        // Score (현재 씬 점수)
        if (resultScoreText != null)
            resultScoreText.text = $"Score: {score}";

        // Accumulated Score (누적 점수)
        int pendingScore = PlayerPrefs.GetInt("PendingScore", 0);
        int totalAccumulatedScore = pendingScore + (isCleared ? score : 0);
        if (resultScoreText != null)
        {
            if (isCleared)
            {
                resultScoreText.text = $"Score: {score} | Total: {totalAccumulatedScore}";
            }
            else
            {
                resultScoreText.text = $"Score: {score} | Total: {pendingScore + score}";
            }
        }

        // Coins
        if (resultCoinText != null)
            resultCoinText.text = $"Coins: {collectedCoins}/{coinsToClear}";

        // 버튼 표시/숨김 제어
        // 클리어 시: 다음 레벨 버튼 + 종료 버튼 표시, 재시작 버튼 숨김
        // 게임오버 시: 다음 레벨 버튼 숨김, 재시작/종료 버튼 표시
        if (nextLevelButton != null)
        {
            nextLevelButton.gameObject.SetActive(isCleared && !isGameOver);
        }
        if (restartButton != null)
        {
            restartButton.gameObject.SetActive(isGameOver);
        }
        if (endGameButton != null)
        {
            // 클리어 시와 게임오버 시 모두 종료 버튼 표시
            endGameButton.gameObject.SetActive(true);
        }

        Time.timeScale = 0f;

        // 점수 처리:
        // - 클리어 시: 점수를 로컬에 저장만 하고 DB 전송 안 함
        // - 게임오버 시: 현재 씬 점수를 임시 저장 (재시작 시 현재 씬 점수만 리셋, 누적 점수는 유지)
        if (isCleared && !isGameOver)
        {
            // 클리어 시: 점수를 로컬에 누적 저장 (다음 씬에서 계속 사용)
            int currentPendingScore = PlayerPrefs.GetInt("PendingScore", 0);
            int newPendingScore = currentPendingScore + score;
            PlayerPrefs.SetInt("PendingScore", newPendingScore);
            PlayerPrefs.Save();
            Debug.Log($"Stage cleared! Score saved locally. Current score: {score}, Total pending: {newPendingScore}");
        }
        else if (isGameOver)
        {
            // 게임오버 시: 현재 씬의 점수를 임시로 저장 (End Game 시 사용)
            // 누적 점수는 유지하고, 현재 씬 점수만 별도 저장
            PlayerPrefs.SetInt("CurrentSceneScore", score);
            PlayerPrefs.Save();
            Debug.Log($"Game over! Current scene score saved: {score}, Pending score: {PlayerPrefs.GetInt("PendingScore", 0)}");
        }
    }

    // ---------------- UI 공통 갱신 ----------------
    void UpdateUI()
    {
        // 현재 씬 점수
        if (scoreText != null)
            scoreText.text = "Score: " + score;

        // 누적 점수 표시
        int pendingScore = PlayerPrefs.GetInt("PendingScore", 0);
        if (accumulatedScoreText != null)
        {
            accumulatedScoreText.text = $"Total: {pendingScore + score}";
        }

        // 생명
        if (lifeText != null)
            lifeText.text = "Life: " + life;

        // 코인 진행 상황 표시
        if (coinProgressText != null)
        {
            coinProgressText.text = $"Coins: {collectedCoins}/{coinsToClear}";
        }
    }

    // Restart 버튼에서 호출할 함수
    public void RestartGame()
    {
        // 버튼 클릭 사운드
        if (AudioPlayer.Instance != null)
        {
            AudioPlayer.Instance.PlayButtonClick();
        }

        // 멈춰있던 시간 원래대로
        Time.timeScale = 1f;

        // 재시작 시: 현재 씬의 점수만 리셋 (저장된 누적 점수는 유지)
        // 현재 씬의 임시 점수만 삭제
        PlayerPrefs.DeleteKey("CurrentSceneScore");
        PlayerPrefs.Save();
        Debug.Log($"Restarting current scene. Pending score maintained: {PlayerPrefs.GetInt("PendingScore", 0)}");

        // 점수 제출 플래그 리셋 (새 게임 시작)
        scoreSubmitted = false;

        // 현재 씬 다시 로드
        string currentScene = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentScene);
    }

    // End Game 버튼에서 호출할 함수
    public void EndGame()
    {
        // 버튼 클릭 사운드
        if (AudioPlayer.Instance != null)
        {
            AudioPlayer.Instance.PlayButtonClick();
        }

        // 멈춘 시간 복구
        Time.timeScale = 1f;

        // 게임 종료 시: 저장된 누적 점수 + 현재 씬 점수를 DB에 전송
        int pendingScore = PlayerPrefs.GetInt("PendingScore", 0);
        int currentSceneScore = PlayerPrefs.GetInt("CurrentSceneScore", 0);
        int totalScoreToSubmit = pendingScore + currentSceneScore;

        // 클리어 후 End Game: pendingScore에 이미 포함됨
        // 게임오버 후 End Game: pendingScore + currentSceneScore
        Debug.Log($"Ending game - Pending: {pendingScore}, Current scene: {currentSceneScore}, Total: {totalScoreToSubmit}");

        if (totalScoreToSubmit > 0 && !scoreSubmitted)
        {
            Debug.Log($"Submitting total score to database: {totalScoreToSubmit}");
            scoreSubmitted = true;
            StartCoroutine(SubmitScoreToDatabase(totalScoreToSubmit));

            // 전송 후 저장된 점수 모두 삭제
            PlayerPrefs.DeleteKey("PendingScore");
            PlayerPrefs.DeleteKey("CurrentSceneScore");
            PlayerPrefs.Save();
        }
        else if (totalScoreToSubmit <= 0)
        {
            Debug.Log("No score to submit.");
        }
        else if (scoreSubmitted)
        {
            Debug.LogWarning("Score already submitted, skipping duplicate submission.");
        }

        // 메인 메뉴 씬으로 이동
        SceneManager.LoadScene(mainMenuSceneName);

        // 만약 빌드된 게임에서 완전 종료하고 싶으면 (PC 빌드용)
        // Application.Quit();
        // (에디터에서는 Quit이 아무 일도 안 일어나는 게 정상이야)
    }

    // Next Level 버튼에서 호출할 함수 (클리어 시에만 표시됨)
    public void GoToNextGameScene()
    {
        // 버튼 클릭 사운드
        if (AudioPlayer.Instance != null)
        {
            AudioPlayer.Instance.PlayButtonClick();
        }

        // 멈춘 시간 복구
        Time.timeScale = 1f;

        // 점수는 이미 ShowResult()에서 로컬에 저장됨
        // DB 전송은 하지 않음 (게임 종료 시에만 전송)
        Debug.Log($"Continuing to next scene. Score saved locally, will submit when game ends.");

        // 현재 씬 이름 찾기
        string currentSceneName = SceneManager.GetActiveScene().name;

        // 다른 게임 씬 찾기
        string nextScene = GetNextGameScene(currentSceneName);

        if (!string.IsNullOrEmpty(nextScene))
        {
            Debug.Log($"Moving to next game scene: {nextScene}");
            SceneManager.LoadScene(nextScene);
        }
        else
        {
            Debug.LogWarning($"Could not find next game scene. Current scene: {currentSceneName}");
            // 다음 씬을 찾을 수 없으면 메인 메뉴로 이동
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    // 현재 씬에서 다른 게임 씬 찾기 (Map1 <-> Map2 번갈아가며 이동)
    private string GetNextGameScene(string currentSceneName)
    {
        // gameSceneNames 배열에서 현재 씬이 아닌 다른 씬 찾기
        // Map1 -> Map2, Map2 -> Map1로 번갈아가며 이동
        foreach (string sceneName in gameSceneNames)
        {
            if (!string.IsNullOrEmpty(sceneName) && sceneName != currentSceneName)
            {
                Debug.Log($"Switching from {currentSceneName} to {sceneName}");
                return sceneName;
            }
        }

        // 배열에서 찾지 못하면 빈 문자열 반환
        Debug.LogWarning($"Could not find alternate scene. Current: {currentSceneName}, Available scenes: {string.Join(", ", gameSceneNames)}");
        return string.Empty;
    }

    // 점수를 데이터베이스에 제출하는 함수
    private IEnumerator SubmitScoreToDatabase(int scoreToSubmit)
    {
        // PlayerID가 "Unknown"이면 로그인하지 않은 상태이므로 제출하지 않음
        if (playerID == "Unknown" || string.IsNullOrEmpty(playerID))
        {
            Debug.LogWarning("Cannot submit score: User not logged in (PlayerID is Unknown)");
            yield break;
        }

        string url = serverUrl + "/game/points/add";

        // 폼 데이터 준비
        WWWForm form = new WWWForm();
        form.AddField("amount", scoreToSubmit.ToString());

        UnityWebRequest request = null;

        try
        {
            request = UnityWebRequest.Post(url, form);
            request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");

            // 세션 ID를 Authorization 헤더로 전송 (Unity는 쿠키를 신뢰할 수 없으므로 명시적으로 전송)
            string sessionId = PlayerPrefs.GetString("SessionId", string.Empty);

            if (!string.IsNullOrEmpty(sessionId))
            {
                request.SetRequestHeader("Authorization", $"Session {sessionId}");
                Debug.Log($"Using session ID: {sessionId.Substring(0, Mathf.Min(30, sessionId.Length))}...");
            }
            else
            {
                Debug.LogError("No saved session ID found! Score submission will fail.");
            }

            Debug.Log($"Submitting score: {scoreToSubmit} for user: {playerID}");
            Debug.Log($"Request URL: {url}");

            yield return request.SendWebRequest();

            string responseText = request.downloadHandler?.text ?? "";
            long responseCode = request.responseCode;

            Debug.Log($"Score Submission Response Code: {responseCode}, Text: {responseText}");

            if (request.result == UnityWebRequest.Result.Success && responseCode >= 200 && responseCode < 300)
            {
                // 성공 응답 파싱
                ScoreResponse response = ParseScoreResponse(responseText);
                if (response != null)
                {
                    Debug.Log($"Score submitted successfully! Points added: {response.pointsAdded}, Total Points: {response.totalPoints}");

                    // PlayerPrefs에 업데이트된 총 포인트 저장
                    PlayerPrefs.SetInt("TotalPoints", response.totalPoints);
                    PlayerPrefs.Save();
                }
            }
            else
            {
                // 에러 응답 처리
                ScoreResponse errorResponse = ParseScoreResponse(responseText);
                if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.error))
                {
                    Debug.LogError($"Failed to submit score: {errorResponse.error}");
                }
                else
                {
                    Debug.LogError($"Failed to submit score: {request.error} (Status: {responseCode})");
                }
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

    // 점수 응답 파싱 헬퍼 함수
    private ScoreResponse ParseScoreResponse(string responseText)
    {
        try
        {
            return JsonUtility.FromJson<ScoreResponse>(responseText);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to parse score response: {e.Message}");
            return null;
        }
    }

    // 점수 제출 요청 데이터 클래스
    [System.Serializable]
    private class ScoreRequest
    {
        public int amount;
    }

    // 점수 제출 응답 데이터 클래스
    [System.Serializable]
    private class ScoreResponse
    {
        public string message;
        public string error;
        public int pointsAdded;
        public int totalPoints;
        public int pointIndexId;
    }

}

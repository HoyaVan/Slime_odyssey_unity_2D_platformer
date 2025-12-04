using UnityEngine;
using UnityEngine.UI;

public class DropdownPanel : MonoBehaviour
{
    [SerializeField] GameObject createAccountPanel;
    [SerializeField] Button Button;

    void Awake()
    {
        // 시작할 때 패널 숨기기
        if (createAccountPanel != null)
            createAccountPanel.SetActive(false);

        // 버튼 클릭 연결
        if (Button != null)
            Button.onClick.AddListener(TogglePanel);
    }

    void TogglePanel()
    {
        if (createAccountPanel == null) return;

        // 버튼 클릭 사운드
        if (AudioPlayer.Instance != null)
        {
            AudioPlayer.Instance.PlayButtonClick();
        }

        bool isActive = createAccountPanel.activeSelf;
        createAccountPanel.SetActive(!isActive);
    }
}

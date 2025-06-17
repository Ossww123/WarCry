using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class RoomListTestLauncher : MonoBehaviour
{
    public TMP_InputField ipInputField;

    private void Awake()
    {
        if (Application.isBatchMode)
        {
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] [MainMenuController] Headless 모드에서 비활성화됨");
            gameObject.SetActive(false);
            return;
        }
    }

    public void StartAsHostDirect()
    {
        var netManager = FindFirstObjectByType<GameNetworkManager>();
        netManager.StartHostMode();

        PlayerPrefs.SetInt("CurrentRoomId", 999); // 테스트용 임시 ID
        SceneManager.LoadScene("WaitingRoomScene");
    }

    public void StartAsClientDirect()
    {  
        string ip = ipInputField.text.Trim();
        if (string.IsNullOrEmpty(ip))
        {
            Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] IP 주소가 비어 있습니다. 기본값 127.0.0.1 사용");
            ip = "127.0.0.1";
        }

        var netManager = FindFirstObjectByType<GameNetworkManager>();
        netManager.SetupClient(ip, 7777); // 포트는 필요 시 조정
        netManager.ConnectClient();

        PlayerPrefs.SetInt("CurrentRoomId", 999);
        SceneManager.LoadScene("WaitingRoomScene");
    }
}

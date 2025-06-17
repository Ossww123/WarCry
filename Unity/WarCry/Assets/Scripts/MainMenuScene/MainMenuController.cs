using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// 메인 메뉴 씬의 전체 흐름과 UI 상호작용을 관리하는 컨트롤러
/// 게임 시작, 옵션 설정, 애플리케이션 종료 등의 메인 메뉴 기능을 제공하며
/// 헤드리스 모드 지원과 씬 전환 관리를 담당
/// </summary>
public class MainMenuController : MonoBehaviour
{
    #region Inspector Fields

    [Header("UI References")]
    [SerializeField] private GameObject optionPanel;
    [SerializeField] private OptionPanelController optionPanelController;

    [Header("Scene Transition")]
    [SerializeField] private string nextSceneName = "RoomListScene";
    [SerializeField] private float sceneTransitionDelay = 0.3f;

    [Header("Audio")]
    [SerializeField] private int buttonClickSoundIndex = 1;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    #endregion

    #region Private Fields

    private bool isSceneTransitioning = false;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (ValidateHeadlessMode())
            return;

        InitializeMainMenu();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// 헤드리스 모드 검증 및 처리
    /// </summary>
    /// <returns>헤드리스 모드인 경우 true</returns>
    private bool ValidateHeadlessMode()
    {
        if (Application.isBatchMode)
        {
            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 헤드리스 모드에서 MainMenuController 비활성화됨");

            gameObject.SetActive(false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 메인 메뉴 초기화
    /// </summary>
    private void InitializeMainMenu()
    {
        ValidateComponents();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] MainMenuController 초기화 완료");
    }

    /// <summary>
    /// 필수 컴포넌트 유효성 검증
    /// </summary>
    private void ValidateComponents()
    {
        if (optionPanelController == null)
        {
            optionPanelController = FindObjectOfType<OptionPanelController>();

            if (optionPanelController == null)
            {
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] OptionPanelController를 찾을 수 없습니다");
            }
        }

        if (optionPanel == null && optionPanelController != null)
        {
            optionPanel = optionPanelController.gameObject;
        }
    }

    #endregion

    #region Public API - UI Events

    /// <summary>
    /// 게임 시작 버튼 클릭 처리
    /// </summary>
    public void OnStartGame()
    {
        if (isSceneTransitioning)
        {
            if (verboseLogging)
                Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 씬 전환이 이미 진행 중입니다");
            return;
        }

        PlayButtonClickSound();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 게임 시작 요청 - 다음 씬: {nextSceneName}");

        StartCoroutine(SceneTransitionCoroutine(nextSceneName));
    }

    /// <summary>
    /// 옵션 패널 열기
    /// </summary>
    public void OnOpenOptions()
    {
        PlayButtonClickSound();

        if (optionPanelController != null)
        {
            optionPanelController.ShowPanel();

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 옵션 패널 열기");
        }
        else
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] OptionPanelController를 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// 옵션 패널 닫기
    /// </summary>
    public void OnCloseOptions()
    {
        PlayButtonClickSound();

        if (optionPanelController != null)
        {
            optionPanelController.HidePanel();

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 옵션 패널 닫기");
        }
        else
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] OptionPanelController를 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// 게임 종료 처리
    /// </summary>
    public void OnQuitGame()
    {
        PlayButtonClickSound();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 게임 종료 요청");

        StartCoroutine(QuitGameCoroutine());
    }

    #endregion

    #region Scene Management

    /// <summary>
    /// 씬 전환 코루틴
    /// </summary>
    /// <param name="sceneName">전환할 씬 이름</param>
    private IEnumerator SceneTransitionCoroutine(string sceneName)
    {
        isSceneTransitioning = true;

        // 전환 지연 시간 대기
        yield return new WaitForSeconds(sceneTransitionDelay);

        // 씬 로드
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// 게임 종료 코루틴
    /// </summary>
    private IEnumerator QuitGameCoroutine()
    {
        // 종료 전 지연 시간 (사운드 재생 완료 대기)
        yield return new WaitForSeconds(0.2f);

        Application.Quit();

        // 에디터에서는 플레이 모드 종료
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    #endregion

    #region Audio

    /// <summary>
    /// 버튼 클릭 사운드 재생
    /// </summary>
    private void PlayButtonClickSound()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(buttonClickSoundIndex);
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// 현재 씬 전환 상태 확인
    /// </summary>
    /// <returns>씬 전환 중이면 true</returns>
    public bool IsSceneTransitioning()
    {
        return isSceneTransitioning;
    }

    /// <summary>
    /// 옵션 패널 활성화 상태 확인
    /// </summary>
    /// <returns>옵션 패널이 활성화되어 있으면 true</returns>
    public bool IsOptionPanelOpen()
    {
        return optionPanel != null && optionPanel.activeInHierarchy;
    }

    #endregion

    #region Debug Methods

    /// <summary>
    /// 메인 메뉴 상태 정보 로그 출력 (디버깅용)
    /// </summary>
    [ContextMenu("Log MainMenu Status")]
    public void LogMainMenuStatus()
    {
        Debug.Log($"=== MainMenu 상태 정보 ===");
        Debug.Log($"씬 전환 중: {isSceneTransitioning}");
        Debug.Log($"옵션 패널 열림: {IsOptionPanelOpen()}");
        Debug.Log($"OptionPanelController 연결: {optionPanelController != null}");
        Debug.Log($"다음 씬: {nextSceneName}");
    }

    #endregion
}
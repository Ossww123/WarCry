using Mirror;
using UnityEngine;
using System.Collections;
using System;
using kcp2k;

/// <summary>
/// 서버 환경 및 헤드리스 모드 관리를 담당하는 매니저
/// 명령줄 인수 처리, 서버 시작, 클라이언트 전용 컴포넌트 제거 등을 처리
/// </summary>
public class ServerManager : MonoBehaviour
{
    #region Inspector Fields

    [Header("Server Settings")]
    [SerializeField] private bool startServerAutomatically = false;
    [SerializeField] private int defaultPort = 7777;

    [Header("Logging")]
    [SerializeField] private bool verboseLogging = true;

    #endregion

    #region Constants

    private const string ServerArgument = "-server";
    private const string PortArgument = "-port";
    private const int ServerStartDelayFrames = 3;

    #endregion

    #region Private Fields

    private GameNetworkManager networkManager;

    #endregion

    #region Initialization

    /// <summary>
    /// ServerManager 초기화
    /// </summary>
    /// <param name="manager">참조할 GameNetworkManager 인스턴스</param>
    public void Initialize(GameNetworkManager manager)
    {
        networkManager = manager;

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] ServerManager 초기화 완료");

        InitializeBasedOnEnvironment();
    }

    /// <summary>
    /// 실행 환경에 따른 초기화 처리
    /// 헤드리스 모드, 명령줄 인수, 자동 시작 설정을 검사하여 서버 모드 결정
    /// </summary>
    private void InitializeBasedOnEnvironment()
    {
        var executionMode = DetermineExecutionMode();

        if (executionMode.isServerMode)
        {
            InitializeServerMode(executionMode.port);
        }
        else
        {
            InitializeClientMode();
        }
    }

    #endregion

    #region Environment Detection

    /// <summary>
    /// 실행 모드 및 포트 정보
    /// </summary>
    private struct ExecutionMode
    {
        public bool isServerMode;
        public int port;
    }

    /// <summary>
    /// 명령줄 인수와 설정을 기반으로 실행 모드 결정
    /// </summary>
    /// <returns>실행 모드 정보</returns>
    private ExecutionMode DetermineExecutionMode()
    {
        string[] args = Environment.GetCommandLineArgs();
        bool hasServerArg = Array.Exists(args, arg => arg == ServerArgument);
        bool isServerMode = Application.isBatchMode || hasServerArg || startServerAutomatically;
        int port = ParsePortFromArgs(args);

        return new ExecutionMode
        {
            isServerMode = isServerMode,
            port = port
        };
    }

    /// <summary>
    /// 명령줄 인수에서 포트 번호 파싱
    /// </summary>
    /// <param name="args">명령줄 인수 배열</param>
    /// <returns>파싱된 포트 번호 (실패 시 기본 포트)</returns>
    private int ParsePortFromArgs(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == PortArgument)
            {
                if (int.TryParse(args[i + 1], out int parsedPort))
                {
                    if (verboseLogging)
                        Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 명령줄에서 포트 파싱됨: {parsedPort}");
                    return parsedPort;
                }
                else
                {
                    Debug.LogWarning($"[{DebugUtils.ResolveCallerMethod()}] 잘못된 포트 값: {args[i + 1]}. 기본 포트 사용: {defaultPort}");
                }
            }
        }

        return defaultPort;
    }

    #endregion

    #region Server Mode Initialization

    /// <summary>
    /// 서버 모드 초기화
    /// </summary>
    /// <param name="port">사용할 포트 번호</param>
    private void InitializeServerMode(int port)
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 서버 모드로 초기화됩니다 (포트: {port})");

        ConfigureServerPort(port);
        StartCoroutine(StartServerAfterFrame());

        if (Application.isBatchMode)
        {
            PrepareHeadlessMode();
        }
    }

    /// <summary>
    /// 클라이언트 모드 초기화
    /// </summary>
    private void InitializeClientMode()
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 클라이언트 모드로 초기화됩니다");
    }

    /// <summary>
    /// 서버 포트 설정
    /// </summary>
    /// <param name="port">설정할 포트 번호</param>
    private void ConfigureServerPort(int port)
    {
        var transport = GetComponent<KcpTransport>();
        if (transport != null)
        {
            transport.Port = (ushort)port;

            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 서버 포트 설정 완료: {port}");
        }
        else
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] KcpTransport 컴포넌트를 찾을 수 없습니다!");
        }
    }

    #endregion

    #region Server Startup

    /// <summary>
    /// 프레임 지연 후 서버 시작
    /// 모든 초기화가 완료되도록 몇 프레임 대기 후 서버 시작
    /// </summary>
    private IEnumerator StartServerAfterFrame()
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {ServerStartDelayFrames} 프레임 후 서버 시작 예정");

        // 초기화 완료를 위한 프레임 대기
        for (int i = 0; i < ServerStartDelayFrames; i++)
        {
            yield return null;
        }

        StartServer();
    }

    /// <summary>
    /// 서버 시작 및 상태 검증
    /// </summary>
    private void StartServer()
    {
        if (networkManager == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] NetworkManager가 초기화되지 않았습니다!");
            return;
        }

        networkManager.StartServer();
        ValidateServerStartup();
    }

    /// <summary>
    /// 서버 시작 상태 검증
    /// </summary>
    private void ValidateServerStartup()
    {
        if (NetworkServer.active)
        {
            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 서버 시작 성공 - NetworkServer.active = true");
        }
        else
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] 서버 시작 실패 - NetworkServer.active = false");
        }
    }

    #endregion

    #region Headless Mode Management

    /// <summary>
    /// 헤드리스 모드 준비 (클라이언트 전용 컴포넌트 제거)
    /// </summary>
    private void PrepareHeadlessMode()
    {
        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 헤드리스 모드 준비 시작");

        DestroyClientOnlyComponents();
        OptimizeResourcesForHeadless();
    }

    /// <summary>
    /// 클라이언트 전용 컴포넌트 제거
    /// UI, 오디오, 카메라 등 서버에서 불필요한 컴포넌트들을 정리
    /// </summary>
    private void DestroyClientOnlyComponents()
    {
        DestroyUIComponents();
        DestroyAudioComponents();
        DestroyVisualComponents();
        DestroyManagerComponents();
    }

    /// <summary>
    /// UI 관련 컴포넌트 제거
    /// </summary>
    private void DestroyUIComponents()
    {
        var canvases = FindObjectsOfType<Canvas>();
        foreach (var canvas in canvases)
        {
            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] UI 캔버스 제거: {canvas.name}");
            Destroy(canvas.gameObject);
        }
    }

    /// <summary>
    /// 오디오 관련 컴포넌트 제거
    /// </summary>
    private void DestroyAudioComponents()
    {
        var audioListeners = FindObjectsOfType<AudioListener>();
        foreach (var listener in audioListeners)
        {
            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 오디오 리스너 제거: {listener.name}");
            Destroy(listener.gameObject);
        }

        var soundManager = FindObjectOfType<SoundManager>();
        if (soundManager != null)
        {
            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] SoundManager 제거");
            Destroy(soundManager.gameObject);
        }
    }

    /// <summary>
    /// 시각적 컴포넌트 제거 (카메라 등)
    /// </summary>
    private void DestroyVisualComponents()
    {
        var cameras = FindObjectsOfType<Camera>();
        foreach (var camera in cameras)
        {
            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 카메라 제거: {camera.name}");
            Destroy(camera.gameObject);
        }
    }

    /// <summary>
    /// 클라이언트 전용 매니저 컴포넌트 제거
    /// </summary>
    private void DestroyManagerComponents()
    {
        // UI 매니저들 제거
        DestroyComponentIfExists<WaitingRoomUIManager>("WaitingRoomUIManager");
        DestroyComponentIfExists<BattleUIManager>("BattleUIManager");
        DestroyComponentIfExists<ResultSceneInitializer>("ResultSceneInitializer");
    }

    /// <summary>
    /// 특정 타입의 컴포넌트가 존재하면 제거
    /// </summary>
    /// <typeparam name="T">제거할 컴포넌트 타입</typeparam>
    /// <param name="componentName">로그용 컴포넌트 이름</param>
    private void DestroyComponentIfExists<T>(string componentName) where T : MonoBehaviour
    {
        var component = FindObjectOfType<T>();
        if (component != null)
        {
            if (verboseLogging)
                Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] {componentName} 제거");
            Destroy(component.gameObject);
        }
    }

    /// <summary>
    /// 헤드리스 모드를 위한 리소스 최적화
    /// </summary>
    private void OptimizeResourcesForHeadless()
    {
        Resources.UnloadUnusedAssets();

        if (verboseLogging)
            Debug.Log($"[{DebugUtils.ResolveCallerMethod()}] 헤드리스 모드 최적화 완료");
    }

    #endregion
}
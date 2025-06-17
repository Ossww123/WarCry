// SceneInitializer.cs 생성
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public abstract class SceneInitializer : MonoBehaviour
{
    protected virtual void Awake()
    {
        // 공통 초기화 로직
        Debug.Log($"[SceneInitializer] {SceneManager.GetActiveScene().name} 씬 초기화 시작");
    }

    protected virtual void Start()
    {
        // 씬 초기화 실행
        InitializeScene();
    }

    protected virtual void OnDestroy()
    {
        // 공통 정리 로직
        Debug.Log($"[SceneInitializer] {SceneManager.GetActiveScene().name} 씬 정리");
    }

    // 각 씬 별로 구현해야 하는 초기화 메서드
    protected abstract void InitializeScene();
}
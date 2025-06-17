using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Mirror;

public class SceneTransitionManager : MonoBehaviour
{
    [Header("Fade Settings")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private float initialFadeDuration = 0.5f;  // 초기 검은 화면 유지 시간
    [SerializeField] private float fadeOutDuration = 1.0f;      // 페이드 아웃 시간

    private void Awake()
    {
        if (fadeImage == null)
        {
            Debug.LogError($"[{DebugUtils.ResolveCallerMethod()}] FadeImage가 할당되지 않았습니다!");
            return;
        }

        // 초기 설정: 완전히 검은 화면으로 시작
        SetFadeImageAlpha(1.0f);
    }

    private void Start()
    {
        // 장면 시작 시 페이드 효과 시작
        StartCoroutine(FadeInSequence());
    }

    private IEnumerator FadeInSequence()
    {
        // 초기 지연 시간 동안 검은 화면 유지
        yield return new WaitForSeconds(initialFadeDuration);

        // 페이드 아웃 (검은색 -> 투명)
        float elapsedTime = 0f;
        while (elapsedTime < fadeOutDuration)
        {
            float alpha = 1.0f - (elapsedTime / fadeOutDuration);
            SetFadeImageAlpha(alpha);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 완전히 투명하게 설정
        SetFadeImageAlpha(0.0f);
    }

    // 씬 전환 시 페이드 아웃 효과를 위한 public 메서드
    public void FadeOut(float duration, System.Action onFadeComplete = null)
    {
        StartCoroutine(FadeOutSequence(duration, onFadeComplete));
    }

    private IEnumerator FadeOutSequence(float duration, System.Action onFadeComplete)
    {
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            float alpha = elapsedTime / duration;
            SetFadeImageAlpha(alpha);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 완전히 불투명하게 설정
        SetFadeImageAlpha(1.0f);

        // 콜백 실행
        onFadeComplete?.Invoke();
    }

    private void SetFadeImageAlpha(float alpha)
    {
        if (fadeImage != null)
        {
            Color color = fadeImage.color;
            color.a = alpha;
            fadeImage.color = color;
        }
    }
}
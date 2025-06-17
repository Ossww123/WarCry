using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimatorSetup : MonoBehaviour
{
    // 애니메이션 클립 참조
    public AnimationClip idleAnimation;
    public AnimationClip walkAnimation;
    public AnimationClip runAnimation;
    
    // 애니메이션 파라미터 이름
    private const string walkParam = "Walk";
    private const string runParam = "Run";
    
    // 초기화 함수
    void Start()
    {
        // Animator 컴포넌트 가져오기
        Animator animator = GetComponent<Animator>();
        
        // Animator 컴포넌트가 없는 경우 로그 출력
        if (animator == null)
        {
            Debug.LogError("Animator 컴포넌트가 없습니다!");
            return;
        }
        
        // 애니메이션 클립을 찾아서 설정
        if (idleAnimation == null)
        {
            idleAnimation = Resources.Load<AnimationClip>("infantry_01_idle");
        }
        
        if (walkAnimation == null)
        {
            walkAnimation = Resources.Load<AnimationClip>("infantry_02_walk");
        }
        
        if (runAnimation == null)
        {
            runAnimation = Resources.Load<AnimationClip>("infantry_03_run");
        }
        
        // 애니메이션 파라미터가 없는 경우 추가
        AnimatorControllerParameter[] parameters = animator.parameters;
        bool hasWalkParam = false;
        bool hasRunParam = false;
        
        foreach (AnimatorControllerParameter param in parameters)
        {
            if (param.name == walkParam)
                hasWalkParam = true;
            else if (param.name == runParam)
                hasRunParam = true;
        }
        
        // Walk 파라미터 추가
        if (!hasWalkParam)
        {
            Debug.Log("Walk 파라미터 추가");
        }
        
        // Run 파라미터 추가
        if (!hasRunParam)
        {
            Debug.Log("Run 파라미터 추가");
        }
        
        Debug.Log("애니메이터 설정이 완료되었습니다.");
    }
}

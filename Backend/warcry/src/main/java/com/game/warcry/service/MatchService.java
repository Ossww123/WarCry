package com.game.warcry.service;

import com.game.warcry.dto.match.*;

public interface MatchService {

    // 매치 목록 조회
    MatchListResponse getMatches(Boolean isPrivate, String status, Integer limit);

    // 매치 상세 조회
    MatchDetailResponse getMatchDetail(Long matchId);

    // 매치 생성
    MatchCreateResponse createMatch(String username, MatchCreateRequest request);

    // 매치 참가
    MatchJoinResponse joinMatch(Long matchId, String username, MatchJoinRequest request);

    // 게임 결과 저장
    MatchResultResponse saveMatchResult(Long matchId, String username, MatchResultRequest request);

    // 매치 나가기 (GUEST)
    MatchLeaveResponse leaveMatch(Long matchId, String username);

    // 매치 나가기 (HOST)
    MatchHostLeaveResponse hostLeaveMatch(Long matchId, String username);
}
package com.game.warcry.service;

import com.game.warcry.dto.listen.ListenMatchCreateRequest;
import com.game.warcry.dto.listen.ListenMatchCreateResponse;
import com.game.warcry.dto.listen.ListenMatchListResponse;
import com.game.warcry.dto.listen.ListenMatchDetailResponse;
import com.game.warcry.dto.listen.ListenMatchJoinRequest;
import com.game.warcry.dto.listen.ListenMatchJoinResponse;
import com.game.warcry.dto.listen.ListenMatchLeaveResponse;
import com.game.warcry.dto.listen.ListenMatchHostLeaveResponse;
import com.game.warcry.dto.listen.ListenMatchResultRequest;
import com.game.warcry.dto.listen.ListenMatchResultResponse;


public interface ListenMatchService {
    // 매치 생성
    ListenMatchCreateResponse createMatch(String username, ListenMatchCreateRequest request);
    // ListenMatchService.java에 추가
    ListenMatchListResponse getMatches(Boolean isPrivate, String status, Integer limit);
    // ListenMatchService.java에 추가
    ListenMatchDetailResponse getMatchDetail(Long matchId);
    // ListenMatchService.java에 추가
    ListenMatchJoinResponse joinMatch(Long matchId, String username, ListenMatchJoinRequest request);
    // ListenMatchService.java에 추가
    ListenMatchLeaveResponse leaveMatch(Long matchId, String username);
    ListenMatchHostLeaveResponse hostLeaveMatch(Long matchId, String username);
    // ListenMatchService.java에 추가
    ListenMatchResultResponse saveMatchResult(Long matchId, String username, ListenMatchResultRequest request);
}
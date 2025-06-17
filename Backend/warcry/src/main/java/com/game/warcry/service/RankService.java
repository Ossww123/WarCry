package com.game.warcry.service;

import com.game.warcry.dto.rank.*;

import java.util.List;

public interface RankService {

    // 유저 랭크 정보 조회
    RankPlayerResponse getPlayerRank(Long userId);

    // 리더보드 조회
    LeaderboardResponse getLeaderboard(Integer tier, Integer page, Integer size);

    // 유저 매치 히스토리 조회
    RankHistoryResponse getMatchHistory(Long userId, Integer page, Integer size);

    // 유저 일일 통계 조회
    DailyStatsResponse getUserDailyStats(Long userId, String startDate, String endDate);

    // 일일 랭킹 통계 조회
    DailyRankStatsResponse getDailyRankStats(String date);

    // 티어 분포 통계 조회
    TierDistributionResponse getTierDistribution();

    // 게임 결과에 따른 랭킹 변화 처리 (매치 결과 저장 시 호출됨)
    List<RatingChangeDTO> processMatchResult(Long matchId, List<Long> winnerIds, List<Long> loserIds);

    // 신규 유저 초기 레이팅 생성
    void initializeUserRating(Long userId);
}
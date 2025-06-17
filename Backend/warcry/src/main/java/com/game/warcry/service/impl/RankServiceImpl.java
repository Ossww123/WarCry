package com.game.warcry.service.impl;

import com.game.warcry.dto.rank.*;
import com.game.warcry.model.*;
import com.game.warcry.repository.*;
import com.game.warcry.service.RankService;
import lombok.RequiredArgsConstructor;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.data.domain.PageRequest;
import org.springframework.data.domain.Pageable;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.LocalDate;
import java.time.LocalDateTime;
import java.time.format.DateTimeFormatter;
import java.util.*;
import java.util.stream.Collectors;

@Service
@RequiredArgsConstructor
public class RankServiceImpl implements RankService {

    private final Logger log = LoggerFactory.getLogger(RankServiceImpl.class);
    private final UserRepository userRepository;
    private final MatchRepository matchRepository;
    private final MatchUserRepository matchUserRepository;
    private final RatingRepository ratingRepository;
    private final RatingHistoryRepository ratingHistoryRepository;
    private final DailyStatsRepository dailyStatsRepository;

    @Override
    public RankPlayerResponse getPlayerRank(Long userId) {
        User user = userRepository.findById(userId)
                .orElseThrow(() -> new IllegalArgumentException("해당 유저를 찾을 수 없습니다."));

        Rating rating = ratingRepository.findByUserId(userId)
                .orElseThrow(() -> new IllegalArgumentException("해당 유저의 랭크 정보를 찾을 수 없습니다."));

        // 글로벌 순위 계산 (자신보다 포인트가 높은 사용자 수 + 1)
        long globalRank = ratingRepository.countPlayersWithHigherPoints(userId) + 1;

        // 티어 내 순위 계산 (같은 티어 내에서 자신보다 포인트가 높은 사용자 수 + 1)
        long tierRank = ratingRepository.countPlayersWithHigherPointsInTier(userId, rating.getTier()) + 1;

        return RankPlayerResponse.builder()
                .success(true)
                .userId(userId)
                .username(user.getUsername())
                .nickname(user.getNickname())
                .points(rating.getPoint())
                .tier(rating.getTier())
                .wins(rating.getWins())
                .losses(rating.getLosses())
                .winRate(rating.getWinRate())
                .globalRank(globalRank)
                .tierRank(tierRank)
                .isPlacement(!rating.getPlacementDone())
                .winStreak(rating.getWinStreak())
                .build();
    }

    @Override
    public LeaderboardResponse getLeaderboard(Integer tier, Integer page, Integer size) {
        Pageable pageable = PageRequest.of(page, size);
        List<Rating> ratings;
        long totalPlayers;

        if (tier != null) {
            // 특정 티어의 리더보드
            ratings = ratingRepository.findByTierOrderByPointDesc(tier, pageable);
            totalPlayers = ratingRepository.countByTier(tier);
        } else {
            // 전체 리더보드
            ratings = ratingRepository.findAllByOrderByPointDesc(pageable);
            totalPlayers = ratingRepository.count();
        }

        List<LeaderboardPlayerDTO> players = new ArrayList<>();
        long startRank = (long) page * size + 1;

        for (int i = 0; i < ratings.size(); i++) {
            Rating rating = ratings.get(i);
            User user = rating.getUser();

            players.add(LeaderboardPlayerDTO.builder()
                    .rank(startRank + i)
                    .userId(user.getId())
                    .nickname(user.getNickname())
                    .points(rating.getPoint())
                    .tier(rating.getTier())
                    .wins(rating.getWins())
                    .losses(rating.getLosses())
                    .build());
        }

        boolean hasNext = (long) (page + 1) * size < totalPlayers;

        return LeaderboardResponse.builder()
                .success(true)
                .totalPlayers(totalPlayers)
                .page(page)
                .size(size)
                .hasNext(hasNext)
                .players(players)
                .build();
    }

    @Override
    public RankHistoryResponse getMatchHistory(Long userId, Integer page, Integer size) {
        // 유저 존재 여부 확인
        User user = userRepository.findById(userId)
                .orElseThrow(() -> new IllegalArgumentException("해당 유저를 찾을 수 없습니다."));

        Pageable pageable = PageRequest.of(page, size);
        List<RatingHistory> histories = ratingHistoryRepository.findByUserIdOrderByChangeTimeDesc(userId, pageable);
        long totalMatches = ratingHistoryRepository.countByUserId(userId);

        List<MatchHistoryDTO> matches = new ArrayList<>();

        for (RatingHistory history : histories) {
            Match match = history.getMatch();

            // 상대방 찾기
            MatchUser opponent = matchUserRepository.findByMatchAndUserNot(match, user)
                    .orElse(null);

            String opponentNickname = opponent != null ? opponent.getUser().getNickname() : "알 수 없음";
            Long opponentId = opponent != null ? opponent.getUser().getId() : null;

            matches.add(MatchHistoryDTO.builder()
                    .matchId(match.getId())
                    .timestamp(history.getChangeTime())
                    .result(history.getWinner() ? "WIN" : "LOSE")
                    .pointsBefore(history.getPointBefore())
                    .pointsAfter(history.getPointAfter())
                    .pointsChange(history.getPointChange())
                    .tierBefore(history.getTierBefore())
                    .tierAfter(history.getTierAfter())
                    .opponentId(opponentId)
                    .opponentNickname(opponentNickname)
                    .build());
        }

        boolean hasNext = (long) (page + 1) * size < totalMatches;

        return RankHistoryResponse.builder()
                .success(true)
                .userId(userId)
                .totalMatches(totalMatches)
                .page(page)
                .size(size)
                .hasNext(hasNext)
                .matches(matches)
                .build();
    }

    @Override
    public DailyStatsResponse getUserDailyStats(Long userId, String startDateStr, String endDateStr) {
        // 유저 존재 여부 확인
        userRepository.findById(userId)
                .orElseThrow(() -> new IllegalArgumentException("해당 유저를 찾을 수 없습니다."));

        DateTimeFormatter formatter = DateTimeFormatter.ofPattern("yyyyMMdd");
        LocalDate startDate = LocalDate.parse(startDateStr, formatter);
        LocalDate endDate = LocalDate.parse(endDateStr, formatter);

        if (startDate.isAfter(endDate)) {
            throw new IllegalArgumentException("시작 날짜가 종료 날짜보다 뒤에 있습니다.");
        }

        List<DailyStats> dailyStats = dailyStatsRepository.findByUserIdAndDateBetweenOrderByDateAsc(
                userId, startDate, endDate);

        List<DailyStatDTO> stats = dailyStats.stream()
                .map(ds -> DailyStatDTO.builder()
                        .date(ds.getDate().format(formatter))
                        .highestPoint(ds.getHighestPoint())
                        .matchCount(ds.getMatchCount())
                        .win(ds.getWinCount())
                        .loss(ds.getLoseCount())
                        .build())
                .collect(Collectors.toList());

        return DailyStatsResponse.builder()
                .success(true)
                .userId(userId)
                .stats(stats)
                .build();
    }

    @Override
    public DailyRankStatsResponse getDailyRankStats(String dateStr) {
        DateTimeFormatter formatter = DateTimeFormatter.ofPattern("yyyyMMdd");
        LocalDate date = LocalDate.parse(dateStr, formatter);

        // 해당 날짜의 활동 플레이어 수
        long totalPlayers = dailyStatsRepository.countActivePlayers(date);

        // 평균 매치 수
        double averageMatches = dailyStatsRepository.getAverageMatchCount(date);

        // 최고 포인트 플레이어 찾기
        Pageable topOne = PageRequest.of(0, 1);
        List<DailyStats> topPlayers = dailyStatsRepository.findByDateOrderByHighestPointDesc(date, topOne);

        DailyRankStatsResponse.TopPlayerDTO topPlayer = null;
        if (!topPlayers.isEmpty()) {
            DailyStats top = topPlayers.get(0);
            topPlayer = DailyRankStatsResponse.TopPlayerDTO.builder()
                    .userId(top.getUser().getId())
                    .nickname(top.getUser().getNickname())
                    .point(top.getHighestPoint())
                    .build();
        }

        return DailyRankStatsResponse.builder()
                .success(true)
                .date(dateStr)
                .totalPlayers(totalPlayers)
                .averageMatches(averageMatches)
                .topPlayer(topPlayer)
                .build();
    }

    @Override
    public TierDistributionResponse getTierDistribution() {
        List<TierCountDTO> tiers = new ArrayList<>();

        // 각 티어별 유저 수 계산
        for (int tier = 1; tier <= 4; tier++) {
            long count = ratingRepository.countByTier(tier);
            tiers.add(TierCountDTO.builder()
                    .tier(tier)
                    .count(count)
                    .build());
        }

        return TierDistributionResponse.builder()
                .success(true)
                .tiers(tiers)
                .build();
    }

    @Override
    @Transactional
    public List<RatingChangeDTO> processMatchResult(Long matchId, List<Long> winnerIds, List<Long> loserIds) {
        Match match = matchRepository.findById(matchId)
                .orElseThrow(() -> new IllegalArgumentException("해당 매치를 찾을 수 없습니다."));

        List<RatingChangeDTO> changes = new ArrayList<>();

        // 승리한 유저들 처리
        for (Long winnerId : winnerIds) {
            processWin(match, winnerId, changes);
        }

        // 패배한 유저들 처리
        for (Long loserId : loserIds) {
            processLoss(match, loserId, changes);
        }

        // 매치 종료 시간 설정
        match.setEndTime(LocalDateTime.now());
        matchRepository.save(match);

        return changes;
    }

    @Override
    @Transactional
    public void initializeUserRating(Long userId) {
        try {
            User user = userRepository.findById(userId)
                    .orElseThrow(() -> new IllegalArgumentException("해당 유저를 찾을 수 없습니다."));

            // 이미 레이팅이 있는지 확인
            if (ratingRepository.findByUserId(userId).isPresent()) {
                return; // 이미 있으면 아무것도 하지 않음
            }

            // 초기 레이팅 생성
            Rating rating = Rating.initializeRating(user);
            ratingRepository.save(rating);
        } catch (Exception e) {
            // 이미 저장된 경우 예외를 무시합니다
            if (e instanceof org.hibernate.StaleObjectStateException ||
                    e.getCause() instanceof org.hibernate.StaleObjectStateException ||
                    e instanceof org.springframework.dao.DataIntegrityViolationException) {
                // 로그만 남기고 무시
                log.warn("이미 레이팅 정보가 생성되어 있습니다. userId: {}", userId);
                return;
            }
            throw e; // 다른 예외는 다시 던짐
        }
    }

    // 승리/패배 처리 공통 로직 추출
    private RatingChangeDTO processRatingChange(Match match, Long userId, boolean isWin) {
        User user = userRepository.findById(userId)
                .orElseThrow(() -> new IllegalArgumentException("해당 유저를 찾을 수 없습니다."));

        // 레이팅 가져오기 (없으면 초기화)
        Rating rating = ratingRepository.findByUserId(userId)
                .orElseGet(() -> {
                    Rating newRating = Rating.initializeRating(user);
                    return ratingRepository.save(newRating);
                });

        // 변경 전 상태 저장
        int pointBefore = rating.getPoint();
        int tierBefore = rating.getTier();

        // 승패에 따라 레이팅 업데이트
        if (isWin) {
            rating.updateForWin();
        } else {
            rating.updateForLoss();
        }

        ratingRepository.save(rating);

        // 레이팅 변화 히스토리 저장
        RatingHistory history = RatingHistory.createHistory(
                user, match, pointBefore, rating.getPoint(),
                tierBefore, rating.getTier(), isWin);
        ratingHistoryRepository.save(history);

        // 일일 통계 업데이트
        updateDailyStats(user, rating.getPoint(), isWin);

        // 변화 정보 반환
        return RatingChangeDTO.builder()
                .userId(userId)
                .previousPoints(pointBefore)
                .newPoints(rating.getPoint())
                .change(rating.getPoint() - pointBefore)
                .previousTier(tierBefore)
                .newTier(rating.getTier())
                .build();
    }

    // 승리 처리 헬퍼 메서드
    @Transactional
    protected void processWin(Match match, Long userId, List<RatingChangeDTO> changes) {
        RatingChangeDTO change = processRatingChange(match, userId, true);
        changes.add(change);
    }

    // 패배 처리 헬퍼 메서드
    @Transactional
    protected void processLoss(Match match, Long userId, List<RatingChangeDTO> changes) {
        RatingChangeDTO change = processRatingChange(match, userId, false);
        changes.add(change);
    }

    // 일일 통계 업데이트 헬퍼 메서드
    @Transactional
    protected void updateDailyStats(User user, int currentPoint, boolean isWin) {
        LocalDate today = LocalDate.now();

        // 오늘 날짜의 통계가 이미 있는지 확인
        Optional<DailyStats> existingStatsOpt = dailyStatsRepository.findByUserIdAndDate(user.getId(), today);

        DailyStats stats;
        if (existingStatsOpt.isPresent()) {
            // 기존 통계 업데이트
            stats = existingStatsOpt.get();
            stats.setHighestPoint(Math.max(stats.getHighestPoint(), currentPoint));
            stats.setMatchCount(stats.getMatchCount() + 1);

            if (isWin) {
                stats.setWinCount(stats.getWinCount() + 1);
            } else {
                stats.setLoseCount(stats.getLoseCount() + 1);
            }
        } else {
            // 새 통계 생성
            stats = DailyStats.builder()
                    .user(user)
                    .date(today)
                    .highestPoint(currentPoint)
                    .matchCount(1)
                    .winCount(isWin ? 1 : 0)
                    .loseCount(isWin ? 0 : 1)
                    .build();
        }

        dailyStatsRepository.save(stats);
    }
}
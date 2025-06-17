package com.game.warcry.controller;

import com.game.warcry.dto.ErrorResponse;
import com.game.warcry.dto.rank.*;
import com.game.warcry.service.RankService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.security.SecurityRequirement;
import io.swagger.v3.oas.annotations.tags.Tag;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.Authentication;
import org.springframework.web.bind.annotation.*;

import java.time.format.DateTimeParseException;

@RestController
@RequestMapping("/api/rank")
@RequiredArgsConstructor
@Tag(name = "Rank Controller", description = "랭크 관련 API")
public class RankController {

    private final RankService rankService;

    @GetMapping("/player/{userId}")
    @Operation(summary = "유저 랭크 정보 조회", description = "특정 유저의 랭크 정보를 조회합니다.",
            security = @SecurityRequirement(name = "bearerAuth"))
    public ResponseEntity<?> getPlayerRank(@PathVariable Long userId) {
        try {
            RankPlayerResponse response = rankService.getPlayerRank(userId);
            return ResponseEntity.ok(response);
        } catch (IllegalArgumentException e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("USER_NOT_FOUND")
                    .message("해당 유저를 찾을 수 없습니다.")
                    .build();
            return ResponseEntity.status(HttpStatus.NOT_FOUND).body(errorResponse);
        } catch (Exception e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("SERVER_ERROR")
                    .message("유저 랭크 정보를 조회하는 중 서버 오류가 발생했습니다.")
                    .build();
            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR).body(errorResponse);
        }
    }

    @GetMapping("/leaderboard")
    @Operation(summary = "리더보드 조회", description = "전체 또는 특정 티어 유저의 랭크 순위를 조회합니다.",
            security = @SecurityRequirement(name = "bearerAuth"))
    public ResponseEntity<?> getLeaderboard(
            @RequestParam(required = false) Integer tier,
            @RequestParam(defaultValue = "0") Integer page,
            @RequestParam(defaultValue = "20") Integer size) {
        try {
            LeaderboardResponse response = rankService.getLeaderboard(tier, page, size);
            return ResponseEntity.ok(response);
        } catch (Exception e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("SERVER_ERROR")
                    .message("리더보드 정보를 조회하는 중 서버 오류가 발생했습니다.")
                    .build();
            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR).body(errorResponse);
        }
    }

    @GetMapping("/history/{userId}")
    @Operation(summary = "유저 매치 히스토리 조회", description = "특정 유저의 매치 결과 및 포인트 변화 이력을 조회합니다.",
            security = @SecurityRequirement(name = "bearerAuth"))
    public ResponseEntity<?> getMatchHistory(
            @PathVariable Long userId,
            @RequestParam(defaultValue = "0") Integer page,
            @RequestParam(defaultValue = "10") Integer size) {
        try {
            RankHistoryResponse response = rankService.getMatchHistory(userId, page, size);
            return ResponseEntity.ok(response);
        } catch (IllegalArgumentException e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("USER_NOT_FOUND")
                    .message("해당 유저를 찾을 수 없습니다.")
                    .build();
            return ResponseEntity.status(HttpStatus.NOT_FOUND).body(errorResponse);
        } catch (Exception e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("SERVER_ERROR")
                    .message("매치 히스토리를 조회하는 중 서버 오류가 발생했습니다.")
                    .build();
            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR).body(errorResponse);
        }
    }

    @GetMapping("/daily/{userId}")
    @Operation(summary = "유저 일일 통계 조회", description = "특정 유저의 날짜별 활동 통계를 조회합니다.",
            security = @SecurityRequirement(name = "bearerAuth"))
    public ResponseEntity<?> getUserDailyStats(
            @PathVariable Long userId,
            @RequestParam String startDate,
            @RequestParam String endDate) {
        try {
            DailyStatsResponse response = rankService.getUserDailyStats(userId, startDate, endDate);
            return ResponseEntity.ok(response);
        } catch (IllegalArgumentException e) {
            if (e.getMessage().contains("유저를 찾을 수 없습니다")) {
                ErrorResponse errorResponse = ErrorResponse.builder()
                        .success(false)
                        .errorCode("USER_NOT_FOUND")
                        .message("해당 유저를 찾을 수 없습니다.")
                        .build();
                return ResponseEntity.status(HttpStatus.NOT_FOUND).body(errorResponse);
            } else {
                ErrorResponse errorResponse = ErrorResponse.builder()
                        .success(false)
                        .errorCode("INVALID_DATE_RANGE")
                        .message("날짜 형식이 올바르지 않거나 범위가 잘못되었습니다.")
                        .build();
                return ResponseEntity.status(HttpStatus.BAD_REQUEST).body(errorResponse);
            }
        } catch (DateTimeParseException e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("INVALID_DATE_FORMAT")
                    .message("날짜 형식이 올바르지 않습니다. (YYYYMMDD 형식이어야 합니다)")
                    .build();
            return ResponseEntity.status(HttpStatus.BAD_REQUEST).body(errorResponse);
        } catch (Exception e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("SERVER_ERROR")
                    .message("유저의 일일 통계를 조회하는 중 서버 오류가 발생했습니다.")
                    .build();
            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR).body(errorResponse);
        }
    }

    @GetMapping("/daily")
    @Operation(summary = "일일 랭킹 통계 조회", description = "특정 일자의 전체 랭킹 활동 통계를 조회합니다.",
            security = @SecurityRequirement(name = "bearerAuth"))
    public ResponseEntity<?> getDailyRankStats(@RequestParam String date) {
        try {
            DailyRankStatsResponse response = rankService.getDailyRankStats(date);
            return ResponseEntity.ok(response);
        } catch (DateTimeParseException e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("INVALID_DATE")
                    .message("날짜 형식이 올바르지 않습니다. (YYYYMMDD)")
                    .build();
            return ResponseEntity.status(HttpStatus.BAD_REQUEST).body(errorResponse);
        } catch (Exception e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("SERVER_ERROR")
                    .message("일일 통계를 조회하는 중 서버 오류가 발생했습니다.")
                    .build();
            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR).body(errorResponse);
        }
    }

    @GetMapping("/stats/tier-distribution")
    @Operation(summary = "티어 분포 통계 조회", description = "현재 각 티어에 속한 유저 수 통계를 조회합니다.",
            security = @SecurityRequirement(name = "bearerAuth"))
    public ResponseEntity<?> getTierDistribution() {
        try {
            TierDistributionResponse response = rankService.getTierDistribution();
            return ResponseEntity.ok(response);
        } catch (Exception e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("SERVER_ERROR")
                    .message("티어 분포 통계를 조회하는 중 서버 오류가 발생했습니다.")
                    .build();
            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR).body(errorResponse);
        }
    }
}
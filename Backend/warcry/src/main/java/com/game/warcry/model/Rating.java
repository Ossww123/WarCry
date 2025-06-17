package com.game.warcry.model;

import jakarta.persistence.*;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

import java.time.LocalDateTime;

@Entity
@Table(name = "ratings")
@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class Rating {

    @Id
    @Column(name = "user_id")
    private Long userId;

    @OneToOne
    @MapsId
    @JoinColumn(name = "user_id")
    private User user;

    @Column(nullable = false)
    private Integer point;

    @Column(nullable = false)
    private Integer tier;

    @Column(nullable = false)
    private Integer wins;

    @Column(nullable = false)
    private Integer losses;

    @Column(name = "placement_matches_played", nullable = false)
    private Integer placementMatchesPlayed;

    @Column(name = "placement_done", nullable = false)
    private Boolean placementDone;

    @Column(name = "win_streak", nullable = false)
    private Integer winStreak;

    @Column(name = "lose_streak", nullable = false)
    private Integer loseStreak;

    @Column(name = "last_match_time")
    private LocalDateTime lastMatchTime;

    @Version
    private Long version;

    // 새로운 유저의 초기 레이팅 값으로 사용할 정적 팩토리 메서드
    public static Rating initializeRating(User user) {
        return Rating.builder()
                .userId(user.getId())
                .user(user)
                .point(100) // 초기 포인트
                .tier(4)    // 초기 티어 (4티어가 가장 낮은 티어)
                .wins(0)
                .losses(0)
                .placementMatchesPlayed(0)
                .placementDone(false)
                .winStreak(0)
                .loseStreak(0)
                .build();
    }

    // 승리 시 포인트 및 통계 업데이트
    public void updateForWin() {
        this.point += 25; // 승리 시 25점 추가
        this.wins += 1;
        this.winStreak += 1;
        this.loseStreak = 0;
        this.lastMatchTime = LocalDateTime.now();

        if (this.placementMatchesPlayed < 3) {
            this.placementMatchesPlayed += 1;
            if (this.placementMatchesPlayed >= 3) {
                this.placementDone = true;
            }
        }

        // 티어 업데이트
        updateTier();
    }

    // 패배 시 포인트 및 통계 업데이트
    public void updateForLoss() {
        // 패배 시 20점 감소 (최소 0점)
        this.point = Math.max(0, this.point - 20);
        this.losses += 1;
        this.loseStreak += 1;
        this.winStreak = 0;
        this.lastMatchTime = LocalDateTime.now();

        if (this.placementMatchesPlayed < 3) {
            this.placementMatchesPlayed += 1;
            if (this.placementMatchesPlayed >= 3) {
                this.placementDone = true;
            }
        }

        // 티어 업데이트
        updateTier();
    }

    // 티어 업데이트 로직
    private void updateTier() {
        if (this.point >= 401) {
            this.tier = 1;
        } else if (this.point >= 301) {
            this.tier = 2;
        } else if (this.point >= 201) {
            this.tier = 3;
        } else {
            this.tier = 4;
        }
    }

    // 승률 계산
    public double getWinRate() {
        int totalGames = this.wins + this.losses;
        if (totalGames == 0) {
            return 0.0;
        }
        return (double) this.wins / totalGames * 100;
    }
}
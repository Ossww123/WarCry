package com.game.warcry.model;

import jakarta.persistence.*;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

import java.time.LocalDate;

@Entity
@Table(name = "daily_stats",
        uniqueConstraints = @UniqueConstraint(columnNames = {"user_id", "date"}))
@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class DailyStats {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @ManyToOne
    @JoinColumn(name = "user_id", nullable = false)
    private User user;

    @Column(nullable = false)
    private LocalDate date;

    @Column(name = "highest_point", nullable = false)
    private Integer highestPoint;

    @Column(name = "match_count", nullable = false)
    private Integer matchCount;

    @Column(name = "win_count", nullable = false)
    private Integer winCount;

    @Column(name = "lose_count", nullable = false)
    private Integer loseCount;

    // 해당 유저와 날짜에 대한 일일 통계 생성 또는 업데이트
    public static DailyStats createOrUpdate(DailyStats existingStats, User user,
                                            Integer currentPoint, Boolean isWin) {
        LocalDate today = LocalDate.now();

        if (existingStats == null) {
            // 새로운 일일 통계 생성
            return DailyStats.builder()
                    .user(user)
                    .date(today)
                    .highestPoint(currentPoint)
                    .matchCount(1)
                    .winCount(isWin ? 1 : 0)
                    .loseCount(isWin ? 0 : 1)
                    .build();
        } else {
            // 기존 통계 업데이트
            existingStats.setHighestPoint(Math.max(existingStats.getHighestPoint(), currentPoint));
            existingStats.setMatchCount(existingStats.getMatchCount() + 1);

            if (isWin) {
                existingStats.setWinCount(existingStats.getWinCount() + 1);
            } else {
                existingStats.setLoseCount(existingStats.getLoseCount() + 1);
            }

            return existingStats;
        }
    }
}
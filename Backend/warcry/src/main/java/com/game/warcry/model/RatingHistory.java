package com.game.warcry.model;

import jakarta.persistence.*;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

import java.time.LocalDateTime;

@Entity
@Table(name = "rating_history")
@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class RatingHistory {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @ManyToOne
    @JoinColumn(name = "user_id", nullable = false)
    private User user;

    @ManyToOne
    @JoinColumn(name = "match_id", nullable = false)
    private Match match;

    @Column(name = "point_before", nullable = false)
    private Integer pointBefore;

    @Column(name = "point_after", nullable = false)
    private Integer pointAfter;

    @Column(name = "point_change", nullable = false)
    private Integer pointChange;

    @Column(name = "tier_before", nullable = false)
    private Integer tierBefore;

    @Column(name = "tier_after", nullable = false)
    private Integer tierAfter;

    @Column(nullable = false)
    private Boolean winner;

    @Column(name = "change_time", nullable = false)
    private LocalDateTime changeTime;

    // 새로운 레이팅 히스토리 생성을 위한 정적 팩토리 메서드
    public static RatingHistory createHistory(User user, Match match,
                                              Integer pointBefore, Integer pointAfter,
                                              Integer tierBefore, Integer tierAfter,
                                              Boolean isWinner) {
        return RatingHistory.builder()
                .user(user)
                .match(match)
                .pointBefore(pointBefore)
                .pointAfter(pointAfter)
                .pointChange(pointAfter - pointBefore)
                .tierBefore(tierBefore)
                .tierAfter(tierAfter)
                .winner(isWinner)
                .changeTime(LocalDateTime.now())
                .build();
    }
}
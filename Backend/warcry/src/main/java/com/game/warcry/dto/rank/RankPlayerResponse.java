package com.game.warcry.dto.rank;

import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class RankPlayerResponse {
    private boolean success;
    private Long userId;
    private String username;
    private String nickname;
    private Integer points;
    private Integer tier;
    private Integer wins;
    private Integer losses;
    private Double winRate;
    private Long globalRank;
    private Long tierRank;
    private Boolean isPlacement;
    private Integer winStreak;
}
package com.game.warcry.dto.rank;

import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class DailyRankStatsResponse {
    private boolean success;
    private String date;
    private Long totalPlayers;
    private Double averageMatches;
    private TopPlayerDTO topPlayer;

    @Data
    @Builder
    @NoArgsConstructor
    @AllArgsConstructor
    public static class TopPlayerDTO {
        private Long userId;
        private String nickname;
        private Integer point;
    }
}
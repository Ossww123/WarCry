package com.game.warcry.dto.match;

import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

import java.util.List;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class MatchListResponse {
    private boolean success;
    private List<MatchItem> matches;

    @Data
    @Builder
    @NoArgsConstructor
    @AllArgsConstructor
    public static class MatchItem {
        private Long matchId;
        private String title;
        private String hostNickname;
        private Boolean isPrivate;
        private String status;
    }
}
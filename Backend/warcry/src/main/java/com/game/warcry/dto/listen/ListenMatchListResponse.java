package com.game.warcry.dto.listen;

import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

import java.util.List;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class ListenMatchListResponse {
    private boolean success;
    private List<MatchSummary> matches;

    @Data
    @Builder
    @NoArgsConstructor
    @AllArgsConstructor
    public static class MatchSummary {
        private Long matchId;
        private String title;
        private String hostNickname;
        private Boolean isPrivate;
        private String status;
    }
}
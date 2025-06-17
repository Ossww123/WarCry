package com.game.warcry.dto.match;

import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class MatchDetailResponse {
    private boolean success;
    private MatchDto match;

    @Data
    @Builder
    @NoArgsConstructor
    @AllArgsConstructor
    public static class MatchDto {
        private Long matchId;
        private String title;
        private Boolean isPrivate;
        private String status;
        private String serverIp;
        private Integer serverPort;
        private String hostNickname;
        private String guestNickname;
    }
}
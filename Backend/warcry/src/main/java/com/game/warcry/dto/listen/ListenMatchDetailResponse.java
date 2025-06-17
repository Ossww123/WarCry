package com.game.warcry.dto.listen;

import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class ListenMatchDetailResponse {
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
        private String hostIp;
        private Integer hostPort;
        private String hostNickname;
        private String guestNickname;
    }
}
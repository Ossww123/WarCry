package com.game.warcry.dto.match;

import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class MatchJoinResponse {
    private boolean success;
    private Long matchId;
    private String serverIp;
    private Integer serverPort;
    private String role;
    private String status;
    private String message;
}
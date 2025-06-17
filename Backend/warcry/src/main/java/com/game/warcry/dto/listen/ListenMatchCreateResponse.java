package com.game.warcry.dto.listen;

import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class ListenMatchCreateResponse {
    private boolean success;
    private Long matchId;
    private String hostIp;
    private Integer hostPort;
    private String status;
    private String message;
}
package com.game.warcry.dto.listen;

import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class ListenMatchHostLeaveResponse {
    private boolean success;
    private Long matchId;
    private String result; // "DISBANDED" 또는 "TRANSFERRED"
    private Long newHostId; // TRANSFERRED인 경우만
    private String newHostNickname; // TRANSFERRED인 경우만
    private String message;
}
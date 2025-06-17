package com.game.warcry.dto.match;

import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class MatchHostLeaveResponse {
    private boolean success;
    private Long matchId;
    private String result; // "DISBANDED" 또는 "TRANSFERRED"
    private Long newHostId; // result가 "TRANSFERRED"인 경우에만 존재
    private String newHostNickname; // result가 "TRANSFERRED"인 경우에만 존재
    private String message;
}
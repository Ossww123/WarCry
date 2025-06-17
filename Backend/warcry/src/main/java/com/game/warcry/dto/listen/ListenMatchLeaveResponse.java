package com.game.warcry.dto.listen;

import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class ListenMatchLeaveResponse {
    private boolean success;
    private Long matchId;
    private String message;
}
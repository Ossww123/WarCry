package com.game.warcry.dto.rank;

import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

import java.time.LocalDateTime;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class MatchHistoryDTO {
    private Long matchId;
    private LocalDateTime timestamp;
    private String result; // "WIN" or "LOSE"
    private Integer pointsBefore;
    private Integer pointsAfter;
    private Integer pointsChange;
    private Integer tierBefore;
    private Integer tierAfter;
    private Long opponentId;
    private String opponentNickname;
}
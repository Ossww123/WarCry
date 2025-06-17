package com.game.warcry.dto.rank;

import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

import java.util.List;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class LeaderboardResponse {
    private boolean success;
    private Long totalPlayers;
    private Integer page;
    private Integer size;
    private Boolean hasNext;
    private List<LeaderboardPlayerDTO> players;
}
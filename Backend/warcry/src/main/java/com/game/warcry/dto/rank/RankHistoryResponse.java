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
public class RankHistoryResponse {
    private boolean success;
    private Long userId;
    private Long totalMatches;
    private Integer page;
    private Integer size;
    private Boolean hasNext;
    private List<MatchHistoryDTO> matches;
}
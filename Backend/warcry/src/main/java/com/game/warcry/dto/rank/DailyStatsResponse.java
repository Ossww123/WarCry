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
public class DailyStatsResponse {
    private boolean success;
    private Long userId;
    private List<DailyStatDTO> stats;
}
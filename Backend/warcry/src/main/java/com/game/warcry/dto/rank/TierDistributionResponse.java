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
public class TierDistributionResponse {
    private boolean success;
    private List<TierCountDTO> tiers;
}
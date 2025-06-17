package com.game.warcry.dto.rank;

import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class RatingChangeDTO {
    private Long userId;
    private Integer previousPoints;
    private Integer newPoints;
    private Integer change;
    private Integer previousTier;
    private Integer newTier;
}
package com.game.warcry.dto.rank;

import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class LeaderboardPlayerDTO {
    private Long rank;
    private Long userId;
    private String nickname;
    private Integer points;
    private Integer tier;
    private Integer wins;
    private Integer losses;
}
package com.game.warcry.dto.rank;

import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class DailyStatDTO {
    private String date; // "YYYYMMDD" 형식
    private Integer highestPoint;
    private Integer matchCount;
    private Integer win;
    private Integer loss;
}
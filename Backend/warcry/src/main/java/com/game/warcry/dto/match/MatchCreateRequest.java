package com.game.warcry.dto.match;

import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class MatchCreateRequest {
    private String title;
    private Boolean isPrivate;
    private String password;
}
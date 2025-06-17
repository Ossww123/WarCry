package com.game.warcry.dto.listen;

import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class ListenMatchCreateRequest {
    private String title;
    private Boolean isPrivate;
    private String password;
    private String hostIp;
    private Integer hostPort;
}
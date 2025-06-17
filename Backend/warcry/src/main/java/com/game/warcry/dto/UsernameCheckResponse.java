package com.game.warcry.dto;

import io.swagger.v3.oas.annotations.media.Schema;

@Schema(name = "UsernameCheckResponse", description = "아이디 중복 체크 응답")
public record UsernameCheckResponse(
        @Schema(description = "사용 가능 여부 (true: 사용 가능, false: 중복)") boolean available
) {}

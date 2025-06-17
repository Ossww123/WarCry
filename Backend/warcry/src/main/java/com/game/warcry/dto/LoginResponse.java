package com.game.warcry.dto;

public record LoginResponse(
        String accessToken,
        String tokenType, // 예: "Bearer"
        Long expiresIn // 토큰 만료 시간 (초 단위) - 선택 사항
) {}
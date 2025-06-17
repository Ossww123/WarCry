package com.game.warcry.dto;

import jakarta.validation.constraints.NotBlank;

public record SignupResponse(
        Long id,
        String username,
        String nickname,
        java.time.LocalDateTime createdAt) {}

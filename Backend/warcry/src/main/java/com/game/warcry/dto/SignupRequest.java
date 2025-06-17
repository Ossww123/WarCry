package com.game.warcry.dto;

import jakarta.validation.constraints.NotBlank;

public record SignupRequest(
        @NotBlank String username,
        @NotBlank String password,
        @NotBlank String nickname) {}
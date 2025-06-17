package com.game.warcry.dto;

import io.swagger.v3.oas.annotations.media.Schema;

@Schema(name = "UserInfoResponse", description = "현재 인증된 사용자의 정보 응답")
public record UserInfoResponse(
        @Schema(description = "사용자명", example = "player123") String username,
        @Schema(description = "닉네임", example = "용맹한기사") String nickname
) {}

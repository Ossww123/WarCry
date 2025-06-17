package com.game.warcry.dto.listen;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

import java.util.List;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
@Schema(description = "게임 결과 저장 요청")
public class ListenMatchResultRequest {

    @Schema(description = "사용자별 게임 결과 목록",
            example = "[{\"role\": \"HOST\", \"result\": \"WIN\"}, {\"role\": \"GUEST\", \"result\": \"LOSE\"}]")
    private List<PlayerResult> results;

    @Data
    @Builder
    @NoArgsConstructor
    @AllArgsConstructor
    @Schema(description = "플레이어 게임 결과")
    public static class PlayerResult {
        @Schema(description = "유저 역할 (HOST, GUEST 중 하나)", example = "HOST")
        private String role;

        @Schema(description = "게임 결과 (WIN, LOSE, NONE 중 하나)", example = "WIN")
        private String result;
    }
}
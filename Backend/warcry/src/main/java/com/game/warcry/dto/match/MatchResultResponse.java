package com.game.warcry.dto.match;

import com.game.warcry.dto.rank.RatingChangeDTO;
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
@Schema(description = "게임 결과 저장 응답")
public class MatchResultResponse {
    @Schema(description = "요청 성공 여부", example = "true")
    private boolean success;

    @Schema(description = "결과가 저장된 매치의 ID", example = "1")
    private Long matchId;

    @Schema(description = "결과 메시지",
            example = "게임 결과가 성공적으로 저장되었습니다.")
    private String message;

    @Schema(description = "랭킹 포인트 변화 정보")
    private List<RatingChangeDTO> ratingChanges;
}
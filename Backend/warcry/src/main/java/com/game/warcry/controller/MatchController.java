package com.game.warcry.controller;

import com.game.warcry.dto.ErrorResponse;
import com.game.warcry.dto.match.*;
import com.game.warcry.service.MatchService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.security.SecurityRequirement;
import io.swagger.v3.oas.annotations.tags.Tag;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.security.access.AccessDeniedException;
import org.springframework.security.core.Authentication;
import org.springframework.web.bind.annotation.*;
import com.game.warcry.dto.match.MatchLeaveResponse;
import com.game.warcry.dto.match.MatchHostLeaveResponse;

@RestController
@RequestMapping("/api/match")
@RequiredArgsConstructor
@Tag(name = "Match Controller", description = "매치 관련 API")
public class MatchController {

    private final MatchService matchService;

    @GetMapping
    @Operation(summary = "매치 목록 조회", description = "생성된 매치들의 목록을 조회합니다.",
            security = @SecurityRequirement(name = "bearerAuth"))
    public ResponseEntity<?> getMatches(
            @RequestParam(required = false) Boolean isPrivate,
            @RequestParam(required = false) String status,
            @RequestParam(required = false, defaultValue = "10") Integer limit) {

        try {
            MatchListResponse response = matchService.getMatches(isPrivate, status, limit);
            return ResponseEntity.ok(response);
        } catch (Exception e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("SERVER_ERROR")
                    .message("매치 목록을 조회하는 중 서버 오류가 발생했습니다.")
                    .build();
            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR).body(errorResponse);
        }
    }

    @GetMapping("/{matchId}")
    @Operation(summary = "매치 상세 조회", description = "특정 매치의 상세 정보를 조회합니다.",
            security = @SecurityRequirement(name = "bearerAuth"))
    public ResponseEntity<?> getMatchDetail(@PathVariable Long matchId) {
        try {
            MatchDetailResponse response = matchService.getMatchDetail(matchId);
            return ResponseEntity.ok(response);
        } catch (IllegalArgumentException e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("MATCH_NOT_FOUND")
                    .message("해당 매치를 찾을 수 없습니다.")
                    .build();
            return ResponseEntity.status(HttpStatus.NOT_FOUND).body(errorResponse);
        } catch (Exception e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("SERVER_ERROR")
                    .message("매치 정보를 조회하는 중 서버 오류가 발생했습니다.")
                    .build();
            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR).body(errorResponse);
        }
    }

    @PostMapping
    @Operation(summary = "매치 생성", description = "새로운 매치를 생성하고, 사용 가능한 Mirror 서버를 할당합니다.",
            security = @SecurityRequirement(name = "bearerAuth"))
    public ResponseEntity<?> createMatch(@RequestBody MatchCreateRequest request, Authentication authentication) {
        try {
            // 인증된 사용자 이름(username) 가져오기
            String username = authentication.getName();

            // 서비스 메서드 호출 시 username 전달 (서비스에서 userId 조회)
            MatchCreateResponse response = matchService.createMatch(username, request);
            return ResponseEntity.status(HttpStatus.CREATED).body(response);
        } catch (IllegalStateException e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("NO_AVAILABLE_SERVER")
                    .message("사용 가능한 게임 서버가 없습니다.")
                    .build();
            return ResponseEntity.status(HttpStatus.SERVICE_UNAVAILABLE).body(errorResponse);
        } catch (IllegalArgumentException e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("INVALID_REQUEST")
                    .message("입력값이 유효하지 않습니다.")
                    .build();
            return ResponseEntity.status(HttpStatus.BAD_REQUEST).body(errorResponse);
        } catch (Exception e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("SERVER_ERROR")
                    .message("매치 생성 중 서버 오류가 발생했습니다.")
                    .build();
            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR).body(errorResponse);
        }
    }

    @PostMapping("/{matchId}/join")
    @Operation(summary = "매치 참가", description = "유저가 특정 매치에 참가하여 GUEST로 등록됩니다.",
            security = @SecurityRequirement(name = "bearerAuth"))
    public ResponseEntity<?> joinMatch(@PathVariable Long matchId, @RequestBody MatchJoinRequest request, Authentication authentication) {
        try {
            // 인증된 사용자 이름(username) 가져오기
            String username = authentication.getName();

            // 서비스 메서드 호출 시 username 전달 (서비스에서 userId 조회)
            MatchJoinResponse response = matchService.joinMatch(matchId, username, request);
            return ResponseEntity.ok(response);
        } catch (IllegalArgumentException e) {
            if (e.getMessage().contains("매치를 찾을 수 없습니다")) {
                ErrorResponse errorResponse = ErrorResponse.builder()
                        .success(false)
                        .errorCode("MATCH_NOT_FOUND")
                        .message("해당 매치를 찾을 수 없습니다.")
                        .build();
                return ResponseEntity.status(HttpStatus.NOT_FOUND).body(errorResponse);
            } else if (e.getMessage().contains("비밀번호가 일치하지 않습니다")) {
                ErrorResponse errorResponse = ErrorResponse.builder()
                        .success(false)
                        .errorCode("INVALID_PASSWORD")
                        .message("비밀번호가 일치하지 않습니다.")
                        .build();
                return ResponseEntity.status(HttpStatus.UNAUTHORIZED).body(errorResponse);
            } else {
                ErrorResponse errorResponse = ErrorResponse.builder()
                        .success(false)
                        .errorCode("INVALID_REQUEST")
                        .message("입력값이 유효하지 않습니다.")
                        .build();
                return ResponseEntity.status(HttpStatus.BAD_REQUEST).body(errorResponse);
            }
        } catch (IllegalStateException e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("MATCH_FULL")
                    .message("해당 매치는 이미 인원이 가득 찼습니다.")
                    .build();
            return ResponseEntity.status(HttpStatus.FORBIDDEN).body(errorResponse);
        } catch (Exception e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("SERVER_ERROR")
                    .message("매치 참가 중 서버 오류가 발생했습니다.")
                    .build();
            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR).body(errorResponse);
        }
    }

    @PostMapping("/{matchId}/result")
    @Operation(summary = "게임 결과 저장", description = "게임 종료 시, 각 유저의 결과(승/패)를 저장하고 매치의 종료 시각을 기록합니다.",
            security = @SecurityRequirement(name = "bearerAuth"))
    public ResponseEntity<?> saveMatchResult(@PathVariable Long matchId,
                                             @RequestBody MatchResultRequest request,
                                             Authentication authentication) {
        try {
            // 인증된 사용자 이름 가져오기
            String username = authentication.getName();

            MatchResultResponse response = matchService.saveMatchResult(matchId, username, request);
            return ResponseEntity.status(HttpStatus.CREATED).body(response);
        } catch (AccessDeniedException e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("ACCESS_DENIED")
                    .message("해당 매치에 참여한 사용자만 결과를 저장할 수 있습니다.")
                    .build();
            return ResponseEntity.status(HttpStatus.FORBIDDEN).body(errorResponse);
        } catch (IllegalArgumentException e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("NOT_FOUND")
                    .message("해당 매치 또는 유저 정보를 찾을 수 없습니다.")
                    .build();
            return ResponseEntity.status(HttpStatus.NOT_FOUND).body(errorResponse);
        } catch (IllegalStateException e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("RESULT_ALREADY_EXISTS")
                    .message("해당 매치의 결과가 이미 저장되어 있습니다.")
                    .build();
            return ResponseEntity.status(HttpStatus.CONFLICT).body(errorResponse);
        } catch (Exception e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("SERVER_ERROR")
                    .message("게임 결과 저장 중 서버 오류가 발생했습니다.")
                    .build();
            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR).body(errorResponse);
        }
    }

    @PostMapping("/{matchId}/leave")
    @Operation(summary = "매치 나가기 (GUEST)", description = "GUEST 역할의 사용자가 매치에서 나갑니다.",
            security = @SecurityRequirement(name = "bearerAuth"))
    public ResponseEntity<?> leaveMatch(@PathVariable Long matchId, Authentication authentication) {
        try {
            // 인증된 사용자 이름 가져오기
            String username = authentication.getName();

            MatchLeaveResponse response = matchService.leaveMatch(matchId, username);
            return ResponseEntity.ok(response);
        } catch (IllegalArgumentException e) {
            if (e.getMessage().contains("매치를 찾을 수 없습니다")) {
                ErrorResponse errorResponse = ErrorResponse.builder()
                        .success(false)
                        .errorCode("MATCH_NOT_FOUND")
                        .message("해당 매치를 찾을 수 없습니다.")
                        .build();
                return ResponseEntity.status(HttpStatus.NOT_FOUND).body(errorResponse);
            } else if (e.getMessage().contains("참여하지 않은 사용자")) {
                ErrorResponse errorResponse = ErrorResponse.builder()
                        .success(false)
                        .errorCode("NOT_PARTICIPANT")
                        .message("해당 매치에 참여하지 않은 사용자입니다.")
                        .build();
                return ResponseEntity.status(HttpStatus.FORBIDDEN).body(errorResponse);
            } else if (e.getMessage().contains("방장은 이 API를 사용할 수 없습니다")) {
                ErrorResponse errorResponse = ErrorResponse.builder()
                        .success(false)
                        .errorCode("HOST_CANNOT_LEAVE")
                        .message("방장은 이 API를 사용할 수 없습니다. 방장 나가기 API를 사용하세요.")
                        .build();
                return ResponseEntity.status(HttpStatus.FORBIDDEN).body(errorResponse);
            } else {
                ErrorResponse errorResponse = ErrorResponse.builder()
                        .success(false)
                        .errorCode("INVALID_REQUEST")
                        .message("입력값이 유효하지 않습니다.")
                        .build();
                return ResponseEntity.status(HttpStatus.BAD_REQUEST).body(errorResponse);
            }
        } catch (IllegalStateException e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("MATCH_ALREADY_STARTED")
                    .message("이미 시작된 매치는 나갈 수 없습니다.")
                    .build();
            return ResponseEntity.status(HttpStatus.FORBIDDEN).body(errorResponse);
        } catch (Exception e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("SERVER_ERROR")
                    .message("매치 나가기 중 서버 오류가 발생했습니다.")
                    .build();
            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR).body(errorResponse);
        }
    }

    @PostMapping("/{matchId}/host-leave")
    @Operation(summary = "매치 나가기 (HOST)", description = "HOST 역할의 사용자가 매치에서 나가되, 다른 GUEST가 있을 경우 자동으로 HOST 권한을 이전합니다.",
            security = @SecurityRequirement(name = "bearerAuth"))
    public ResponseEntity<?> hostLeaveMatch(@PathVariable Long matchId, Authentication authentication) {
        try {
            // 인증된 사용자 이름 가져오기
            String username = authentication.getName();

            MatchHostLeaveResponse response = matchService.hostLeaveMatch(matchId, username);
            return ResponseEntity.ok(response);
        } catch (IllegalArgumentException e) {
            if (e.getMessage().contains("매치를 찾을 수 없습니다")) {
                ErrorResponse errorResponse = ErrorResponse.builder()
                        .success(false)
                        .errorCode("MATCH_NOT_FOUND")
                        .message("해당 매치를 찾을 수 없습니다.")
                        .build();
                return ResponseEntity.status(HttpStatus.NOT_FOUND).body(errorResponse);
            } else if (e.getMessage().contains("참여하지 않은 사용자")) {
                ErrorResponse errorResponse = ErrorResponse.builder()
                        .success(false)
                        .errorCode("NOT_PARTICIPANT")
                        .message("해당 매치에 참여하지 않은 사용자입니다.")
                        .build();
                return ResponseEntity.status(HttpStatus.FORBIDDEN).body(errorResponse);
            } else if (e.getMessage().contains("방장만 이 기능을 사용할 수 있습니다")) {
                ErrorResponse errorResponse = ErrorResponse.builder()
                        .success(false)
                        .errorCode("NOT_HOST")
                        .message("방장만 이 기능을 사용할 수 있습니다.")
                        .build();
                return ResponseEntity.status(HttpStatus.FORBIDDEN).body(errorResponse);
            } else {
                ErrorResponse errorResponse = ErrorResponse.builder()
                        .success(false)
                        .errorCode("INVALID_REQUEST")
                        .message("입력값이 유효하지 않습니다.")
                        .build();
                return ResponseEntity.status(HttpStatus.BAD_REQUEST).body(errorResponse);
            }
        } catch (IllegalStateException e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("MATCH_ALREADY_STARTED")
                    .message("이미 시작된 매치는 나갈 수 없습니다.")
                    .build();
            return ResponseEntity.status(HttpStatus.FORBIDDEN).body(errorResponse);
        } catch (Exception e) {
            ErrorResponse errorResponse = ErrorResponse.builder()
                    .success(false)
                    .errorCode("SERVER_ERROR")
                    .message("매치 나가기 중 서버 오류가 발생했습니다.")
                    .build();
            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR).body(errorResponse);
        }
    }
}
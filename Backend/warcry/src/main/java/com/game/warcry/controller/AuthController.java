package com.game.warcry.controller;

import com.game.warcry.dto.ErrorResponse;
import com.game.warcry.dto.LoginRequest;
import com.game.warcry.dto.LoginResponse;
import com.game.warcry.dto.SignupRequest;
import com.game.warcry.dto.SignupResponse;
import com.game.warcry.dto.UsernameCheckResponse;
import com.game.warcry.service.AuthService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.media.Content;
import io.swagger.v3.oas.annotations.media.Schema;
import io.swagger.v3.oas.annotations.responses.ApiResponse;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;
import com.game.warcry.dto.UserInfoResponse;
import io.swagger.v3.oas.annotations.security.SecurityRequirement;
import org.springframework.security.core.Authentication;

@RestController
@RequestMapping("/api/auth")
@RequiredArgsConstructor
@Tag(name = "Auth Controller", description = "회원가입 및 인증 관련 API를 제공합니다.")
public class AuthController {

    private final AuthService authService;

    @PostMapping("/signup")
    @Operation(
            summary = "회원가입 API",
            description = "사용자명, 비밀번호, 닉네임을 포함한 회원가입 요청을 받아 사용자 계정을 생성합니다.",
            responses = {
                    @ApiResponse(responseCode = "200", description = "회원가입 성공",
                            content = @Content(schema = @Schema(implementation = SignupResponse.class))),
                    @ApiResponse(responseCode = "409", description = "이미 존재하는 아이디입니다.",
                            content = @Content(schema = @Schema(implementation = ErrorResponse.class))),
                    @ApiResponse(responseCode = "400", description = "요청 데이터 유효성 검사 실패",
                            content = @Content(schema = @Schema(implementation = ErrorResponse.class)))
            }
    )
    public ResponseEntity<?> signup(@Valid @RequestBody SignupRequest req) {
        try {
            SignupResponse response = authService.signup(req);
            return ResponseEntity.ok(response);
        } catch (IllegalArgumentException e) {
            // 중복 아이디
            if ("이미 존재하는 아이디입니다.".equals(e.getMessage())) {
                ErrorResponse error = ErrorResponse.builder()
                        .success(false)
                        .errorCode("USERNAME_ALREADY_EXISTS")
                        .message(e.getMessage())
                        .build();
                return ResponseEntity
                        .status(HttpStatus.CONFLICT)
                        .body(error);
            }
            // 기타 잘못된 요청
            ErrorResponse error = ErrorResponse.builder()
                    .success(false)
                    .errorCode("INVALID_REQUEST")
                    .message(e.getMessage())
                    .build();
            return ResponseEntity
                    .badRequest()
                    .body(error);
        }
    }

    @PostMapping("/login")
    @Operation(
            summary = "로그인 API",
            description = "사용자명과 비밀번호로 로그인을 시도하고, 성공 시 JWT 액세스 토큰을 발급합니다.",
            responses = {
                    @ApiResponse(responseCode = "200", description = "로그인 성공",
                            content = @Content(schema = @Schema(implementation = LoginResponse.class))),
                    @ApiResponse(responseCode = "401", description = "인증 실패 (잘못된 사용자명 또는 비밀번호)",
                            content = @Content(schema = @Schema(implementation = ErrorResponse.class)))
            }
    )
    public ResponseEntity<?> login(@Valid @RequestBody LoginRequest req) {
        try {
            LoginResponse loginResponse = authService.login(req);
            return ResponseEntity.ok(loginResponse);
        } catch (Exception e) {
            ErrorResponse error = ErrorResponse.builder()
                    .success(false)
                    .errorCode("AUTH_FAILED")
                    .message("아이디 또는 비밀번호가 올바르지 않습니다.")
                    .build();
            return ResponseEntity
                    .status(HttpStatus.UNAUTHORIZED)
                    .body(error);
        }
    }

    @GetMapping("/check-username")
    @Operation(
            summary = "아이디 중복 체크 API",
            description = "쿼리파라미터로 주어진 username이 사용 가능한지 여부를 반환합니다.",
            responses = {
                    @ApiResponse(responseCode = "200", description = "중복 체크 결과",
                            content = @Content(schema = @Schema(implementation = UsernameCheckResponse.class))),
                    @ApiResponse(responseCode = "400", description = "username 파라미터 누락 또는 빈 값",
                            content = @Content(schema = @Schema(implementation = ErrorResponse.class)))
            }
    )
    public ResponseEntity<?> checkUsername(
            @RequestParam @NotBlank
            @Schema(description = "체크할 사용자명", example = "newuser123")
            String username) {
        boolean exists = authService.existsByUsername(username);
        // available = !exists
        UsernameCheckResponse response = new UsernameCheckResponse(!exists);
        return ResponseEntity.ok(response);
    }

    @GetMapping("/me")
    @Operation(
            summary = "유저 정보 조회 API",
            description = "헤더의 Bearer 토큰으로 현재 사용자의 username·nickname을 반환합니다.",
            security = @SecurityRequirement(name = "bearerAuth"),
            responses = {
                    @ApiResponse(responseCode = "200", description = "조회 성공",
                            content = @Content(schema = @Schema(implementation = UserInfoResponse.class))),
                    @ApiResponse(responseCode = "401", description = "인증 실패",
                            content = @Content(schema = @Schema(implementation = ErrorResponse.class)))
            }
    )
    public ResponseEntity<?> getCurrentUser(Authentication authentication) {
        String username = authentication.getName();
        UserInfoResponse info = authService.getUserInfo(username);
        return ResponseEntity.ok(info);
    }
}

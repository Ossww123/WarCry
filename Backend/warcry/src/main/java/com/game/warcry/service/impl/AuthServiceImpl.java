package com.game.warcry.service.impl;

import com.game.warcry.dto.LoginRequest;
import com.game.warcry.dto.LoginResponse;
import com.game.warcry.dto.SignupRequest;
import com.game.warcry.dto.SignupResponse;
import com.game.warcry.model.User;
import com.game.warcry.repository.UserRepository;
import com.game.warcry.security.jwt.JWTUtil; // JWTUtil 주입
import com.game.warcry.service.AuthService;
import com.game.warcry.service.RankService;
//import jakarta.transaction.Transactional;
import lombok.RequiredArgsConstructor;
import org.springframework.security.authentication.AuthenticationManager;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.security.core.userdetails.UsernameNotFoundException;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.stereotype.Service;
import com.game.warcry.dto.UserInfoResponse;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
public class AuthServiceImpl implements AuthService {

    private final UserRepository userRepository;
    private final PasswordEncoder passwordEncoder;
    private final JWTUtil jwtUtil; // JWT 생성 유틸리티
    private final AuthenticationManager authenticationManager; // Spring Security 인증 관리자
    private final RankService rankService; // 랭크 서비스 추가

    @Override
    @Transactional
    public SignupResponse signup(SignupRequest req) {
        if (userRepository.existsByUsername(req.username())) {
            throw new IllegalArgumentException("이미 존재하는 아이디입니다.");
        }

        User saved = userRepository.save(
                User.builder()
                        .username(req.username())
                        .password(passwordEncoder.encode(req.password()))
                        .nickname(req.nickname())
                        .build());

        // 레이팅 정보 초기화
        rankService.initializeUserRating(saved.getId());

        return new SignupResponse(saved.getId(), saved.getUsername(),
                saved.getNickname(), saved.getCreatedAt());
    }

    @Override
    public LoginResponse login(LoginRequest req) {
        // 1. 사용자 인증 시도
        Authentication authentication = authenticationManager.authenticate(
                new UsernamePasswordAuthenticationToken(req.username(), req.password())
        );

        // 2. 인증 성공 시 JWT 생성
        String username = authentication.getName(); // 인증된 사용자 이름 가져오기
        String token = jwtUtil.generate(username);

        // 3. LoginResponse 반환
        // JWTUtil에 EXP 상수가 있으므로 이를 활용하거나, 설정에서 가져올 수 있습니다.
        // 예시로 1시간(3600초)을 사용합니다. 실제로는 JWTUtil의 EXP 값을 사용하세요.
        long expiresIn = 2592000; // jwtUtil.getExpirationTimeInSeconds(); 와 같이 가져올 수 있도록 JWTUtil 수정 고려
        return new LoginResponse(token, "Bearer", expiresIn);
    }

    @Override
    public boolean existsByUsername(String username) {
        return userRepository.existsByUsername(username);
    }

    @Override
    @Transactional(readOnly = true)
    public UserInfoResponse getUserInfo(String username) {
        User user = userRepository.findByUsername(username)
                .orElseThrow(() -> new UsernameNotFoundException("유효하지 않은 사용자입니다."));
        return new UserInfoResponse(user.getUsername(), user.getNickname());
    }
}
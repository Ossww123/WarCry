package com.game.warcry.service;

import com.game.warcry.dto.LoginRequest;
import com.game.warcry.dto.LoginResponse;
import com.game.warcry.dto.SignupRequest;
import com.game.warcry.dto.SignupResponse;
import com.game.warcry.dto.UserInfoResponse;

public interface AuthService {
    SignupResponse signup(SignupRequest req);
    LoginResponse login(LoginRequest req);
    /**
     * @param username 체크할 사용자명
     * @return 이미 존재하면 true, 아니면 false
     */
    boolean existsByUsername(String username);

    /**
     * @param username 토큰에서 꺼낸 사용자명
     * @return 해당 사용자의 public 정보 (username, nickname)
     */
    UserInfoResponse getUserInfo(String username);
}

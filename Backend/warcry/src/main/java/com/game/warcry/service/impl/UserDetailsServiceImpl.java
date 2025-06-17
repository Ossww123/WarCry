package com.game.warcry.service.impl;

import com.game.warcry.model.User;
import com.game.warcry.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.userdetails.UserDetails;
import org.springframework.security.core.userdetails.UserDetailsService;
import org.springframework.security.core.userdetails.UsernameNotFoundException;
import org.springframework.stereotype.Service;

import java.util.Collections; // 권한이 없는 경우 사용

@Service
@RequiredArgsConstructor
public class UserDetailsServiceImpl implements UserDetailsService {

    private final UserRepository userRepository;

    @Override
    public UserDetails loadUserByUsername(String username) throws UsernameNotFoundException {
        User user = userRepository.findByUsername(username)
                .orElseThrow(() -> new UsernameNotFoundException("해당 사용자명을 찾을 수 없습니다: " + username));

        // 현재는 권한(authorities) 없이 설정합니다.
        // 만약 User 모델에 역할(Role) 관련 필드가 있고, 이를 사용하려면 다음과 같이 설정할 수 있습니다:
        //
        // List<GrantedAuthority> authorities = user.getRoles().stream()
        //          .map(role -> new SimpleGrantedAuthority(role.getName()))
        //          .collect(Collectors.toList());
        //
        // return new org.springframework.security.core.userdetails.User(
        //         user.getUsername(),
        //         user.getPassword(),
        //         authorities // 설정된 권한 목록
        // );

        return new org.springframework.security.core.userdetails.User(
                user.getUsername(),
                user.getPassword(),
                Collections.emptyList() // 현재는 빈 권한 목록을 사용
        );
    }
}
package com.game.warcry.security.jwt;

import io.jsonwebtoken.*;
import io.jsonwebtoken.security.Keys;
import jakarta.annotation.PostConstruct;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import javax.crypto.SecretKey;
import java.nio.charset.StandardCharsets;
import java.util.Date;

@Component
public class JWTUtil {

    @Value("${jwt.secret:change-this-secret-string-to-32bytes-min}")
    private String secret;

    private SecretKey key;
    private static final long EXP_MILLISECONDS = 1000L * 60 * 60; // 1h (밀리초 단위)

    @PostConstruct
    private void init() {
        key = Keys.hmacShaKeyFor(secret.getBytes(StandardCharsets.UTF_8));
    }

    public String generate(String username) {
        return Jwts.builder()
                .subject(username)
                .expiration(new Date(System.currentTimeMillis() + EXP_MILLISECONDS))
                .signWith(key, Jwts.SIG.HS256) // HS512에서 HS256으로 변경
                .compact();
    }

    public String extractUsername(String token) {
        Claims claims = Jwts.parser()
                .verifyWith(key)
                .build()
                .parseSignedClaims(token)
                .getPayload();
        return claims.getSubject();
    }

    // 토큰 만료 시간(초 단위) 반환 메소드 (선택 사항)
    public long getExpirationTimeInSeconds() {
        return EXP_MILLISECONDS / 1000;
    }
}
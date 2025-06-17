// src/main/java/com/game/warcry/domain/User.java
package com.game.warcry.model;

import jakarta.persistence.*;
import lombok.*;
import org.hibernate.annotations.CreationTimestamp;

import java.time.LocalDateTime;

@Entity
@Table(name = "users")
@Getter @Setter @Builder
@NoArgsConstructor @AllArgsConstructor
public class User {

    @Id @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(unique = true, nullable = false)
    private String username;     // 아이디

    @Column(nullable = false)
    private String password;     // 암호 (BCrypt)

    @Column(nullable = false)
    private String nickname;

    @CreationTimestamp      // INSERT 시 자동으로 현재 시각 주입
    @Column(name = "created_at", updatable = false)
    private LocalDateTime createdAt;
}

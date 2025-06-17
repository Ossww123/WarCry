package com.game.warcry.model;

import jakarta.persistence.*;
import lombok.*;

@Entity
@Table(name = "match_users")
@Getter @Setter @Builder
@NoArgsConstructor @AllArgsConstructor
public class MatchUser {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "user_id", nullable = false)
    private User user;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "match_id", nullable = false)
    private Match match;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    private UserRole role;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    private GameResult result;

    @PrePersist
    public void prePersist() {
        if (result == null) {
            result = GameResult.NONE;
        }
    }

    public enum UserRole {
        HOST, GUEST
    }

    public enum GameResult {
        WIN, LOSE, NONE
    }
}
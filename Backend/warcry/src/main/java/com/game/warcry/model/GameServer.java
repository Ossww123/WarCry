package com.game.warcry.model;

import jakarta.persistence.*;
import lombok.*;

import java.time.LocalDateTime;

@Entity
@Table(name = "game_servers")
@Getter @Setter @Builder
@NoArgsConstructor @AllArgsConstructor
public class GameServer {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private String serverIp;

    @Column(nullable = false)
    private Integer serverPort;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    private ServerStatus status;

    @Column(name = "last_updated")
    private LocalDateTime lastUpdated;

    public enum ServerStatus {
        AVAILABLE, IN_USE, MAINTENANCE
    }
}

package com.game.warcry.model;

import jakarta.persistence.*;
import lombok.*;

import java.time.LocalDateTime;

@Entity
@Table(name = "matches")
@Getter @Setter @Builder
@NoArgsConstructor @AllArgsConstructor
public class Match {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "server_id", nullable = true)
    private GameServer gameServer;

    @Column(nullable = false)
    private String title;

    @Column(nullable = false)
    private Boolean isPrivate;

    private String password;

    @Column(name = "start_time")
    private LocalDateTime startTime;

    @Column(name = "end_time")
    private LocalDateTime endTime;

    private String hostIp;

    private Integer hostPort;

    @Transient
    public MatchStatus getStatus() {
        if (endTime != null) {
            return MatchStatus.ENDED;
        } else if (startTime != null) {
            return MatchStatus.PLAYING;
        } else {
            return MatchStatus.WAITING;
        }
    }

    // Listen Server인지 확인하는 메서드 추가
    @Transient
    public boolean isListenServer() {
        return hostIp != null && hostPort != null;
    }

    public enum MatchStatus {
        WAITING, PLAYING, ENDED
    }
}

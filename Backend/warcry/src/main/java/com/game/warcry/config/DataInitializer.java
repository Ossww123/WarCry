package com.game.warcry.config;

import com.game.warcry.model.GameServer;
import com.game.warcry.repository.GameServerRepository;
import org.springframework.boot.CommandLineRunner;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

import java.time.LocalDateTime;
import java.util.List;

@Configuration
public class DataInitializer {

    @Bean
    public CommandLineRunner initData(GameServerRepository gameServerRepository) {
        return args -> {
            // 기존 데이터가 없는 경우에만 초기화
            if (gameServerRepository.count() == 0) {
                List<GameServer> servers = List.of(
                        GameServer.builder()
                                .serverIp("k12d104.p.ssafy.io")
                                .serverPort(7777)
                                .status(GameServer.ServerStatus.AVAILABLE)
                                .lastUpdated(LocalDateTime.now())
                                .build(),
                        GameServer.builder()
                                .serverIp("k12d104.p.ssafy.io")
                                .serverPort(7778)
                                .status(GameServer.ServerStatus.AVAILABLE)
                                .lastUpdated(LocalDateTime.now())
                                .build(),
                        GameServer.builder()
                                .serverIp("k12d104.p.ssafy.io")
                                .serverPort(7779)
                                .status(GameServer.ServerStatus.AVAILABLE)
                                .lastUpdated(LocalDateTime.now())
                                .build(),
                        GameServer.builder()
                                .serverIp("k12d104.p.ssafy.io")
                                .serverPort(7780)
                                .status(GameServer.ServerStatus.AVAILABLE)
                                .lastUpdated(LocalDateTime.now())
                                .build(),
                        GameServer.builder()
                                .serverIp("k12d104.p.ssafy.io")
                                .serverPort(7781)
                                .status(GameServer.ServerStatus.AVAILABLE)
                                .lastUpdated(LocalDateTime.now())
                                .build(),
                        GameServer.builder()
                                .serverIp("k12d104.p.ssafy.io")
                                .serverPort(7782)
                                .status(GameServer.ServerStatus.AVAILABLE)
                                .lastUpdated(LocalDateTime.now())
                                .build(),
                        GameServer.builder()
                                .serverIp("k12d104.p.ssafy.io")
                                .serverPort(7783)
                                .status(GameServer.ServerStatus.AVAILABLE)
                                .lastUpdated(LocalDateTime.now())
                                .build(),
                        GameServer.builder()
                                .serverIp("k12d104.p.ssafy.io")
                                .serverPort(7784)
                                .status(GameServer.ServerStatus.AVAILABLE)
                                .lastUpdated(LocalDateTime.now())
                                .build(),
                        GameServer.builder()
                                .serverIp("k12d104.p.ssafy.io")
                                .serverPort(7785)
                                .status(GameServer.ServerStatus.AVAILABLE)
                                .lastUpdated(LocalDateTime.now())
                                .build(),
                        GameServer.builder()
                                .serverIp("k12d104.p.ssafy.io")
                                .serverPort(7786)
                                .status(GameServer.ServerStatus.AVAILABLE)
                                .lastUpdated(LocalDateTime.now())
                                .build(),
                        GameServer.builder()
                                .serverIp("k12d104.p.ssafy.io")
                                .serverPort(7787)
                                .status(GameServer.ServerStatus.AVAILABLE)
                                .lastUpdated(LocalDateTime.now())
                                .build(),
                        GameServer.builder()
                                .serverIp("k12d104.p.ssafy.io")
                                .serverPort(7788)
                                .status(GameServer.ServerStatus.AVAILABLE)
                                .lastUpdated(LocalDateTime.now())
                                .build(),
                        GameServer.builder()
                                .serverIp("k12d104.p.ssafy.io")
                                .serverPort(7789)
                                .status(GameServer.ServerStatus.AVAILABLE)
                                .lastUpdated(LocalDateTime.now())
                                .build(),
                        GameServer.builder()
                                .serverIp("k12d104.p.ssafy.io")
                                .serverPort(7790)
                                .status(GameServer.ServerStatus.AVAILABLE)
                                .lastUpdated(LocalDateTime.now())
                                .build(),
                        GameServer.builder()
                                .serverIp("k12d104.p.ssafy.io")
                                .serverPort(7791)
                                .status(GameServer.ServerStatus.AVAILABLE)
                                .lastUpdated(LocalDateTime.now())
                                .build()
                );

                gameServerRepository.saveAll(servers);
                System.out.println("GameServer 초기 데이터가 생성되었습니다: " + servers.size() + "개");
            } else {
                System.out.println("GameServer 데이터가 이미 존재합니다. 초기화를 건너뜁니다.");
            }
        };
    }
}
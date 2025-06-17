package com.game.warcry;

import io.swagger.v3.oas.annotations.OpenAPIDefinition;
import io.swagger.v3.oas.annotations.info.Info; // 추가
import io.swagger.v3.oas.annotations.servers.Server;
import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;

@SpringBootApplication
@OpenAPIDefinition( // Swagger 기본 정보 설정
        info = @Info(title = "Warcry API", version = "v1", description = "Warcry 게임 백엔드 API 문서입니다.")
//        ,
//        servers = { // 운영 및 로컬 서버 정보 (선택 사항)
//                @Server(url = "https://k12d104.p.ssafy.io", description = "Prod (HTTPS)"),
//                @Server(url = "http://localhost:8080",   description = "Local (HTTP)")
//        }
)
public class WarcrybackApplication {
    public static void main(String[] args) {
        SpringApplication.run(WarcrybackApplication.class, args);
    }
}
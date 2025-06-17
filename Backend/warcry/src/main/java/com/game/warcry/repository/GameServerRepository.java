package com.game.warcry.repository;

import com.game.warcry.model.GameServer;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.stereotype.Repository;

import java.util.Optional;

@Repository
public interface GameServerRepository extends JpaRepository<GameServer, Long> {

    @Query("SELECT gs FROM GameServer gs WHERE gs.status = com.game.warcry.model.GameServer.ServerStatus.AVAILABLE ORDER BY gs.id LIMIT 1")
    Optional<GameServer> findFirstAvailableServer();
}
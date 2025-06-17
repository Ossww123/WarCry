package com.game.warcry.repository;

import com.game.warcry.model.Match;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.Optional;

@Repository
public interface MatchRepository extends JpaRepository<Match, Long> {

    @Query("SELECT m FROM Match m WHERE (:isPrivate IS NULL OR m.isPrivate = :isPrivate) AND " +
            "(:status = 'WAITING' AND m.startTime IS NULL OR " +
            ":status = 'PLAYING' AND m.startTime IS NOT NULL AND m.endTime IS NULL OR " +
            ":status = 'ENDED' AND m.endTime IS NOT NULL OR " +
            ":status IS NULL)")
    List<Match> findMatchesByFilters(@Param("isPrivate") Boolean isPrivate,
                                     @Param("status") String status,
                                     @Param("limit") Integer limit);

    // Listen Server에서 사용할 메서드: 동일한 IP와 Port 조합을 가진 매치 찾기
    Optional<Match> findByHostIpAndHostPort(String hostIp, Integer hostPort);
}
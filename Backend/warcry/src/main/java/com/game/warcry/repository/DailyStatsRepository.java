package com.game.warcry.repository;

import com.game.warcry.model.DailyStats;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.stereotype.Repository;

import java.time.LocalDate;
import java.util.List;
import java.util.Optional;

@Repository
public interface DailyStatsRepository extends JpaRepository<DailyStats, Long> {

    List<DailyStats> findByUserIdAndDateBetweenOrderByDateAsc(Long userId, LocalDate startDate, LocalDate endDate);

    List<DailyStats> findByDateOrderByHighestPointDesc(LocalDate date, Pageable pageable);

    Optional<DailyStats> findByUserIdAndDate(Long userId, LocalDate date);

    @Query("SELECT COUNT(DISTINCT ds.user.id) FROM DailyStats ds WHERE ds.date = :date")
    long countActivePlayers(LocalDate date);

    @Query("SELECT AVG(ds.matchCount) FROM DailyStats ds WHERE ds.date = :date")
    double getAverageMatchCount(LocalDate date);
}
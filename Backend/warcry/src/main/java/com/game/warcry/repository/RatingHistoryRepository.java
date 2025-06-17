package com.game.warcry.repository;

import com.game.warcry.model.RatingHistory;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;

@Repository
public interface RatingHistoryRepository extends JpaRepository<RatingHistory, Long> {

    List<RatingHistory> findByUserIdOrderByChangeTimeDesc(Long userId, Pageable pageable);

    long countByUserId(Long userId);
}
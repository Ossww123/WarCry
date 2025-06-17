package com.game.warcry.repository;

import com.game.warcry.model.Rating;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.Optional;

@Repository
public interface RatingRepository extends JpaRepository<Rating, Long> {

    Optional<Rating> findByUserId(Long userId);

    List<Rating> findByTierOrderByPointDesc(Integer tier, Pageable pageable);

    List<Rating> findAllByOrderByPointDesc(Pageable pageable);

    long countByTier(Integer tier);

    @Query("SELECT COUNT(r) FROM Rating r WHERE r.point > (SELECT r2.point FROM Rating r2 WHERE r2.userId = :userId)")
    long countPlayersWithHigherPoints(Long userId);

    @Query("SELECT COUNT(r) FROM Rating r WHERE r.tier = :tier AND r.point > (SELECT r2.point FROM Rating r2 WHERE r2.userId = :userId)")
    long countPlayersWithHigherPointsInTier(Long userId, Integer tier);
}
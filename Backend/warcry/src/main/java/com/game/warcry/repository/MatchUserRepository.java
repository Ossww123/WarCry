package com.game.warcry.repository;

import com.game.warcry.model.Match;
import com.game.warcry.model.MatchUser;
import com.game.warcry.model.User;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.Optional;

@Repository
public interface MatchUserRepository extends JpaRepository<MatchUser, Long> {

    List<MatchUser> findByMatch(Match match);

    Optional<MatchUser> findByMatchAndRole(Match match, MatchUser.UserRole role);

    Optional<MatchUser> findByMatchAndUser(Match match, User user);

    boolean existsByMatchAndUser(Match match, User user);

    boolean existsByMatchAndRole(Match match, MatchUser.UserRole role);

    int countByMatch(Match match);

    Optional<MatchUser> findByMatchAndUserNot(Match match, User user);
}
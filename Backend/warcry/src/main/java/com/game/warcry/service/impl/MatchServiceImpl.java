package com.game.warcry.service.impl;

import com.game.warcry.dto.match.*;
import com.game.warcry.dto.rank.RatingChangeDTO;
import com.game.warcry.model.GameServer;
import com.game.warcry.model.Match;
import com.game.warcry.model.MatchUser;
import com.game.warcry.model.User;
import com.game.warcry.repository.GameServerRepository;
import com.game.warcry.repository.MatchRepository;
import com.game.warcry.repository.MatchUserRepository;
import com.game.warcry.repository.UserRepository;
import com.game.warcry.service.MatchService;
import com.game.warcry.service.RankService;
import lombok.RequiredArgsConstructor;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.security.access.AccessDeniedException;
import org.springframework.security.core.userdetails.UsernameNotFoundException;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import com.game.warcry.dto.match.MatchLeaveResponse;
import com.game.warcry.dto.match.MatchHostLeaveResponse;

import java.time.LocalDateTime;
import java.util.List;
import java.util.Objects;
import java.util.stream.Collectors;

@Service
@RequiredArgsConstructor
public class MatchServiceImpl implements MatchService {

    private final Logger log = LoggerFactory.getLogger(MatchServiceImpl.class);
    private final MatchRepository matchRepository;
    private final GameServerRepository gameServerRepository;
    private final MatchUserRepository matchUserRepository;
    private final UserRepository userRepository;
    private final RankService rankService;

    @Override
    @Transactional(readOnly = true)
    public MatchListResponse getMatches(Boolean isPrivate, String status, Integer limit) {
        List<Match> matches = matchRepository.findMatchesByFilters(isPrivate, status, limit);

        List<MatchListResponse.MatchItem> matchItems = matches.stream()
                .map(match -> {
                    // 호스트 정보 조회
                    MatchUser hostMatchUser = matchUserRepository.findByMatchAndRole(match, MatchUser.UserRole.HOST)
                            .orElse(null);

                    String hostNickname = hostMatchUser != null ? hostMatchUser.getUser().getNickname() : "Unknown";

                    return MatchListResponse.MatchItem.builder()
                            .matchId(match.getId())
                            .title(match.getTitle())
                            .hostNickname(hostNickname)
                            .isPrivate(match.getIsPrivate())
                            .status(match.getStatus().name())
                            .build();
                })
                .collect(Collectors.toList());

        return MatchListResponse.builder()
                .success(true)
                .matches(matchItems)
                .build();
    }

    @Override
    @Transactional(readOnly = true)
    public MatchDetailResponse getMatchDetail(Long matchId) {
        Match match = matchRepository.findById(matchId)
                .orElseThrow(() -> new IllegalArgumentException("매치를 찾을 수 없습니다."));

        // 호스트 정보 조회
        MatchUser hostMatchUser = matchUserRepository.findByMatchAndRole(match, MatchUser.UserRole.HOST)
                .orElse(null);

        // 게스트 정보 조회
        MatchUser guestMatchUser = matchUserRepository.findByMatchAndRole(match, MatchUser.UserRole.GUEST)
                .orElse(null);

        String hostNickname = hostMatchUser != null ? hostMatchUser.getUser().getNickname() : "Unknown";
        String guestNickname = guestMatchUser != null ? guestMatchUser.getUser().getNickname() : null;

        MatchDetailResponse.MatchDto matchDto = MatchDetailResponse.MatchDto.builder()
                .matchId(match.getId())
                .title(match.getTitle())
                .isPrivate(match.getIsPrivate())
                .status(match.getStatus().name())
                .serverIp(match.getGameServer().getServerIp())
                .serverPort(match.getGameServer().getServerPort())
                .hostNickname(hostNickname)
                .guestNickname(guestNickname)
                .build();

        return MatchDetailResponse.builder()
                .success(true)
                .match(matchDto)
                .build();
    }

    @Override
    @Transactional
    public MatchCreateResponse createMatch(String username, MatchCreateRequest request) {
        // 사용 가능한 서버 찾기
        GameServer gameServer = gameServerRepository.findFirstAvailableServer()
                .orElseThrow(() -> new IllegalStateException("사용 가능한 게임 서버가 없습니다."));

        // 유저 확인 (username으로 조회)
        User user = userRepository.findByUsername(username)
                .orElseThrow(() -> new UsernameNotFoundException("유효하지 않은 사용자입니다."));

        // 매치 생성
        Match match = Match.builder()
                .gameServer(gameServer)
                .title(request.getTitle())
                .isPrivate(request.getIsPrivate())
                .password(request.getPassword())
                .build();

        Match savedMatch = matchRepository.save(match);

        // 서버 상태 업데이트
        gameServer.setStatus(GameServer.ServerStatus.IN_USE);
        gameServer.setLastUpdated(LocalDateTime.now());
        gameServerRepository.save(gameServer);

        // 호스트로 MatchUser 생성
        MatchUser matchUser = MatchUser.builder()
                .match(savedMatch)
                .user(user)
                .role(MatchUser.UserRole.HOST)
                .result(MatchUser.GameResult.NONE)
                .build();

        matchUserRepository.save(matchUser);

        // 신규 유저의 경우 초기 레이팅 생성
        // rankService.initializeUserRating(user.getId());

        return MatchCreateResponse.builder()
                .success(true)
                .matchId(savedMatch.getId())
                .serverIp(gameServer.getServerIp())
                .serverPort(gameServer.getServerPort())
                .status(savedMatch.getStatus().name())
                .message("매치가 성공적으로 생성되었습니다.")
                .build();
    }

    @Override
    @Transactional
    public MatchJoinResponse joinMatch(Long matchId, String username, MatchJoinRequest request) {
        Match match = matchRepository.findById(matchId)
                .orElseThrow(() -> new IllegalArgumentException("해당 매치를 찾을 수 없습니다."));

        // 유저 확인 (username으로 조회)
        User user = userRepository.findByUsername(username)
                .orElseThrow(() -> new UsernameNotFoundException("유효하지 않은 사용자입니다."));

        // 매치가 가득 찼는지 확인
        int participantCount = matchUserRepository.countByMatch(match);
        if (participantCount >= 2) {
            throw new IllegalStateException("해당 매치는 이미 인원이 가득 찼습니다.");
        }

        // 비공개 매치인 경우 비밀번호 확인
        if (match.getIsPrivate() && (request.getPassword() == null || !Objects.equals(request.getPassword(), match.getPassword()))) {
            throw new IllegalArgumentException("비밀번호가 일치하지 않습니다.");
        }

        // 게스트로 MatchUser 생성
        MatchUser matchUser = MatchUser.builder()
                .match(match)
                .user(user)
                .role(MatchUser.UserRole.GUEST)
                .result(MatchUser.GameResult.NONE)
                .build();

        matchUserRepository.save(matchUser);

        // 신규 유저의 경우 초기 레이팅 생성
        // rankService.initializeUserRating(user.getId());

        return MatchJoinResponse.builder()
                .success(true)
                .matchId(match.getId())
                .serverIp(match.getGameServer().getServerIp())
                .serverPort(match.getGameServer().getServerPort())
                .role("GUEST")
                .status("WAITING")
                .message("매치에 성공적으로 참가했습니다.")
                .build();
    }

    @Override
    @Transactional
    public MatchResultResponse saveMatchResult(Long matchId, String username, MatchResultRequest request) {
        // 1. 매치 존재 여부 확인
        Match match = matchRepository.findById(matchId)
                .orElseThrow(() -> new IllegalArgumentException("해당 매치 또는 유저 정보를 찾을 수 없습니다."));

        // 2. 요청자가 매치 참여자인지 확인 (보안 검증)
        User requester = userRepository.findByUsername(username)
                .orElseThrow(() -> new UsernameNotFoundException("유효하지 않은 사용자입니다."));

        boolean isParticipant = matchUserRepository.existsByMatchAndUser(match, requester);
        if (!isParticipant) {
            throw new AccessDeniedException("해당 매치에 참여한 사용자만 결과를 저장할 수 있습니다.");
        }

        // 3. 이미 결과가 저장된 매치인지 확인 (end_time이 있으면 이미 종료된 매치)
        if (match.getEndTime() != null) {
            throw new IllegalStateException("해당 매치의 결과가 이미 저장되어 있습니다.");
        }

        // 4. 요청 데이터 유효성 검증
        if (request.getResults() == null || request.getResults().size() != 2) {
            throw new IllegalArgumentException("입력값이 유효하지 않습니다. 두 플레이어의 결과가 필요합니다.");
        }

        // 5. WIN, LOSE 각각 1개씩 존재하는지 확인
        long winCount = request.getResults().stream()
                .filter(result -> "WIN".equals(result.getResult()))
                .count();

        long loseCount = request.getResults().stream()
                .filter(result -> "LOSE".equals(result.getResult()))
                .count();

        if (winCount != 1 || loseCount != 1) {
            throw new IllegalArgumentException("입력값이 유효하지 않습니다. 승리와 패배는 각각 1명씩이어야 합니다.");
        }

        // 6. 각 역할별 유저를 찾아 결과 업데이트
        List<MatchUser> matchUsers = matchUserRepository.findByMatch(match);

        for (MatchResultRequest.PlayerResult playerResult : request.getResults()) {
            // 역할로 매치 유저 찾기
            MatchUser.UserRole role = MatchUser.UserRole.valueOf(playerResult.getRole());
            MatchUser matchUser = matchUserRepository.findByMatchAndRole(match, role)
                    .orElseThrow(() -> new IllegalArgumentException("해당 역할의 유저를 찾을 수 없습니다."));

            // 결과 업데이트 (String을 Enum으로 변환)
            MatchUser.GameResult gameResult = MatchUser.GameResult.valueOf(playerResult.getResult());
            matchUser.setResult(gameResult);
            matchUserRepository.save(matchUser);
        }

        // 7. 매치 종료 시간 업데이트
        match.setEndTime(LocalDateTime.now());
        matchRepository.save(match);

        // 8. 추가: 게임 서버 상태 업데이트
        GameServer gameServer = match.getGameServer();
        if (gameServer != null && gameServer.getStatus() == GameServer.ServerStatus.IN_USE) {
            gameServer.setStatus(GameServer.ServerStatus.AVAILABLE);
            gameServer.setLastUpdated(LocalDateTime.now());
            gameServerRepository.save(gameServer);
            log.info("매치 ID: {} 종료로 서버 ID: {} 상태를 IN_USE → AVAILABLE로 변경", matchId, gameServer.getId());
        }

        // 9. 승자와 패자 ID 목록 생성
        List<Long> winnerIds = matchUsers.stream()
                .filter(mu -> mu.getResult() == MatchUser.GameResult.WIN)
                .map(mu -> mu.getUser().getId())
                .collect(Collectors.toList());

        List<Long> loserIds = matchUsers.stream()
                .filter(mu -> mu.getResult() == MatchUser.GameResult.LOSE)
                .map(mu -> mu.getUser().getId())
                .collect(Collectors.toList());

        // 10. 랭킹 시스템 업데이트
        List<RatingChangeDTO> ratingChanges = rankService.processMatchResult(matchId, winnerIds, loserIds);

        // 11. 응답 생성
        return MatchResultResponse.builder()
                .success(true)
                .matchId(match.getId())
                .message("게임 결과가 성공적으로 저장되었습니다.")
                .ratingChanges(ratingChanges) // 랭킹 변경 정보 추가
                .build();
    }

    @Override
    @Transactional
    public MatchLeaveResponse leaveMatch(Long matchId, String username) {
        // 1. 매치 존재 여부 확인
        Match match = matchRepository.findById(matchId)
                .orElseThrow(() -> new IllegalArgumentException("해당 매치를 찾을 수 없습니다."));

        // 2. 유저 확인
        User user = userRepository.findByUsername(username)
                .orElseThrow(() -> new UsernameNotFoundException("유효하지 않은 사용자입니다."));

        // 3. 유저가 해당 매치에 참여하는지 확인
        MatchUser matchUser = matchUserRepository.findByMatchAndUser(match, user)
                .orElseThrow(() -> new IllegalArgumentException("해당 매치에 참여하지 않은 사용자입니다."));

        // 4. 게임이 시작됐는지 확인
        if (match.getStartTime() != null) {
            throw new IllegalStateException("이미 시작된 매치는 나갈 수 없습니다.");
        }

        // 5. 역할 확인: HOST인 경우 hostLeaveMatch 사용 안내
        if (matchUser.getRole() == MatchUser.UserRole.HOST) {
            throw new IllegalArgumentException("방장은 이 API를 사용할 수 없습니다. 방장 나가기 API를 사용하세요.");
        }

        // 6. GUEST 유저 제거
        matchUserRepository.delete(matchUser);

        // 7. 응답 반환
        return MatchLeaveResponse.builder()
                .success(true)
                .matchId(matchId)
                .message("매치에서 성공적으로 나갔습니다.")
                .build();
    }

    @Override
    @Transactional
    public MatchHostLeaveResponse hostLeaveMatch(Long matchId, String username) {
        // 1. 매치 존재 여부 확인
        Match match = matchRepository.findById(matchId)
                .orElseThrow(() -> new IllegalArgumentException("해당 매치를 찾을 수 없습니다."));

        // 2. 유저 확인
        User user = userRepository.findByUsername(username)
                .orElseThrow(() -> new UsernameNotFoundException("유효하지 않은 사용자입니다."));

        // 3. 유저가 해당 매치에 참여하는지 확인
        MatchUser hostMatchUser = matchUserRepository.findByMatchAndUser(match, user)
                .orElseThrow(() -> new IllegalArgumentException("해당 매치에 참여하지 않은 사용자입니다."));

        // 4. 유저가 HOST인지 확인
        if (hostMatchUser.getRole() != MatchUser.UserRole.HOST) {
            throw new IllegalArgumentException("방장만 이 기능을 사용할 수 있습니다.");
        }

        // 5. 게임이 시작됐는지 확인
        if (match.getStartTime() != null) {
            throw new IllegalStateException("이미 시작된 매치는 나갈 수 없습니다.");
        }

        // 6. GUEST 역할의 참가자가 있는지 확인
        List<MatchUser> allUsers = matchUserRepository.findByMatch(match);
        List<MatchUser> guests = allUsers.stream()
                .filter(mu -> mu.getRole() == MatchUser.UserRole.GUEST)
                .collect(Collectors.toList());

        if (guests.isEmpty()) {
            // 다른 참가자가 없으면 매치 삭제
            // 먼저 모든 MatchUser 레코드 삭제
            matchUserRepository.deleteAll(allUsers);

            // 게임 서버 상태 변경
            GameServer gameServer = match.getGameServer();
            gameServer.setStatus(GameServer.ServerStatus.AVAILABLE);
            gameServer.setLastUpdated(LocalDateTime.now());
            gameServerRepository.save(gameServer);
            log.info("호스트 나가기로 매치 ID: {} 해산으로 서버 ID: {} 상태를 AVAILABLE로 변경", matchId, gameServer.getId());

            // 매치 삭제
            matchRepository.delete(match);

            return MatchHostLeaveResponse.builder()
                    .success(true)
                    .matchId(matchId)
                    .result("DISBANDED")
                    .message("매치가 성공적으로 해산되었습니다.")
                    .build();
        } else {
            // GUEST 중 가장 먼저 참가한 유저를 새 HOST로 지정
            MatchUser newHost = guests.get(0);
            newHost.setRole(MatchUser.UserRole.HOST);
            matchUserRepository.save(newHost);

            // 기존 HOST 정보 삭제
            matchUserRepository.delete(hostMatchUser);

            return MatchHostLeaveResponse.builder()
                    .success(true)
                    .matchId(matchId)
                    .result("TRANSFERRED")
                    .newHostId(newHost.getUser().getId())
                    .newHostNickname(newHost.getUser().getNickname())
                    .message("매치에서 나갔습니다. 방장 권한이 다른 참가자에게 이전되었습니다.")
                    .build();
        }
    }
}
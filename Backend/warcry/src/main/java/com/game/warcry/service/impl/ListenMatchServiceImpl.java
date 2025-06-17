package com.game.warcry.service.impl;

import com.game.warcry.dto.listen.ListenMatchCreateRequest;
import com.game.warcry.dto.listen.ListenMatchCreateResponse;
import com.game.warcry.dto.listen.ListenMatchListResponse;
import com.game.warcry.dto.listen.ListenMatchDetailResponse;
import com.game.warcry.dto.listen.ListenMatchJoinRequest;
import com.game.warcry.dto.listen.ListenMatchJoinResponse;
import com.game.warcry.dto.listen.ListenMatchLeaveResponse;
import com.game.warcry.dto.listen.ListenMatchHostLeaveResponse;
import com.game.warcry.dto.listen.ListenMatchResultRequest;
import com.game.warcry.dto.listen.ListenMatchResultResponse;
import com.game.warcry.dto.rank.RatingChangeDTO;
import com.game.warcry.service.RankService;
import org.springframework.security.access.AccessDeniedException;
import java.util.List;
import java.util.stream.Collectors;
import java.util.Objects;
import com.game.warcry.model.Match;
import com.game.warcry.model.MatchUser;
import com.game.warcry.model.User;
import com.game.warcry.repository.MatchRepository;
import com.game.warcry.repository.MatchUserRepository;
import com.game.warcry.repository.UserRepository;
import com.game.warcry.service.ListenMatchService;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.Optional;

@Service
@RequiredArgsConstructor
public class ListenMatchServiceImpl implements ListenMatchService {

    private final MatchRepository matchRepository;
    private final UserRepository userRepository;
    private final MatchUserRepository matchUserRepository;
    private final RankService rankService;

    @Override
    @Transactional
    public ListenMatchCreateResponse createMatch(String username, ListenMatchCreateRequest request) {
        // 요청 검증
        if (request.getTitle() == null || request.getIsPrivate() == null ||
                request.getHostIp() == null || request.getHostPort() == null) {
            throw new IllegalArgumentException("필수 필드가 누락되었습니다.");
        }

        // 비공개 매치의 경우 비밀번호 필수
        if (Boolean.TRUE.equals(request.getIsPrivate()) &&
                (request.getPassword() == null || request.getPassword().isEmpty())) {
            throw new IllegalArgumentException("비공개 매치는 비밀번호가 필요합니다.");
        }

        // IP/PORT 조합 중복 확인
        Optional<Match> existingMatch = matchRepository.findByHostIpAndHostPort(
                request.getHostIp(), request.getHostPort());
        if (existingMatch.isPresent()) {
            throw new IllegalStateException("해당 IP/PORT 조합이 이미 사용 중입니다.");
        }

        // 사용자 조회
        User user = userRepository.findByUsername(username)
                .orElseThrow(() -> new IllegalArgumentException("사용자를 찾을 수 없습니다."));

        // 매치 생성
        Match match = Match.builder()
                .title(request.getTitle())
                .isPrivate(request.getIsPrivate())
                .password(request.getPassword())
                .hostIp(request.getHostIp())
                .hostPort(request.getHostPort())
                .gameServer(null) // 명시적으로 null 설정하였음.
                .build();
        match = matchRepository.save(match);

        // HOST로 사용자 등록
        MatchUser matchUser = MatchUser.builder()
                .user(user)
                .match(match)
                .role(MatchUser.UserRole.HOST)
                .build();
        matchUserRepository.save(matchUser);

        // 신규 유저의 경우 초기 레이팅 생성
        rankService.initializeUserRating(user.getId());

        // 응답 생성
        return ListenMatchCreateResponse.builder()
                .success(true)
                .matchId(match.getId())
                .hostIp(match.getHostIp())
                .hostPort(match.getHostPort())
                .status(match.getStatus().name())
                .message("매치가 성공적으로 생성되었습니다.")
                .build();
    }

    @Override
    @Transactional(readOnly = true)
    public ListenMatchListResponse getMatches(Boolean isPrivate, String status, Integer limit) {
        // 모든 매치를 가져와서 필터링
        List<Match> allMatches = matchRepository.findMatchesByFilters(isPrivate, status, limit);

        // Listen Server 매치만 필터링 (hostIp, hostPort가 null이 아닌 매치)
        List<Match> listenMatches = allMatches.stream()
                .filter(Match::isListenServer)
                .collect(Collectors.toList());

        // DTO 변환
        List<ListenMatchListResponse.MatchSummary> matchSummaries = listenMatches.stream()
                .map(match -> {
                    // HOST 유저 찾기
                    MatchUser hostUser = matchUserRepository.findByMatchAndRole(match, MatchUser.UserRole.HOST)
                            .orElse(null);

                    String hostNickname = hostUser != null ? hostUser.getUser().getNickname() : "Unknown";

                    return ListenMatchListResponse.MatchSummary.builder()
                            .matchId(match.getId())
                            .title(match.getTitle())
                            .hostNickname(hostNickname)
                            .isPrivate(match.getIsPrivate())
                            .status(match.getStatus().name())
                            .build();
                })
                .collect(Collectors.toList());

        return ListenMatchListResponse.builder()
                .success(true)
                .matches(matchSummaries)
                .build();
    }

    @Override
    @Transactional(readOnly = true)
    public ListenMatchDetailResponse getMatchDetail(Long matchId) {
        // 매치 조회
        Match match = matchRepository.findById(matchId)
                .orElseThrow(() -> new IllegalArgumentException("해당 매치를 찾을 수 없습니다."));

        // Listen Server 매치인지 확인
        if (!match.isListenServer()) {
            throw new IllegalArgumentException("해당 매치는 Listen Server 매치가 아닙니다.");
        }

        // 호스트 정보 조회
        MatchUser hostUser = matchUserRepository.findByMatchAndRole(match, MatchUser.UserRole.HOST)
                .orElse(null);

        // 게스트 정보 조회
        MatchUser guestUser = matchUserRepository.findByMatchAndRole(match, MatchUser.UserRole.GUEST)
                .orElse(null);

        String hostNickname = hostUser != null ? hostUser.getUser().getNickname() : "Unknown";
        String guestNickname = guestUser != null ? guestUser.getUser().getNickname() : null;

        // DTO 변환
        ListenMatchDetailResponse.MatchDto matchDto = ListenMatchDetailResponse.MatchDto.builder()
                .matchId(match.getId())
                .title(match.getTitle())
                .isPrivate(match.getIsPrivate())
                .status(match.getStatus().name())
                .hostIp(match.getHostIp())
                .hostPort(match.getHostPort())
                .hostNickname(hostNickname)
                .guestNickname(guestNickname)
                .build();

        return ListenMatchDetailResponse.builder()
                .success(true)
                .match(matchDto)
                .build();
    }

    @Override
    @Transactional
    public ListenMatchJoinResponse joinMatch(Long matchId, String username, ListenMatchJoinRequest request) {
        // 1. 매치 조회 (기존 코드 유지)
        Match match = matchRepository.findById(matchId)
                .orElseThrow(() -> new IllegalArgumentException("해당 매치를 찾을 수 없습니다."));

        // 2. Listen Server 매치인지 확인 (기존 코드 유지)
        if (!match.isListenServer()) {
            throw new IllegalArgumentException("해당 매치는 Listen Server 매치가 아닙니다.");
        }

        // 3. 유저 조회 (기존 코드 유지)
        User user = userRepository.findByUsername(username)
                .orElseThrow(() -> new IllegalArgumentException("사용자를 찾을 수 없습니다."));

        // 4. 역할 확인 - HOST 존재 여부 확인 (새로 추가)
        boolean hasHost = matchUserRepository.existsByMatchAndRole(match, MatchUser.UserRole.HOST);

        // 5. 참가자 수 확인 (기존 코드 수정)
        int participantCount = matchUserRepository.countByMatch(match);
        if (participantCount >= 2) {
            throw new IllegalStateException("해당 매치는 이미 인원이 가득 찼습니다.");
        }

        // 6. 이미 참가했는지 확인 (기존 코드 수정 - Optional 사용)
        Optional<MatchUser> existingUser = matchUserRepository.findByMatchAndUser(match, user);
        if (existingUser.isPresent()) {
            throw new IllegalStateException("이미 해당 매치에 참가 중입니다.");
        }

        // 7. 비공개 매치인 경우 비밀번호 확인 (기존 코드 유지)
        if (Boolean.TRUE.equals(match.getIsPrivate()) &&
                (request.getPassword() == null || !Objects.equals(request.getPassword(), match.getPassword()))) {
            throw new IllegalArgumentException("비밀번호가 일치하지 않습니다.");
        }

        // 8. 역할 결정 - HOST가 없으면 HOST, 있으면 GUEST (새로 추가)
        MatchUser.UserRole role;
        if (!hasHost) {
            role = MatchUser.UserRole.HOST;
        } else {
            // HOST가 이미 있으면 GUEST로 참가
            boolean hasGuest = matchUserRepository.existsByMatchAndRole(match, MatchUser.UserRole.GUEST);
            if (hasGuest) {
                throw new IllegalStateException("해당 매치는 이미 인원이 가득 찼습니다.");
            }
            role = MatchUser.UserRole.GUEST;
        }

        // 9. 매치 유저 생성 (기존 코드 수정)
        MatchUser matchUser = MatchUser.builder()
                .match(match)
                .user(user)
                .role(role)  // 동적으로 결정된 역할 사용
                .result(MatchUser.GameResult.NONE)
                .build();

        matchUserRepository.save(matchUser);

        // 신규 유저의 경우 초기 레이팅 생성
        rankService.initializeUserRating(user.getId());


        // 10. 응답 생성 (기존 코드 수정)
        return ListenMatchJoinResponse.builder()
                .success(true)
                .matchId(match.getId())
                .hostIp(match.getHostIp())
                .hostPort(match.getHostPort())
                .role(role.name())  // 동적으로 결정된 역할 반환
                .status(match.getStatus().name())
                .message("매치에 성공적으로 참가했습니다.")
                .build();
    }

    @Override
    @Transactional
    public ListenMatchLeaveResponse leaveMatch(Long matchId, String username) {
        // 1. 매치 존재 여부 확인
        Match match = matchRepository.findById(matchId)
                .orElseThrow(() -> new IllegalArgumentException("해당 매치를 찾을 수 없습니다."));

        // 2. Listen Server 매치인지 확인
        if (!match.isListenServer()) {
            throw new IllegalArgumentException("해당 매치는 Listen Server 매치가 아닙니다.");
        }

        // 3. 유저 조회
        User user = userRepository.findByUsername(username)
                .orElseThrow(() -> new IllegalArgumentException("사용자를 찾을 수 없습니다."));

        // 4. 유저가 해당 매치에 참여하는지 확인
        MatchUser matchUser = matchUserRepository.findByMatchAndUser(match, user)
                .orElseThrow(() -> new IllegalArgumentException("해당 매치에 참여하지 않은 사용자입니다."));

        // 5. 게임이 시작됐는지 확인
        if (match.getStartTime() != null) {
            throw new IllegalStateException("이미 시작된 매치는 나갈 수 없습니다.");
        }

        // 6. 역할 확인: HOST인 경우 hostLeaveMatch 사용 안내
        if (matchUser.getRole() == MatchUser.UserRole.HOST) {
            throw new IllegalArgumentException("방장은 이 API를 사용할 수 없습니다. 방장 나가기 API를 사용하세요.");
        }

        // 7. GUEST 유저 제거
        matchUserRepository.delete(matchUser);

        // 8. 응답 반환
        return ListenMatchLeaveResponse.builder()
                .success(true)
                .matchId(matchId)
                .message("매치에서 성공적으로 나갔습니다.")
                .build();
    }

    @Override
    @Transactional
    public ListenMatchHostLeaveResponse hostLeaveMatch(Long matchId, String username) {
        // 1. 매치 존재 여부 확인
        Match match = matchRepository.findById(matchId)
                .orElseThrow(() -> new IllegalArgumentException("해당 매치를 찾을 수 없습니다."));

        // 2. Listen Server 매치인지 확인
        if (!match.isListenServer()) {
            throw new IllegalArgumentException("해당 매치는 Listen Server 매치가 아닙니다.");
        }

        // 3. 유저 조회
        User user = userRepository.findByUsername(username)
                .orElseThrow(() -> new IllegalArgumentException("사용자를 찾을 수 없습니다."));

        // 4. 유저가 해당 매치에 참여하는지 확인
        MatchUser hostMatchUser = matchUserRepository.findByMatchAndUser(match, user)
                .orElseThrow(() -> new IllegalArgumentException("해당 매치에 참여하지 않은 사용자입니다."));

        // 5. 유저가 HOST인지 확인
        if (hostMatchUser.getRole() != MatchUser.UserRole.HOST) {
            throw new IllegalArgumentException("방장만 이 기능을 사용할 수 있습니다.");
        }

        // 6. 게임이 시작됐는지 확인
        if (match.getStartTime() != null) {
            throw new IllegalStateException("이미 시작된 매치는 나갈 수 없습니다.");
        }

        // 7. GUEST 역할의 참가자가 있는지 확인
        List<MatchUser> allUsers = matchUserRepository.findByMatch(match);
        List<MatchUser> guests = allUsers.stream()
                .filter(mu -> mu.getRole() == MatchUser.UserRole.GUEST)
                .collect(Collectors.toList());

        if (guests.isEmpty()) {
            // 다른 참가자가 없으면 매치 삭제
            // 먼저 모든 MatchUser 레코드 삭제
            matchUserRepository.deleteAll(allUsers);

            // 매치 삭제
            matchRepository.delete(match);

            return ListenMatchHostLeaveResponse.builder()
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

            return ListenMatchHostLeaveResponse.builder()
                    .success(true)
                    .matchId(matchId)
                    .result("TRANSFERRED")
                    .newHostId(newHost.getUser().getId())
                    .newHostNickname(newHost.getUser().getNickname())
                    .message("HOST 권한이 GUEST에게 자동 이전되었습니다.")
                    .build();
        }
    }

    @Override
    @Transactional
    public ListenMatchResultResponse saveMatchResult(Long matchId, String username, ListenMatchResultRequest request) {
        // 1. 매치 존재 여부 확인
        Match match = matchRepository.findById(matchId)
                .orElseThrow(() -> new IllegalArgumentException("해당 매치 또는 유저 정보를 찾을 수 없습니다."));

        // 2. Listen Server 매치인지 확인
        if (!match.isListenServer()) {
            throw new IllegalArgumentException("해당 매치는 Listen Server 매치가 아닙니다.");
        }

        // 3. 요청자가 매치 참여자인지 확인 (보안 검증)
        User requester = userRepository.findByUsername(username)
                .orElseThrow(() -> new IllegalArgumentException("사용자를 찾을 수 없습니다."));

        boolean isParticipant = matchUserRepository.existsByMatchAndUser(match, requester);
        if (!isParticipant) {
            throw new AccessDeniedException("해당 매치에 참여한 사용자만 결과를 저장할 수 있습니다.");
        }

        // 4. 이미 결과가 저장된 매치인지 확인 (end_time이 있으면 이미 종료된 매치)
        if (match.getEndTime() != null) {
            throw new IllegalStateException("해당 매치의 결과가 이미 저장되어 있습니다.");
        }

        // 5. 요청 데이터 유효성 검증
        if (request.getResults() == null || request.getResults().size() != 2) {
            throw new IllegalArgumentException("입력값이 유효하지 않습니다. 두 플레이어의 결과가 필요합니다.");
        }

        // 6. WIN, LOSE 각각 1개씩 존재하는지 확인
        long winCount = request.getResults().stream()
                .filter(result -> "WIN".equals(result.getResult()))
                .count();

        long loseCount = request.getResults().stream()
                .filter(result -> "LOSE".equals(result.getResult()))
                .count();

        if (winCount != 1 || loseCount != 1) {
            throw new IllegalArgumentException("입력값이 유효하지 않습니다. 승리와 패배는 각각 1명씩이어야 합니다.");
        }

        // 7. 각 역할별 유저를 찾아 결과 업데이트
        List<MatchUser> matchUsers = matchUserRepository.findByMatch(match);

        for (ListenMatchResultRequest.PlayerResult playerResult : request.getResults()) {
            // 역할로 매치 유저 찾기
            MatchUser.UserRole role = MatchUser.UserRole.valueOf(playerResult.getRole());
            MatchUser matchUser = matchUserRepository.findByMatchAndRole(match, role)
                    .orElseThrow(() -> new IllegalArgumentException("해당 역할의 유저를 찾을 수 없습니다."));

            // 결과 업데이트 (String을 Enum으로 변환)
            MatchUser.GameResult gameResult = MatchUser.GameResult.valueOf(playerResult.getResult());
            matchUser.setResult(gameResult);
            matchUserRepository.save(matchUser);
        }

        // 8. 매치 종료 시간 업데이트
        match.setEndTime(java.time.LocalDateTime.now());
        matchRepository.save(match);

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
        return ListenMatchResultResponse.builder()
                .success(true)
                .matchId(match.getId())
                .message("게임 결과가 성공적으로 저장되었습니다.")
                .ratingChanges(ratingChanges) // 랭킹 변경 정보 추가
                .build();
    }

}
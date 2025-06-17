// src/utils/mockData.js
/**
 * 랭킹 시스템 개발 중 사용할 모의 데이터
 */

// 유저 랭크 정보 모의 데이터
export const generateMockPlayerRank = (userId) => {
  // 티어별 범위에 따른 점수 생성
  const tiers = [
    { tier: 1, min: 401, max: 600 },
    { tier: 2, min: 301, max: 400 },
    { tier: 3, min: 201, max: 300 },
    { tier: 4, min: 0, max: 200 }
  ];
  
  // 무작위 티어 선택 (1~4, 값이 클수록 낮은 티어)
  const tierIndex = Math.floor(Math.random() * 4);
  const selectedTier = tiers[tierIndex];
  
  // 선택된 티어 범위 내의 무작위 점수
  const points = Math.floor(Math.random() * (selectedTier.max - selectedTier.min + 1)) + selectedTier.min;
  
  // 승패 및 연승/연패 생성
  const wins = Math.floor(Math.random() * 50) + 1;
  const losses = Math.floor(Math.random() * 50) + 1;
  
  // 연승 또는 연패 중 하나만 가능
  const winStreak = Math.random() > 0.5 ? Math.floor(Math.random() * 5) + 1 : 0;
  const loseStreak = winStreak > 0 ? 0 : Math.floor(Math.random() * 5) + 1;
  
  return {
    userId: parseInt(userId),
    username: `player${userId}`,
    nickname: `Player ${userId}`,
    points: points,
    tier: selectedTier.tier,
    wins: wins,
    losses: losses,
    winRate: ((wins / (wins + losses)) * 100).toFixed(1),
    globalRank: Math.floor(Math.random() * 100) + 1,
    tierRank: Math.floor(Math.random() * 50) + 1,
    isPlacement: false,
    winStreak: winStreak,
    loseStreak: loseStreak
  };
};

// 매치 히스토리 모의 데이터
export const generateMockMatchHistory = (userId) => {
  const matches = [];
  const totalMatches = 10 + Math.floor(Math.random() * 5); // 10~14개 경기 생성
  
  let currentPoints = 300; // 시작 포인트
  let currentTier = 2; // 시작 티어
  
  for (let i = 0; i < totalMatches; i++) {
    const isWin = Math.random() > 0.4; // 60% 확률로 승리
    const pointChange = isWin ? 25 : -20;
    
    currentPoints += pointChange;
    
    // 티어 계산
    let newTier = currentTier;
    if (currentPoints >= 401) newTier = 1;
    else if (currentPoints >= 301) newTier = 2;
    else if (currentPoints >= 201) newTier = 3;
    else newTier = 4;
    
    // 최근 경기가 가장 앞에 오도록 날짜 조정
    const date = new Date();
    date.setDate(date.getDate() - (totalMatches - i));
    
    matches.push({
      matchId: 1000 + i,
      timestamp: date.toISOString(),
      result: isWin ? 'WIN' : 'LOSE',
      pointsBefore: currentPoints - pointChange,
      pointsAfter: currentPoints,
      pointsChange: pointChange,
      tierBefore: currentTier,
      tierAfter: newTier,
      opponentId: 100 + Math.floor(Math.random() * 100),
      opponentNickname: `Opponent ${Math.floor(Math.random() * 100)}`
    });
    
    currentTier = newTier;
  }
  
  return {
    userId: parseInt(userId),
    totalMatches: totalMatches,
    page: 0,
    size: totalMatches,
    hasNext: false,
    matches: matches
  };
};

// 일별 통계 모의 데이터
export const generateMockDailyStats = (userId, startDate, endDate) => {
  const stats = [];
  
  // 날짜 문자열을 Date 객체로 변환
  const start = new Date(startDate.substring(0, 4) + '-' + startDate.substring(4, 6) + '-' + startDate.substring(6, 8));
  const end = new Date(endDate.substring(0, 4) + '-' + endDate.substring(4, 6) + '-' + endDate.substring(6, 8));
  
  // 두 날짜 사이의 일수 계산
  const diffTime = Math.abs(end - start);
  const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24)) + 1;
  
  // 각 일자별 통계 생성
  for (let i = 0; i < diffDays; i++) {
    const currentDate = new Date(start);
    currentDate.setDate(start.getDate() + i);
    
    // 50% 확률로 해당 날짜에 활동이 있음
    if (Math.random() > 0.5) {
      const matchCount = Math.floor(Math.random() * 5) + 1; // 1~5경기
      const winCount = Math.floor(Math.random() * (matchCount + 1)); // 0~matchCount 승리
      const lossCount = matchCount - winCount;
      
      stats.push({
        date: currentDate.toISOString().split('T')[0].replace(/-/g, ''),
        highestPoint: 300 + Math.floor(Math.random() * 200),
        matchCount: matchCount,
        winCount: winCount,
        loseCount: lossCount
      });
    }
  }
  
  return {
    userId: parseInt(userId),
    stats: stats
  };
};

// 티어 분포 모의 데이터
export const generateMockTierDistribution = () => {
  return {
    success: true,
    tiers: [
      { tier: 1, count: 15 + Math.floor(Math.random() * 10) },
      { tier: 2, count: 45 + Math.floor(Math.random() * 20) },
      { tier: 3, count: 120 + Math.floor(Math.random() * 30) },
      { tier: 4, count: 70 + Math.floor(Math.random() * 25) }
    ]
  };
};

// 리더보드 모의 데이터
export const generateMockLeaderboard = (tier = null, page = 0, size = 20) => {
  const players = [];
  const totalPlayers = 200 + Math.floor(Math.random() * 100);
  
  // 시작 순위 계산
  const startRank = page * size + 1;
  
  // 페이지에 표시할 플레이어 수 계산
  const pageSize = Math.min(size, totalPlayers - (page * size));
  
  for (let i = 0; i < pageSize; i++) {
    const rank = startRank + i;
    const playerTier = tier || (rank <= 15 ? 1 : (rank <= 60 ? 2 : (rank <= 150 ? 3 : 4)));
    
    // 티어별 점수 범위 설정
    let pointsMin, pointsMax;
    switch (playerTier) {
      case 1: pointsMin = 401; pointsMax = 600; break;
      case 2: pointsMin = 301; pointsMax = 400; break;
      case 3: pointsMin = 201; pointsMax = 300; break;
      default: pointsMin = 0; pointsMax = 200;
    }
    
    // 순위에 따른 점수 조정 (상위 순위일수록 높은 점수)
    const rankFactor = Math.max(0, 1 - (rank / totalPlayers));
    const points = Math.floor(pointsMin + rankFactor * (pointsMax - pointsMin));
    
    // 승패 생성
    const wins = Math.floor(Math.random() * 50) + 10;
    const losses = Math.floor(Math.random() * 30) + 5;
    
    players.push({
      rank: rank,
      userId: 100 + i,
      nickname: `Player${100 + i}`,
      points: points,
      tier: playerTier,
      wins: wins,
      losses: losses
    });
  }
  
  return {
    success: true,
    totalPlayers: totalPlayers,
    page: page,
    size: size,
    hasNext: (page + 1) * size < totalPlayers,
    players: players
  };
};
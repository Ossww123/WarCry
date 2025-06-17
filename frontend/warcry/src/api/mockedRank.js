// src/api/mockedRank.js
import { 
  generateMockPlayerRank, 
  generateMockMatchHistory, 
  generateMockDailyStats,
  generateMockTierDistribution,
  generateMockLeaderboard
} from '../utils/mockData';

// 지연 시간을 두어 실제 API 호출 흉내내기
const delay = (ms) => new Promise(resolve => setTimeout(resolve, ms));

/**
 * 유저 랭크 정보 조회 (모의 구현)
 */
export const getPlayerRank = async (userId) => {
  await delay(700); // 700ms 지연
  return generateMockPlayerRank(userId);
};

/**
 * 리더보드 조회 (모의 구현)
 */
export const getLeaderboard = async (tier = null, page = 0, size = 20) => {
  await delay(800); // 800ms 지연
  return generateMockLeaderboard(tier, page, size);
};

/**
 * 유저 매치 히스토리 조회 (모의 구현)
 */
export const getMatchHistory = async (userId, page = 0, size = 10) => {
  await delay(600); // 600ms 지연
  return generateMockMatchHistory(userId);
};

/**
 * 일일 통계 조회 (모의 구현)
 */
export const getUserDailyStats = async (userId, startDate, endDate) => {
  await delay(500); // 500ms 지연
  return generateMockDailyStats(userId, startDate, endDate);
};

/**
 * 티어 분포 통계 조회 (모의 구현)
 */
export const getTierDistribution = async () => {
  await delay(400); // 400ms 지연
  return generateMockTierDistribution();
};
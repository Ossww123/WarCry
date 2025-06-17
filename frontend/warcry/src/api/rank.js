// src/api/rank.js
import axios from 'axios';

// 인증 헤더 가져오기 함수
// src/api/rank.js의 getAuthHeaders 함수 확인 및 필요시 수정

const getAuthHeaders = () => {
  const token = localStorage.getItem('token');
  // 토큰 형식이 'Bearer {token}' 형식이 아닌 경우 추가
  if (token && !token.startsWith('Bearer ')) {
    return {
      headers: {
        Authorization: `Bearer ${token}`
      }
    };
  }
  // 이미 'Bearer {token}' 형식이면 그대로 사용
  return {
    headers: {
      Authorization: token || ''
    }
  };
};

/**
 * 유저 랭크 정보 조회
 * @param {number} userId - 조회할 유저 ID
 * @returns {Promise<Object>} - 유저 랭크 정보
 */
export const getPlayerRank = async (userId) => {
  try {
    const response = await axios.get(`/api/rank/player/${userId}`, getAuthHeaders());
    return response.data;
  } catch (error) {
    console.error('Failed to fetch player rank:', error);
    throw error;
  }
};

/**
 * 리더보드 조회
 * @param {number|null} tier - 필터링할 티어 (없으면 전체)
 * @param {number} page - 페이지 번호 (0부터 시작)
 * @param {number} size - 한 페이지당 아이템 수
 * @returns {Promise<Object>} - 리더보드 데이터
 */
export const getLeaderboard = async (tier = null, page = 0, size = 20) => {
  try {
    const params = new URLSearchParams();
    if (tier !== null) params.append('tier', tier);
    params.append('page', page);
    params.append('size', size);
    
    const response = await axios.get(`/api/rank/leaderboard?${params}`, getAuthHeaders());
    return response.data;
  } catch (error) {
    console.error('Failed to fetch leaderboard:', error);
    throw error;
  }
};

/**
 * 유저 매치 히스토리 조회
 * @param {number} userId - 조회할 유저 ID
 * @param {number} page - 페이지 번호
 * @param {number} size - 한 페이지당 아이템 수
 * @returns {Promise<Object>} - 매치 히스토리 데이터
 */
export const getMatchHistory = async (userId, page = 0, size = 10) => {
  try {
    const params = new URLSearchParams();
    params.append('page', page);
    params.append('size', size);
    
    const response = await axios.get(`/api/rank/history/${userId}?${params}`, getAuthHeaders());
    return response.data;
  } catch (error) {
    console.error('Failed to fetch match history:', error);
    throw error;
  }
};

/**
 * 일일 통계 조회
 * @param {number} userId - 조회할 유저 ID
 * @param {string} startDate - 시작 날짜 (YYYYMMDD)
 * @param {string} endDate - 종료 날짜 (YYYYMMDD)
 * @returns {Promise<Object>} - 일일 통계 데이터
 */
export const getUserDailyStats = async (userId, startDate, endDate) => {
  try {
    const params = new URLSearchParams();
    params.append('startDate', startDate);
    params.append('endDate', endDate);
    
    const response = await axios.get(`/api/rank/daily/${userId}?${params}`, getAuthHeaders());
    return response.data;
  } catch (error) {
    console.error('Failed to fetch daily stats:', error);
    throw error;
  }
};

/**
 * 티어 분포 통계 조회
 * @returns {Promise<Object>} - 티어별 유저 분포 데이터
 */
export const getTierDistribution = async () => {
  try {
    const response = await axios.get('/api/rank/stats/tier-distribution', getAuthHeaders());
    return response.data;
  } catch (error) {
    console.error('Failed to fetch tier distribution:', error);
    throw error;
  }
};
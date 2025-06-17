// src/pages/ProfileDetailPage.js
import React, { useState, useEffect } from 'react';
import { useParams } from 'react-router-dom';
import { format, subDays } from 'date-fns';
import { getPlayerRank, getMatchHistory, getUserDailyStats } from '../api/rank';
import ProfileHeader from '../components/profile/ProfileHeader';
import StatsSummary from '../components/profile/StatsSummary';
import MatchHistory from '../components/profile/MatchHistory';
import PointsGraph from '../components/profile/PointsGraph';
import PageTransition from '../components/common/PageTransition';
import LoadingSpinner from '../components/common/LoadingSpinner';
import './ProfileDetailPage.css';

const ProfileDetailPage = () => {
  const { userId } = useParams();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [userRank, setUserRank] = useState(null);
  const [matchHistory, setMatchHistory] = useState(null);
  const [dailyStats, setDailyStats] = useState(null);
  
  // 데이터 로딩 함수
  useEffect(() => {
  const fetchData = async () => {
    setLoading(true);
    try {
      // 유저 랭크 정보 가져오기
      const rankData = await getPlayerRank(userId);
      console.log('유저 랭크 정보:', rankData); // 응답 구조 확인
      setUserRank(rankData);
      
      // 최근 매치 히스토리 가져오기
      const historyData = await getMatchHistory(userId, 0, 10);
      console.log('매치 히스토리 데이터:', historyData); // 응답 구조 확인
      setMatchHistory(historyData);
      
      // 일별 통계 가져오기 (최근 30일)
      const today = new Date();
      const thirtyDaysAgo = subDays(today, 30);
      
      const statsData = await getUserDailyStats(
        userId,
        format(thirtyDaysAgo, 'yyyyMMdd'),
        format(today, 'yyyyMMdd')
      );
      console.log('일별 통계 데이터:', statsData); // 응답 구조 확인
      setDailyStats(statsData);
    } catch (err) {
      console.error('API 오류:', err); // 오류 상세 정보 확인
      setError('프로필 데이터를 불러오는 중 오류가 발생했습니다.');
    } finally {
      setLoading(false);
    }
  };

  if (userId) {
    fetchData();
  }
}, [userId]);

  // 로딩 중 표시
  if (loading) {
    return (
      <PageTransition> {/* ✅ 애니메이션 감싸기 */}
        <div className="profile-detail-page loading">
          <ProfileHeader userRank={null} />
          <StatsSummary userRank={null} />
          <PointsGraph matchHistory={null} />
          <MatchHistory matchHistory={null} />
        </div>
      </PageTransition>
    );
  }

  // 로딩 중 표시 부분 수정
  if (loading) {
    return (
      <PageTransition>
        <div className="profile-detail-page">
          <LoadingSpinner size="large" message="프로필 데이터를 불러오는 중입니다..." />
        </div>
      </PageTransition>
    );
  }

  // 에러 표시
  if (error) {
    return <div className="error">{error}</div>;
  }

  return (
    <PageTransition> {/* ✅ 애니메이션 감싸기 */}
      <div className="profile-detail-page">
        {/* 프로필 헤더 */}
        <ProfileHeader userRank={userRank} />
        
        {/* 통계 요약 */}
        <StatsSummary userRank={userRank} />
        
        {/* 포인트 변화 그래프 */}
        <PointsGraph matchHistory={matchHistory} />
        
        {/* 최근 전적 */}
        <MatchHistory matchHistory={matchHistory} />
      </div>
    </PageTransition>
  );
};

export default ProfileDetailPage;

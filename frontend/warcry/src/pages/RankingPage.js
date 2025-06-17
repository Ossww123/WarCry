import React, { useState, useEffect } from 'react';
import { getTierDistribution, getLeaderboard } from '../api/rank';
import TierPyramid from '../components/ranks/TierPyramid';
import UserDistribution from '../components/ranks/UserDistribution';
import LeaderboardTable from '../components/ranks/LeaderboardTable';
import TierFilter from '../components/ranks/TierFilter';
import SearchBar from '../components/common/SearchBar';
import PageTransition from '../components/common/PageTransition';
import LoadingSpinner from '../components/common/LoadingSpinner';
import './RankingPage.css';

const RankingPage = () => {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [tierDistribution, setTierDistribution] = useState(null);
  const [leaderboard, setLeaderboard] = useState(null);
  const [selectedTier, setSelectedTier] = useState(null);
  const [currentPage, setCurrentPage] = useState(0);
  const [pageSize] = useState(10); // 한 페이지당 10명씩 표시

  // 데이터 로딩 함수
  useEffect(() => {
    const fetchData = async () => {
      setLoading(true);
      try {
        // 티어 분포 데이터 가져오기
        const tierData = await getTierDistribution();
        console.log('티어 분포 데이터:', tierData); // 응답 구조 확인
        setTierDistribution(tierData);
        
        // 리더보드 데이터 가져오기 (선택한 티어 기준)
        const leaderboardData = await getLeaderboard(selectedTier, currentPage, pageSize);
        console.log('리더보드 데이터:', leaderboardData); // 응답 구조 확인
        setLeaderboard(leaderboardData);
      } catch (err) {
        console.error('API 오류:', err); // 오류 상세 정보 확인
        setError('데이터를 불러오는 중 오류가 발생했습니다.');
      } finally {
        setLoading(false);
      }
    };

    fetchData();
  }, [selectedTier, currentPage, pageSize]);
  // 티어 선택 핸들러
  const handleTierSelect = (tier) => {
    setSelectedTier(tier);
    setCurrentPage(0); // 티어 변경 시 첫 페이지로 리셋
  };

  // 페이지 변경 핸들러
  const handlePageChange = (newPage) => {
    setCurrentPage(newPage);
  };

 // 로딩 중 표시 부분 수정
if (loading) {
  return (
    <PageTransition>
      <div className="ranking-page">
        <h1>랭킹 시스템</h1>
        <LoadingSpinner size="large" message="랭킹 데이터를 불러오는 중입니다..." />
      </div>
    </PageTransition>
  );
}

  // 에러 표시
  if (error) {
    return <div className="error">{error}</div>;
  }

  return (
    <PageTransition>
      <div className="ranking-page">
        <h1>랭킹 시스템</h1>
        
        {/* 티어 피라미드 */}
        <TierPyramid />
        
        {/* 유저 분포 차트 */}
        <UserDistribution distribution={tierDistribution} />

        {/* 검색 바 */}
        <SearchBar />
        
        {/* 티어 필터 */}
        <TierFilter selectedTier={selectedTier} onTierSelect={handleTierSelect} />
        
        {/* 리더보드 테이블 */}
        <LeaderboardTable 
          leaderboard={leaderboard} 
          selectedTier={selectedTier} 
          currentPage={currentPage}
          onPageChange={handlePageChange}
        />
      </div>
    </PageTransition>
  );
};

export default RankingPage;

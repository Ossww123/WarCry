// src/pages/TierDetailPage.js
import React, { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import { getLeaderboard } from '../api/rank';
import Pagination from '../components/common/Pagination';
import PageTransition from '../components/common/PageTransition'; // ✅ 페이지 전환 애니메이션 컴포넌트 추가
import './TierDetailPage.css';

const TierDetailPage = () => {
  const { tierId } = useParams();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [leaderboard, setLeaderboard] = useState(null);
  const [page, setPage] = useState(0);
  const [pageSize] = useState(20);
  
  // 티어별 색상과 이름 정의
  const tierInfo = {
    1: { name: '1티어', color: '#FFD700', range: '401점 이상' },
    2: { name: '2티어', color: '#E5E4E2', range: '301-400점' },
    3: { name: '3티어', color: '#CD7F32', range: '201-300점' },
    4: { name: '4티어', color: '#43464B', range: '0-200점' }
  };
  
  const currentTier = tierInfo[tierId] || { name: '알 수 없음', color: '#CCCCCC', range: '' };

  // 데이터 로딩 함수
  useEffect(() => {
    const fetchData = async () => {
      setLoading(true);
      try {
        const data = await getLeaderboard(parseInt(tierId), page, pageSize);
        setLeaderboard(data);        
      } catch (err) {
        setError('티어 데이터를 불러오는 중 오류가 발생했습니다.');
        console.error(err);
      } finally {
        setLoading(false);
      }
    };

    if (tierId) {
      fetchData();
    }
  }, [tierId, page, pageSize]);

  // 페이지 변경 핸들러
  const handlePageChange = (newPage) => {
    setPage(newPage);
  };

  // 로딩 중 표시
  if (loading) {
    return <div className="loading">로딩 중...</div>;
  }

  // 에러 표시
  if (error) {
    return <div className="error">{error}</div>;
  }

  // 티어ID가 유효하지 않을 경우
  if (!tierInfo[tierId]) {
    return (
      <div className="error">
        <p>유효하지 않은 티어입니다.</p>
        <Link to="/ranking">랭킹 페이지로 돌아가기</Link>
      </div>
    );
  }

  // 총 페이지 수 계산
  const totalPages = leaderboard ? Math.ceil(leaderboard.totalPlayers / pageSize) : 0;

  return (
    <PageTransition> {/* ✅ 페이지 애니메이션 래퍼 시작 */}
      <div className="tier-detail-page" style={{ '--tier-color': currentTier.color }}>
        <div className="tier-header">
          <Link to="/ranking" className="back-link">← 랭킹으로 돌아가기</Link>
          <h1>{currentTier.name} 랭킹</h1>
          <div className="tier-info">
            <span className="tier-range">포인트 범위: {currentTier.range}</span>
            <span className="tier-users">총 플레이어: {leaderboard?.totalPlayers || 0}명</span>
          </div>
        </div>
        
        {/* 리더보드 테이블 */}
        <div className="table-container">
          {leaderboard && leaderboard.players.length > 0 ? (
            <table className="tier-leaderboard">
              <thead>
                <tr>
                  <th>순위</th>
                  <th>닉네임</th>
                  <th>포인트</th>
                  <th>승/패</th>
                  <th>승률</th>
                </tr>
              </thead>
              <tbody>
                {leaderboard.players.map((player) => (
                  <tr key={player.userId}>
                    <td>
                      <div className={`rank-badge ${player.rank <= 3 ? `top-${player.rank}` : ''}`}>
                        {player.rank}
                      </div>
                    </td>
                    <td>
                      <Link 
                        to={`/profile/${player.userId}`} 
                        className="player-name"
                      >
                        {player.nickname}
                      </Link>
                    </td>
                    <td>{player.points}</td>
                    <td>{player.wins} / {player.losses}</td>
                    <td>{((player.wins / (player.wins + player.losses)) * 100).toFixed(1)}%</td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : (
            <p className="no-data">이 티어에 속한 플레이어가 없습니다.</p>
          )}
        </div>
        
        {/* 페이지네이션 */}
        {totalPages > 1 && (
          <Pagination 
            currentPage={page} 
            totalPages={totalPages} 
            onPageChange={handlePageChange} 
          />
        )}
      </div>
    </PageTransition> // ✅ 페이지 애니메이션 래퍼 끝
  );
};

export default TierDetailPage;

// src/components/ranks/LeaderboardTable.js
import React from 'react';
import { Link } from 'react-router-dom';
import Pagination from '../common/Pagination';
import './LeaderboardTable.css';

const LeaderboardTable = ({ leaderboard, selectedTier, currentPage, onPageChange }) => {
  // 데이터가 없을 경우
  if (!leaderboard || !leaderboard.players || leaderboard.players.length === 0) {
    return (
      <div className="leaderboard-table-wrapper">
        <h2>
          {selectedTier ? `${selectedTier}티어 ` : ''}
          리더보드
        </h2>
        <p className="no-data">표시할 데이터가 없습니다.</p>
      </div>
    );
  }
  
  // 티어별 색상 정의
  const tierColors = {
    1: '#FFD700', // 1티어 (골드)
    2: '#E5E4E2', // 2티어 (실버)
    3: '#CD7F32', // 3티어 (브론즈)
    4: '#43464B'  // 4티어 (아이언)
  };

  // 총 페이지 수 계산
  const totalPages = Math.ceil(leaderboard.totalPlayers / leaderboard.size);

  return (
    <div className="leaderboard-table-wrapper">
      <h2>
        {selectedTier ? `${selectedTier}티어 ` : ''}
        리더보드
      </h2>
      
      <div className="table-info">
        <span>전체 {leaderboard.totalPlayers}명</span>
      </div>
      
      <div className="table-container">
        <table className="leaderboard-table">
          <thead>
            <tr>
              <th>순위</th>
              <th>닉네임</th>
              <th>티어</th>
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
                <td>
                  <div 
                    className="tier-badge" 
                    style={{ '--tier-color': tierColors[player.tier] }}
                  >
                    {player.tier}티어
                  </div>
                </td>
                <td>{player.points}</td>
                <td>{player.wins} / {player.losses}</td>
                <td>{((player.wins / (player.wins + player.losses)) * 100).toFixed(1)}%</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      
      {/* 페이지네이션 컴포넌트 추가 */}
      <Pagination 
        currentPage={currentPage} 
        totalPages={totalPages} 
        onPageChange={onPageChange} 
      />
    </div>
  );
};

export default LeaderboardTable;
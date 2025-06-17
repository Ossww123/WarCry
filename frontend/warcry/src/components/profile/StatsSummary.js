// src/components/profile/StatsSummary.js
import React from 'react';
import './StatsSummary.css';

const StatsSummary = ({ userRank }) => {
  if (!userRank) {
    return <div className="stats-summary skeleton"></div>;
  }

  const totalGames = userRank.wins + userRank.losses;
  const winRate = totalGames > 0 ? (userRank.wins / totalGames) * 100 : 0;

  // 통계 데이터 배열
  const statItems = [
    {
      label: '승/패',
      value: `${userRank.wins} / ${userRank.losses}`,
      highlight: false,
    },
    {
      label: '승률',
      value: `${winRate.toFixed(1)}%`,
      highlight: winRate >= 60, // 60% 이상 승률은 하이라이트
    },
    {
      label: '총 경기',
      value: totalGames,
      highlight: false,
    },
    {
      label: '연승/연패',
      value: userRank.winStreak > 0 ? `${userRank.winStreak}연승` : `${userRank.loseStreak}연패`,
      highlight: userRank.winStreak >= 3 || userRank.loseStreak >= 3, // 3연승 이상은 하이라이트
      type: userRank.winStreak > 0 ? 'win' : 'lose',
    },
  ];

  return (
    <div className="stats-summary">
      <h2>전적 요약</h2>
      <div className="stats-grid">
        {statItems.map((item, index) => (
          <div 
            key={index} 
            className={`stat-card ${item.highlight ? 'highlight' : ''} ${item.type || ''}`}
          >
            <div className="stat-label">{item.label}</div>
            <div className="stat-value">{item.value}</div>
          </div>
        ))}
      </div>
    </div>
  );
};

export default StatsSummary;
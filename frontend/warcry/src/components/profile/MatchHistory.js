// src/components/profile/MatchHistory.js
import React from 'react';
import { Link } from 'react-router-dom';
import { format } from 'date-fns';
import './MatchHistory.css';

const MatchHistory = ({ matchHistory }) => {
  if (!matchHistory) {
    return <div className="match-history skeleton"></div>;
  }

  if (!matchHistory.matches || matchHistory.matches.length === 0) {
    return (
      <div className="match-history">
        <h2>최근 전적</h2>
        <p className="no-matches">아직 경기 기록이 없습니다.</p>
      </div>
    );
  }

  return (
    <div className="match-history">
      <h2>최근 전적</h2>
      <div className="match-list">
        {matchHistory.matches.map((match) => (
          <div 
            key={match.matchId} 
            className={`match-item ${match.result === 'WIN' ? 'win' : 'lose'}`}
          >
            <div className="match-date">
              {format(new Date(match.timestamp), 'yyyy.MM.dd HH:mm')}
            </div>
            
            <div className="match-result">
              <span className="result-badge">{match.result === 'WIN' ? '승리' : '패배'}</span>
            </div>
            
            <div className="match-opponent">
              <span className="opponent-label">상대</span>
              <Link to={`/profile/${match.opponentId}`} className="opponent-name">
                {match.opponentNickname}
              </Link>
            </div>
            
            <div className="match-points">
              <div className="points-change">
                <span className={`change-value ${match.pointsChange >= 0 ? 'positive' : 'negative'}`}>
                  {match.pointsChange >= 0 ? '+' : ''}{match.pointsChange}
                </span>
              </div>
              <div className="points-after">
                {match.pointsAfter}점
              </div>
            </div>
            
            {match.tierAfter !== match.tierBefore && (
              <div className="tier-change">
                {match.tierAfter < match.tierBefore ? (
                  <span className="promotion">승급!</span>
                ) : (
                  <span className="demotion">강등</span>
                )}
              </div>
            )}
          </div>
        ))}
      </div>
      
      {matchHistory.hasNext && (
        <div className="view-more">
          <button className="view-more-button">더 보기</button>
        </div>
      )}
    </div>
  );
};

export default MatchHistory;
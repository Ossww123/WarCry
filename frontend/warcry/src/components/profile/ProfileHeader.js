// src/components/profile/ProfileHeader.js
import React from 'react';
import { Link } from 'react-router-dom';
import './ProfileHeader.css';
import { motion } from 'framer-motion'; // ✅ 애니메이션 추가

const ProfileHeader = ({ userRank }) => {
  if (!userRank) {
    return <div className="profile-header skeleton"></div>;
  }

  const tierColors = {
    1: '#FFD700',
    2: '#E5E4E2',
    3: '#CD7F32',
    4: '#43464B'
  };

  const tierRanges = {
    1: { min: 401, max: Infinity },
    2: { min: 301, max: 400 },
    3: { min: 201, max: 300 },
    4: { min: 0, max: 200 }
  };

  const currentTier = tierRanges[userRank.tier];

  const calculateProgress = () => {
    if (userRank.tier === 1) {
      return Math.min(((userRank.points - currentTier.min) / 200) * 100, 100);
    }
    const tierRange = currentTier.max - currentTier.min;
    const userProgress = userRank.points - currentTier.min;
    return (userProgress / tierRange) * 100;
  };

  const pointsToNextTier = userRank.tier > 1
    ? currentTier.max + 1 - userRank.points
    : null;

  return (
    <motion.div
      className="profile-header"
      style={{ '--tier-color': tierColors[userRank.tier] }}
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.5 }}
    >
      <Link to="/ranking" className="back-link">← 랭킹으로 돌아가기</Link>
      
      <h1>{userRank.nickname}의 프로필</h1>
      
      <div className="tier-badge">
        <span className="tier-number">{userRank.tier}티어</span>
        <span className="tier-points">{userRank.points}점</span>
      </div>
      
      <div className="tier-progress-container">
        <div className="tier-progress-bar">
          <div 
            className="tier-progress-fill" 
            style={{ width: `${calculateProgress()}%` }}
          ></div>
        </div>
        {userRank.tier > 1 && (
          <div className="tier-progress-text">
            다음 티어까지 {pointsToNextTier}점 남음
          </div>
        )}
      </div>
      
      <div className="rank-info">
        <div className="rank-item">
          <span className="rank-label">전체 랭킹</span>
          <span className="rank-value">{userRank.globalRank}위</span>
        </div>
        <div className="rank-item">
          <span className="rank-label">티어 랭킹</span>
          <span className="rank-value">{userRank.tierRank}위</span>
        </div>
      </div>
    </motion.div>
  );
};

export default ProfileHeader;

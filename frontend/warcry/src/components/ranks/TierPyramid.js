// src/components/ranks/TierPyramid.js
import React from 'react';
import { useNavigate } from 'react-router-dom';
import './TierPyramid.css';

const TierPyramid = () => {
  const navigate = useNavigate();

  // 티어 정보 배열
  const tiers = [
    { id: 1, name: '1티어', color: '#FFD700', range: '401점 이상' },
    { id: 2, name: '2티어', color: '#E5E4E2', range: '301-400점' },
    { id: 3, name: '3티어', color: '#CD7F32', range: '201-300점' },
    { id: 4, name: '4티어', color: '#43464B', range: '0-200점' }
  ];

  // 티어 클릭시 해당 티어 페이지로 이동
  const handleTierClick = (tierId) => {
    navigate(`/ranking/tier/${tierId}`);
  };

  return (
    <div className="tier-pyramid">
      <h2>티어 구조</h2>
      <div className="pyramid-container">
        {tiers.map((tier) => (
          <div
            key={tier.id}
            className={`tier-block tier-${tier.id}`}
            style={{ '--tier-color': tier.color }}
            onClick={() => handleTierClick(tier.id)}
          >
            <div className="tier-content">
              <h3>{tier.name}</h3>
              <p>{tier.range}</p>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};

export default TierPyramid;
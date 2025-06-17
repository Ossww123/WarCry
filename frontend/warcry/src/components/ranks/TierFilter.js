// src/components/ranks/TierFilter.js
import React from 'react';
import './TierFilter.css';

const TierFilter = ({ selectedTier, onTierSelect }) => {
  // 티어 정보 배열
  const tiers = [
    { id: null, name: '전체' },
    { id: 1, name: '1티어', color: '#FFD700' },
    { id: 2, name: '2티어', color: '#E5E4E2' },
    { id: 3, name: '3티어', color: '#CD7F32' },
    { id: 4, name: '4티어', color: '#43464B' }
  ];

  return (
    <div className="tier-filter">
      <h2>티어 필터</h2>
      <div className="filter-buttons">
        {tiers.map((tier) => (
          <button
            key={tier.id === null ? 'all' : tier.id}
            className={`filter-button ${selectedTier === tier.id ? 'active' : ''}`}
            style={tier.color ? { '--tier-color': tier.color } : {}}
            onClick={() => onTierSelect(tier.id)}
          >
            {tier.name}
          </button>
        ))}
      </div>
    </div>
  );
};

export default TierFilter;
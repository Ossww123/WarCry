// src/components/ranks/UserDistribution.js
import React from 'react';
import { Doughnut } from 'react-chartjs-2';
import { Chart as ChartJS, ArcElement, Tooltip, Legend } from 'chart.js';
import './UserDistribution.css';

// ChartJS 등록
ChartJS.register(ArcElement, Tooltip, Legend);

const UserDistribution = ({ distribution }) => {
  // 데이터가 없을 경우
  if (!distribution || !distribution.tiers || distribution.tiers.length === 0) {
    return (
      <div className="user-distribution">
        <h2>티어별 유저 분포</h2>
        <p className="no-data">데이터가 없습니다.</p>
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

  // 차트 데이터 변환
  const chartData = {
    labels: distribution.tiers.map(t => `${t.tier}티어`),
    datasets: [
      {
        data: distribution.tiers.map(t => t.count),
        backgroundColor: distribution.tiers.map(t => tierColors[t.tier]),
        borderColor: distribution.tiers.map(t => tierColors[t.tier]),
        borderWidth: 1,
      },
    ],
  };

  // 차트 옵션
  const chartOptions = {
    plugins: {
      legend: {
        position: 'right',
        labels: {
          color: '#f8f8f8',
          font: {
            size: 14
          }
        }
      },
      tooltip: {
        callbacks: {
          label: function(context) {
            const label = context.label || '';
            const value = context.raw || 0;
            const total = context.dataset.data.reduce((acc, curr) => acc + curr, 0);
            const percentage = ((value / total) * 100).toFixed(1);
            return `${label}: ${value}명 (${percentage}%)`;
          }
        }
      }
    },
    cutout: '60%', // 도넛 차트의 중앙 구멍 크기
    maintainAspectRatio: false,
  };

  // 총 유저 수 계산
  const totalUsers = distribution.tiers.reduce((sum, tier) => sum + tier.count, 0);

  return (
    <div className="user-distribution">
      <h2>티어별 유저 분포</h2>
      <div className="chart-container">
        <Doughnut data={chartData} options={chartOptions} />
        <div className="total-users">
          <span className="total-count">{totalUsers}</span>
          <span className="total-label">유저</span>
        </div>
      </div>
    </div>
  );
};

export default UserDistribution;
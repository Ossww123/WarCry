// src/components/profile/PointsGraph.js
import React, { useEffect, useRef } from 'react';
import { Line } from 'react-chartjs-2';
import { 
  Chart as ChartJS, 
  CategoryScale, 
  LinearScale, 
  PointElement, 
  LineElement, 
  Title, 
  Tooltip, 
  Legend,
  Filler
} from 'chart.js';
import './PointsGraph.css';

// Chart.js 등록
ChartJS.register(
  CategoryScale, 
  LinearScale, 
  PointElement, 
  LineElement, 
  Title, 
  Tooltip, 
  Legend,
  Filler
);

const PointsGraph = ({ matchHistory }) => {
  if (!matchHistory || !matchHistory.matches || matchHistory.matches.length === 0) {
    return (
      <div className="points-graph">
        <h2>포인트 변화</h2>
        <div className="no-data">포인트 변화 데이터가 없습니다.</div>
      </div>
    );
  }

  // 데이터 준비 (날짜 역순으로 정렬된 매치 기록을 그래프용으로 다시 정렬)
  const matches = [...matchHistory.matches].reverse();
  
  // 마지막 10게임만 표시 (개수 조정 가능)
  const displayMatches = matches.slice(-10);
  
  const labels = displayMatches.map((match, index) => `${index + 1}게임`);
  const pointsData = displayMatches.map(match => match.pointsAfter);
  
  // 승패에 따른 포인트 데이터 구분 (승리: 녹색, 패배: 빨간색)
  const pointColors = displayMatches.map(match => 
    match.result === 'WIN' ? 'rgba(0, 255, 127, 1)' : 'rgba(255, 69, 0, 1)'
  );
  
  // 그래프 데이터 구성
  const data = {
    labels,
    datasets: [
      {
        label: '포인트',
        data: pointsData,
        backgroundColor: pointColors,
        borderColor: 'rgba(255, 255, 255, 0.5)',
        tension: 0.3,
        fill: false,
        pointBackgroundColor: pointColors,
        pointBorderColor: '#fff',
        pointBorderWidth: 2,
        pointRadius: 5,
        pointHoverRadius: 7,
      }
    ]
  };
  
  // 그래프 옵션
  const options = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: false
      },
      tooltip: {
        mode: 'index',
        intersect: false,
        callbacks: {
          label: function(context) {
            const match = displayMatches[context.dataIndex];
            return `포인트: ${match.pointsAfter} (${match.result === 'WIN' ? '승리' : '패배'}, ${match.pointsChange >= 0 ? '+' : ''}${match.pointsChange})`;
          }
        }
      }
    },
    scales: {
      x: {
        grid: {
          color: 'rgba(255, 255, 255, 0.1)'
        },
        ticks: {
          color: '#aaa'
        }
      },
      y: {
        grid: {
          color: 'rgba(255, 255, 255, 0.1)'
        },
        ticks: {
          color: '#aaa'
        }
      }
    }
  };

  return (
    <div className="points-graph">
      <h2>포인트 변화</h2>
      <div className="graph-container">
        <Line data={data} options={options} />
      </div>
    </div>
  );
};

export default PointsGraph;
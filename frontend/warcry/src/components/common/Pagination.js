// src/components/common/Pagination.js
import React from 'react';
import './Pagination.css';

const Pagination = ({ currentPage, totalPages, onPageChange }) => {
  // 표시할 페이지 번호 범위 계산
  const getPageRange = () => {
    const range = [];
    const maxVisiblePages = 5; // 한 번에 최대 5개 페이지 버튼 표시
    
    let startPage = Math.max(0, currentPage - Math.floor(maxVisiblePages / 2));
    let endPage = startPage + maxVisiblePages - 1;
    
    if (endPage >= totalPages) {
      endPage = totalPages - 1;
      startPage = Math.max(0, endPage - maxVisiblePages + 1);
    }
    
    for (let i = startPage; i <= endPage; i++) {
      range.push(i);
    }
    
    return range;
  };

  if (totalPages <= 1) {
    return null; // 페이지가 1개 이하면 페이지네이션 표시 안 함
  }

  return (
    <div className="pagination">
      {/* 첫 페이지 버튼 */}
      <button 
        className="page-button first"
        onClick={() => onPageChange(0)}
        disabled={currentPage === 0}
      >
        &laquo;
      </button>
      
      {/* 이전 페이지 버튼 */}
      <button 
        className="page-button prev"
        onClick={() => onPageChange(currentPage - 1)}
        disabled={currentPage === 0}
      >
        &lt;
      </button>
      
      {/* 페이지 번호 버튼들 */}
      {getPageRange().map(page => (
        <button
          key={page}
          className={`page-button ${currentPage === page ? 'active' : ''}`}
          onClick={() => onPageChange(page)}
        >
          {page + 1}
        </button>
      ))}
      
      {/* 다음 페이지 버튼 */}
      <button 
        className="page-button next"
        onClick={() => onPageChange(currentPage + 1)}
        disabled={currentPage === totalPages - 1}
      >
        &gt;
      </button>
      
      {/* 마지막 페이지 버튼 */}
      <button 
        className="page-button last"
        onClick={() => onPageChange(totalPages - 1)}
        disabled={currentPage === totalPages - 1}
      >
        &raquo;
      </button>
    </div>
  );
};

export default Pagination;
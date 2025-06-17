// src/components/common/SearchBar.js 수정
import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import './SearchBar.css';

const SearchBar = () => {
  const [searchTerm, setSearchTerm] = useState('');
  const [searchResults, setSearchResults] = useState([]);
  const [isSearching, setIsSearching] = useState(false);
  const navigate = useNavigate();

  const handleSearch = async (e) => {
    e.preventDefault();
    if (searchTerm.trim()) {
      // 실제로는 닉네임 검색 API 호출이 필요합니다
      // 현재는 입력된 값을 사용자 ID로 간주하고 프로필 페이지로 이동
      navigate(`/profile/${searchTerm}`);
    }
  };

  return (
    <div className="search-bar">
      <form onSubmit={handleSearch}>
        <input
          type="text"
          placeholder="닉네임으로 검색"
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
        />
        <button type="submit" disabled={isSearching}>
          {isSearching ? '검색 중...' : '검색'}
        </button>
      </form>

      {/* 검색 결과 표시 영역 - 실제 API 연동 시 구현 */}
      {searchResults.length > 0 && (
        <div className="search-results">
          {searchResults.map(user => (
            <div 
              key={user.userId} 
              className="search-result-item"
              onClick={() => navigate(`/profile/${user.userId}`)}
            >
              <span className="nickname">{user.nickname}</span>
              <span className="tier">{user.tier}티어</span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

export default SearchBar;
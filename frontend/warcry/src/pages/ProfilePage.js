// src/pages/ProfilePage.js
import React, { useContext, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { AuthContext } from '../context/AuthContext';
import './ProfilePage.css';

const ProfilePage = () => {
  const { user } = useContext(AuthContext);
  const navigate = useNavigate();

  useEffect(() => {
    // 콘솔에 user 객체 출력하여 구조 확인
    console.log("User object:", user);
    
    if (user) {
      // user 객체에서 ID를 가져오는 속성을 확인하고 적절히 수정
      // user.id, user.userId, user._id 등 실제 사용자 객체의 ID 필드에 맞게 수정
      const userId = user.id; // 또는 user._id 또는 user.userId 등 실제 구조에 맞게 변경
      
      if (userId) {
        navigate(`/profile/${userId}`);
      } else {
        console.error("User ID is undefined or null:", user);
      }
    }
  }, [user, navigate]);

  // 로그인되지 않은 경우나 사용자 ID가 없는 경우 표시할 내용
  if (!user) {
    return (
      <div className="profile-container">
        <h1>로그인이 필요합니다</h1>
        <p>프로필 정보를 보려면 로그인해 주세요.</p>
        <button 
          className="login-button" 
          onClick={() => navigate('/login')}
        >
          로그인 하기
        </button>
      </div>
    );
  }

  // 리다이렉트 실패 시 표시할 내용
  return (
    <div className="profile-container">
      <h1>내 정보</h1>
      <p><strong>아이디:</strong> {user.username}</p>
      <p><strong>닉네임:</strong> {user.nickname}</p>
      <p className="error-message">사용자 ID를 찾을 수 없어 상세 프로필로 이동할 수 없습니다.</p>
    </div>
  );
};

export default ProfilePage;
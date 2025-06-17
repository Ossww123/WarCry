// src/components/Navbar.js
import React, { useContext } from 'react';
import { Link } from 'react-router-dom';
import './Navbar.css';
import logo from '../assets/logo.svg';
import { AuthContext } from '../context/AuthContext';

const Navbar = () => {
  const { user, logout } = useContext(AuthContext);

  return (
    <nav className="navbar">
      <div className="navbar-container">
        <Link to="/" className="navbar-logo">
          <img src={logo} alt="WARCRY" className="logo" />
          <span>WARCRY</span>
        </Link>
        <ul className="nav-menu">
          {/* 기존 메뉴 */}
          <li className="nav-item">
            <Link to="/" className="nav-link">홈</Link>
          </li>
          <li className="nav-item">
            <Link to="/download" className="nav-link">다운로드</Link>
          </li>
          <li className="nav-item">
            <Link to="/game-details" className="nav-link">게임 소개</Link>
          </li>
          <li className="nav-item">
            <Link to="/about" className="nav-link">개발자 소개</Link>
          </li>
          
          {/* 랭킹 메뉴 삭제 - 아래 조건부 렌더링으로 이동 */}
          {/* <li className="nav-item">
            <Link to="/ranking" className="nav-link">랭킹</Link>
          </li> */}
          
          {/* 인증 상태에 따른 메뉴 */}
          {user ? (
            <>
              {/* 랭킹 메뉴 - 로그인 상태일 때만 표시 */}
              <li className="nav-item">
                <Link to="/ranking" className="nav-link">랭킹</Link>
              </li>
              <li className="nav-item">
                <Link to="/profile" className="nav-link">
                  {user.nickname}님
                </Link>
              </li>
              <li className="nav-item">
                <button className="nav-link" onClick={logout}>로그아웃</button>
              </li>
            </>
          ) : (
            <>
              <li className="nav-item">
                <Link to="/login" className="nav-link">로그인</Link>
              </li>
              <li className="nav-item">
                <Link to="/signup" className="nav-link">회원가입</Link>
              </li>
            </>
          )}
        </ul>
      </div>
    </nav>
  );
};

export default Navbar;
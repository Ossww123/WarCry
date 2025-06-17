import React from "react";
import { Link } from "react-router-dom";
import "./HomePage.css";

// 이미지 import 추가
import vikingWarrior from "../assets/viking_warrior.png";
import blueKnight from "../assets/blue_knight.png";
import warcryLogo from "../assets/warcrylogo.png"; // 로고 이미지 import

const HomePage = () => {
  return (
    <div className="home-container">
      {/* 좌측 이미지 추가 */}
      <div className="left-character">
        <img src={blueKnight} alt="Blue Knight" />
      </div>
      
      {/* 우측 이미지 추가 */}
      <div className="right-character">
        <img src={vikingWarrior} alt="Viking Warrior" />
      </div>

      <section className="hero-section">
        <div className="hero-content">
          {/* 로고 이미지 추가 */}
          <div className="game-logo">
            <img src={warcryLogo} alt="WARCRY Logo" />
          </div>

          <h2>1대1 전략 시뮬레이션 게임</h2>
          <p>
            음성 명령으로 유닛을 지휘하고 왕 캐릭터를 직접 조작하며 승리를
            쟁취하세요!
          </p>
          <div className="cta-buttons">
            <Link to="/game-details" className="btn-primary">
              게임 소개
            </Link>
            <a href="#features" className="btn-secondary">
              주요 특징
            </a>
          </div>
        </div>
      </section>

      <section id="features" className="features-section">
        <h2 className="section-title">주요 특징</h2>
        <div className="features-grid">
          <div className="feature-card">
            <div className="feature-icon">🎮</div>
            <h3>1대1 대전 방식</h3>
            <p>전략적 사고와 실시간 전투 테크닉이 승패를 가릅니다.</p>
          </div>
          <div className="feature-card">
            <div className="feature-icon">👑</div>
            <h3>왕 캐릭터 직접 조작</h3>
            <p>강력한 능력을 가진 왕 캐릭터를 직접 조작하세요.</p>
          </div>
          <div className="feature-card">
            <div className="feature-icon">🗣️</div>
            <h3>음성 명령 시스템</h3>
            <p>음성으로 유닛에게 전략적 명령을 내릴 수 있습니다.</p>
          </div>
        </div>
      </section>

      <section className="game-preview">
        <h2 className="section-title">게임 미리보기</h2>
        <div className="preview-container">
          <div
            className="video-wrapper"
            style={{
              position: 'relative',
              paddingBottom: '56.25%',
              height: 0,
              overflow: 'hidden',
            }}
          >
            <iframe
              src="https://www.youtube.com/embed/2ji622V9z34?si=uIsrdPFAtxcefuyr" // Updated src
              title="WARCRY Gameplay Preview" // Updated title
              frameBorder="0"
              allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
              referrerPolicy="strict-origin-when-cross-origin"
              allowFullScreen
              style={{
                position: 'absolute',
                top: 0,
                left: 0,
                width: '100%',
                height: '100%',
              }}
            />
          </div>
        </div>
      </section>


      <section className="technical-highlight">
        <h2 className="section-title">기술적 도전 요소</h2>
        <div className="tech-cards">
          <div className="tech-card">
            <h3>AI 시스템</h3>
            <p>경로 탐색 AI, 행동 트리, 학습형 AI를 통한 지능적인 게임플레이</p>
          </div>
          <div className="tech-card">
            <h3>물리 시뮬레이션</h3>
            <p>현실적인 물체 움직임과 충돌, 유체 역학 시뮬레이션</p>
          </div>
          <div className="tech-card">
            <h3>실시간 네트워킹</h3>
            <p>다수 유닛의 상태를 동기화하고 네트워크 지연을 보정하는 시스템</p>
          </div>
        </div>
      </section>
    </div>
  );
};

export default HomePage;
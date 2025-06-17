import React from "react";
import "./Footer.css";

const Footer = () => {
  return (
    <footer className="footer">
      <div className="footer-container">
        <div className="footer-content">
          <div className="footer-section">
            <h3>WARCRY</h3>
            <p>1대1 전략 시뮬레이션 게임</p>
            <p>SSAFY 최종 프로젝트</p>
          </div>

          <div className="footer-section">
            <h3>빠른 링크</h3>
            <ul className="footer-links">
              <li>
                <a href="/">홈</a>
              </li>
              <li>
                <a href="/game-details">게임 소개</a>
              </li>
              <li>
                <a href="/about">개발자 소개</a>
              </li>
            </ul>
          </div>

          <div className="footer-section">
            <h3>연락처</h3>
            <p>이메일: contact@warcry.com</p>
            <p>GitHub: github.com/warcry-team</p>
          </div>
        </div>

        <div className="footer-bottom">
          <p>&copy; {new Date().getFullYear()} WARCRY. All Rights Reserved.</p>
        </div>
      </div>
    </footer>
  );
};

export default Footer;

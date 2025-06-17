import React from "react";
import "./AboutPage.css";

const AboutPage = () => {
  return (
    <div className="about-container">
      <header className="page-header">
        <h1>개발자 소개</h1>
        <p>WARCRY 개발팀을 소개합니다</p>
      </header>

      <section className="team-section section">
        <h2 className="section-title">개발 팀</h2>
        <p className="team-intro">
          WARCRY는 삼성청년소프트웨어아카데미(SSAFY)의 최종 프로젝트로, 열정적인
          6명의 개발자가 함께 만들어가고 있습니다.
        </p>

        <div className="team-grid">
          {/* 팀원 정보는 실제 데이터로 교체 필요 */}
          <div className="team-member">
            <div className="member-avatar">👨‍💻</div>
            <h3>구민성</h3>
            <p className="member-role">팀장 / 클라이언트 개발</p>
            <p>플레이어(왕) 로직</p>
          </div>

          <div className="team-member">
            <div className="member-avatar">👩‍💻</div>
            <h3>박성민</h3>
            <p className="member-role">인프라/ 서버 / 백엔드 개발</p>
            <p>홈페이지와 게임서버 배포</p>
          </div>

          <div className="team-member">
            <div className="member-avatar">👩‍💻</div>
            <h3>오승우</h3>
            <p className="member-role">클라이언트 개발</p>
            <p>UI 그래픽, 네트워크 기능능 담당</p>
          </div>

          <div className="team-member">
            <div className="member-avatar">👨‍💻</div>
            <h3>윤동욱</h3>
            <p className="member-role">클라이언트 개발</p>
            <p>음성 인식 시스템 구현, 유닛 로직</p>
          </div>

          <div className="team-member">
            <div className="member-avatar">👨‍💻</div>
            <h3>이강민</h3>
            <p className="member-role">클라이언트 개발</p>
            <p>맵</p>
          </div>

          <div className="team-member">
            <div className="member-avatar">👩‍💻</div>
            <h3>태성원</h3>
            <p className="member-role">PM / 서버 / 백엔드 / 프론트엔드 개발</p>
            <p>홈페이지와 백엔드, DB 관리</p>
          </div>
        </div>
      </section>

      <section className="development-process section">
        <h2 className="section-title">개발 과정</h2>

        <div className="timeline">
          <div className="timeline-item">
            <div className="timeline-date">2025.04.23</div>
            <div className="timeline-content">
              <h3>프로젝트 기획 시작</h3>
              <p>초기 기획 및 팀 구성</p>
            </div>
          </div>

          <div className="timeline-item">
            <div className="timeline-date">2025.04.28</div>
            <div className="timeline-content">
              <h3>에셋 수령 및 개발 환경 구축</h3>
              <p>캐릭터, GUI, SFX, VFX 에셋 수령</p>
            </div>
          </div>

          <div className="timeline-item">
            <div className="timeline-date">2025.05.05</div>
            <div className="timeline-content">
              <h3>기본 기능 구현</h3>
              <p>유닛 이동, 공격 시스템 개발</p>
            </div>
          </div>

          <div className="timeline-item">
            <div className="timeline-date">2025.05.15</div>
            <div className="timeline-content">
              <h3>음성 인식 시스템 통합</h3>
              <p>음성 명령 기능 완료</p>
            </div>
          </div>

          <div className="timeline-item">
            <div className="timeline-date">2025.05.22</div>
            <div className="timeline-content">
              <h3>최종 발표 준비</h3>
              <p>테스트 및 QA</p>
            </div>
          </div>
        </div>
      </section>

      <section className="technology-stack section">
        <h2 className="section-title">기술 스택</h2>

        <div className="tech-stack-grid">
          <div className="tech-item">
            <h3>게임 엔진</h3>
            <p>유니티 (Unity)</p>
          </div>

          <div className="tech-item">
            <h3>서버</h3>
            <p>스프링부트 + 유니티(Mirror)</p>
          </div>

          <div className="tech-item">
            <h3>음성 인식</h3>
            <p>Whisper API</p>
          </div>

          <div className="tech-item">
            <h3>프론트엔드</h3>
            <p>React (웹사이트)</p>
          </div>

          <div className="tech-item">
            <h3>데이터베이스</h3>
            <p>PostgreSQL</p>
          </div>

          <div className="tech-item">
            <h3>버전 관리</h3>
            <p>Gitlab, Jenkins</p>
          </div>

          <div className="tech-item">
            <h3>협업 툴</h3>
            <p>Jira</p>
          </div>
        </div>
      </section>

      <section className="contact-section section">
        <h2 className="section-title">연락처</h2>

        <div className="contact-info">
          <p>이메일: contact@warcry.com</p>
          <p>GitHub: github.com/warcry-team</p>
          <p>SSAFY: 삼성청년소프트웨어아카데미</p>
        </div>
      </section>
    </div>
  );
};

export default AboutPage;

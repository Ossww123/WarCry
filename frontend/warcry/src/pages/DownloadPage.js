import React from "react";
import "./DownloadPage.css";

const DownloadPage = () => {
  // 다운로드 준비 중 알림 표시 함수
  const handleDownload = (type) => {
    alert(`${type} 버전 다운로드는 현재 준비 중입니다. 곧 서비스될 예정입니다.`);
  };

  return (
    <div className="download-container">
      <header className="page-header">
        <h1>게임 다운로드</h1>
        <p>WARCRY 게임 클라이언트를 다운로드하세요</p>
      </header>

      <section className="download-section section">
        <h2 className="section-title">다운로드</h2>
        <p className="download-intro">
          최신 버전의 WARCRY를 다운로드하고 전략과 전투의 세계를 경험하세요.<br/><br/>
          음성 명령으로 유닛을 지휘하고 왕 캐릭터를 직접 조작하는 혁신적인 게임플레이를 즐겨보세요.
        </p>

        <div className="download-buttons">
          {/* 수정된 부분: button 태그를 a 태그로 변경하고 href 속성 추가 */}
          <a
            href="https://drive.google.com/drive/folders/14nJpVCh0V5o4OoMFHx8H_uqyM8iDWosH?usp=sharing"
            target="_blank" // 새 탭에서 링크 열기
            rel="noopener noreferrer" // 보안 및 개인 정보 보호 강화
            className="download-btn primary-btn" // 기존 스타일 유지
          >
            Windows 다운로드 (64bit)
          </a>
        </div>

        <div className="version-info">
          <p>최신 버전: v1.0.0 (2025.05.13)</p>
          <p>업데이트 내역: 최초 릴리즈</p>
        </div>
      </section>

      <section className="system-requirements section">
        <h2 className="section-title">시스템 요구사항</h2>

        <div className="requirements-grid">
          <div className="requirement-card">
            <h3>최소 사양</h3>
            <ul className="requirements-list">
              <li><strong>OS:</strong> Windows 10 (64bit)</li>
              <li><strong>프로세서:</strong> Intel Core i5-6600 / AMD Ryzen 3 1300X</li>
              <li><strong>메모리:</strong> 8GB RAM</li>
              <li><strong>그래픽:</strong> NVIDIA GTX 960 / AMD Radeon RX 560</li>
              <li><strong>DirectX:</strong> 버전 11</li>
              <li><strong>저장공간:</strong> 20GB</li>
              <li><strong>네트워크:</strong> 광대역 인터넷 연결</li>
            </ul>
          </div>

          <div className="requirement-card">
            <h3>권장 사양</h3>
            <ul className="requirements-list">
              <li><strong>OS:</strong> Windows 10/11 (64bit)</li>
              <li><strong>프로세서:</strong> 11th Gen Intel(R) Core(TM) i7-11600H @ 2.90GHz</li>
              <li><strong>메모리:</strong> 16GB RAM</li>
              <li><strong>그래픽:</strong> NVIDIA GTX 1660</li>
              <li><strong>DirectX:</strong> 버전 12</li>
              <li><strong>저장공간:</strong> 2GB SSD</li>
              <li><strong>네트워크:</strong> 광대역 인터넷 연결</li>
            </ul>
          </div>
        </div>
      </section>

      <section className="installation-guide section">
        <h2 className="section-title">설치 가이드</h2>

        <div className="guide-steps">
          <div className="guide-step">
            <div className="step-number">1</div>
            <div className="step-content">
              <h3>다운로드</h3>
              <p>운영체제에 맞는 설치 파일을 다운로드합니다.</p>
            </div>
          </div>

          <div className="guide-step">
            <div className="step-number">2</div>
            <div className="step-content">
              <h3>설치 실행</h3>
              <p>다운로드된 파일을 실행하고 설치 마법사의 안내를 따릅니다.</p>
            </div>
          </div>

          <div className="guide-step">
            <div className="step-number">3</div>
            <div className="step-content">
              <h3>게임 실행</h3>
              <p>설치 완료 후 바탕화면에 생성된 아이콘을 통해 게임을 실행합니다.</p>
            </div>
          </div>

          <div className="guide-step">
            <div className="step-number">4</div>
            <div className="step-content">
              <h3>계정 생성 및 로그인</h3>
              <p>첫 실행 시 계정을 생성하거나 기존 계정으로 로그인합니다.</p>
            </div>
          </div>
        </div>
      </section>

      <section className="faq-section section">
        <h2 className="section-title">자주 묻는 질문</h2>

        <div className="faq-list">
          <div className="faq-item">
            <h3>게임 실행 중 문제가 발생했을 때 어떻게 해결하나요?</h3>
            <p>
              게임 설치 폴더의 진단 도구를 실행하거나, 공식 커뮤니티 포럼에서 도움을 
              요청할 수 있습니다. 또한 설정에서 '로그 파일 보내기' 옵션을 사용하여 
              개발팀에게 문제를 보고할 수 있습니다.
            </p>
          </div>

          <div className="faq-item">
            <h3>음성 인식 기능을 사용하려면 어떤 장비가 필요한가요?</h3>
            <p>
              기본적인 마이크가 있는 헤드셋이면 충분합니다. 게임 내 설정에서 
              마이크 감도와 음성 인식 민감도를 조절할 수 있습니다.
            </p>
          </div>

          <div className="faq-item">
            <h3>게임 데이터는 어디에 저장되나요?</h3>
            <p>
              게임 진행 상황과 계정 정보는 서버에 저장되므로 어떤 컴퓨터에서도 
              동일한 계정으로 접속하면 이어서 플레이할 수 있습니다. 로컬 설정은 
              문서 폴더의 WARCRY 디렉토리에 저장됩니다.
            </p>
          </div>
        </div>
      </section>
    </div>
  );
};

export default DownloadPage;
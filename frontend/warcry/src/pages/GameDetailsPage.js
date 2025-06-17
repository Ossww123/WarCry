import React from "react";
import "./GameDetailsPage.css";

const GameDetailsPage = () => {
  return (
    <div className="game-details-container">
      <header className="page-header">
        <h1>게임 소개</h1>
        <p>WARCRY - 1대1 전략 시뮬레이션 게임</p>
      </header>

      <section className="game-overview section">
        <h2 className="section-title">게임 개요</h2>
        <div className="game-highlight">
          <h3>핵심 컨셉</h3>
          <ul>
            <li>1대1 대전 방식의 전략 시뮬레이션 게임</li>
            <li>플레이어는 각자 왕 캐릭터를 선택하고 유닛을 전략적으로 배치</li>
            <li>
              음성 명령을 통해 AI 유닛에게 전략 지시 + 왕 캐릭터 직접 조작
            </li>
            <li>격자 형태의 지형에서 진행되는 탑뷰 방식 게임플레이</li>
            <li>라운드 기반 승부 시스템 (1, 3, 5 라운드 중 선택)</li>
          </ul>
        </div>
      </section>

      <section className="gameplay-flow section">
        <h2 className="section-title">게임플레이 흐름</h2>
        <div className="flow-steps">
          <div className="flow-step">
            <div className="step-number">1</div>
            <div className="step-content">
              <h3>게임 시작 및 로그인</h3>
              <p>간편한 로그인 시스템을 통해 게임에 접속합니다.</p>
            </div>
          </div>
          <div className="flow-step">
            <div className="step-number">2</div>
            <div className="step-content">
              <h3>매치메이킹</h3>
              <p>방 생성 또는 기존 방 입장을 통해 상대방과 매칭됩니다.</p>
            </div>
          </div>
          <div className="flow-step">
            <div className="step-number">3</div>
            <div className="step-content">
              <h3>전투 진행</h3>
              <p>
                유닛 배치, 왕 캐릭터 조작, 음성 명령을 통해 전략적 전투를
                펼칩니다.
              </p>
            </div>
          </div>
          <div className="flow-step">
            <div className="step-number">4</div>
            <div className="step-content">
              <h3>승리 조건 달성</h3>
              <p>상대팀 진영의 성채 유닛을 파괴하여 라운드를 승리하세요.</p>
            </div>
          </div>
        </div>
      </section>

      <section className="game-systems section">
        <h2 className="section-title">게임 시스템</h2>

        <div className="system-card">
          <h3>왕 캐릭터</h3>
          <p>
            플레이어가 직접 조작하는 강력한 캐릭터로, 유닛에게 음성 명령을 내릴
            수 있습니다.
          </p>
          <p>
            사망 시 10초 후 자신의 성채에서 부활하며, 고유 스킬 시스템을
            보유하고 있습니다.
          </p>
        </div>

        <div className="system-card">
          <h3>유닛 시스템</h3>
          <p>
            보병, 궁병, 기병 등 다양한 유형의 유닛을 전략적으로 배치하고
            지휘하세요.
          </p>
          <p>
            각 유닛은 고유한 체력, 공격력, 사거리, 이동속도, 인식 범위를
            가집니다.
          </p>
        </div>

        <div className="system-card">
          <h3>음성 명령 시스템</h3>
          <p>
            게임 진행 중 음성을 통해 명령을 입력하고 유닛들의 행동을 지시할 수
            있습니다.
          </p>
          <p>
            왕 캐릭터 주변 일정 범위 내의 유닛만 명령을 수신할 수 있어 전략적
            포지셔닝이 중요합니다.
          </p>
        </div>
      </section>

      <section className="technical-features section">
        <h2 className="section-title">기술적 특징</h2>

        <div className="tech-feature">
          <h3>격자 기반 지형 시스템</h3>
          <p>
            다양한 타입의 지형 타일(기본, 수풀, 돌 등)이 게임에 전략적 요소를
            더합니다.
          </p>
          <p>각 지형은 은폐나 이동속도 감소 등의 특수 효과를 제공합니다.</p>
        </div>

        <div className="tech-feature">
          <h3>실시간 유닛 상태 동기화</h3>
          <p>
            다수 유닛의 위치, 체력, 공격 상태를 멀티플레이어 환경에서 정확히
            동기화합니다.
          </p>
          <p>서버 기반 권위 모델로 공정한 게임플레이를 보장합니다.</p>
        </div>

        <div className="tech-feature">
          <h3>음성 기반 유닛 제어</h3>
          <p>
            음성 명령어를 게임 내 행동으로 변환하는 첨단 기술을 적용하였습니다.
          </p>
          <p>
            기본 AI 행동 로직 위에 음성 명령을 반영하여 지능적인 유닛 행동이
            가능합니다.
          </p>
        </div>
      </section>

      <section className="ai-highlight section">
        <h2 className="section-title">인공지능 기술 하이라이트</h2>
        <div className="ai-features">
          <div className="ai-feature">
            <h3>경로 탐색 AI</h3>
            <p>A* 알고리즘 등을 활용한 지능적인 NPC 이동 시스템</p>
          </div>
        </div>
      </section>

      <section className="networking section">
        <h2 className="section-title">네트워킹 시스템</h2>
        <div className="network-features">
          <div className="network-feature">
            <h3>실시간 동기화</h3>
            <p>다수 플레이어의 움직임과 행동을 정확히 동기화</p>
          </div>
          <div className="network-feature">
            <h3>예측/보간 시스템</h3>
            <p>네트워크 지연을 보상하는 고급 메커니즘</p>
          </div>
          <div className="network-feature">
            <h3>로비/매치메이킹</h3>
            <p>효율적인 플레이어 연결 및 게임 세션 관리</p>
          </div>
          <div className="network-feature">
            <h3>권한 분리</h3>
            <p>보안을 위한 클라이언트/서버 간 권한 설계</p>
          </div>
        </div>
      </section>
    </div>
  );
};

export default GameDetailsPage;

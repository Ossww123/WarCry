import React from "react";
import ReactDOM from "react-dom/client";
import "./index.css";
import App from "./App";
import reportWebVitals from "./reportWebVitals";

// 브라우저가 자동 복원하려는 걸 끕니다
if ('scrollRestoration' in window.history) {
  window.history.scrollRestoration = 'manual';
}

// React 18 이상의 새로운 렌더링 방식
const root = ReactDOM.createRoot(document.getElementById("root"));
root.render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);

// 웹 성능 측정을 위한 reportWebVitals
// 콘솔에 로그를 출력하려면 아래 코드의 주석을 해제하세요:
// reportWebVitals(console.log);
reportWebVitals();

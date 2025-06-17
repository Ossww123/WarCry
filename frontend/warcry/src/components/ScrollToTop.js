// src/components/ScrollToTop.js

import { useLayoutEffect } from "react";
import { useLocation, useNavigationType } from "react-router-dom";

export default function ScrollToTop() {
  const { pathname, search, hash } = useLocation();
  const navigationType = useNavigationType(); // POP, PUSH, REPLACE

  useLayoutEffect(() => {
    // 경로가 바뀔 때마다 항상 최상단으로 스크롤
    window.scrollTo(0, 0);
  }, [pathname, search, hash, navigationType]);

  return null;
}

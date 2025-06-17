// src/components/CustomCursor.js

import React, { useEffect, useState } from "react";
import "./CustomCursor.css";

const CustomCursor = () => {
  const [pos, setPos] = useState({ x: 0, y: 0 });
  const [slashing, setSlashing] = useState(false);

  useEffect(() => {
    const onMouseMove = (e) => setPos({ x: e.clientX, y: e.clientY });
    const onMouseDown = () => setSlashing(true);

    document.addEventListener("mousemove", onMouseMove);
    document.addEventListener("mousedown", onMouseDown);
    return () => {
      document.removeEventListener("mousemove", onMouseMove);
      document.removeEventListener("mousedown", onMouseDown);
    };
  }, []);

  const handleAnimationEnd = () => setSlashing(false);

  return (
    <div
      className={`cursor ${slashing ? "cursor--slash" : ""}`}
      style={{ left: pos.x, top: pos.y }}
      onAnimationEnd={handleAnimationEnd}
    />
  );
};

export default CustomCursor;

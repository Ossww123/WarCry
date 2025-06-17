// src/components/common/PageTransition.js
import React from 'react';
import { motion } from 'framer-motion';

// 페이지 전환 애니메이션 컴포넌트
const PageTransition = ({ children }) => {
  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.3 }}
    >
      {children}
    </motion.div>
  );
};

export default PageTransition;
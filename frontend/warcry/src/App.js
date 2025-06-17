// src/App.js
import React from 'react';
import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import './App.css';
import CustomCursor from './components/CustomCursor';
import ScrollToTop from './components/ScrollToTop';

// 기존 페이지 imports
import HomePage from './pages/HomePage';
import GameDetailsPage from './pages/GameDetailsPage';
import DownloadPage from './pages/DownloadPage';
import AboutPage from './pages/AboutPage';
import LoginPage from './pages/LoginPage';
import SignupPage from './pages/SignupPage';
import ProfilePage from './pages/ProfilePage';

// 새로운 페이지 imports (나중에 구현할 페이지)
import RankingPage from './pages/RankingPage';
import TierDetailPage from './pages/TierDetailPage';
import ProfileDetailPage from './pages/ProfileDetailPage';

import Navbar from './components/Navbar';
import Footer from './components/Footer';
import { AuthProvider } from './context/AuthContext';

function App() {
  return (
    <Router>
      <CustomCursor />
      <ScrollToTop />
      <AuthProvider>
        <div className="App">
          <Navbar />
          <main className="content">
            <Routes>
              {/* 기존 라우트 */}
              <Route path="/login" element={<LoginPage />} />
              <Route path="/signup" element={<SignupPage />} />
              <Route path="/profile" element={<ProfilePage />} />
              <Route path="/" element={<HomePage />} />
              <Route path="/game-details" element={<GameDetailsPage />} />
              <Route path="/download" element={<DownloadPage />} />
              <Route path="/about" element={<AboutPage />} />
              
              {/* 새로운 라우트 */}
              <Route path="/ranking" element={<RankingPage />} />
              <Route path="/ranking/tier/:tierId" element={<TierDetailPage />} />
              <Route path="/profile/:userId" element={<ProfileDetailPage />} />
            </Routes>
          </main>
          <Footer />
        </div>
      </AuthProvider>
    </Router>
  );
}

export default App;
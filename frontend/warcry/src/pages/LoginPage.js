// src/pages/LoginPage.js
import React, { useState, useContext } from 'react';
import './LoginPage.css';
import { AuthContext } from '../context/AuthContext';

const LoginPage = () => {
  const { login } = useContext(AuthContext);
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      await login({ username, password });
    } catch (e) {
      setError('로그인에 실패했습니다. 아이디와 비밀번호를 확인하세요.');
    }
  };

  return (
    <div className="auth-container">
      <h2>로그인</h2>
      <form onSubmit={handleSubmit} className="auth-form">
        <label>아이디</label>
        <input value={username} onChange={e => setUsername(e.target.value)} required />

        <label>비밀번호</label>
        <input type="password" value={password} onChange={e => setPassword(e.target.value)} required />

        {error && <p className="error-msg">{error}</p>}
        <button type="submit">로그인</button>
      </form>
    </div>
  );
};

export default LoginPage;
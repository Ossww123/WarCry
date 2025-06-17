// src/pages/SignupPage.js
import React, { useState, useContext } from 'react';
import './SignupPage.css';
import { AuthContext } from '../context/AuthContext';
import { checkUsername } from '../api/auth';

const SignupPage = () => {
  const { signup } = useContext(AuthContext);
  const [form, setForm] = useState({ username: '', password: '', nickname: '' });
  const [available, setAvailable] = useState(null);
  const [error, setError] = useState('');

  const handleChange = e => {
    setForm({ ...form, [e.target.name]: e.target.value });
  };

  const handleBlur = async () => {
    if (form.username) {
      const ok = await checkUsername(form.username);
      setAvailable(ok);
    }
  };

  const handleSubmit = async e => {
    e.preventDefault();
    if (available === false) {
      setError('이미 존재하는 아이디입니다.');
      return;
    }
    try {
      await signup(form);
    } catch (e) {
      setError('회원가입에 실패했습니다.');
    }
  };

  return (
    <div className="auth-container">
      <h2>회원가입</h2>
      <form onSubmit={handleSubmit} className="auth-form">
        <label>아이디</label>
        <input
          name="username"
          value={form.username}
          onChange={handleChange}
          onBlur={handleBlur}
          required
        />
        {available != null && (
          <p className={`avail-msg ${available ? 'ok' : 'no'}`}> 
            {available ? '사용 가능한 아이디입니다.' : '사용 불가능한 아이디입니다.'}
          </p>
        )}

        <label>비밀번호</label>
        <input
          name="password"
          type="password"
          value={form.password}
          onChange={handleChange}
          required
        />

        <label>닉네임</label>
        <input
          name="nickname"
          value={form.nickname}
          onChange={handleChange}
          required
        />

        {error && <p className="error-msg">{error}</p>}
        <button type="submit">회원가입</button>
      </form>
    </div>
  );
};

export default SignupPage;
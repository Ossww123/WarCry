// src/api/auth.js
import axios from 'axios';

// 백엔드 기본 URL만 설정
// 개발용
// axios.defaults.baseURL = '/';

// 배포용
axios.defaults.baseURL = 'https://k12d104.p.ssafy.io';

export const signup = async ({ username, password, nickname }) => {
  const res = await axios.post('/api/auth/signup', {
    username,
    password,
    nickname
  });
  return res.data;
};

export const login = async ({ username, password }) => {
  const res = await axios.post('/api/auth/login', {
    username,
    password
  });
  return res.data;
};

export const checkUsername = async (username) => {
  const res = await axios.get('/api/auth/check-username', {
    params: { username }
  });
  return res.data.available;
};

export const fetchCurrentUser = async (token) => {
  const res = await axios.get('/api/auth/me', {
    headers: { Authorization: token }
  });
  return res.data;
};

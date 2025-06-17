// src/context/AuthContext.js
import React, { createContext, useState, useEffect } from 'react';
import { login as apiLogin, signup as apiSignup, fetchCurrentUser } from '../api/auth';
import { useNavigate } from 'react-router-dom';

export const AuthContext = createContext();

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(null);
  const [token, setToken] = useState(localStorage.getItem('token'));
  const navigate = useNavigate();

  useEffect(() => {
    if (token) {
      fetchCurrentUser(token)
        .then(data => setUser(data))
        .catch(() => {
          setToken(null);
          localStorage.removeItem('token');
        });
    }
  }, [token]);

  const login = async (credentials) => {
    const data = await apiLogin(credentials);
    const bearer = `${data.tokenType} ${data.accessToken}`;
    setToken(bearer);
    localStorage.setItem('token', bearer);
    const userInfo = await fetchCurrentUser(bearer);
    setUser(userInfo);
    navigate('/');
  };

  const signup = async (info) => {
    await apiSignup(info);
    // 가입 후 바로 로그인
    await login({ username: info.username, password: info.password });
  };

  const logout = () => {
    setUser(null);
    setToken(null);
    localStorage.removeItem('token');
    navigate('/login');
  };

  return (
    <AuthContext.Provider value={{ user, token, login, signup, logout }}>
      {children}
    </AuthContext.Provider>
  );
};